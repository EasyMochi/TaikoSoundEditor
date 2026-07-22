using System;
using System.Collections.Generic;
using System.IO;

namespace TaikoSoundEditor.Project
{
    internal sealed class TaikoProject
    {
        private readonly Dictionary<string, LosslessDatatableDocument> datatables =
            new Dictionary<string, LosslessDatatableDocument>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> deletedSongIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private TaikoProject(ProjectPaths paths, bool encrypted)
        {
            Paths = paths;
            IsEncrypted = encrypted;
        }

        public ProjectPaths Paths { get; }
        public bool IsEncrypted { get; }
        public IReadOnlyDictionary<string, LosslessDatatableDocument> Datatables => datatables;
        public IReadOnlyCollection<string> DeletedSongIds => deletedSongIds;

        public LosslessDatatableDocument MusicAiSection => Get("music_ai_section.bin");
        public LosslessDatatableDocument MusicAttribute => Get("music_attribute.bin");
        public LosslessDatatableDocument MusicOrder => Get("music_order.bin");
        public LosslessDatatableDocument MusicUsbSetting => Get("music_usbsetting.bin");
        public LosslessDatatableDocument MusicInfo => Get("musicinfo.bin");
        public LosslessDatatableDocument WordList => Get("wordlist.bin");

        public static TaikoProject Open(string rootPath, bool encrypted)
        {
            var paths = new ProjectPaths(rootPath);
            var missing = paths.FindMissingDatatables();
            if (missing.Count > 0)
                throw new InvalidDataException(
                    "The selected data folder is missing required datatables:\n\n" +
                    string.Join("\n", missing));

            var project = new TaikoProject(paths, encrypted);
            foreach (var fileName in ProjectPaths.RequiredDatatables)
            {
                project.datatables.Add(
                    fileName,
                    LosslessDatatableDocument.Load(paths.DatatableFile(fileName), encrypted));
            }

            return project;
        }

        public static ProjectPaths CreateStructure(string rootPath)
        {
            var paths = new ProjectPaths(rootPath);
            paths.EnsureStructure();
            return paths;
        }

        public ProjectIndex BuildIndex() => ProjectIndex.Build(this);

        public void MarkSongDeleted(string songId)
        {
            if (string.IsNullOrWhiteSpace(songId))
                throw new ArgumentException("A song id is required.", nameof(songId));
            deletedSongIds.Add(songId);
        }

        public void ApplyPendingAssetDeletions(ProjectPaths output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            foreach (var songId in deletedSongIds)
            {
                var soundBank = output.SoundFile(songId);
                if (File.Exists(soundBank)) File.Delete(soundBank);

                var fumenDirectory = output.FumenDirectory(songId);
                if (Directory.Exists(fumenDirectory)) Directory.Delete(fumenDirectory, true);
            }
        }

        public void WriteDatatables(string datatableDirectory)
        {
            Directory.CreateDirectory(datatableDirectory);
            foreach (var pair in datatables)
                pair.Value.Write(Path.Combine(datatableDirectory, pair.Key), IsEncrypted);
        }

        private LosslessDatatableDocument Get(string fileName)
        {
            if (!datatables.TryGetValue(fileName, out var document))
                throw new InvalidOperationException($"Datatable {fileName} has not been loaded.");
            return document;
        }
    }
}
