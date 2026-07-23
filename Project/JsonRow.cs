using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal static class JsonRow
    {
        public static string GetString(JsonObject row, string propertyName)
        {
            if (row == null || !row.TryGetPropertyValue(propertyName, out var node) || node == null)
                return null;

            if (node is JsonValue value && value.TryGetValue<string>(out var text))
                return text;

            return node.ToString();
        }

        public static int? GetInt(JsonObject row, string propertyName)
        {
            if (row == null || !row.TryGetPropertyValue(propertyName, out var node) || node == null)
                return null;

            if (node is JsonValue value)
            {
                if (value.TryGetValue<int>(out var integer)) return integer;
                if (value.TryGetValue<long>(out var longInteger) && longInteger >= int.MinValue && longInteger <= int.MaxValue)
                    return (int)longInteger;
                if (value.TryGetValue<string>(out var text) &&
                    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    return integer;
            }

            return int.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
        }

        public static double? GetDouble(JsonObject row, string propertyName)
        {
            if (row == null || !row.TryGetPropertyValue(propertyName, out var node) || node == null)
                return null;

            if (node is JsonValue value)
            {
                if (value.TryGetValue<double>(out var number)) return number;
                if (value.TryGetValue<float>(out var single)) return single;
                if (value.TryGetValue<int>(out var integer)) return integer;
                if (value.TryGetValue<long>(out var longInteger)) return longInteger;
                if (value.TryGetValue<string>(out var text) &&
                    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    return number;
            }

            return double.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (double?)null;
        }

        public static bool? GetBool(JsonObject row, string propertyName)
        {
            if (row == null || !row.TryGetPropertyValue(propertyName, out var node) || node == null)
                return null;

            if (node is JsonValue value)
            {
                if (value.TryGetValue<bool>(out var boolean)) return boolean;
                if (value.TryGetValue<int>(out var integer)) return integer != 0;
                if (value.TryGetValue<string>(out var text))
                {
                    if (bool.TryParse(text, out boolean)) return boolean;
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                        return integer != 0;
                }
            }

            return null;
        }

        public static bool MatchesSong(JsonObject row, string songId, int uniqueId)
        {
            var rowId = GetString(row, "id");
            var rowUniqueId = GetInt(row, "uniqueId");

            return (!string.IsNullOrEmpty(songId) && string.Equals(rowId, songId, StringComparison.Ordinal)) ||
                   (uniqueId != 0 && rowUniqueId == uniqueId);
        }
    }
}
