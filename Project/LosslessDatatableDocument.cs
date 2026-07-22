using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TaikoSoundEditor.Commons.IO;

namespace TaikoSoundEditor.Project
{
    internal sealed class LosslessDatatableDocument
    {
        private readonly JsonObject root;
        private readonly JsonArray items;

        private LosslessDatatableDocument(string fileName, JsonObject root, JsonArray items)
        {
            FileName = fileName;
            this.root = root;
            this.items = items;
        }

        public string FileName { get; }
        public JsonArray Items => items;

        public static LosslessDatatableDocument Load(string path, bool encrypted)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Datatable was not found.", path);

            string json;
            if (encrypted)
            {
                var decrypted = SSL.DecryptDatatable(File.ReadAllBytes(path));
                json = GZ.DecompressBytes(decrypted);
            }
            else
            {
                json = GZ.DecompressString(path);
            }

            var root = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidDataException($"{Path.GetFileName(path)} does not contain a JSON object.");
            var items = root["items"] as JsonArray
                ?? throw new InvalidDataException($"{Path.GetFileName(path)} does not contain an items array.");

            return new LosslessDatatableDocument(Path.GetFileName(path), root, items);
        }

        public string ToJson(bool indented = false)
        {
            return root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = indented
            });
        }

        public void Write(string path, bool encrypted, bool indented = false)
        {
            var compressed = GZ.CompressToBytes(ToJson(indented));
            var bytes = encrypted ? SSL.EncryptDatatable(compressed) : compressed;
            File.WriteAllBytes(path, bytes);
        }

        public void MergeKnownItems(string serializedCollectionJson)
        {
            var replacementRoot = JsonNode.Parse(serializedCollectionJson) as JsonObject
                ?? throw new InvalidDataException("Serialized collection does not contain a JSON object.");
            var replacements = replacementRoot["items"] as JsonArray
                ?? throw new InvalidDataException("Serialized collection does not contain an items array.");

            var existingByIdentity = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var node in items.OfType<JsonObject>())
            {
                var identity = GetIdentity(node);
                if (identity != null && !existingByIdentity.ContainsKey(identity))
                    existingByIdentity.Add(identity, node);
            }

            var merged = new JsonArray();
            foreach (var replacement in replacements.OfType<JsonObject>())
            {
                var identity = GetIdentity(replacement);
                JsonObject target;
                if (identity != null && existingByIdentity.TryGetValue(identity, out var existing))
                    target = (JsonObject)existing.DeepClone();
                else
                    target = new JsonObject();

                foreach (var property in replacement)
                    target[property.Key] = property.Value?.DeepClone();

                merged.Add(target);
            }

            root["items"] = merged;
            items.Clear();
            foreach (var node in merged)
                items.Add(node?.DeepClone());
            root["items"] = items;
        }

        private static string GetIdentity(JsonObject item)
        {
            if (item["key"] != null)
                return "key:" + item["key"];

            var id = item["id"]?.ToString();
            var uniqueId = item["uniqueId"]?.ToString();
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(uniqueId))
                return $"song:{id}:{uniqueId}";
            if (!string.IsNullOrEmpty(id))
                return "id:" + id;
            if (!string.IsNullOrEmpty(uniqueId))
                return "uid:" + uniqueId;

            return null;
        }
    }
}
