using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace TaikoSoundEditor.Project
{
    internal sealed class ProjectChartMetadataAudit
    {
        public ProjectChartMetadataAudit(
            IReadOnlyList<ProjectDiagnostic> diagnostics,
            IReadOnlyList<ProjectRepairAction> repairActions)
        {
            Diagnostics = diagnostics;
            RepairActions = repairActions;
        }

        public IReadOnlyList<ProjectDiagnostic> Diagnostics { get; }
        public IReadOnlyList<ProjectRepairAction> RepairActions { get; }
    }

    internal static class ProjectChartMetadataAnalyzer
    {
        private const double RendaToleranceSeconds = 0.05d;

        private static readonly DifficultyMap[] Difficulties =
        {
            new DifficultyMap("Easy", "e", "starEasy", "branchEasy", "easyOnpuNum",
                "rendaTimeEasy", "fuusenTotalEasy", "shinutiEasy", "shinutiScoreEasy",
                "shinutiEasyDuet", "shinutiScoreEasyDuet", false),
            new DifficultyMap("Normal", "n", "starNormal", "branchNormal", "normalOnpuNum",
                "rendaTimeNormal", "fuusenTotalNormal", "shinutiNormal", "shinutiScoreNormal",
                "shinutiNormalDuet", "shinutiScoreNormalDuet", false),
            new DifficultyMap("Hard", "h", "starHard", "branchHard", "hardOnpuNum",
                "rendaTimeHard", "fuusenTotalHard", "shinutiHard", "shinutiScoreHard",
                "shinutiHardDuet", "shinutiScoreHardDuet", false),
            new DifficultyMap("Oni", "m", "starMania", "branchMania", "maniaOnpuNum",
                "rendaTimeMania", "fuusenTotalMania", "shinutiMania", "shinutiScoreMania",
                "shinutiManiaDuet", "shinutiScoreManiaDuet", false),
            new DifficultyMap("Ura", "x", "starUra", "branchUra", "uraOnpuNum",
                "rendaTimeUra", "fuusenTotalUra", "shinutiUra", "shinutiScoreUra",
                "shinutiUraDuet", "shinutiScoreUraDuet", true)
        };

        public static ProjectChartMetadataAudit Analyze(TaikoProject project, ProjectIndex index)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (index == null) throw new ArgumentNullException(nameof(index));

            var diagnostics = new List<ProjectDiagnostic>();
            var actions = new List<ProjectRepairAction>();

            foreach (var record in index.ById.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var exactChanges = new List<MetadataChange>();
                var generatedChanges = new List<MetadataChange>();
                var scoreChanges = new List<MetadataChange>();

                foreach (var difficulty in Difficulties)
                {
                    AnalyzeDifficulty(project, record, difficulty, diagnostics,
                        exactChanges, generatedChanges, scoreChanges);
                }

                AddGroupedAction(record, "musicinfo/chart", "Repair fumen-derived chart metadata",
                    "Missing values come directly from the selected fumen branch. Existing nonzero combo, renda, and fuusen values are preserved because legacy and console charts can use different metadata conventions.",
                    exactChanges, actions);
                AddGroupedAction(record, "musicinfo/score", "Fill missing generated Shinuchi values",
                    "These missing initial/score values use the same deterministic generated rule as the fixed6 TJA importer. Existing nonzero initials are never replaced by this action, and implausibly tiny score sentinels are treated as missing.",
                    generatedChanges, actions);
                AddGroupedAction(record, "musicinfo/score", "Repair Shinuchi score totals",
                    "These score totals are calculated from an existing initial plus fumen combo, renda, and fuusen data using the same Shinuchi bonus model as the fixed6 importer. Existing plausible nonzero totals are preserved.",
                    scoreChanges, actions);
            }

            return new ProjectChartMetadataAudit(diagnostics, actions);
        }

        private static void AnalyzeDifficulty(TaikoProject project, SongRecord record, DifficultyMap map,
            List<ProjectDiagnostic> diagnostics, List<MetadataChange> exactChanges,
            List<MetadataChange> generatedChanges, List<MetadataChange> scoreChanges)
        {
            var row = record.MusicInfo;
            if (row == null) return;

            var metadataCombo = JsonRow.GetInt(row, map.NoteField) ?? 0;
            var basePath = Path.Combine(project.Paths.FumenDirectory(record.Id),
                $"{record.Id}_{map.FileCode}.bin");
            if (!File.Exists(basePath)) return;

            if (!FumenChartStatistics.TryRead(basePath, out var chart, out var readError))
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "fumen metadata", $"{map.DisplayName}: could not inspect {Path.GetFileName(basePath)}: {readError}"));
                return;
            }

            if (chart.Wrapper == FumenWrapper.AesRaw)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "fumen wrapper", $"{map.DisplayName}: encrypted fumen is missing its gzip layer."));
            }
            if (chart.PaddedBytes > 0)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Info, record.Id,
                    "fumen metadata", $"{map.DisplayName}: parser tolerated {chart.PaddedBytes} missing EOF byte(s)."));
            }

            var oldBranchFlag = JsonRow.GetBool(row, map.BranchField) ?? false;
            var expectedBranchFlag = chart.HeaderHasBranches || chart.HasMultiplePopulatedBranches;
            if (oldBranchFlag != expectedBranchFlag)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/chart", $"{map.DisplayName}: {map.BranchField} is {oldBranchFlag.ToString().ToLowerInvariant()} but the fumen indicates {expectedBranchFlag.ToString().ToLowerInvariant()}."));
                AddChange(exactChanges, map.BranchField, oldBranchFlag, expectedBranchFlag,
                    $"{map.DisplayName}: fumen branch header and populated paths in {Path.GetFileName(basePath)}");
            }

            var choice = ChooseBranch(chart, metadataCombo);
            if (!choice.IsUsable)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/chart", $"{map.DisplayName}: {choice.Reason} {DescribeBranches(chart)}"));
                return;
            }

            var branch = choice.Branch;
            var source = $"{map.DisplayName}, {Path.GetFileName(basePath)}, branch {choice.BranchName}";

            if (branch.HasSpecialNoteTypes)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Info, record.Id,
                    "fumen metadata", $"{map.DisplayName}: special/unknown note type(s) {FormatTypes(branch.SpecialNoteTypes)}; combo and generated Shinuchi repairs are withheld."));
            }

            var mismatchParts = new List<string>();
            if (!branch.HasSpecialNoteTypes && metadataCombo != branch.NoteCount)
            {
                mismatchParts.Add($"notes {metadataCombo} vs fumen {branch.NoteCount}");
                // A nonzero legacy/console combo can intentionally differ from a generic parser.
                // Only fill an actually missing count; leave nonzero disagreements for review.
                if (metadataCombo <= 0)
                    AddChange(exactChanges, map.NoteField, metadataCombo, branch.NoteCount, source);
            }

            var oldRenda = JsonRow.GetDouble(row, map.RendaField) ?? 0d;
            if (oldRenda <= 0d && branch.RendaTimeSeconds > RendaToleranceSeconds)
            {
                mismatchParts.Add($"renda {Format(oldRenda)}s → {Format(branch.RendaTimeSeconds)}s");
                AddChange(exactChanges, map.RendaField, oldRenda, branch.RendaTimeSeconds, source);
            }

            var oldFuusen = JsonRow.GetInt(row, map.FuusenField) ?? 0;
            if (oldFuusen <= 0 && branch.BalloonHits > 0)
            {
                mismatchParts.Add($"fuusen {oldFuusen} → {branch.BalloonHits}");
                AddChange(exactChanges, map.FuusenField, oldFuusen, branch.BalloonHits, source);
            }

            if (mismatchParts.Count > 0)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/chart", $"{map.DisplayName}: {string.Join("; ", mismatchParts)}."));
            }

            AnalyzeSoloScores(record, map, branch, choice, source, diagnostics,
                generatedChanges, scoreChanges);
            AnalyzeDuetScores(project, record, map, branch, choice, diagnostics,
                generatedChanges, scoreChanges);
        }

        private static void AnalyzeSoloScores(SongRecord record, DifficultyMap map,
            FumenBranchStatistics branch, BranchChoice choice, string source,
            List<ProjectDiagnostic> diagnostics, List<MetadataChange> generatedChanges,
            List<MetadataChange> scoreChanges)
        {
            var row = record.MusicInfo;
            var initial = JsonRow.GetInt(row, map.ShinuchiField) ?? 0;
            var score = JsonRow.GetInt(row, map.ShinuchiScoreField) ?? 0;

            if (initial <= 0)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName}: {map.ShinuchiField} is missing or zero."));

                if (!branch.HasSpecialNoteTypes && choice.IsUsable && branch.NoteCount > 0)
                {
                    var generated = branch.CalculateGeneratedShinuchi();
                    AddChange(generatedChanges, map.ShinuchiField, initial, generated.Initial,
                        source + "; fixed6 generated rule");
                    if (IsMissingOrInvalidScore(score))
                        AddChange(generatedChanges, map.ShinuchiScoreField, score, generated.Score,
                            source + "; fixed6 generated rule");
                }
                return;
            }

            if (branch.HasSpecialNoteTypes) return;
            var calculated = branch.CalculateScoreFromInitial(initial);
            if (IsMissingOrInvalidScore(score))
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName}: {map.ShinuchiScoreField} is missing or invalid; calculated total is {calculated}."));
                AddChange(scoreChanges, map.ShinuchiScoreField, score, calculated,
                    source + "; Shinuchi score total from existing initial");
            }
            else if (score > 5_000_000 && calculated > 0)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName}: {map.ShinuchiScoreField}={score} is implausibly high; calculated total is {calculated}."));
                AddChange(scoreChanges, map.ShinuchiScoreField, score, calculated,
                    source + "; impossible-value correction");
            }
        }

        private static void AnalyzeDuetScores(TaikoProject project, SongRecord record, DifficultyMap map,
            FumenBranchStatistics baseBranch, BranchChoice baseChoice,
            List<ProjectDiagnostic> diagnostics, List<MetadataChange> generatedChanges,
            List<MetadataChange> scoreChanges)
        {
            var row = record.MusicInfo;
            var duetInitial = JsonRow.GetInt(row, map.ShinuchiDuetField) ?? 0;
            var duetScore = JsonRow.GetInt(row, map.ShinuchiScoreDuetField) ?? 0;
            if (duetInitial > 0 && !IsMissingOrInvalidScore(duetScore) && duetScore <= 5_000_000) return;

            var directory = project.Paths.FumenDirectory(record.Id);
            var path1 = Path.Combine(directory, $"{record.Id}_{map.FileCode}_1.bin");
            var path2 = Path.Combine(directory, $"{record.Id}_{map.FileCode}_2.bin");
            if (!File.Exists(path1) || !File.Exists(path2)) return;

            var read1 = FumenChartStatistics.TryRead(path1, out var chart1, out var error1);
            var read2 = FumenChartStatistics.TryRead(path2, out var chart2, out var error2);
            if (!read1 || !read2)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName} duet: could not inspect both player fumens: {error1 ?? error2}"));
                return;
            }

            var metadataCombo = JsonRow.GetInt(row, map.NoteField) ?? 0;
            var choice1 = ChooseBranch(chart1, metadataCombo);
            var choice2 = ChooseBranch(chart2, metadataCombo);
            if (!choice1.IsUsable || !choice2.IsUsable ||
                choice1.Branch.HasSpecialNoteTypes || choice2.Branch.HasSpecialNoteTypes)
                return;

            if (!choice1.Branch.ScoreEquivalentTo(choice2.Branch))
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName} duet: P1 and P2 produce different score statistics; automatic duet score repair is withheld."));
                return;
            }

            var branch = choice1.Branch;
            var source = $"{map.DisplayName} duet, {Path.GetFileName(path1)} + {Path.GetFileName(path2)}";
            var targetInitial = duetInitial;
            var generatedInitial = false;

            if (duetInitial <= 0)
            {
                if (baseChoice.IsUsable && branch.ScoreEquivalentTo(baseBranch))
                {
                    var soloInitial = JsonRow.GetInt(row, map.ShinuchiField) ?? 0;
                    if (soloInitial > 0) targetInitial = soloInitial;
                }

                if (targetInitial <= 0)
                {
                    targetInitial = branch.CalculateGeneratedShinuchi().Initial;
                    generatedInitial = true;
                }

                if (targetInitial > 0)
                {
                    diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                        "musicinfo/score", $"{map.DisplayName}: {map.ShinuchiDuetField} is missing or zero."));
                    AddChange(generatedChanges, map.ShinuchiDuetField, duetInitial, targetInitial,
                        generatedInitial ? source + "; fixed6 generated rule" : source + "; copied from equivalent solo chart");
                }
            }

            if (targetInitial <= 0) return;

            int targetScore;
            string scoreReason;
            if (generatedInitial)
            {
                targetScore = branch.CalculateGeneratedShinuchi().Score;
                scoreReason = source + "; fixed6 generated rule";
            }
            else
            {
                targetScore = branch.CalculateScoreFromInitial(targetInitial);
                scoreReason = source + "; Shinuchi score total from existing initial";
            }

            if (IsMissingOrInvalidScore(duetScore))
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName}: {map.ShinuchiScoreDuetField} is missing or invalid; calculated total is {targetScore}."));
                var targetList = duetInitial <= 0 ? generatedChanges : scoreChanges;
                AddChange(targetList, map.ShinuchiScoreDuetField, duetScore, targetScore, scoreReason);
            }
            else if (duetScore > 5_000_000 && targetScore > 0)
            {
                diagnostics.Add(new ProjectDiagnostic(DiagnosticSeverity.Warning, record.Id,
                    "musicinfo/score", $"{map.DisplayName}: {map.ShinuchiScoreDuetField}={duetScore} is implausibly high; calculated total is {targetScore}."));
                AddChange(scoreChanges, map.ShinuchiScoreDuetField, duetScore, targetScore,
                    source + "; impossible-value correction");
            }
        }

        private static bool IsMissingOrInvalidScore(int value) => value < 100_000;

        private static BranchChoice ChooseBranch(FumenChartStatistics chart, int metadataCombo)
        {
            var all = chart.Branches
                .Select((branch, index) => new { Branch = branch, Index = index })
                .ToList();
            var populated = all.Where(item => item.Branch.NoteCount > 0).ToList();
            if (populated.Count == 0)
                populated = all.Where(item => item.Branch.IsPopulated).ToList();

            if (populated.Count == 0)
                return BranchChoice.Unusable("fumen has no populated branch.");

            if (metadataCombo > 0)
            {
                var matches = populated.Where(item => item.Branch.NoteCount == metadataCombo).ToList();
                if (matches.Count == 1)
                    return BranchChoice.Usable(matches[0].Branch, matches[0].Index);
                if (matches.Count > 1 && matches.All(item => item.Branch.ScoreEquivalentTo(matches[0].Branch)))
                {
                    var chosen = matches.OrderByDescending(item => item.Index).First();
                    return BranchChoice.Usable(chosen.Branch, chosen.Index);
                }
            }

            if (populated.Count == 1)
                return BranchChoice.Usable(populated[0].Branch, populated[0].Index);

            if (metadataCombo <= 0)
            {
                if (chart.HeaderHasBranches && chart.Branches.Count > 2 &&
                    chart.Branches[2].NoteCount > 0)
                    return BranchChoice.Usable(chart.Branches[2], 2);

                var max = populated.Max(item => item.Branch.NoteCount);
                var maxima = populated.Where(item => item.Branch.NoteCount == max).ToList();
                if (maxima.Count == 1)
                    return BranchChoice.Usable(maxima[0].Branch, maxima[0].Index);
                if (maxima.All(item => item.Branch.ScoreEquivalentTo(maxima[0].Branch)))
                {
                    var chosen = maxima.OrderByDescending(item => item.Index).First();
                    return BranchChoice.Usable(chosen.Branch, chosen.Index);
                }
            }

            return BranchChoice.Unusable(
                $"musicinfo combo {metadataCombo} does not identify one unambiguous branch.");
        }

        private static string DescribeBranches(FumenChartStatistics chart)
        {
            var names = new[] { "N", "E", "M" };
            var values = chart.Branches.Select((branch, index) =>
                $"{names[index]}={branch.NoteCount}");
            return "Branches: " + string.Join(", ", values) + ".";
        }

        private static string FormatTypes(IEnumerable<int> types) =>
            string.Join(", ", types.OrderBy(value => value).Select(value => $"0x{value:X}"));

        private static void AddChange(List<MetadataChange> target, string field,
            object oldValue, object newValue, string reason)
        {
            if (ValuesEqual(oldValue, newValue)) return;
            if (target.Any(change => string.Equals(change.Field, field, StringComparison.Ordinal))) return;
            target.Add(new MetadataChange(field, oldValue, newValue, reason));
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (left is double leftDouble && right is double rightDouble)
                return Math.Abs(leftDouble - rightDouble) < 0.0000001d;
            return Equals(left, right);
        }

        private static void AddGroupedAction(SongRecord record, string component, string title,
            string explanation, List<MetadataChange> changes, List<ProjectRepairAction> actions)
        {
            if (changes.Count == 0) return;
            var captured = changes.ToList();
            var preview = new StringBuilder();
            preview.AppendLine(explanation);
            preview.AppendLine();
            foreach (var change in captured)
            {
                preview.AppendLine($"{change.Field}: {FormatValue(change.OldValue)} → {FormatValue(change.NewValue)}");
                preview.AppendLine($"  {change.Reason}");
            }

            actions.Add(new ProjectRepairAction(record.Id, component, title, preview.ToString(), () =>
            {
                foreach (var change in captured)
                    SetValue(record.MusicInfo, change.Field, change.NewValue);
            }));
        }

        private static void SetValue(JsonObject row, string field, object value)
        {
            switch (value)
            {
                case int integer:
                    row[field] = integer;
                    break;
                case double number:
                    row[field] = number;
                    break;
                case bool boolean:
                    row[field] = boolean;
                    break;
                default:
                    row[field] = JsonValue.Create(value?.ToString() ?? string.Empty);
                    break;
            }
        }

        private static string FormatValue(object value)
        {
            if (value is double number) return Format(number);
            if (value is bool boolean) return boolean ? "true" : "false";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>";
        }

        private static string Format(double value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);

        private sealed class MetadataChange
        {
            public MetadataChange(string field, object oldValue, object newValue, string reason)
            {
                Field = field;
                OldValue = oldValue;
                NewValue = newValue;
                Reason = reason;
            }

            public string Field { get; }
            public object OldValue { get; }
            public object NewValue { get; }
            public string Reason { get; }
        }

        private sealed class DifficultyMap
        {
            public DifficultyMap(string displayName, string fileCode, string starField, string branchField,
                string noteField, string rendaField, string fuusenField, string shinuchiField,
                string shinuchiScoreField, string shinuchiDuetField, string shinuchiScoreDuetField,
                bool isUra)
            {
                DisplayName = displayName;
                FileCode = fileCode;
                StarField = starField;
                BranchField = branchField;
                NoteField = noteField;
                RendaField = rendaField;
                FuusenField = fuusenField;
                ShinuchiField = shinuchiField;
                ShinuchiScoreField = shinuchiScoreField;
                ShinuchiDuetField = shinuchiDuetField;
                ShinuchiScoreDuetField = shinuchiScoreDuetField;
                IsUra = isUra;
            }

            public string DisplayName { get; }
            public string FileCode { get; }
            public string StarField { get; }
            public string BranchField { get; }
            public string NoteField { get; }
            public string RendaField { get; }
            public string FuusenField { get; }
            public string ShinuchiField { get; }
            public string ShinuchiScoreField { get; }
            public string ShinuchiDuetField { get; }
            public string ShinuchiScoreDuetField { get; }
            public bool IsUra { get; }
        }

        private sealed class BranchChoice
        {
            private static readonly string[] Names = { "N", "E", "M" };

            private BranchChoice(FumenBranchStatistics branch, int branchIndex, string reason)
            {
                Branch = branch;
                BranchIndex = branchIndex;
                Reason = reason;
            }

            public FumenBranchStatistics Branch { get; }
            public int BranchIndex { get; }
            public string Reason { get; }
            public bool IsUsable => Branch != null;
            public string BranchName => IsUsable && BranchIndex >= 0 && BranchIndex < Names.Length
                ? Names[BranchIndex]
                : "?";

            public static BranchChoice Usable(FumenBranchStatistics branch, int index) =>
                new BranchChoice(branch, index, string.Empty);

            public static BranchChoice Unusable(string reason) =>
                new BranchChoice(null, -1, reason);
        }
    }
}
