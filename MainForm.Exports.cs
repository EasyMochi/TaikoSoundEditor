using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            mi.Items.AddRange(MusicInfos.Items);
            mi.Items.AddRange(AddedMusic.Select(item => item.MusicInfo));

            var ma = new MusicAttributes();
            ma.Items.AddRange(MusicAttributes.Items);
            ma.Items.AddRange(AddedMusic.Select(item => item.MusicAttribute));

            var mo = new MusicOrders();
            mo.Items.AddRange(MusicOrderViewer.SongCards.Select(card => card.MusicOrder));

            var wl = new WordList();
            wl.Items.AddRange(WordList.Items);

            CurrentProject.MusicInfo.MergeKnownItems(
                Json.DynamicSerialize(mi.Cast(DatatableTypes.MusicInfo), false));
            CurrentProject.MusicAttribute.MergeKnownItems(
                Json.DynamicSerialize(ma.Cast(DatatableTypes.MusicAttribute), false));
            CurrentProject.MusicOrder.MergeKnownItems(
                Json.DynamicSerialize(mo.Cast(DatatableTypes.MusicOrder), false));
            CurrentProject.WordList.MergeKnownItems(
                Json.DynamicSerialize(wl.Cast(DatatableTypes.Word), false));
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
                        bytes = SSL.EncryptFumen(bytes);
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

        private void ExportAllButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            var path = PickPath();
            if (path == null)
            {
                MessageBox.Show("No path chosen. Operation canceled");
                return;
            }

            MergeEditableDatatables();
            ProjectExporter.Export(CurrentProject, path, output =>
            {
                ExportSoundBinaries(output.Fumen);
                ExportNusBanks(output.Sound);
            });

            MessageBox.Show("Project exported and validated successfully.");
            if (ExportOpenOnFinished.Checked) Process.Start("explorer.exe", path);
        });
    }
}
