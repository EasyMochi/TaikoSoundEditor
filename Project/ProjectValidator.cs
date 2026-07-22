using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaikoSoundEditor.Project
{
    internal static class ProjectValidator
    {
        public static IReadOnlyList<ProjectDiagnostic> Validate(TaikoProject project, ProjectIndex index)
        {
            var diagnostics = new List<ProjectDiagnostic>();

            foreach (var record in index.ById.Values)
            {
                if (record.UniqueId == 0)
                    diagnostics.Add(Error(record, "musicinfo", "Song has uniqueId 0."));
                if (record.MusicAttribute == null)
                    diagnostics.Add(Error(record, "music_attribute", "Missing row."));
                if (record.MusicOrders.Count == 0)
                    diagnostics.Add(Error(record, "music_order", "Song is not present in any category/order row."));
                if (record.TitleWord == null)
                    diagnostics.Add(Error(record, "wordlist", "Missing title row."));
                if (record.SubtitleWord == null)
                    diagnostics.Add(Warning(record, "wordlist", "Missing subtitle row."));
                if (record.DetailWord == null)
                    diagnostics.Add(Warning(record, "wordlist", "Missing detail row."));

                var primaryGenre = JsonRow.GetInt(record.MusicInfo, "genreNo");
                if (primaryGenre.HasValue && record.MusicOrders.Count > 0 &&
                    !record.MusicOrders.Any(row => JsonRow.GetInt(row, "genreNo") == primaryGenre))
                {
                    diagnostics.Add(Error(record, "music_order",
                        $"Primary genre {primaryGenre.Value} has no matching category placement."));
                }

                foreach (var duplicate in record.MusicOrders
                    .GroupBy(row => JsonRow.GetInt(row, "genreNo") ?? -1)
                    .Where(group => group.Count() > 1))
                {
                    diagnostics.Add(Error(record, "music_order",
                        $"Duplicate placement in category {duplicate.Key}."));
                }

                foreach (var row in record.MusicOrders)
                {
                    var rowId = JsonRow.GetString(row, "id");
                    var rowUniqueId = JsonRow.GetInt(row, "uniqueId") ?? 0;
                    if (!string.IsNullOrEmpty(rowId) && rowId != record.Id)
                        diagnostics.Add(Error(record, "music_order", $"Placement id is {rowId}."));
                    if (rowUniqueId != 0 && rowUniqueId != record.UniqueId)
                        diagnostics.Add(Error(record, "music_order", $"Placement uniqueId is {rowUniqueId}."));
                }

                if (!record.Assets.HasSoundBank)
                    diagnostics.Add(Warning(record, "sound", $"Missing {Path.GetFileName(record.Assets.SoundBankPath)}."));
                if (!record.Assets.HasFumenDirectory)
                    diagnostics.Add(Warning(record, "fumen", "Missing fumen directory."));
                foreach (var missing in record.Assets.MissingFumens)
                    diagnostics.Add(Warning(record, "fumen", $"Missing {missing}."));
                foreach (var unexpected in record.Assets.UnexpectedFumens)
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Info, record.Id, "fumen", $"Unexpected file {unexpected}."));
            }

            foreach (var bank in Directory.Exists(project.Paths.Sound)
                ? Directory.GetFiles(project.Paths.Sound, "song_*.nus3bank", SearchOption.TopDirectoryOnly)
                : new string[0])
            {
                var name = Path.GetFileNameWithoutExtension(bank);
                var id = name.StartsWith("song_") ? name.Substring(5) : name;
                if (!index.ById.ContainsKey(id))
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, id, "sound", "Orphan sound bank."));
            }

            foreach (var directory in Directory.Exists(project.Paths.Fumen)
                ? Directory.GetDirectories(project.Paths.Fumen, "*", SearchOption.TopDirectoryOnly)
                : new string[0])
            {
                var id = Path.GetFileName(directory);
                if (!index.ById.ContainsKey(id))
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, id, "fumen", "Orphan fumen directory."));
            }

            return diagnostics;
        }

        private static ProjectDiagnostic Error(SongRecord record, string component, string message) =>
            new ProjectDiagnostic(DiagnosticSeverity.Error, record.Id, component, message);
        private static ProjectDiagnostic Warning(SongRecord record, string component, string message) =>
            new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id, component, message);
    }
}
