using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal static class SongAdvancedMetadata
    {
        private const double LongChartThresholdSeconds = 100.0;

        public static JsonObject CreateAiRow(string id, int uniqueId, TJA tja, IMusicInfo musicInfo)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A song id is required.", nameof(id));
            if (uniqueId == 0) throw new ArgumentException("A non-zero unique id is required.", nameof(uniqueId));
            if (musicInfo == null) throw new ArgumentNullException(nameof(musicInfo));

            return new JsonObject
            {
                ["id"] = id,
                ["uniqueId"] = uniqueId,
                ["easy"] = CalculateSectionCount(tja, 0),
                ["normal"] = CalculateSectionCount(tja, 1),
                ["hard"] = CalculateSectionCount(tja, 2),
                ["oni"] = CalculateSectionCount(tja, 3),
                ["ura"] = CalculateSectionCount(tja, 4),
                ["oniLevel11"] = musicInfo.StarMania == 10 ? "o" : string.Empty,
                ["uraLevel11"] = musicInfo.StarUra == 10 ? "o" : string.Empty
            };
        }

        public static JsonObject CreateDefaultAiRow(string id, int uniqueId, IMusicInfo musicInfo)
        {
            if (musicInfo == null) throw new ArgumentNullException(nameof(musicInfo));
            return new JsonObject
            {
                ["id"] = id,
                ["uniqueId"] = uniqueId,
                ["easy"] = 3,
                ["normal"] = 3,
                ["hard"] = 3,
                ["oni"] = 3,
                ["ura"] = 3,
                ["oniLevel11"] = musicInfo.StarMania == 10 ? "o" : string.Empty,
                ["uraLevel11"] = musicInfo.StarUra == 10 ? "o" : string.Empty
            };
        }

        public static JsonObject CreateUsbRow(string id, int uniqueId) => new JsonObject
        {
            ["id"] = id,
            ["uniqueId"] = uniqueId,
            ["usbVer"] = string.Empty
        };

        public static JsonObject FindExactRow(JsonArray rows, string id, int uniqueId, string component)
        {
            var exact = rows.OfType<JsonObject>()
                .Where(row => string.Equals(JsonRow.GetString(row, "id"), id, StringComparison.Ordinal) &&
                              JsonRow.GetInt(row, "uniqueId") == uniqueId)
                .ToList();
            if (exact.Count > 1)
                throw new InvalidOperationException($"{component} contains {exact.Count} exact rows for {id} / {uniqueId}.");
            if (exact.Count == 1) return exact[0];

            var conflicts = rows.OfType<JsonObject>()
                .Where(row => string.Equals(JsonRow.GetString(row, "id"), id, StringComparison.Ordinal) ||
                              JsonRow.GetInt(row, "uniqueId") == uniqueId)
                .ToList();
            if (conflicts.Count > 0)
                throw new InvalidOperationException($"{component} contains a row whose song id and unique id disagree for {id} / {uniqueId}.");
            return null;
        }

        public static JsonObject EnsureAiRow(TaikoProject project, string id, int uniqueId, IMusicInfo musicInfo)
        {
            var row = FindExactRow(project.MusicAiSection.Items, id, uniqueId, "music_ai_section");
            if (row != null) return row;
            row = CreateDefaultAiRow(id, uniqueId, musicInfo);
            project.MusicAiSection.Items.Add(row);
            return row;
        }

        public static JsonObject EnsureUsbRow(TaikoProject project, string id, int uniqueId)
        {
            var row = FindExactRow(project.MusicUsbSetting.Items, id, uniqueId, "music_usbsetting");
            if (row != null) return row;
            row = CreateUsbRow(id, uniqueId);
            project.MusicUsbSetting.Items.Add(row);
            return row;
        }

        public static void Upsert(JsonArray rows, JsonObject source, string component)
        {
            if (source == null) return;
            var id = JsonRow.GetString(source, "id");
            var uniqueId = JsonRow.GetInt(source, "uniqueId") ?? 0;
            var target = FindExactRow(rows, id, uniqueId, component);
            if (target == null)
            {
                rows.Add(source.DeepClone());
                return;
            }

            foreach (var property in source)
                target[property.Key] = property.Value?.DeepClone();
        }

        public static void RemoveOwnedRows(JsonArray rows, IEnumerable<string> ownedIds, ISet<string> activeIds)
        {
            var owned = new HashSet<string>(ownedIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (owned.Count == 0) return;

            foreach (var row in rows.OfType<JsonObject>().ToList())
            {
                var id = JsonRow.GetString(row, "id");
                if (!string.IsNullOrEmpty(id) && owned.Contains(id) && !activeIds.Contains(id))
                    rows.Remove(row);
            }
        }

        private static int CalculateSectionCount(TJA tja, int difficulty)
        {
            if (tja == null || !tja.Courses.TryGetValue(difficulty, out var course) || course.Measures.Count == 0)
                return 3;

            var bpm = tja.Headers.Bpm > 0 ? (double)tja.Headers.Bpm : 120.0;
            var durationSeconds = 0.0;

            foreach (var measure in course.Measures)
            {
                if (measure.Length == null || measure.Length.Length < 2 || measure.Length[1] == 0)
                    continue;

                var measureBeats = (double)measure.Length[0] / measure.Length[1] * 4.0;
                var dataLength = Math.Max(1, measure.MeasureData?.Length ?? 0);
                var previousPosition = 0;

                foreach (var bpmEvent in measure.Events
                             .Where(evt => string.Equals(evt.Name, "bpm", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(evt => evt.Position))
                {
                    var position = Math.Max(previousPosition, Math.Min(dataLength, bpmEvent.Position));
                    var segmentBeats = measureBeats * (position - previousPosition) / dataLength;
                    durationSeconds += segmentBeats * 60.0 / bpm;
                    if (bpmEvent.Value > 0) bpm = bpmEvent.Value;
                    previousPosition = position;
                }

                var remainingBeats = measureBeats * (dataLength - previousPosition) / dataLength;
                durationSeconds += remainingBeats * 60.0 / bpm;
            }

            return durationSeconds < LongChartThresholdSeconds ? 3 : 5;
        }
    }
}
