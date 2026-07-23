using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TaikoSoundEditor.Commons.IO;

namespace TaikoSoundEditor.Project
{
    internal enum FumenWrapper
    {
        Raw,
        Gzip,
        AesGzip,
        AesRaw
    }

    internal sealed class FumenBranchStatistics
    {
        private readonly Dictionary<(int Initial, int Difference), int> scorePairs =
            new Dictionary<(int Initial, int Difference), int>();
        private readonly HashSet<int> specialNoteTypes = new HashSet<int>();

        public int NoteCount { get; private set; }
        public int BigNoteCount { get; private set; }
        public int BalloonCount { get; private set; }
        public int BalloonHits { get; private set; }
        public double RendaTimeSeconds { get; private set; }
        public IReadOnlyCollection<int> SpecialNoteTypes => specialNoteTypes;
        public bool HasSpecialNoteTypes => specialNoteTypes.Count > 0;
        public bool IsPopulated => NoteCount > 0 || BalloonCount > 0 || RendaTimeSeconds > 0d;

        public int DominantScoreInitial => scorePairs.Count == 0
            ? 0
            : scorePairs.OrderByDescending(pair => pair.Value)
                .ThenByDescending(pair => pair.Key.Initial)
                .First().Key.Initial;

        public int DominantScoreDifference => scorePairs.Count == 0
            ? 0
            : scorePairs.OrderByDescending(pair => pair.Value)
                .ThenByDescending(pair => pair.Key.Initial)
                .First().Key.Difference;

        public bool HasMultipleScorePairs => scorePairs.Count > 1;

        public GeneratedShinuchiResult CalculateGeneratedShinuchi() =>
            ShinuchiCalculator.CalculateGenerated(NoteCount, RendaTimeSeconds, BalloonHits);

        public int CalculateScoreFromInitial(int initial) =>
            ShinuchiCalculator.CalculateScoreFromInitial(
                NoteCount, RendaTimeSeconds, BalloonHits, initial);

        public bool ScoreEquivalentTo(FumenBranchStatistics other)
        {
            if (other == null) return false;
            return NoteCount == other.NoteCount &&
                   BigNoteCount == other.BigNoteCount &&
                   BalloonCount == other.BalloonCount &&
                   BalloonHits == other.BalloonHits &&
                   Math.Abs(RendaTimeSeconds - other.RendaTimeSeconds) <= 0.001d &&
                   HasSpecialNoteTypes == other.HasSpecialNoteTypes;
        }

        internal void AddNote(int noteType, ushort scoreInitial, ushort rawScoreDifference,
            float durationMilliseconds)
        {
            if (IsComboNote(noteType))
            {
                NoteCount++;
                if (IsBigNote(noteType)) BigNoteCount++;
            }
            else if (IsRenda(noteType))
            {
                if (!float.IsNaN(durationMilliseconds) && !float.IsInfinity(durationMilliseconds))
                    RendaTimeSeconds += Math.Max(0d, durationMilliseconds / 1000d);
            }
            else if (IsBalloon(noteType))
            {
                BalloonCount++;
                BalloonHits += Math.Max(0, (int)scoreInitial);
            }
            else
            {
                specialNoteTypes.Add(noteType);
            }

            if (!IsBalloon(noteType))
            {
                var pair = ((int)scoreInitial, (int)rawScoreDifference / 4);
                if (!scorePairs.TryGetValue(pair, out var count)) count = 0;
                scorePairs[pair] = count + 1;
            }
        }

        internal void Finish()
        {
            RendaTimeSeconds = Math.Round(Math.Max(0d, RendaTimeSeconds), 6,
                MidpointRounding.AwayFromZero);
        }

        private static bool IsComboNote(int type) =>
            type == 0x1 || type == 0x2 || type == 0x3 || type == 0x4 || type == 0x5 ||
            type == 0x7 || type == 0x8 || type == 0xb || type == 0xd;

        private static bool IsBigNote(int type) =>
            type == 0x7 || type == 0x8 || type == 0xb || type == 0xd;

        private static bool IsRenda(int type) => type == 0x6 || type == 0x9 || type == 0x62;
        private static bool IsBalloon(int type) => type == 0xa || type == 0xc;
    }

    internal sealed class FumenChartStatistics
    {
        private sealed class CacheEntry
        {
            public long Length { get; init; }
            public long LastWriteTicks { get; init; }
            public FumenChartStatistics Statistics { get; init; }
            public string Error { get; init; }
        }

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, CacheEntry> Cache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        private FumenChartStatistics(string path, FumenWrapper wrapper, bool bigEndian,
            bool headerHasBranches, int measureCount, IReadOnlyList<FumenBranchStatistics> branches,
            int paddedBytes)
        {
            Path = path;
            Wrapper = wrapper;
            BigEndian = bigEndian;
            HeaderHasBranches = headerHasBranches;
            MeasureCount = measureCount;
            Branches = branches;
            PaddedBytes = paddedBytes;
        }

        public string Path { get; }
        public FumenWrapper Wrapper { get; }
        public bool BigEndian { get; }
        public bool HeaderHasBranches { get; }
        public int MeasureCount { get; }
        public IReadOnlyList<FumenBranchStatistics> Branches { get; }
        public int PaddedBytes { get; }
        public int PopulatedBranchCount => Branches.Count(branch => branch.IsPopulated);
        public int ComboBranchCount => Branches.Count(branch => branch.NoteCount > 0);
        public bool HasMultiplePopulatedBranches => PopulatedBranchCount > 1;

        public static void ClearCache()
        {
            lock (CacheLock) Cache.Clear();
        }

        public static bool TryRead(string path, out FumenChartStatistics statistics, out string error)
        {
            statistics = null;
            error = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "File does not exist.";
                return false;
            }

            path = System.IO.Path.GetFullPath(path);
            var info = new FileInfo(path);
            lock (CacheLock)
            {
                if (Cache.TryGetValue(path, out var cached) &&
                    cached.Length == info.Length && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
                {
                    statistics = cached.Statistics;
                    error = cached.Error;
                    return statistics != null;
                }
            }

            try
            {
                var fileBytes = File.ReadAllBytes(path);
                if (!TryReadUnwrapped(path, fileBytes, out statistics, out error))
                    statistics = null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                statistics = null;
            }

            lock (CacheLock)
            {
                if (statistics != null)
                {
                    Cache[path] = new CacheEntry
                    {
                        Length = info.Length,
                        LastWriteTicks = info.LastWriteTimeUtc.Ticks,
                        Statistics = statistics,
                        Error = null
                    };
                }
                else
                {
                    Cache.Remove(path);
                }
            }
            return statistics != null;
        }

        private static bool TryReadUnwrapped(string path, byte[] fileBytes,
            out FumenChartStatistics statistics, out string error)
        {
            statistics = null;
            var failures = new List<string>();

            if (TryParseRaw(path, fileBytes, FumenWrapper.Raw, out statistics, out var rawError))
            {
                error = null;
                return true;
            }
            failures.Add("raw: " + rawError);

            if (TryDecompress(fileBytes, out var gzipBytes, out var gzipError))
            {
                if (TryParseRaw(path, gzipBytes, FumenWrapper.Gzip, out statistics, out var parsedGzipError))
                {
                    error = null;
                    return true;
                }
                failures.Add("gzip payload: " + parsedGzipError);
            }
            else
            {
                failures.Add("gzip: " + gzipError);
            }

            byte[] decrypted = null;
            try
            {
                decrypted = SSL.DecryptFumen(fileBytes);
            }
            catch (Exception ex)
            {
                failures.Add("AES: " + ex.Message);
            }

            if (decrypted != null)
            {
                if (TryDecompress(decrypted, out var aesGzipBytes, out var aesGzipError))
                {
                    if (TryParseRaw(path, aesGzipBytes, FumenWrapper.AesGzip,
                            out statistics, out var parsedAesGzipError))
                    {
                        error = null;
                        return true;
                    }
                    failures.Add("AES+gzip payload: " + parsedAesGzipError);
                }
                else
                {
                    failures.Add("AES gzip: " + aesGzipError);
                }

                if (TryParseRaw(path, decrypted, FumenWrapper.AesRaw,
                        out statistics, out var parsedAesRawError))
                {
                    error = null;
                    return true;
                }
                failures.Add("AES raw payload: " + parsedAesRawError);
            }

            error = string.Join(" | ", failures.Where(value => !string.IsNullOrWhiteSpace(value)));
            return false;
        }

        private static bool TryDecompress(byte[] input, out byte[] output, out string error)
        {
            output = null;
            error = null;
            try
            {
                using var source = new MemoryStream(input, false);
                using var gzip = new GZipStream(source, CompressionMode.Decompress);
                using var target = new MemoryStream();
                gzip.CopyTo(target);
                output = target.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseRaw(string path, byte[] raw, FumenWrapper wrapper,
            out FumenChartStatistics statistics, out string error)
        {
            statistics = null;
            error = null;
            if (raw == null || raw.Length < 520)
            {
                error = $"Payload is only {raw?.Length ?? 0} bytes; a fumen header needs 520.";
                return false;
            }

            var failures = new List<string>();
            foreach (var bigEndian in new[] { false, true })
            {
                try
                {
                    var measureCount = ReadInt32At(raw, 512, bigEndian);
                    if (measureCount < 0 || measureCount > 10000)
                    {
                        failures.Add((bigEndian ? "BE" : "LE") +
                                     $" measure count {measureCount} is implausible");
                        continue;
                    }

                    statistics = ParseRaw(path, raw, wrapper, bigEndian, measureCount);
                    return true;
                }
                catch (Exception ex)
                {
                    failures.Add((bigEndian ? "BE" : "LE") + ": " + ex.Message);
                }
            }

            error = string.Join(" | ", failures);
            return false;
        }

        private static FumenChartStatistics ParseRaw(string path, byte[] raw, FumenWrapper wrapper,
            bool bigEndian, int measureCount)
        {
            var cursor = new FumenBinaryCursor(raw, bigEndian, 520);
            var headerHasBranches = ReadInt32At(raw, 432, bigEndian) != 0;
            var branches = new[]
            {
                new FumenBranchStatistics(),
                new FumenBranchStatistics(),
                new FumenBranchStatistics()
            };

            for (var measure = 0; measure < measureCount; measure++)
            {
                var bpm = cursor.ReadSingle();
                cursor.ReadSingle();
                cursor.ReadByte();
                cursor.ReadByte();
                cursor.ReadUInt16();
                for (var value = 0; value < 7; value++) cursor.ReadInt32();

                if (float.IsNaN(bpm) || float.IsInfinity(bpm) || bpm < 0f || bpm > 10000f)
                    throw new InvalidDataException($"Measure {measure} has invalid BPM {bpm}.");

                for (var branchIndex = 0; branchIndex < 3; branchIndex++)
                {
                    var totalNotes = cursor.ReadUInt16();
                    cursor.ReadUInt16();
                    var speed = cursor.ReadSingle();
                    if (float.IsNaN(speed) || float.IsInfinity(speed) || Math.Abs(speed) > 10000f)
                        throw new InvalidDataException(
                            $"Measure {measure}, branch {branchIndex} has invalid speed {speed}.");

                    for (var note = 0; note < totalNotes; note++)
                    {
                        var noteType = cursor.ReadInt32();
                        cursor.ReadSingle();
                        cursor.ReadInt32();
                        cursor.ReadSingle();
                        var scoreInitial = cursor.ReadUInt16();
                        var rawScoreDifference = cursor.ReadUInt16();
                        var duration = cursor.ReadSingle();
                        branches[branchIndex].AddNote(noteType, scoreInitial,
                            rawScoreDifference, duration);

                        if (noteType == 0x6 || noteType == 0x9 || noteType == 0x62)
                            cursor.Skip(8);
                    }
                }
            }

            foreach (var branch in branches) branch.Finish();

            if (cursor.Offset < raw.Length)
            {
                var trailing = raw.AsSpan(cursor.Offset);
                if (trailing.Length > 64 || trailing.ToArray().Any(value => value != 0))
                    throw new InvalidDataException(
                        $"Parser stopped {trailing.Length} byte(s) before EOF.");
            }
            else if (cursor.Offset - raw.Length > 2)
            {
                throw new EndOfStreamException(
                    $"Fumen ended {cursor.Offset - raw.Length} byte(s) early.");
            }

            return new FumenChartStatistics(path, wrapper, bigEndian, headerHasBranches,
                measureCount, branches, cursor.PaddedBytes);
        }

        private static int ReadInt32At(byte[] data, int offset, bool bigEndian)
        {
            var span = data.AsSpan(offset, 4);
            return bigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(span)
                : BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        private sealed class FumenBinaryCursor
        {
            private readonly byte[] data;
            private readonly bool bigEndian;

            public FumenBinaryCursor(byte[] data, bool bigEndian, int offset)
            {
                this.data = data;
                this.bigEndian = bigEndian;
                Offset = offset;
            }

            public int Offset { get; private set; }
            public int PaddedBytes { get; private set; }

            public byte ReadByte()
            {
                Span<byte> bytes = stackalloc byte[1];
                Fill(bytes);
                return bytes[0];
            }

            public ushort ReadUInt16()
            {
                Span<byte> bytes = stackalloc byte[2];
                Fill(bytes);
                return bigEndian
                    ? BinaryPrimitives.ReadUInt16BigEndian(bytes)
                    : BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            }

            public int ReadInt32()
            {
                Span<byte> bytes = stackalloc byte[4];
                Fill(bytes);
                return bigEndian
                    ? BinaryPrimitives.ReadInt32BigEndian(bytes)
                    : BinaryPrimitives.ReadInt32LittleEndian(bytes);
            }

            public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

            public void Skip(int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (count <= 32)
                {
                    Span<byte> scratch = stackalloc byte[count];
                    Fill(scratch);
                }
                else
                {
                    Fill(new byte[count]);
                }
            }

            private void Fill(Span<byte> target)
            {
                var available = Math.Max(0, Math.Min(target.Length, data.Length - Offset));
                if (available > 0)
                    data.AsSpan(Offset, available).CopyTo(target);
                if (available < target.Length)
                {
                    var missing = target.Length - available;
                    if (missing > 2 || Offset < data.Length - 2)
                        throw new EndOfStreamException(
                            $"Unexpected EOF at 0x{Offset:X}; needed {target.Length} byte(s), found {available}.");
                    target.Slice(available).Clear();
                    PaddedBytes += missing;
                }
                Offset += target.Length;
            }
        }
    }
}
