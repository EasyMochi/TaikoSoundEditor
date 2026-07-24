using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal static class EventFolderProjectValidator
    {
        public static void ValidateForExport(TaikoProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!project.HasGenreFolderInfo) return;

            var eventRows = project.GenreFolderInfo.Items.OfType<JsonObject>()
                .Where(row => ReadBool(row, "isServerReleasedFlag") == true)
                .ToList();
            var errors = new List<string>();

            foreach (var duplicate in eventRows
                .Select(row => ReadInt(row, "uniqueId"))
                .Where(value => value.HasValue)
                .GroupBy(value => value.Value)
                .Where(group => group.Count() > 1))
                errors.Add($"genre_folderinfo contains duplicate event folder UID {duplicate.Key}.");

            foreach (var duplicate in eventRows
                .Select(row => ReadString(row, "id"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1))
                errors.Add($"genre_folderinfo contains duplicate event folder ID '{duplicate.Key}'.");

            foreach (var row in eventRows)
            {
                var uid = ReadInt(row, "uniqueId");
                var id = ReadString(row, "id");
                if (!uid.HasValue)
                {
                    errors.Add("A server-released genre_folderinfo row has no numeric uniqueId.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"Event folder {uid.Value} has no internal ID.");
                    continue;
                }

                var titleKeys = new[] { "folder_" + id, "folder_" + uid.Value };
                if (!HasAnyWordKey(project, titleKeys))
                    errors.Add($"Event folder {uid.Value} ({id}) has no folder title wordlist row.");
            }

            if (errors.Count > 0)
                throw new InvalidDataException("Event-folder client validation failed:\n\n" + string.Join("\n", errors));
        }

        private static bool HasAnyWordKey(TaikoProject project, IEnumerable<string> keys)
        {
            var expected = new HashSet<string>(keys, StringComparer.Ordinal);
            return project.WordList.Items.OfType<JsonObject>()
                .Any(row => expected.Contains(ReadString(row, "key") ?? string.Empty));
        }

        private static JsonNode Find(JsonObject row, string propertyName)
        {
            if (row == null) return null;
            foreach (var property in row)
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            return null;
        }

        private static string ReadString(JsonObject row, string propertyName)
        {
            var node = Find(row, propertyName);
            if (node == null) return null;
            if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text;
            return node.ToString();
        }

        private static int? ReadInt(JsonObject row, string propertyName)
        {
            var node = Find(row, propertyName);
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<int>(out var integer)) return integer;
            if (value.TryGetValue<long>(out var longInteger) && longInteger >= int.MinValue && longInteger <= int.MaxValue)
                return (int)longInteger;
            return null;
        }

        private static bool? ReadBool(JsonObject row, string propertyName)
        {
            var node = Find(row, propertyName);
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<bool>(out var boolean)) return boolean;
            if (value.TryGetValue<int>(out var integer)) return integer != 0;
            return null;
        }
    }
}
