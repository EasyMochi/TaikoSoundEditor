using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TaikoSoundEditor.Project
{
    internal sealed class EseBatchImportCandidate : INotifyPropertyChanged
    {
        private bool include;
        private string songId = string.Empty;
        private string runtimeError = string.Empty;
        private string status = string.Empty;

        public bool Include
        {
            get => include;
            set => SetField(ref include, value, nameof(Include));
        }

        public Genre Genre { get; init; }
        public string GenreFolder { get; init; } = string.Empty;
        public string SongFolder { get; init; } = string.Empty;
        public string RelativeFolder { get; init; } = string.Empty;

        public string SongId
        {
            get => songId;
            set => SetField(ref songId, value ?? string.Empty, nameof(SongId));
        }

        public string JapaneseTitle { get; init; } = string.Empty;
        public string EnglishTitle { get; init; } = string.Empty;
        public string TjaPath { get; init; } = string.Empty;
        public string AudioPath { get; init; } = string.Empty;
        public string EncodingName { get; init; } = string.Empty;
        public string Charts { get; init; } = string.Empty;
        public string TjaFileName => string.IsNullOrWhiteSpace(TjaPath) ? string.Empty : Path.GetFileName(TjaPath);
        public string AudioFileName => string.IsNullOrWhiteSpace(AudioPath) ? string.Empty : Path.GetFileName(AudioPath);
        public TjaImportSource Source { get; init; }
        public IReadOnlyList<string> ScanErrors { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ScanWarnings { get; init; } = Array.Empty<string>();

        public string RuntimeError
        {
            get => runtimeError;
            set => SetField(ref runtimeError, value ?? string.Empty, nameof(RuntimeError));
        }

        public string Status
        {
            get => status;
            set => SetField(ref status, value ?? string.Empty, nameof(Status));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal sealed class EseBatchScanResult
    {
        public string RootPath { get; init; } = string.Empty;
        public IReadOnlyList<EseBatchImportCandidate> Candidates { get; init; } =
            Array.Empty<EseBatchImportCandidate>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    internal static class EseBatchImportScanner
    {
        private static readonly Regex ValidSongId =
            new Regex("^[A-Za-z0-9_]{1,6}$", RegexOptions.Compiled);

        private static readonly HashSet<string> SupportedAudioExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".ogg", ".wav", ".mp3", ".flac"
            };

        private static readonly HashSet<string> GenericFileStems =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "song", "main", "chart", "fumen", "music", "audio", "sound", "data"
            };

        public static EseBatchScanResult Scan(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("An ESE root folder is required.", nameof(rootPath));
            rootPath = Path.GetFullPath(rootPath);
            if (!Directory.Exists(rootPath))
                throw new DirectoryNotFoundException(rootPath);

            var candidates = new List<EseBatchImportCandidate>();
            var warnings = new List<string>();

            foreach (var genreDirectory in Directory.EnumerateDirectories(rootPath)
                         .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                var genreFolderName = Path.GetFileName(genreDirectory);
                if (!TryParseGenreFolder(genreFolderName, out var genre))
                {
                    warnings.Add($"Skipped unrecognized top-level folder '{genreFolderName}'.");
                    continue;
                }

                var songDirectories = Directory.EnumerateDirectories(genreDirectory)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (songDirectories.Count == 0)
                    warnings.Add($"Genre folder '{genreFolderName}' contains no song subfolders.");

                foreach (var songDirectory in songDirectories)
                    candidates.Add(ScanSongFolder(rootPath, genreFolderName, genre, songDirectory));
            }

            return new EseBatchScanResult
            {
                RootPath = rootPath,
                Candidates = candidates,
                Warnings = warnings
            };
        }

        public static bool IsValidSongId(string value) =>
            !string.IsNullOrWhiteSpace(value) && ValidSongId.IsMatch(value.Trim());

        private static EseBatchImportCandidate ScanSongFolder(string rootPath, string genreFolder,
            Genre genre, string songDirectory)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var files = Directory.EnumerateFiles(songDirectory, "*", SearchOption.TopDirectoryOnly)
                .ToList();
            var tjaFiles = files.Where(path =>
                    string.Equals(Path.GetExtension(path), ".tja", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var audioFiles = files.Where(path => SupportedAudioExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tjaPath = SelectTja(songDirectory, tjaFiles, audioFiles, errors);
            TjaImportSource source = null;
            if (!string.IsNullOrEmpty(tjaPath))
            {
                try
                {
                    source = TjaImportSource.LoadAuto(tjaPath);
                }
                catch (Exception ex)
                {
                    errors.Add("TJA parse failed: " + ex.Message);
                }
            }

            if (source != null)
            {
                if (source.Tja.Courses.Count == 0)
                    errors.Add("TJA contains no chart courses.");
                var missing = new[] { 0, 1, 2, 3 }
                    .Where(difficulty => !source.Tja.Courses.ContainsKey(difficulty))
                    .Select(difficulty => new[] { "Easy", "Normal", "Hard", "Oni" }[difficulty])
                    .ToList();
                if (missing.Count > 0)
                    warnings.Add("Missing " + string.Join(", ", missing) +
                                 "; 1-note placeholder chart(s) will be generated.");
                if (string.IsNullOrWhiteSpace(source.Tja.Headers.Title) &&
                    string.IsNullOrWhiteSpace(source.Tja.Headers.TitleJa) &&
                    string.IsNullOrWhiteSpace(source.Tja.Headers.TitleEn))
                    warnings.Add("The TJA contains no title metadata.");
            }

            var audioPath = SelectAudio(tjaPath, source, audioFiles, errors);
            if (!string.IsNullOrEmpty(audioPath) &&
                !Path.GetExtension(audioPath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Using {Path.GetExtension(audioPath)} audio because no unambiguous OGG was found.");

            var folderName = Path.GetFileName(songDirectory);
            var songId = SuggestSongId(tjaPath, audioPath, folderName,
                Path.GetRelativePath(rootPath, songDirectory), out var generatedId);
            if (generatedId)
                warnings.Add($"Generated ID '{songId}' because no ESE-style 1-6 character ID was found; review it.");
            var japaneseTitle = source == null
                ? folderName
                : FirstNonEmpty(source.Tja.Headers.TitleJa, source.Tja.Headers.Title, folderName);
            var englishTitle = source == null
                ? string.Empty
                : FirstNonEmpty(source.Tja.Headers.TitleEn, source.Tja.Headers.Title,
                    source.Tja.Headers.TitleJa);

            if (!IsValidSongId(songId))
                warnings.Add("Edit the generated song ID before importing.");

            var status = errors.Count > 0
                ? string.Join("; ", errors)
                : warnings.Count > 0
                    ? "Ready with warning: " + string.Join("; ", warnings)
                    : "Ready";

            return new EseBatchImportCandidate
            {
                Include = errors.Count == 0,
                Genre = genre,
                GenreFolder = genreFolder,
                SongFolder = folderName,
                RelativeFolder = Path.GetRelativePath(rootPath, songDirectory),
                SongId = songId,
                JapaneseTitle = japaneseTitle,
                EnglishTitle = englishTitle,
                TjaPath = tjaPath ?? string.Empty,
                AudioPath = audioPath ?? string.Empty,
                EncodingName = source?.EncodingName ?? string.Empty,
                Charts = BuildChartSummary(source),
                Source = source,
                ScanErrors = errors,
                ScanWarnings = warnings,
                Status = status
            };
        }


        private static string BuildChartSummary(TjaImportSource source)
        {
            if (source == null) return string.Empty;
            var names = new[] { "E", "N", "H", "M", "X" };
            return string.Join("/", source.Tja.Courses.Keys
                .Where(key => key >= 0 && key < names.Length)
                .OrderBy(key => key)
                .Select(key => names[key]));
        }

        private static string SelectTja(string songDirectory, IReadOnlyList<string> tjaFiles,
            IReadOnlyList<string> audioFiles, List<string> errors)
        {
            if (tjaFiles.Count == 0)
            {
                errors.Add("No TJA file found.");
                return null;
            }
            if (tjaFiles.Count == 1) return tjaFiles[0];

            var folderName = Path.GetFileName(songDirectory);
            var folderMatch = tjaFiles.Where(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path), folderName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (folderMatch.Count == 1) return folderMatch[0];

            var audioStems = new HashSet<string>(audioFiles.Select(Path.GetFileNameWithoutExtension),
                StringComparer.OrdinalIgnoreCase);
            var stemMatches = tjaFiles.Where(path =>
                    audioStems.Contains(Path.GetFileNameWithoutExtension(path)))
                .ToList();
            if (stemMatches.Count == 1) return stemMatches[0];

            errors.Add($"Multiple TJA files found ({tjaFiles.Count}); could not choose one safely.");
            return null;
        }

        private static string SelectAudio(string tjaPath, TjaImportSource source,
            IReadOnlyList<string> audioFiles, List<string> errors)
        {
            if (audioFiles.Count == 0)
            {
                errors.Add("No OGG or supported audio file found.");
                return null;
            }

            var referencedWave = source?.Tja.Headers.Wave;
            if (!string.IsNullOrWhiteSpace(referencedWave))
            {
                var waveName = Path.GetFileName(referencedWave.Trim().Trim('"'));
                var waveMatches = audioFiles.Where(path => string.Equals(
                        Path.GetFileName(path), waveName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (waveMatches.Count == 1) return waveMatches[0];

                var waveStem = Path.GetFileNameWithoutExtension(waveName);
                var waveStemMatches = audioFiles.Where(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path), waveStem,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (waveStemMatches.Count == 1) return waveStemMatches[0];
            }

            if (!string.IsNullOrWhiteSpace(tjaPath))
            {
                var tjaStem = Path.GetFileNameWithoutExtension(tjaPath);
                var stemMatches = audioFiles.Where(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path), tjaStem,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (stemMatches.Count == 1) return stemMatches[0];
            }

            var oggFiles = audioFiles.Where(path =>
                    Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (oggFiles.Count == 1) return oggFiles[0];
            if (audioFiles.Count == 1) return audioFiles[0];

            errors.Add($"Multiple audio files found ({audioFiles.Count}); WAVE did not identify one.");
            return null;
        }

        private static string SuggestSongId(string tjaPath, string audioPath, string folderName,
            string relativePath, out bool generated)
        {
            generated = false;
            var candidates = new[]
            {
                string.IsNullOrWhiteSpace(tjaPath) ? string.Empty : Path.GetFileNameWithoutExtension(tjaPath),
                string.IsNullOrWhiteSpace(audioPath) ? string.Empty : Path.GetFileNameWithoutExtension(audioPath),
                folderName ?? string.Empty
            };
            foreach (var candidate in candidates)
            {
                var normalized = NormalizeNaturalId(candidate);
                if (!string.IsNullOrEmpty(normalized)) return normalized;
            }

            var leadingToken = Regex.Match(folderName ?? string.Empty,
                @"^\s*[\[\(]?([A-Za-z0-9_]{1,6})[\]\)]?(?:\s*[-_. ]|$)");
            if (leadingToken.Success && IsValidSongId(leadingToken.Groups[1].Value))
                return leadingToken.Groups[1].Value.ToLowerInvariant();

            generated = true;
            return CreateDeterministicId(relativePath);
        }

        private static string NormalizeNaturalId(string value)
        {
            var candidate = (value ?? string.Empty).Trim();
            if (candidate.StartsWith("song_", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(5);
            if (!IsValidSongId(candidate) || GenericFileStems.Contains(candidate))
                return string.Empty;
            return candidate.ToLowerInvariant();
        }

        private static string CreateDeterministicId(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var b in Encoding.UTF8.GetBytes(value ?? string.Empty))
                {
                    hash ^= b;
                    hash *= 16777619;
                }

                const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
                var chars = new char[5];
                for (var i = chars.Length - 1; i >= 0; i--)
                {
                    chars[i] = alphabet[(int)(hash % 36)];
                    hash /= 36;
                }
                return "e" + new string(chars);
            }
        }

        private static bool TryParseGenreFolder(string folderName, out Genre genre)
        {
            genre = Genre.Pop;
            var match = Regex.Match(folderName ?? string.Empty,
                @"^\s*(?:(\d{1,2})\s*[-_. ]*)?(.*?)\s*$");
            var number = match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var parsed)
                ? parsed
                : 0;
            var name = Regex.Replace(match.Groups[2].Value.ToLowerInvariant(), @"[^a-z0-9]+", " ")
                .Trim();

            if (name.Contains("vocaloid")) genre = Genre.Vocaloid;
            else if (name.Contains("anime")) genre = Genre.Anime;
            else if (name.Contains("kid") || name.Contains("children")) genre = Genre.Kids;
            else if (name.Contains("game")) genre = Genre.GameMusic;
            else if (name.Contains("namco") || name.Contains("original")) genre = Genre.NamcoOriginal;
            else if (name.Contains("variety")) genre = Genre.Variety;
            else if (name.Contains("classical") || name.Contains("classic")) genre = Genre.Classical;
            else if (name.Contains("pop") || name.Contains("j pop") || name.Contains("pops")) genre = Genre.Pop;
            else
            {
                if (number < 1 || number > 8) return false;
                genre = (Genre)(number - 1);
            }

            return true;
        }

        private static string FirstNonEmpty(params string[] values) =>
            values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
