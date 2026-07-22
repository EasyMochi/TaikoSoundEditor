using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem deleteSongToolStripMenuItem;

        private void InitializeSongDeletionMenu()
        {
            if (deleteSongToolStripMenuItem != null) return;

            deleteSongToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "deleteSongToolStripMenuItem",
                Text = "Delete Song...",
                Enabled = CurrentProject != null
            };
            deleteSongToolStripMenuItem.Click += DeleteSongToolStripMenuItem_Click;
            menuStrip1.Items.Add(deleteSongToolStripMenuItem);
        }

        private void RefreshSongDeletionState()
        {
            if (deleteSongToolStripMenuItem != null)
                deleteSongToolStripMenuItem.Enabled = CurrentProject != null && MusicInfos != null;
        }

        private void DeleteSongToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null || MusicAttributes == null ||
                MusicOrders == null || WordList == null)
            {
                MessageBox.Show("No data project is loaded.");
                return;
            }

            MergeEditableDatatables();
            using var form = new SongDeletionForm(CurrentProject);
            if (form.ShowDialog(this) != DialogResult.OK || form.SelectedPlan == null)
                return;

            var plan = form.SelectedPlan;
            plan.Apply(CurrentProject);
            RemoveSongFromEditableState(plan.Id, plan.UniqueId);

            MessageBox.Show(
                $"{plan.Title ?? plan.Id} was removed from the in-memory project.\n\n" +
                "Use complete project export to produce a validated data folder without its rows and assets.",
                "Song deletion staged",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });

        private void RemoveSongFromEditableState(string id, int uniqueId)
        {
            MusicInfos.Items.RemoveAll(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal) || item.UniqueId == uniqueId);
            MusicAttributes.Items.RemoveAll(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal) || item.UniqueId == uniqueId);
            MusicOrders.Items.RemoveAll(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal) || item.UniqueId == uniqueId);

            var wordKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "song_" + id,
                "song_sub_" + id,
                "song_detail_" + id
            };
            WordList.Items.RemoveAll(item => wordKeys.Contains(item.Key));

            MusicOrderViewer.RemoveAllSongs(uniqueId);
            AddedMusic.RemoveAll(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal) || item.UniqueId == uniqueId);
            AddedMusicBinding.ResetBindings(false);

            if (LoadedMusicBinding != null)
            {
                LoadedMusicBinding.DataSource = MusicInfos.Items
                    .Where(item => item.UniqueId != 0)
                    .OrderBy(item => item.UniqueId)
                    .ToList();
                LoadedMusicBinding.ResetBindings(false);
            }

            RefreshProjectDiagnosticsState();
            RefreshCategoryEditorState();
            RefreshSongDeletionState();
        }
    }
}
