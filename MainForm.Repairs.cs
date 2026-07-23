using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Collections;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Data;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem repairsToolStripMenuItem;

        private void InitializeProjectRepairsMenu()
        {
            if (repairsToolStripMenuItem != null) return;

            repairsToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "repairsToolStripMenuItem",
                Text = "Repairs...",
                Enabled = CurrentProject != null
            };
            repairsToolStripMenuItem.Click += RepairsToolStripMenuItem_Click;
            menuStrip1.Items.Add(repairsToolStripMenuItem);
        }

        private void RefreshProjectRepairsState()
        {
            if (repairsToolStripMenuItem != null)
                repairsToolStripMenuItem.Enabled = CurrentProject != null && MusicInfos != null;
        }

        private void RepairsToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null)
            {
                MessageBox.Show("No data project is loaded.");
                return;
            }

            MergeEditableDatatables();
            UseWaitCursor = true;
            ProjectRepairForm form;
            try
            {
                form = new ProjectRepairForm(CurrentProject);
            }
            finally
            {
                UseWaitCursor = false;
            }

            using (form)
                form.ShowDialog(this);
            if (!form.RepairsApplied) return;

            ReloadEditableStateFromProject();
            RegisterUnifiedRepairs(form.AppliedSongIds, form.AppliedCount);
            RefreshProjectDiagnosticsState();
            RefreshCategoryEditorState();
            RefreshSongDeletionState();
            RefreshAdvancedMetadataState();
            RefreshProjectRepairsState();
        });

        private void ReloadEditableStateFromProject()
        {
            var musicInfoRows = Collections.Collections.FromJson<IMusicInfo>(
                CurrentProject.MusicInfo.ToJson(), DatatableTypes.MusicInfo);
            var musicAttributeRows = Collections.Collections.FromJson<IMusicAttribute>(
                CurrentProject.MusicAttribute.ToJson(), DatatableTypes.MusicAttribute);
            var musicOrderRows = Collections.Collections.FromJson<IMusicOrder>(
                CurrentProject.MusicOrder.ToJson(), DatatableTypes.MusicOrder);
            var wordRows = Collections.Collections.FromJson<IWord>(
                CurrentProject.WordList.ToJson(), DatatableTypes.Word);

            MusicInfos = new MusicInfos();
            MusicInfos.Items.AddRange(musicInfoRows.Items);
            MusicAttributes = new MusicAttributes();
            MusicAttributes.Items.AddRange(musicAttributeRows.Items);
            MusicOrders = new MusicOrders();
            MusicOrders.Items.AddRange(musicOrderRows.Items);
            WordList = new WordList();
            WordList.Items.AddRange(wordRows.Items);

            foreach (var pending in AddedMusic)
            {
                pending.MusicInfo = MusicInfos.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, pending.Id, StringComparison.Ordinal) && item.UniqueId == pending.UniqueId)
                    ?? pending.MusicInfo;
                pending.MusicAttribute = MusicAttributes.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, pending.Id, StringComparison.Ordinal) && item.UniqueId == pending.UniqueId)
                    ?? pending.MusicAttribute;
                pending.MusicOrder = MusicOrders.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, pending.Id, StringComparison.Ordinal) && item.UniqueId == pending.UniqueId)
                    ?? pending.MusicOrder;
                pending.Word = WordList.GetBySong(pending.Id) ?? pending.Word;
                pending.WordSub = WordList.GetBySongSub(pending.Id) ?? pending.WordSub;
                pending.WordDetail = WordList.GetBySongDetail(pending.Id) ?? pending.WordDetail;
                pending.MusicAiSection = SongAdvancedMetadata.FindExactRow(
                    CurrentProject.MusicAiSection.Items, pending.Id, pending.UniqueId, "music_ai_section")
                    ?? pending.MusicAiSection;
                pending.MusicUsbSetting = SongAdvancedMetadata.FindExactRow(
                    CurrentProject.MusicUsbSetting.Items, pending.Id, pending.UniqueId, "music_usbsetting")
                    ?? pending.MusicUsbSetting;
            }

            var pendingIds = new HashSet<string>(AddedMusic.Select(item => item.Id), StringComparer.Ordinal);
            LoadedMusicBinding = new BindingSource
            {
                DataSource = MusicInfos.Items
                    .Where(item => item.UniqueId != 0 && !pendingIds.Contains(item.Id))
                    .OrderBy(item => item.UniqueId)
                    .ToList()
            };
            LoadedMusicBox.DataSource = LoadedMusicBinding;

            MusicOrderViewer.SongCards.Clear();
            MusicOrderViewer.WordList = WordList;
            foreach (var order in MusicOrders.Items.Where(order =>
                         MusicInfos.Items.Any(info => info.UniqueId == order.UniqueId &&
                                                     string.Equals(info.Id, order.Id, StringComparison.Ordinal))))
                MusicOrderViewer.AddSong(order);
            MusicOrderViewer.SortSongs();
            MusicOrderViewer.MusicOrdersPanel_Update();

            AddedMusicBinding.ResetBindings(false);
            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
        }
    }
}
