using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class ProjectIndex
    {
        private readonly Dictionary<string, SongRecord> byId =
            new Dictionary<string, SongRecord>(StringComparer.Ordinal);
        private readonly Dictionary<int, List<SongRecord>> byUniqueId =
            new Dictionary<int, List<SongRecord>>();

        private ProjectIndex() { }

        public IReadOnlyDictionary<string, SongRecord> ById => byId;
        public IReadOnlyDictionary<int, List<SongRecord>> ByUniqueId => byUniqueId;
        public IReadOnlyList<ProjectDiagnostic> Diagnostics { get; private set; }

        public static ProjectIndex Build(TaikoProject project)
        {
            var index = new ProjectIndex();
            var diagnostics = new List<ProjectDiagnostic>();

            foreach (var row in project.MusicInfo.Items.OfType<JsonObject>())
            {
                var id = JsonRow.GetString(row, "id");
                var uniqueId = JsonRow.GetInt(row, "uniqueId") ?? 0;
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Error, string.Empty,
                        "musicinfo", "A row has no song id."));
                    continue;
                }

                if (index.byId.ContainsKey(id))
                {
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Error, id,
                        "musicinfo", "Duplicate song id."));
                    continue;
                }

                var record = new SongRecord { Id = id, UniqueId = uniqueId, MusicInfo = row };
                index.byId.Add(id, record);
                if (!index.byUniqueId.TryGetValue(uniqueId, out var uidRows))
                    index.byUniqueId.Add(uniqueId, uidRows = new List<SongRecord>());
                uidRows.Add(record);
            }

            foreach (var pair in index.byUniqueId.Where(pair => pair.Key != 0 && pair.Value.Count > 1))
            {
                foreach (var record in pair.Value)
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Error, record.Id,
                        "musicinfo", $"Unique id {pair.Key} is shared by multiple songs."));
            }

            AttachSingle(project.MusicAttribute.Items, index, diagnostics, "music_attribute",
                (record, row) => { record.MusicAttribute = row; record.HasUra = JsonRow.GetBool(row, "canPlayUra") == true; });
            AttachMany(project.MusicOrder.Items, index, diagnostics, "music_order", (record, row) => record.MusicOrders.Add(row));
            AttachMany(project.MusicAiSection.Items, index, diagnostics, "music_ai_section", (record, row) => record.MusicAiSections.Add(row));
            AttachMany(project.MusicUsbSetting.Items, index, diagnostics, "music_usbsetting", (record, row) => record.MusicUsbSettings.Add(row));

            foreach (var row in project.WordList.Items.OfType<JsonObject>())
            {
                var key = JsonRow.GetString(row, "key");
                if (string.IsNullOrEmpty(key)) continue;
                AssignWord(index, key, "song_sub_", row, (record, value) => record.SubtitleWord = value);
                AssignWord(index, key, "song_detail_", row, (record, value) => record.DetailWord = value);
                AssignWord(index, key, "song_", row, (record, value) => record.TitleWord = value);
            }

            foreach (var record in index.byId.Values)
                record.Assets = SongAssets.Discover(project.Paths, record.Id, record.HasUra);

            diagnostics.AddRange(ProjectValidator.Validate(project, index));
            index.Diagnostics = diagnostics;
            return index;
        }

        private static void AttachSingle(JsonArray rows, ProjectIndex index, List<ProjectDiagnostic> diagnostics,
            string component, Action<SongRecord, JsonObject> attach)
        {
            foreach (var row in rows.OfType<JsonObject>())
            {
                var record = Find(index, row);
                if (record == null)
                {
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning,
                        JsonRow.GetString(row, "id"), component, "Row does not match any musicinfo song."));
                    continue;
                }
                attach(record, row);
            }
        }

        private static void AttachMany(JsonArray rows, ProjectIndex index, List<ProjectDiagnostic> diagnostics,
            string component, Action<SongRecord, JsonObject> attach)
        {
            foreach (var row in rows.OfType<JsonObject>())
            {
                var record = Find(index, row);
                if (record == null)
                {
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning,
                        JsonRow.GetString(row, "id"), component, "Row does not match any musicinfo song."));
                    continue;
                }
                attach(record, row);
            }
        }

        private static SongRecord Find(ProjectIndex index, JsonObject row)
        {
            var id = JsonRow.GetString(row, "id");
            if (!string.IsNullOrEmpty(id) && index.byId.TryGetValue(id, out var byId)) return byId;
            var uid = JsonRow.GetInt(row, "uniqueId") ?? 0;
            return uid != 0 && index.byUniqueId.TryGetValue(uid, out var records) && records.Count == 1
                ? records[0]
                : null;
        }

        private static void AssignWord(ProjectIndex index, string key, string prefix, JsonObject row,
            Action<SongRecord, JsonObject> assign)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) return;
            var id = key.Substring(prefix.Length);
            if (index.byId.TryGetValue(id, out var record)) assign(record, row);
        }
    }
}
