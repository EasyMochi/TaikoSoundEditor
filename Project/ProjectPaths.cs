using System;
using System.Collections.Generic;
using System.IO;

namespace TaikoSoundEditor.Project
{
    internal sealed class ProjectPaths
    {
        public static readonly string[] RequiredDatatables =
        {
            "music_ai_section.bin",
            "music_attribute.bin",
            "music_order.bin",
            "music_usbsetting.bin",
            "musicinfo.bin",
            "wordlist.bin"
        };

        public ProjectPaths(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("A project root path is required.", nameof(rootPath));

            Root = Path.GetFullPath(rootPath);
            Datatable = Path.Combine(Root, "datatable");
            Sound = Path.Combine(Root, "sound");
            Fumen = Path.Combine(Root, "fumen");
        }

        public string Root { get; }
        public string Datatable { get; }
        public string Sound { get; }
        public string Fumen { get; }

        public string DatatableFile(string fileName) => Path.Combine(Datatable, fileName);
        public string SoundFile(string songId) => Path.Combine(Sound, $"song_{songId}.nus3bank");
        public string FumenDirectory(string songId) => Path.Combine(Fumen, songId);

        public void EnsureStructure()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Datatable);
            Directory.CreateDirectory(Sound);
            Directory.CreateDirectory(Fumen);
        }

        public IReadOnlyList<string> FindMissingDatatables()
        {
            var missing = new List<string>();
            foreach (var fileName in RequiredDatatables)
            {
                if (!File.Exists(DatatableFile(fileName)))
                    missing.Add(fileName);
            }
            return missing;
        }
    }
}
