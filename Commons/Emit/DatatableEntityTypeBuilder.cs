using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Serialization;
using TaikoSoundEditor.Commons.Extensions;
using TaikoSoundEditor.Commons.Utils;

namespace TaikoSoundEditor.Commons.Emit
{
    internal sealed class DatatableEntityTypeBuilder
    {
        private readonly ModuleBuilder moduleBuilder;

        public DatatableEntityTypeBuilder()
        {
            var assemblyName = new AssemblyName(DynamicAssemblyName);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name ?? DynamicAssemblyName);
        }

        public static Dictionary<string, Type> LoadTypes(DynamicTypeCollection dynamicTypes)
        {
            var builder = new DatatableEntityTypeBuilder();
            return dynamicTypes.Types.ToDictionary(
                dynamicType => dynamicType.Name,
                dynamicType => builder.BuildType(
                    dynamicType.Name,
                    Types.GetTypeByName(dynamicType.Interface),
                    dynamicType.Properties.Select(property => property.CreatePropertyInfo())));
        }

        public Type BuildType(string name, Type interfaceType, IEnumerable<EntityPropertyInfo> properties)
        {
            var typeBuilder = interfaceType == null
                ? moduleBuilder.DefineType(name, TypeAttributes.Public)
                : moduleBuilder.DefineType(name, TypeAttributes.Public, null, new[] { interfaceType });

            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);
            var constructorIl = constructor.GetILGenerator();
            constructorIl.Emit(OpCodes.Ldarg_0);
            constructorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

            var generatedProperties = new Dictionary<string, GeneratedProperty>(StringComparer.Ordinal);
            foreach (var property in properties)
                generatedProperties.Add(property.Name, GenerateProperty(typeBuilder, constructorIl, property));

            constructorIl.Emit(OpCodes.Ret);

            if (interfaceType != null)
            {
                foreach (var property in interfaceType.GetProperties())
                {
                    var recast = property.GetCustomAttribute<RecastAttribute>();
                    if (recast == null) continue;
                    if (!generatedProperties.TryGetValue(recast.PropertyName, out var source))
                        throw new InvalidOperationException(
                            $"Cannot recast {property.Name}: source property {recast.PropertyName} was not generated.");
                    GenerateRecastedProperty(typeBuilder, property, source);
                }
            }

            return typeBuilder.CreateType();
        }

        private static GeneratedProperty GenerateProperty(
            TypeBuilder typeBuilder,
            ILGenerator constructorIl,
            EntityPropertyInfo property)
        {
            var field = typeBuilder.DefineField($"m_{property.Name}", property.Type, FieldAttributes.Private);
            if (property.DefaultValue != null)
            {
                constructorIl.Emit(OpCodes.Ldarg_0);
                EmitConstant(constructorIl, property.DefaultValue, property.Type);
                constructorIl.Emit(OpCodes.Stfld, field);
            }

            var propertyBuilder = typeBuilder.DefineProperty(
                property.Name,
                PropertyAttributes.HasDefault,
                property.Type,
                null);

            var getter = typeBuilder.DefineMethod(
                $"get_{property.Name}",
                AccessorAttributes,
                property.Type,
                Type.EmptyTypes);
            var getterIl = getter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, field);
            getterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetGetMethod(getter);

            var setter = typeBuilder.DefineMethod(
                $"set_{property.Name}",
                AccessorAttributes,
                null,
                new[] { property.Type });
            var setterIl = setter.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, field);
            setterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setter);

            if (!string.IsNullOrEmpty(property.JsonPropertyName))
                AddAttribute(propertyBuilder, typeof(JsonPropertyNameAttribute), property.JsonPropertyName);
            else
                AddAttribute(propertyBuilder, typeof(JsonIgnoreAttribute));

            if (property.IsReadOnly)
                AddAttribute(propertyBuilder, typeof(ReadOnlyAttribute), true);

            return new GeneratedProperty(property.Type, getter, setter);
        }

        private static void GenerateRecastedProperty(
            TypeBuilder typeBuilder,
            PropertyInfo interfaceProperty,
            GeneratedProperty source)
        {
            if (!CanRecast(source.Type, interfaceProperty.PropertyType))
                throw new InvalidOperationException(
                    $"Cannot recast {source.Type} to {interfaceProperty.PropertyType} for {interfaceProperty.Name}.");

            var propertyBuilder = typeBuilder.DefineProperty(
                interfaceProperty.Name,
                PropertyAttributes.HasDefault,
                interfaceProperty.PropertyType,
                null);

            if (interfaceProperty.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                AddAttribute(propertyBuilder, typeof(JsonIgnoreAttribute));

            var getter = typeBuilder.DefineMethod(
                $"get_{interfaceProperty.Name}",
                AccessorAttributes,
                interfaceProperty.PropertyType,
                Type.EmptyTypes);
            var getterIl = getter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Call, source.Getter);
            EmitNumericConversion(getterIl, interfaceProperty.PropertyType);
            getterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetGetMethod(getter);

            var setter = typeBuilder.DefineMethod(
                $"set_{interfaceProperty.Name}",
                AccessorAttributes,
                null,
                new[] { interfaceProperty.PropertyType });
            var setterIl = setter.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            EmitNumericConversion(setterIl, source.Type);
            setterIl.Emit(OpCodes.Call, source.Setter);
            setterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setter);

            typeBuilder.DefineMethodOverride(getter, interfaceProperty.GetMethod);
            typeBuilder.DefineMethodOverride(setter, interfaceProperty.SetMethod);
        }

        private static bool CanRecast(Type source, Type target)
        {
            var sourceValue = source.IsEnum ? Enum.GetUnderlyingType(source) : source;
            var targetValue = target.IsEnum ? Enum.GetUnderlyingType(target) : target;
            return sourceValue == targetValue ||
                   (IsIntegral(sourceValue) && IsIntegral(targetValue));
        }

        private static bool IsIntegral(Type type) =>
            type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong);

        private static void EmitNumericConversion(ILGenerator il, Type type)
        {
            type = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
            if (type == typeof(byte)) il.Emit(OpCodes.Conv_U1);
            else if (type == typeof(sbyte)) il.Emit(OpCodes.Conv_I1);
            else if (type == typeof(short)) il.Emit(OpCodes.Conv_I2);
            else if (type == typeof(ushort)) il.Emit(OpCodes.Conv_U2);
            else if (type == typeof(int)) il.Emit(OpCodes.Conv_I4);
            else if (type == typeof(uint)) il.Emit(OpCodes.Conv_U4);
            else if (type == typeof(long)) il.Emit(OpCodes.Conv_I8);
            else if (type == typeof(ulong)) il.Emit(OpCodes.Conv_U8);
        }

        private static void EmitConstant(ILGenerator il, object value, Type targetType)
        {
            if (targetType == typeof(string)) il.Emit(OpCodes.Ldstr, (string)value);
            else if (targetType == typeof(bool)) il.Emit((bool)value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            else if (targetType == typeof(int)) il.Emit(OpCodes.Ldc_I4, (int)value);
            else if (targetType == typeof(uint)) il.Emit(OpCodes.Ldc_I4, unchecked((int)(uint)value));
            else if (targetType == typeof(short)) il.Emit(OpCodes.Ldc_I4, (short)value);
            else if (targetType == typeof(ushort)) il.Emit(OpCodes.Ldc_I4, (ushort)value);
            else if (targetType == typeof(double)) il.Emit(OpCodes.Ldc_R8, (double)value);
            else
                throw new InvalidOperationException($"Unsupported default value type {targetType}.");
        }

        private static void AddAttribute(PropertyBuilder property, Type attributeType, params object[] arguments)
        {
            var constructor = attributeType.GetConstructors()
                .FirstOrDefault(candidate =>
                {
                    var parameters = candidate.GetParameters();
                    return parameters.Length == arguments.Length &&
                           parameters.Select(parameter => parameter.ParameterType)
                               .Zip(arguments, (parameter, argument) =>
                                   argument == null ? parameter.IsClass : parameter.IsAssignableFrom(argument.GetType()))
                               .All(matches => matches);
                });

            if (constructor == null)
                throw new InvalidOperationException($"No matching constructor found for {attributeType.Name}.");

            property.SetCustomAttribute(new CustomAttributeBuilder(constructor, arguments));
        }

        private sealed class GeneratedProperty
        {
            public GeneratedProperty(Type type, MethodBuilder getter, MethodBuilder setter)
            {
                Type = type;
                Getter = getter;
                Setter = setter;
            }

            public Type Type { get; }
            public MethodBuilder Getter { get; }
            public MethodBuilder Setter { get; }
        }

        private const MethodAttributes AccessorAttributes =
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual;
        private const string DynamicAssemblyName = "TSEDynEntities";
    }
}
