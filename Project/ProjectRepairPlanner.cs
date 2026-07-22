using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TaikoSoundEditor.Commons.IO;

namespace TaikoSoundEditor.Project
{
    internal static class ProjectRepairPlanner
    {
        public static IReadOnlyList<ProjectRepairAction> Build(TaikoProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var index = project.BuildIndex();
            var actions = new List<ProjectRepairAction>();

            foreach (var record in index.ById.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                AddMissingRows(project, record, actions);
                AddIdentityRepairs(project, index, record, actions);
            }

            AddOrphanRowRepairs(project, index, actions);
            return actions;
        }

        private static void AddMissingRows(TaikoProject project, SongRecord record, List<ProjectRepairAction> actions)
        {
            if (record.UniqueId <= 0 || HasAmbiguousIdentity(project, record)) return;

            if (record.MusicAttribute == null)
            {
                var typed = DatatableTypes.CreateMusicAttribute(record.Id, record.UniqueId, false);
                var row = SerializeRow(typed);
                actions.Add(AddRow(record.Id, "music_attribute", "Create missing attribute row", row,
                    project.MusicAttribute.Items));
            }

            if (record.MusicOrders.Count == 0)
            {
                var genreNo = JsonRow.GetInt(record.MusicInfo, "genreNo");
                if (genreNo.HasValue && genreNo.Value >= 0 && genreNo.Value <= 7)
                {
                    var row = new JsonObject
                    {
                        ["genreNo"] = genreNo.Value,
                        ["id"] = record.Id,
                        ["uniqueId"] = record.UniqueId,
                        ["closeDispType"] = 0
                    };
                    actions.Add(AddRow(record.Id, "music_order", "Create missing primary category placement", row,
                        project.MusicOrder.Items));
                }
            }
            else
            {
                var primaryGenre = JsonRow.GetInt(record.MusicInfo, "genreNo");
                if (primaryGenre.HasValue && primaryGenre.Value >= 0 && primaryGenre.Value <= 7 &&
                    !record.MusicOrders.Any(row => JsonRow.GetInt(row, "genreNo") == primaryGenre.Value))
                {
                    var row = new JsonObject
                    {
                        ["genreNo"] = primaryGenre.Value,
                        ["id"] = record.Id,
                        ["uniqueId"] = record.UniqueId,
                        ["closeDispType"] = 0
                    };
                    actions.Add(AddRow(record.Id, "music_order", "Add missing primary category placement", row,
                        project.MusicOrder.Items));
                }
            }

            if (record.TitleWord == null)
                actions.Add(AddWord(project, record.Id, "song_" + record.Id, "Create missing title row"));
            if (record.SubtitleWord == null)
                actions.Add(AddWord(project, record.Id, "song_sub_" + record.Id, "Create missing subtitle row"));
            if (record.DetailWord == null)
                actions.Add(AddWord(project, record.Id, "song_detail_" + record.Id, "Create missing detail row"));

            if (record.MusicAiSections.Count == 0)
            {
                var info = DatatableTypes.CreateMusicInfo(record.Id, record.UniqueId);
                info.StarMania = JsonRow.GetInt(record.MusicInfo, "starMania") ?? 0;
                info.StarUra = JsonRow.GetInt(record.MusicInfo, "starUra") ?? 0;
                var row = SongAdvancedMetadata.CreateDefaultAiRow(record.Id, record.UniqueId, info);
                actions.Add(AddRow(record.Id, "music_ai_section", "Create missing provisional AI row", row,
                    project.MusicAiSection.Items));
            }

            if (record.MusicUsbSettings.Count == 0)
            {
                var row = SongAdvancedMetadata.CreateUsbRow(record.Id, record.UniqueId);
                actions.Add(AddRow(record.Id, "music_usbsetting", "Create missing blank USB row", row,
                    project.MusicUsbSetting.Items));
            }
        }

        private static void AddIdentityRepairs(TaikoProject project, ProjectIndex index, SongRecord record,
            List<ProjectRepairAction> actions)
        {
            if (record.UniqueId <= 0 || HasAmbiguousIdentity(project, record)) return;

            AddIdentityRepairsForRows(project.MusicAttribute.Items, "music_attribute", index, record, actions);
            AddIdentityRepairsForRows(project.MusicOrder.Items, "music_order", index, record, actions);
            AddIdentityRepairsForRows(project.MusicAiSection.Items, "music_ai_section", index, record, actions);
            AddIdentityRepairsForRows(project.MusicUsbSetting.Items, "music_usbsetting", index, record, actions);
        }

        private static void AddIdentityRepairsForRows(JsonArray rows, string component, ProjectIndex index,
            SongRecord record, List<ProjectRepairAction> actions)
        {
            foreach (var row in rows.OfType<JsonObject>().ToList())
            {
                var rowId = JsonRow.GetString(row, "id");
                var rowUid = JsonRow.GetInt(row, "uniqueId") ?? 0;
                var idPointsHere = string.Equals(rowId, record.Id, StringComparison.Ordinal);
                var uidPointsHere = rowUid == record.UniqueId;
                if (idPointsHere == uidPointsHere) continue;

                if (idPointsHere)
                {
                    if (rowUid != 0 && index.ByUniqueId.TryGetValue(rowUid, out var uidRecords) && uidRecords.Count > 0)
                        continue;

                    var preview = $"{component} row identity:\n  uniqueId: {rowUid} → {record.UniqueId}\n  id remains: {record.Id}";
                    actions.Add(new ProjectRepairAction(record.Id, component, "Normalize row unique ID", preview,
                        () => row["uniqueId"] = record.UniqueId));
                }
                else if (uidPointsHere)
                {
                    if (!string.IsNullOrEmpty(rowId) && index.ById.ContainsKey(rowId)) continue;

                    var preview = $"{component} row identity:\n  id: {rowId ?? "<empty>"} → {record.Id}\n  uniqueId remains: {record.UniqueId}";
                    actions.Add(new ProjectRepairAction(record.Id, component, "Normalize row song ID", preview,
                        () => row["id"] = record.Id));
                }
            }
        }

        private static void AddOrphanRowRepairs(TaikoProject project, ProjectIndex index,
            List<ProjectRepairAction> actions)
        {
            AddOrphanRows(project.MusicAttribute.Items, "music_attribute", index, actions);
            AddOrphanRows(project.MusicOrder.Items, "music_order", index, actions);
            AddOrphanRows(project.MusicAiSection.Items, "music_ai_section", index, actions);
            AddOrphanRows(project.MusicUsbSetting.Items, "music_usbsetting", index, actions);

            foreach (var row in project.WordList.Items.OfType<JsonObject>().ToList())
            {
                var key = JsonRow.GetString(row, "key");
                var id = ExtractCanonicalWordSongId(key);
                if (id == null || index.ById.ContainsKey(id)) continue;

                var preview = $"Remove orphan wordlist row:\n  key: {key}\n  No musicinfo song uses id '{id}'.";
                actions.Add(new ProjectRepairAction(id, "wordlist", "Remove orphan canonical word row", preview,
                    () => project.WordList.Items.Remove(row)));
            }
        }

        private static void AddOrphanRows(JsonArray rows, string component, ProjectIndex index,
            List<ProjectRepairAction> actions)
        {
            foreach (var row in rows.OfType<JsonObject>().ToList())
            {
                var id = JsonRow.GetString(row, "id");
                var uid = JsonRow.GetInt(row, "uniqueId") ?? 0;
                var idMatch = !string.IsNullOrEmpty(id) && index.ById.ContainsKey(id);
                var uidMatch = uid != 0 && index.ByUniqueId.TryGetValue(uid, out var records) && records.Count == 1;
                if (idMatch || uidMatch) continue;

                var preview = $"Remove orphan {component} row:\n  id: {id ?? "<empty>"}\n  uniqueId: {uid}\n  Neither identity matches a musicinfo song.";
                actions.Add(new ProjectRepairAction(id, component, "Remove orphan row", preview,
                    () => rows.Remove(row)));
            }
        }

        private static ProjectRepairAction AddWord(TaikoProject project, string songId, string key, string title)
        {
            var row = SerializeRow(DatatableTypes.CreateWord(key, string.Empty));
            return AddRow(songId, "wordlist", title, row, project.WordList.Items);
        }

        private static ProjectRepairAction AddRow(string songId, string component, string title,
            JsonObject row, JsonArray target)
        {
            var preview = $"Add {component} row:\n{row.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}";
            return new ProjectRepairAction(songId, component, title, preview,
                () => target.Add(row.DeepClone()));
        }

        private static JsonObject SerializeRow(object value)
        {
            return JsonNode.Parse(Json.DynamicSerialize(value, false)) as JsonObject
                ?? throw new InvalidOperationException("Could not serialize a repair row.");
        }

        private static bool HasAmbiguousIdentity(TaikoProject project, SongRecord record)
        {
            var sameId = project.MusicInfo.Items.OfType<JsonObject>()
                .Count(row => string.Equals(JsonRow.GetString(row, "id"), record.Id, StringComparison.Ordinal));
            var sameUid = project.MusicInfo.Items.OfType<JsonObject>()
                .Count(row => JsonRow.GetInt(row, "uniqueId") == record.UniqueId);
            return sameId != 1 || sameUid != 1;
        }

        private static string ExtractCanonicalWordSongId(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (key.StartsWith("song_sub_", StringComparison.Ordinal)) return key.Substring("song_sub_".Length);
            if (key.StartsWith("song_detail_", StringComparison.Ordinal)) return key.Substring("song_detail_".Length);
            if (key.StartsWith("song_", StringComparison.Ordinal)) return key.Substring("song_".Length);
            return null;
        }
    }
}
