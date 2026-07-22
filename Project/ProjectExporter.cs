using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaikoSoundEditor.Project
{
    internal static class ProjectExporter
    {
        public static void Export(TaikoProject source, string targetRoot, Action<ProjectPaths> writeChanges)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(targetRoot)) throw new ArgumentException("A target path is required.", nameof(targetRoot));

            var target = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var sourceRoot = source.Paths.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (IsInside(target, sourceRoot) && !string.Equals(target, sourceRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The export target cannot be inside the source data folder.");

            var parent = Directory.GetParent(target)?.FullName
                ?? throw new InvalidOperationException("The target folder must have a parent directory.");
            Directory.CreateDirectory(parent);

            var name = Path.GetFileName(target);
            var staging = Path.Combine(parent, name + ".__staging_" + Guid.NewGuid().ToString("N"));
            var backup = Path.Combine(parent, name + ".__backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            var stagingPaths = new ProjectPaths(staging);

            try
            {
                stagingPaths.EnsureStructure();
                CopyDirectory(source.Paths.Sound, stagingPaths.Sound);
                CopyDirectory(source.Paths.Fumen, stagingPaths.Fumen);
                source.WriteDatatables(stagingPaths.Datatable);
                writeChanges?.Invoke(stagingPaths);
                source.ApplyPendingAssetDeletions(stagingPaths);

                var staged = TaikoProject.Open(staging, source.IsEncrypted);
                EnsureSemanticDatatables(source, staged);
                EnsureNoNewErrors(source.BuildIndex().Diagnostics, staged.BuildIndex().Diagnostics);

                var targetExisted = Directory.Exists(target);
                if (targetExisted)
                    Directory.Move(target, backup);

                try
                {
                    Directory.Move(staging, target);
                }
                catch
                {
                    if (targetExisted && Directory.Exists(backup) && !Directory.Exists(target))
                        Directory.Move(backup, target);
                    throw;
                }
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }

        private static void EnsureSemanticDatatables(TaikoProject source, TaikoProject staged)
        {
            foreach (var fileName in ProjectPaths.RequiredDatatables)
            {
                if (!source.Datatables[fileName].SemanticallyEquals(staged.Datatables[fileName]))
                    throw new InvalidDataException($"Staged {fileName} does not match the project data that was written.");
            }
        }

        private static void EnsureNoNewErrors(IEnumerable<ProjectDiagnostic> before, IEnumerable<ProjectDiagnostic> after)
        {
            var existing = new HashSet<string>(before
                .Where(item => item.Severity == DiagnosticSeverity.Error)
                .Select(item => item.Signature), StringComparer.Ordinal);
            var introduced = after
                .Where(item => item.Severity == DiagnosticSeverity.Error && !existing.Contains(item.Signature))
                .ToList();

            if (introduced.Count > 0)
                throw new InvalidDataException("The staged project introduced new errors:\n\n" +
                    string.Join("\n", introduced.Select(item => item.ToString())));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            if (!Directory.Exists(source)) return;

            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(destination, RelativePath(source, directory)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var output = Path.Combine(destination, RelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.Copy(file, output, true);
            }
        }

        private static string RelativePath(string root, string path)
        {
            var rootUri = new Uri(AppendSeparator(Path.GetFullPath(root)));
            var pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;

        private static bool IsInside(string candidate, string root) =>
            candidate.StartsWith(AppendSeparator(root), StringComparison.OrdinalIgnoreCase);
    }
}
