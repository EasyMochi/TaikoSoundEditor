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
