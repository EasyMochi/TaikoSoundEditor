using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongAssets
    {
        private static readonly string[] BaseDifficulties = { "e", "n", "h", "m" };

        private SongAssets(string songId, string soundBankPath, string fumenDirectory,
            IReadOnlyList<string> expectedFumens, IReadOnlyList<string> existingFumens,
            IReadOnlyList<string> missingFumens, IReadOnlyList<string> unexpectedFumens)
        {
            SongId = songId;
            SoundBankPath = soundBankPath;
            FumenDirectory = fumenDirectory;
            ExpectedFumens = expectedFumens;
            ExistingFumens = existingFumens;
            MissingFumens = missingFumens;
            UnexpectedFumens = unexpectedFumens;
        }

        public string SongId { get; }
        public string SoundBankPath { get; }
        public string FumenDirectory { get; }
        public bool HasSoundBank => File.Exists(SoundBankPath);
        public bool HasFumenDirectory => Directory.Exists(FumenDirectory);
        public IReadOnlyList<string> ExpectedFumens { get; }
        public IReadOnlyList<string> ExistingFumens { get; }
        public IReadOnlyList<string> MissingFumens { get; }
        public IReadOnlyList<string> UnexpectedFumens { get; }

        public static SongAssets Discover(ProjectPaths paths, string songId, bool hasUra)
        {
            var expected = new List<string>();
            foreach (var difficulty in BaseDifficulties)
            {
                expected.Add($"{songId}_{difficulty}.bin");
                expected.Add($"{songId}_{difficulty}_1.bin");
                expected.Add($"{songId}_{difficulty}_2.bin");
            }

            if (hasUra)
            {
                expected.Add($"{songId}_x.bin");
                expected.Add($"{songId}_x_1.bin");
                expected.Add($"{songId}_x_2.bin");
            }

            var directory = paths.FumenDirectory(songId);
            var existing = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.bin", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();

            var expectedSet = new HashSet<string>(expected, StringComparer.OrdinalIgnoreCase);
            var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            return new SongAssets(
                songId,
                paths.SoundFile(songId),
                directory,
                expected,
                existing,
                expected.Where(name => !existingSet.Contains(name)).ToList(),
                existing.Where(name => !expectedSet.Contains(name)).ToList());
        }
    }
}
