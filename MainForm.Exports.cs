using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TaikoSoundEditor.Collections;
using TaikoSoundEditor.Commons;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Data;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private void MergeEditableDatatables()
        {
            if (CurrentProject == null)
                throw new InvalidOperationException("No data project is loaded.");

            var mi = new MusicInfos();
            mi.Items.AddRange(MusicInfos.Items
                .Concat(AddedMusic.Select(item => item.MusicInfo))
                .GroupBy(item => (item.Id, item.UniqueId))
                .Select(group => group.Last()));

            var ma = new MusicAttributes();
            ma.Items.AddRange(MusicAttributes.Items
                .Concat(AddedMusic.Select(item => item.MusicAttribute))
                .GroupBy(item => (item.Id, item.UniqueId))
                .Select(group => group.Last()));

            var mo = new MusicOrders();
            mo.Items.AddRange(MusicOrderViewer.SongCards
                .Select(card => card.MusicOrder)
                .GroupBy(item => (item.Id, item.UniqueId, item.GenreNo))
                .Select(group => group.Last()));

            var wl = new WordList();
            wl.Items.AddRange(WordList.Items
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .Select(group => group.Last()));

            CurrentProject.MusicInfo.MergeKnownItems(
                Json.DynamicSerialize(mi.Cast(DatatableTypes.MusicInfo), false));
            CurrentProject.MusicAttribute.MergeKnownItems(
                Json.DynamicSerialize(ma.Cast(DatatableTypes.MusicAttribute), false));
            CurrentProject.MusicOrder.MergeKnownItems(
                Json.DynamicSerialize(mo.Cast(DatatableTypes.MusicOrder), false));
            CurrentProject.WordList.MergeKnownItems(
                Json.DynamicSerialize(wl.Cast(DatatableTypes.Word), false));

            var activeImportedIds = new HashSet<string>(AddedMusic.Select(item => item.Id), StringComparer.Ordinal);
            SongAdvancedMetadata.RemoveOwnedRows(CurrentProject.MusicAiSection.Items,
                ImportedAdvancedMetadataIds, activeImportedIds);
            SongAdvancedMetadata.RemoveOwnedRows(CurrentProject.MusicUsbSetting.Items,
                ImportedAdvancedMetadataIds, activeImportedIds);

            foreach (var item in AddedMusic)
            {
                SongAdvancedMetadata.Upsert(CurrentProject.MusicAiSection.Items,
                    item.MusicAiSection, "music_ai_section");
                SongAdvancedMetadata.Upsert(CurrentProject.MusicUsbSetting.Items,
                    item.MusicUsbSetting, "music_usbsetting");
            }
        }

        private void ExportDatatable(string path)
        {
            Logger.Info($"Exporting complete datatable set to '{path}'");
            MergeEditableDatatables();
            CurrentProject.WriteDatatables(path);
        }

        private void ExportNusBanks(string path)
        {
            Logger.Info($"Exporting NUS3BANK files to '{path}'");
            Directory.CreateDirectory(path);
            foreach (var ns in AddedMusic)
                File.WriteAllBytes(Path.Combine(path, $"song_{ns.Id}.nus3bank"), ns.Nus3Bank);
        }

        private void ExportSoundBinaries(string path)
        {
            Logger.Info($"Exporting fumen files to '{path}'");
            Directory.CreateDirectory(path);
            foreach (var ns in AddedMusic)
            {
                var songDirectory = Path.Combine(path, ns.Id);
                Directory.CreateDirectory(songDirectory);

                void Save(string suffix, byte[] bytes)
                {
                    if (UseEncryptionBox.Checked)
                    {
                        // Nijiiro fumens are gzip-wrapped before AES-CBC encryption.
                        bytes = SSL.EncryptFumen(GZ.CompressToBytes(bytes));
                    }
                    File.WriteAllBytes(Path.Combine(songDirectory, $"{ns.Id}_{suffix}.bin"), bytes);
                }

                Save("e", ns.EBin);
                Save("n", ns.NBin);
                Save("h", ns.HBin);
                Save("m", ns.MBin);
                Save("e_1", ns.EBin1);
                Save("n_1", ns.NBin1);
                Save("h_1", ns.HBin1);
                Save("m_1", ns.MBin1);
                Save("e_2", ns.EBin2);
                Save("n_2", ns.NBin2);
                Save("h_2", ns.HBin2);
                Save("m_2", ns.MBin2);

                if (ns.MusicAttribute.CanPlayUra)
                {
                    Save("x", ns.XBin);
                    Save("x_1", ns.XBin1);
                    Save("x_2", ns.XBin2);
                }
            }
        }

        private void ExportDatatableButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            var path = PickPath();
            if (path == null)
            {
                MessageBox.Show("No path chosen. Operation canceled");
                return;
            }
            ExportDatatable(path);
            MessageBox.Show("Done");
            if (ExportOpenOnFinished.Checked) Process.Start("explorer.exe", path);
        });

        private void ExportSoundFoldersButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            var path = PickPath();
            if (path == null)
            {
                MessageBox.Show("No path chosen. Operation canceled");
                return;
            }
            ExportSoundBinaries(path);
            MessageBox.Show("Done");
            if (ExportOpenOnFinished.Checked) Process.Start("explorer.exe", path);
        });

        private void ExportSoundBanksButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            var path = PickPath();
            if (path == null)
            {
                MessageBox.Show("No path chosen. Operation canceled");
                return;
            }
            ExportNusBanks(path);
            MessageBox.Show("Done");
            if (ExportOpenOnFinished.Checked) Process.Start("explorer.exe", path);
        });

        private static string PickPath()
        {
            var picker = new FolderPicker();
            return picker.ShowDialog() == true ? picker.ResultPath : null;
        }

        private string BuildValidatedExportSummary(string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Create a validated exported project?");
            builder.AppendLine();
            builder.AppendLine($"Source: {CurrentProject.Paths.Root}");
            builder.AppendLine($"Destination: {Path.GetFullPath(path)}");
            builder.AppendLine();
            builder.AppendLine("STAGED CHANGES");
            builder.AppendLine($"  Imported songs: {AddedMusic.Count}");
            builder.AppendLine($"  Directly edited songs: {GetUnifiedEditedSongIds().Count}");
            builder.AppendLine($"  Applied repairs: {GetUnifiedAppliedRepairCount()}");
            builder.AppendLine($"  Repaired songs: {GetUnifiedRepairedSongIds().Count}");
            builder.AppendLine($"  Category/order changes: {(GetUnifiedCategoryChangesStaged() ? "yes" : "no")}");
            builder.AppendLine($"  Event-folder changes: {(unifiedEventFolderChangesStaged ? "yes" : "no")}");
            builder.AppendLine($"  Pending song deletions: {CurrentProject.DeletedSongIds.Count}");
            builder.AppendLine();
            builder.AppendLine("OUTPUT");
            builder.AppendLine($"  All {CurrentProject.Datatables.Count} loaded datatables will be written losslessly.");
            if (CurrentProject.HasGenreFolderInfo)
                builder.AppendLine("  genre_folderinfo.bin is included in the client datatables.");
            if (EventFolderData != null)
                builder.AppendLine("  Server event-folder data will be written as JSON, GZip and Brotli under server/.");
            builder.AppendLine($"  New sound banks: {AddedMusic.Count}");
            builder.AppendLine($"  New fumen directories: {AddedMusic.Count}");
            builder.AppendLine("  Pending deletions are applied only inside the staged output.");
            builder.AppendLine("  The output is reopened and validated before it replaces the destination.");
            builder.AppendLine();
            builder.AppendLine("The selected source folder will remain untouched.");
            return builder.ToString();
        }

        private void ExportAllButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            var path = PickPath();
            if (path == null)
            {
                MessageBox.Show("No path chosen. Operation canceled");
                return;
            }

            var confirmation = MessageBox.Show(this, BuildValidatedExportSummary(path),
                "Validated project export", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (confirmation != DialogResult.OK) return;

            MergeEditableDatatables();
            EventFolderProjectValidator.ValidateForExport(CurrentProject);
            ProjectExporter.Export(CurrentProject, path, output =>
            {
                ExportSoundBinaries(output.Fumen);
                ExportNusBanks(output.Sound);
                EventFolderData?.WriteAll(Path.Combine(output.Root, "server"));
            });

            MarkUnifiedExportComplete(path);
            MessageBox.Show(this,
                $"Project exported and validated successfully.\n\nDestination:\n{Path.GetFullPath(path)}",
                "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (ExportOpenOnFinished.Checked) Process.Start("explorer.exe", path);
        });
    }
}
