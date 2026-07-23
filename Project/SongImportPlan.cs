using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongImportPlan
    {
        private static readonly int[] RequiredDifficulties = { 0, 1, 2, 3 };
        private static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard", "Oni", "Ura" };
        private static readonly string[] DifficultySuffixes = { "e", "n", "h", "m", "x" };

        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<int> placeholderDifficulties = new List<int>();
        private readonly string[] sourceLines;
        private readonly int silenceSeconds;
        private readonly float adjustedOffset;
        private readonly float adjustedDemoStart;

        private SongImportPlan(TaikoProject project, IEnumerable<NewSongData> pendingSongs,
            string audioPath, string tjaPath, string songId, int uniqueId, TJA tja,
            string[] sourceLines, int silenceSeconds)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            PendingSongs = (pendingSongs ?? Enumerable.Empty<NewSongData>()).ToList();
            AudioPath = audioPath;
            TjaPath = tjaPath;
            SongId = songId?.Trim();
            UniqueId = uniqueId;
            Tja = tja ?? throw new ArgumentNullException(nameof(tja));
            this.sourceLines = sourceLines ?? Array.Empty<string>();
            this.silenceSeconds = Math.Max(0, silenceSeconds);
            adjustedOffset = Tja.Headers.Offset - this.silenceSeconds;
            adjustedDemoStart = Tja.Headers.DemoStart + this.silenceSeconds;

            Analyze();
        }

        public TaikoProject Project { get; }
        public IReadOnlyList<NewSongData> PendingSongs { get; }
        public string AudioPath { get; }
        public string TjaPath { get; }
        public string SongId { get; }
        public int UniqueId { get; }
        public TJA Tja { get; }
        public Genre Genre { get; private set; }
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public IReadOnlyList<int> PlaceholderDifficulties => placeholderDifficulties;
        public bool CanImport => errors.Count == 0;

        public static SongImportPlan Create(TaikoProject project, IEnumerable<NewSongData> pendingSongs,
            string audioPath, string tjaPath, string songId, int uniqueId, TJA tja,
            string[] sourceLines, int silenceSeconds)
        {
            return new SongImportPlan(project, pendingSongs, audioPath, tjaPath, songId, uniqueId,
                tja, sourceLines, silenceSeconds);
        }

        public static int FindNextUniqueId(TaikoProject project, IEnumerable<NewSongData> pendingSongs)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var used = new HashSet<int>();
            foreach (var document in project.Datatables.Values)
            {
                foreach (var row in document.Items.OfType<JsonObject>())
                {
                    var uniqueId = JsonRow.GetInt(row, "uniqueId") ?? 0;
                    if (uniqueId > 0) used.Add(uniqueId);
                }
            }

            foreach (var pending in pendingSongs ?? Enumerable.Empty<NewSongData>())
                if (pending.UniqueId > 0) used.Add(pending.UniqueId);

            return used.Count == 0 ? 1 : checked(used.Max() + 1);
        }

        public NewSongData Convert(Action<string> progress)
        {
            if (!CanImport)
                throw new InvalidOperationException("The import plan contains blocking errors.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "TaikoSoundEditor", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                progress?.Invoke("Preparing normalized TJA...");
                EnsurePlaceholderCourses();
                var normalizedTjaPath = Path.Combine(tempRoot, SongId + ".tja");
                File.WriteAllLines(normalizedTjaPath, BuildNormalizedTjaLines(), Encoding.UTF8);

                progress?.Invoke("Converting audio to WAV...");
                var wavPath = Path.Combine(tempRoot, SongId + ".wav");
                WAV.ConvertToWav(AudioPath, wavPath, silenceSeconds);

                progress?.Invoke("Converting TJA charts to fumens...");
                var fumenBinaries = TJA.RunTja2Fumen(normalizedTjaPath);
                if (fumenBinaries.Count < 15)
                    throw new InvalidDataException("tja2fumen did not return the expected 15 chart variants.");

                progress?.Invoke("Converting WAV to IDSP...");
                var idspPath = Path.Combine(tempRoot, SongId + ".idsp");
                IDSP.WavToIdsp(wavPath, idspPath);
                var idsp = File.ReadAllBytes(idspPath);

                progress?.Invoke("Creating NUS3BANK...");
                var musicInfo = CreateMusicInfo();
                var musicAttribute = CreateMusicAttribute();
                var musicOrder = DatatableTypes.CreateMusicOrder(Genre, SongId, UniqueId);

                var titleWord = CreateLocalizedWord("song_" + SongId, BuildTitleTexts(Tja.Headers));
                var subtitleWord = CreateLocalizedWord("song_sub_" + SongId, BuildSubtitleTexts(Tja.Headers));
                var detailWord = CreateLocalizedWord("song_detail_" + SongId, BuildDetailTexts());

                var data = new NewSongData
                {
                    Id = SongId,
                    UniqueId = UniqueId,
                    Wav = File.ReadAllBytes(wavPath),
                    EBin = fumenBinaries[0],
                    HBin = fumenBinaries[1],
                    MBin = fumenBinaries[2],
                    NBin = fumenBinaries[3],
                    XBin = fumenBinaries[4],
                    EBin1 = fumenBinaries[5],
                    HBin1 = fumenBinaries[6],
                    MBin1 = fumenBinaries[7],
                    NBin1 = fumenBinaries[8],
                    XBin1 = fumenBinaries[9],
                    EBin2 = fumenBinaries[10],
                    HBin2 = fumenBinaries[11],
                    MBin2 = fumenBinaries[12],
                    NBin2 = fumenBinaries[13],
                    XBin2 = fumenBinaries[14],
                    MusicInfo = musicInfo,
                    MusicAttribute = musicAttribute,
                    MusicOrder = musicOrder,
                    Word = titleWord.Typed,
                    WordSub = subtitleWord.Typed,
                    WordDetail = detailWord.Typed,
                    WordRow = titleWord.Raw,
                    WordSubRow = subtitleWord.Raw,
                    WordDetailRow = detailWord.Raw,
                    MusicAiSection = SongAdvancedMetadata.CreateAiRow(SongId, UniqueId, Tja, musicInfo),
                    MusicUsbSetting = SongAdvancedMetadata.CreateUsbRow(SongId, UniqueId)
                };
                data.Nus3Bank = NUS3Bank.GetNus3Bank(SongId, UniqueId, idsp, adjustedDemoStart);
                progress?.Invoke("Import conversion complete.");
                return data;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // Temporary-file cleanup must not hide the real conversion result or error.
                }
            }
        }

        public string BuildPreview()
        {
            var text = new StringBuilder();
            text.AppendLine("PROJECT-AWARE TJA IMPORT");
            text.AppendLine();
            var titleTexts = BuildTitleTexts(Tja.Headers);
            var subtitleTexts = BuildSubtitleTexts(Tja.Headers);
            text.AppendLine($"Title (Japanese): {titleTexts.Japanese}");
            text.AppendLine($"Title (English): {titleTexts.English}");
            text.AppendLine($"Title (Traditional Chinese): {titleTexts.ChineseTraditional}");
            text.AppendLine($"Title (Korean): {titleTexts.Korean}");
            text.AppendLine($"Title (Simplified Chinese): {titleTexts.ChineseSimplified}");
            text.AppendLine($"Subtitle (Japanese / English): {subtitleTexts.Japanese} / {subtitleTexts.English}");
            text.AppendLine($"ID / unique ID: {SongId} / {UniqueId}");
            text.AppendLine($"Genre: {Genre} (TJA: {Tja.Headers.Genre})");
            text.AppendLine($"Audio: {AudioPath}");
            text.AppendLine($"TJA: {TjaPath}");
            text.AppendLine($"Added silence: {silenceSeconds} second(s)");
            text.AppendLine($"Adjusted OFFSET / DEMOSTART: {Format(adjustedOffset)} / {Format(adjustedDemoStart)}");
            text.AppendLine();
            text.AppendLine("CHARTS");
            for (var difficulty = 0; difficulty <= 4; difficulty++)
            {
                if (!Tja.Courses.TryGetValue(difficulty, out var course))
                {
                    text.AppendLine($"  {DifficultyNames[difficulty]}: not present");
                    continue;
                }

                var placeholder = placeholderDifficulties.Contains(difficulty) ? "generated placeholder, " : string.Empty;
                var aiSections = GetAiSectionCount(difficulty);
                var stats = TjaCourseStatistics.Calculate(Tja.Headers, course);
                text.AppendLine($"  {DifficultyNames[difficulty]}: {placeholder}{course.Headers.Level}★, " +
                                $"{stats.NoteCount} note(s), renda={Format(stats.RendaTimeSeconds)}s, " +
                                $"fuusen={stats.FuusenTotal}, shinuti={stats.Shinuti}, " +
                                $"branch={course.HasBranches}, AI={aiSections}");
            }

            text.AppendLine();
            text.AppendLine("ROWS TO ADD");
            text.AppendLine("  musicinfo: 1");
            text.AppendLine("  music_attribute: 1");
            text.AppendLine("  music_order: 1 initial category placement");
            text.AppendLine("  music_ai_section: 1 provisional deterministic row");
            text.AppendLine("  music_usbsetting: 1 row with usbVer=\"\"");
            text.AppendLine("  wordlist: 3 rows (title, subtitle, detail)");
            text.AppendLine();
            text.AppendLine("ASSETS TO STAGE");
            text.AppendLine($"  sound/song_{SongId}.nus3bank");
            text.AppendLine($"  fumen/{SongId}/{SongId}_[e,n,h,m]{'{'}_1,_2{'}'}.bin");
            if (Tja.Courses.ContainsKey(4))
                text.AppendLine($"  fumen/{SongId}/{SongId}_x{'{'}_1,_2{'}'}.bin");

            if (warnings.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("WARNINGS");
                foreach (var warning in warnings) text.AppendLine("  • " + warning);
            }

            if (errors.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("IMPORT BLOCKED");
                foreach (var error in errors) text.AppendLine("  • " + error);
            }
            else
            {
                text.AppendLine();
                text.AppendLine("Nothing has been changed yet. Confirmation runs all conversions first, then adds the complete song to the in-memory project in one operation.");
            }

            return text.ToString();
        }

        private sealed class LocalizedTextSet
        {
            public string Japanese { get; init; } = string.Empty;
            public string English { get; init; } = string.Empty;
            public string ChineseTraditional { get; init; } = string.Empty;
            public string Korean { get; init; } = string.Empty;
            public string ChineseSimplified { get; init; } = string.Empty;
        }

        private sealed class LocalizedWord
        {
            public IWord Typed { get; init; }
            public JsonObject Raw { get; init; }
        }

        private static LocalizedTextSet BuildTitleTexts(TJA.Header headers)
        {
            var english = FirstNonEmpty(headers.TitleEn, headers.Title, headers.TitleJa);
            var japanese = FirstNonEmpty(headers.TitleJa, headers.Title, english);

            return new LocalizedTextSet
            {
                Japanese = japanese,
                English = english,
                ChineseTraditional = FirstNonEmpty(headers.TitleChineseTraditional, english),
                Korean = FirstNonEmpty(headers.TitleKo, english),
                ChineseSimplified = FirstNonEmpty(headers.TitleChineseSimplified, english)
            };
        }

        private static LocalizedTextSet BuildSubtitleTexts(TJA.Header headers)
        {
            var english = FirstNonEmpty(headers.SubtitleEn, headers.Subtitle, headers.SubtitleJa);
            var japanese = FirstNonEmpty(headers.SubtitleJa, headers.Subtitle, english);

            return new LocalizedTextSet
            {
                Japanese = japanese,
                English = english,
                ChineseTraditional = FirstNonEmpty(headers.SubtitleChineseTraditional, english),
                Korean = FirstNonEmpty(headers.SubtitleKo, english),
                ChineseSimplified = FirstNonEmpty(headers.SubtitleChineseSimplified, english)
            };
        }

        private static LocalizedTextSet BuildDetailTexts()
        {
            // TITLEJA belongs in the Japanese title field, not in song_detail. Keep the
            // detail row available for later editing without inventing duplicate text.
            return new LocalizedTextSet();
        }

        private static string FirstNonEmpty(params string[] values) =>
            values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        private static LocalizedWord CreateLocalizedWord(string key, LocalizedTextSet texts)
        {
            texts ??= new LocalizedTextSet();
            var typed = DatatableTypes.CreateWord(key, texts.Japanese);
            var raw = new JsonObject
            {
                ["key"] = key,
                ["japaneseText"] = texts.Japanese,
                ["japaneseFontType"] = 0,
                ["englishUsText"] = texts.English,
                ["englishUsFontType"] = 1,
                ["chineseTText"] = texts.ChineseTraditional,
                ["chineseTFontType"] = 2,
                ["koreanText"] = texts.Korean,
                ["koreanFontType"] = 3,
                ["chineseSText"] = texts.ChineseSimplified,
                ["chineseSFontType"] = 4
            };

            // Some datatable definitions expose every language as typed properties while
            // older definitions expose Japanese only. Populate whatever exists, then keep
            // the raw row as the lossless source for the remaining fields.
            SetTypedWordProperty(typed, "JapaneseText", texts.Japanese);
            SetTypedWordProperty(typed, "JapaneseFontType", 0);
            SetTypedWordProperty(typed, "EnglishUsText", texts.English);
            SetTypedWordProperty(typed, "EnglishUsFontType", 1);
            SetTypedWordProperty(typed, "ChineseTText", texts.ChineseTraditional);
            SetTypedWordProperty(typed, "ChineseTFontType", 2);
            SetTypedWordProperty(typed, "KoreanText", texts.Korean);
            SetTypedWordProperty(typed, "KoreanFontType", 3);
            SetTypedWordProperty(typed, "ChineseSText", texts.ChineseSimplified);
            SetTypedWordProperty(typed, "ChineseSFontType", 4);

            return new LocalizedWord { Typed = typed, Raw = raw };
        }

        private static void SetTypedWordProperty(object target, string propertyName, object value)
        {
            if (target == null) return;
            var property = target.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null || !property.CanWrite) return;
            property.SetValue(target, value);
        }

        private void Analyze()
        {
            if (string.IsNullOrWhiteSpace(AudioPath) || !File.Exists(AudioPath))
                errors.Add("The selected audio file does not exist.");
            if (string.IsNullOrWhiteSpace(TjaPath) || !File.Exists(TjaPath))
                errors.Add("The selected TJA file does not exist.");
            if (string.IsNullOrWhiteSpace(SongId) || SongId.Length > 6)
                errors.Add("The song id must contain between 1 and 6 characters.");
            if (UniqueId <= 0 || UniqueId > ushort.MaxValue)
                errors.Add("The unique id must fit the NUS3BANK 16-bit song-id field (1-65535).");

            Genre = ParseGenre(Tja.Headers.Genre, out var knownGenre);
            if (!knownGenre)
                warnings.Add($"Unknown TJA genre '{Tja.Headers.Genre}'. The initial category will be Pop.");

            foreach (var difficulty in RequiredDifficulties.Where(value => !Tja.Courses.ContainsKey(value)))
            {
                placeholderDifficulties.Add(difficulty);
                warnings.Add($"{DifficultyNames[difficulty]} is missing and will receive a one-note 1★ placeholder chart.");
            }

            CheckSongIdentityCollisions();
            CheckAssetCollisions();
            CheckWordCollisions();
        }

        private void CheckSongIdentityCollisions()
        {
            foreach (var pair in Project.Datatables)
            {
                foreach (var row in pair.Value.Items.OfType<JsonObject>())
                {
                    var id = JsonRow.GetString(row, "id");
                    var uniqueId = JsonRow.GetInt(row, "uniqueId") ?? 0;
                    if (string.Equals(id, SongId, StringComparison.Ordinal))
                        errors.Add($"{pair.Key} already contains song id '{SongId}'.");
                    if (uniqueId == UniqueId)
                        errors.Add($"{pair.Key} already contains unique id {UniqueId}.");
                }
            }

            foreach (var pending in PendingSongs)
            {
                if (string.Equals(pending.Id, SongId, StringComparison.Ordinal))
                    errors.Add($"A pending imported song already uses id '{SongId}'.");
                if (pending.UniqueId == UniqueId)
                    errors.Add($"A pending imported song already uses unique id {UniqueId}.");
            }

            Deduplicate(errors);
        }

        private void CheckAssetCollisions()
        {
            var soundPath = Project.Paths.SoundFile(SongId);
            var fumenPath = Project.Paths.FumenDirectory(SongId);
            if (File.Exists(soundPath)) errors.Add($"The source project already contains {soundPath}.");
            if (Directory.Exists(fumenPath)) errors.Add($"The source project already contains {fumenPath}.");
        }

        private void CheckWordCollisions()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal)
            {
                "song_" + SongId,
                "song_sub_" + SongId,
                "song_detail_" + SongId
            };
            foreach (var row in Project.WordList.Items.OfType<JsonObject>())
            {
                var key = JsonRow.GetString(row, "key");
                if (key != null && keys.Contains(key))
                    errors.Add($"wordlist already contains key '{key}'.");
            }
        }

        private void EnsurePlaceholderCourses()
        {
            foreach (var difficulty in placeholderDifficulties)
            {
                if (Tja.Courses.ContainsKey(difficulty)) continue;
                Tja.Courses[difficulty] = new TJA.Course(difficulty,
                    new TJA.CourseHeader
                    {
                        Course = DifficultyNames[difficulty],
                        Level = 1,
                        Balloon = Array.Empty<int>()
                    },
                    new List<TJA.Measure>
                    {
                        new TJA.Measure(new[] { 4, 4 }, new Dictionary<string, bool>(), "1000",
                            new List<TJA.MeasureEvent>())
                    });
            }
        }

        private string[] BuildNormalizedTjaLines()
        {
            var output = new List<string>();
            var foundOffset = false;
            var foundDemoStart = false;
            foreach (var sourceLine in sourceLines)
            {
                var trimmed = sourceLine.TrimStart();
                if (trimmed.StartsWith("OFFSET:", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add("OFFSET:" + Format(adjustedOffset));
                    foundOffset = true;
                }
                else if (trimmed.StartsWith("DEMOSTART:", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add("DEMOSTART:" + Format(adjustedDemoStart));
                    foundDemoStart = true;
                }
                else
                {
                    output.Add(sourceLine);
                }
            }

            if (!foundOffset) output.Insert(0, "OFFSET:" + Format(adjustedOffset));
            if (!foundDemoStart) output.Insert(foundOffset ? 1 : 0, "DEMOSTART:" + Format(adjustedDemoStart));

            foreach (var difficulty in placeholderDifficulties)
            {
                output.Add(string.Empty);
                output.Add("COURSE:" + DifficultyNames[difficulty]);
                output.Add("LEVEL:1");
                output.Add("BALLOON:");
                output.Add("#START");
                output.Add("1000,");
                output.Add("#END");
            }

            return output.ToArray();
        }

        private IMusicInfo CreateMusicInfo()
        {
            var info = DatatableTypes.CreateMusicInfo(SongId, UniqueId);
            info.Genre = Genre;
            info.SongFileName = "sound/song_" + SongId;

            ApplyCourse(info, 0);
            ApplyCourse(info, 1);
            ApplyCourse(info, 2);
            ApplyCourse(info, 3);
            ApplyCourse(info, 4);
            return info;
        }

        private IMusicAttribute CreateMusicAttribute()
        {
            var attribute = DatatableTypes.CreateMusicAttribute(SongId, UniqueId, true);
            attribute.CanPlayUra = Tja.Courses.ContainsKey(4);
            return attribute;
        }

        private void ApplyCourse(IMusicInfo info, int difficulty)
        {
            if (!Tja.Courses.TryGetValue(difficulty, out var course)) return;
            var stats = TjaCourseStatistics.Calculate(Tja.Headers, course);

            switch (difficulty)
            {
                case 0:
                    info.EasyOnpuNum = stats.NoteCount;
                    info.StarEasy = course.Headers.Level;
                    info.BranchEasy = course.HasBranches;
                    info.RendaTimeEasy = stats.RendaTimeSeconds;
                    info.FuusenTotalEasy = stats.FuusenTotal;
                    info.ShinutiEasy = info.ShinutiEasyDuet = stats.Shinuti;
                    info.ShinutiScoreEasy = info.ShinutiScoreEasyDuet = stats.ShinutiScore;
                    break;
                case 1:
                    info.NormalOnpuNum = stats.NoteCount;
                    info.StarNormal = course.Headers.Level;
                    info.BranchNormal = course.HasBranches;
                    info.RendaTimeNormal = stats.RendaTimeSeconds;
                    info.FuusenTotalNormal = stats.FuusenTotal;
                    info.ShinutiNormal = info.ShinutiNormalDuet = stats.Shinuti;
                    info.ShinutiScoreNormal = info.ShinutiScoreNormalDuet = stats.ShinutiScore;
                    break;
                case 2:
                    info.HardOnpuNum = stats.NoteCount;
                    info.StarHard = course.Headers.Level;
                    info.BranchHard = course.HasBranches;
                    info.RendaTimeHard = stats.RendaTimeSeconds;
                    info.FuusenTotalHard = stats.FuusenTotal;
                    info.ShinutiHard = info.ShinutiHardDuet = stats.Shinuti;
                    info.ShinutiScoreHard = info.ShinutiScoreHardDuet = stats.ShinutiScore;
                    break;
                case 3:
                    info.ManiaOnpuNum = stats.NoteCount;
                    info.StarMania = course.Headers.Level;
                    info.BranchMania = course.HasBranches;
                    info.RendaTimeMania = stats.RendaTimeSeconds;
                    info.FuusenTotalMania = stats.FuusenTotal;
                    info.ShinutiMania = info.ShinutiManiaDuet = stats.Shinuti;
                    info.ShinutiScoreMania = info.ShinutiScoreManiaDuet = stats.ShinutiScore;
                    break;
                case 4:
                    info.UraOnpuNum = stats.NoteCount;
                    info.StarUra = course.Headers.Level;
                    info.BranchUra = course.HasBranches;
                    info.RendaTimeUra = stats.RendaTimeSeconds;
                    info.FuusenTotalUra = stats.FuusenTotal;
                    info.ShinutiUra = info.ShinutiUraDuet = stats.Shinuti;
                    info.ShinutiScoreUra = info.ShinutiScoreUraDuet = stats.ShinutiScore;
                    break;
            }
        }

        private int GetAiSectionCount(int difficulty)
        {
            var info = DatatableTypes.CreateMusicInfo(SongId, UniqueId);
            ApplyCourse(info, difficulty);
            var row = SongAdvancedMetadata.CreateAiRow(SongId, UniqueId, Tja, info);
            return JsonRow.GetInt(row, difficulty switch
            {
                0 => "easy",
                1 => "normal",
                2 => "hard",
                3 => "oni",
                _ => "ura"
            }) ?? 3;
        }

        private static Genre ParseGenre(string value, out bool known)
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant()
                .Replace("_", " ").Replace("-", " ");
            known = true;
            return normalized switch
            {
                "POP" => Genre.Pop,
                "ANIME" => Genre.Anime,
                "KIDS" => Genre.Kids,
                "VOCALOID" => Genre.Vocaloid,
                "VOCALOID MUSIC" => Genre.Vocaloid,
                "GAME MUSIC" => Genre.GameMusic,
                "GAMEMUSIC" => Genre.GameMusic,
                "NAMCO ORIGINAL" => Genre.NamcoOriginal,
                "NAMCOORIGINAL" => Genre.NamcoOriginal,
                "VARIETY" => Genre.Variety,
                "CLASSICAL" => Genre.Classical,
                _ => UnknownGenre(out known)
            };
        }

        private static Genre UnknownGenre(out bool known)
        {
            known = false;
            return Genre.Pop;
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static void Deduplicate(List<string> items)
        {
            var unique = items.Distinct(StringComparer.Ordinal).ToList();
            items.Clear();
            items.AddRange(unique);
        }
    }
}
