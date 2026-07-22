using System;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem advancedMetadataToolStripMenuItem;

        private void InitializeAdvancedMetadataMenu()
        {
            if (advancedMetadataToolStripMenuItem != null) return;

            advancedMetadataToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "advancedMetadataToolStripMenuItem",
                Text = "AI / USB Metadata...",
                Enabled = CurrentProject != null && MusicInfos != null
            };
            advancedMetadataToolStripMenuItem.Click += AdvancedMetadataToolStripMenuItem_Click;
            menuStrip1.Items.Add(advancedMetadataToolStripMenuItem);
        }

        private void RefreshAdvancedMetadataState()
        {
            if (advancedMetadataToolStripMenuItem != null)
                advancedMetadataToolStripMenuItem.Enabled = CurrentProject != null && MusicInfos != null;
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
