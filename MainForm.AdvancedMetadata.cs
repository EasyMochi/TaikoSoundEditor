using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem advancedMetadataToolStripMenuItem;
        private bool advancedMetadataImportHooked;

        private void InitializeAdvancedMetadataMenu()
        {
            if (advancedMetadataToolStripMenuItem == null)
            {
                advancedMetadataToolStripMenuItem = new ToolStripMenuItem
                {
                    Name = "advancedMetadataToolStripMenuItem",
                    Text = "AI / USB Metadata...",
                    Enabled = CurrentProject != null && MusicInfos != null
                };
                advancedMetadataToolStripMenuItem.Click += AdvancedMetadataToolStripMenuItem_Click;
                menuStrip1.Items.Add(advancedMetadataToolStripMenuItem);
            }

            if (!advancedMetadataImportHooked)
            {
                AddedMusicBinding.ListChanged += AddedMusicBinding_ListChangedForAdvancedMetadata;
                advancedMetadataImportHooked = true;
            }
        }

        private void RefreshAdvancedMetadataState()
        {
            if (advancedMetadataToolStripMenuItem != null)
                advancedMetadataToolStripMenuItem.Enabled = CurrentProject != null && MusicInfos != null;
        }

        private void AddedMusicBinding_ListChangedForAdvancedMetadata(object sender, ListChangedEventArgs e)
        {
            var pending = AddedMusic.Where(item => item.MusicAiSection == null || item.MusicUsbSetting == null).ToList();
            if (pending.Count == 0) return;

            TJA tja = null;
            if (!string.IsNullOrWhiteSpace(TJASelector.Path) && File.Exists(TJASelector.Path))
            {
                tja = TjaEncAuto.Checked ? TJA.ReadDefault(TJASelector.Path)
                    : TjaEncUTF8.Checked ? TJA.ReadAsUTF8(TJASelector.Path)
                    : TJA.ReadAsShiftJIS(TJASelector.Path);
            }

            foreach (var item in pending)
            {
                item.MusicAiSection = tja != null
                    ? SongAdvancedMetadata.CreateAiRow(item.Id, item.UniqueId, tja, item.MusicInfo)
                    : SongAdvancedMetadata.CreateDefaultAiRow(item.Id, item.UniqueId, item.MusicInfo);
                item.MusicUsbSetting = SongAdvancedMetadata.CreateUsbRow(item.Id, item.UniqueId);
                ImportedAdvancedMetadataIds.Add(item.Id);
            }
        }

        private void AdvancedMetadataToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null)
            {
                MessageBox.Show("No data project is loaded.");
                return;
            }

            MergeEditableDatatables();
            using var form = new AdvancedMetadataForm(CurrentProject,
                MusicInfos.Items.Concat(AddedMusic.Select(item => item.MusicInfo)));
            form.ShowDialog(this);
            RefreshProjectDiagnosticsState();
        });
    }
}
