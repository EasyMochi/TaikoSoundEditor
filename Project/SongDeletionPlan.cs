using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongDeletionPlan
    {
        private readonly List<JsonObject> musicInfoRows = new List<JsonObject>();
        private readonly List<JsonObject> musicAttributeRows = new List<JsonObject>();
        private readonly List<JsonObject> musicOrderRows = new List<JsonObject>();
        private readonly List<JsonObject> musicAiSectionRows = new List<JsonObject>();
        private readonly List<JsonObject> musicUsbSettingRows = new List<JsonObject>();
        private readonly List<JsonObject> wordRows = new List<JsonObject>();
        private readonly List<string> blockers = new List<string>();

        private SongDeletionPlan(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public int UniqueId { get; private set; }
        public string Title { get; private set; }
        public bool HasSoundBank { get; private set; }
        public bool HasFumenDirectory { get; private set; }
        public string SoundBankPath { get; private set; }
        public string FumenDirectory { get; private set; }
        public IReadOnlyList<string> Blockers => blockers;
        public bool CanDelete => blockers.Count == 0;

        public static SongDeletionPlan Create(TaikoProject project, string id)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A song id is required.", nameof(id));

            var plan = new SongDeletionPlan(id);
            plan.Build(project);
            return plan;
        }

        public void Apply(TaikoProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!CanDelete)
                throw new InvalidOperationException("The deletion plan is ambiguous and cannot be applied.");

            RemoveRows(project.MusicInfo.Items, musicInfoRows);
            RemoveRows(project.MusicAttribute.Items, musicAttributeRows);
            RemoveRows(project.MusicOrder.Items, musicOrderRows);
            RemoveRows(project.MusicAiSection.Items, musicAiSectionRows);
            RemoveRows(project.MusicUsbSetting.Items, musicUsbSettingRows);
            RemoveRows(project.WordList.Items, wordRows);
            project.MarkSongDeleted(Id);
        }

        public string BuildPreview()
        {
            var text = new StringBuilder();
            text.AppendLine($"Song: {Title ?? Id}");
            text.AppendLine($"ID: {Id}");
            text.AppendLine($"Unique ID: {UniqueId}");
            text.AppendLine();
            text.AppendLine("Rows to remove:");
            text.AppendLine($"  musicinfo: {musicInfoRows.Count}");
            text.AppendLine($"  music_attribute: {musicAttributeRows.Count}");
            text.AppendLine($"  music_order: {musicOrderRows.Count}");
            text.AppendLine($"  music_ai_section: {musicAiSectionRows.Count}");
            text.AppendLine($"  music_usbsetting: {musicUsbSettingRows.Count}");
            text.AppendLine($"  wordlist: {wordRows.Count}");
            text.AppendLine();
            text.AppendLine("Assets removed from the next complete staged export:");
            text.AppendLine($"  sound bank: {(HasSoundBank ? SoundBankPath : "not present")}");
            text.AppendLine($"  fumen directory: {(HasFumenDirectory ? FumenDirectory : "not present")}");

            if (blockers.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Deletion blocked:");
                foreach (var blocker in blockers)
                    text.AppendLine("  • " + blocker);
            }
            else
            {
                text.AppendLine();
                text.AppendLine("The source data folder is not modified now. Changes become physical only through complete project export.");
            }

            return text.ToString();
        }

        private void Build(TaikoProject project)
        {
            musicInfoRows.AddRange(project.MusicInfo.Items.OfType<JsonObject>()
                .Where(row => string.Equals(JsonRow.GetString(row, "id"), Id, StringComparison.Ordinal)));

            if (musicInfoRows.Count != 1)
            {
                blockers.Add(musicInfoRows.Count == 0
                    ? "No exact musicinfo row exists for this song id."
                    : $"There are {musicInfoRows.Count} musicinfo rows with this id.");
            }

            if (musicInfoRows.Count > 0)
                UniqueId = JsonRow.GetInt(musicInfoRows[0], "uniqueId") ?? 0;
            if (UniqueId == 0)
                blockers.Add("The song has no non-zero unique id, so related rows cannot be identified safely.");

            if (UniqueId != 0)
            {
                var conflictingMusicInfo = project.MusicInfo.Items.OfType<JsonObject>()
                    .Where(row => !musicInfoRows.Contains(row))
                    .Where(row => JsonRow.GetInt(row, "uniqueId") == UniqueId)
                    .Select(row => JsonRow.GetString(row, "id") ?? "<missing id>")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (conflictingMusicInfo.Count > 0)
                    blockers.Add($"Unique id {UniqueId} is also used by musicinfo song(s): {string.Join(", ", conflictingMusicInfo)}.");
            }

            CollectRelated(project.MusicAttribute.Items, musicAttributeRows, "music_attribute");
            CollectRelated(project.MusicOrder.Items, musicOrderRows, "music_order");
            CollectRelated(project.MusicAiSection.Items, musicAiSectionRows, "music_ai_section");
            CollectRelated(project.MusicUsbSetting.Items, musicUsbSettingRows, "music_usbsetting");

            var wordKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "song_" + Id,
                "song_sub_" + Id,
                "song_detail_" + Id
            };
            wordRows.AddRange(project.WordList.Items.OfType<JsonObject>()
                .Where(row => wordKeys.Contains(JsonRow.GetString(row, "key") ?? string.Empty)));
            Title = wordRows
                .Where(row => string.Equals(JsonRow.GetString(row, "key"), "song_" + Id, StringComparison.Ordinal))
                .Select(row => JsonRow.GetString(row, "japaneseText"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            SoundBankPath = project.Paths.SoundFile(Id);
            FumenDirectory = project.Paths.FumenDirectory(Id);
            HasSoundBank = File.Exists(SoundBankPath);
            HasFumenDirectory = Directory.Exists(FumenDirectory);
        }

        private void CollectRelated(JsonArray source, List<JsonObject> target, string component)
        {
            foreach (var row in source.OfType<JsonObject>())
            {
                var rowId = JsonRow.GetString(row, "id");
                var rowUniqueId = JsonRow.GetInt(row, "uniqueId") ?? 0;

                if (string.Equals(rowId, Id, StringComparison.Ordinal) ||
                    (string.IsNullOrWhiteSpace(rowId) && UniqueId != 0 && rowUniqueId == UniqueId))
                {
                    target.Add(row);
                    continue;
                }

                if (UniqueId != 0 && rowUniqueId == UniqueId && !string.IsNullOrWhiteSpace(rowId))
                    blockers.Add($"{component} contains unique id {UniqueId} under a different song id: {rowId}.");
            }
        }

        private static void RemoveRows(JsonArray source, IEnumerable<JsonObject> rows)
        {
            foreach (var row in rows.ToList())
                source.Remove(row);
        }
    }
}
