using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Data;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private void OpenEseBatchImporter() => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null || MusicAttributes == null ||
                MusicOrders == null || WordList == null)
            {
                MessageBox.Show(this,
                    "Load a complete data project before starting an ESE batch import.",
                    "No project loaded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var picker = new FolderBrowserDialog
            {
                Description = "Select the ESE root containing genre folders such as 01 Pop and 02 Anime",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };
            if (picker.ShowDialog(this) != DialogResult.OK ||
                string.IsNullOrWhiteSpace(picker.SelectedPath))
                return;

            EseBatchScanResult scan;
            UseWaitCursor = true;
            try
            {
                scan = EseBatchImportScanner.Scan(picker.SelectedPath);
            }
            finally
            {
                UseWaitCursor = false;
            }

            if (scan.Candidates.Count == 0)
            {
                MessageBox.Show(this,
                    "No ESE song folders were found. The selected root should contain genre folders, " +
                    "then one subfolder per song.",
                    "Nothing to import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var form = new EseBatchImportForm(CurrentProject, AddedMusic, scan);
            if (form.ShowDialog(this) != DialogResult.OK || form.ImportedSongs.Count == 0)
                return;

            StageImportedSongs(form.ImportedSongs);
            MessageBox.Show(this,
                $"{form.ImportedSongs.Count} song(s) were converted and staged in the in-memory project.\n\n" +
                "Run validated export when you are ready to produce the complete data folder.",
                "ESE batch import staged",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });

        private void StageImportedSongs(IEnumerable<NewSongData> songs)
        {
            var batch = (songs ?? Enumerable.Empty<NewSongData>())
                .Where(song => song != null)
                .ToList();
            if (batch.Count == 0) return;

            var touched = new List<NewSongData>();
            try
            {
                foreach (var song in batch)
                {
                    touched.Add(song);
                    AddedMusic.Add(song);
                    ImportedAdvancedMetadataIds.Add(song.Id);
                    WordList.Items.Add(song.Word);
                    WordList.Items.Add(song.WordSub);
                    WordList.Items.Add(song.WordDetail);
                    StageImportedWordRow(song.WordRow);
                    StageImportedWordRow(song.WordSubRow);
                    StageImportedWordRow(song.WordDetailRow);
                    MusicAttributes.Items.Add(song.MusicAttribute);
                    MusicOrderViewer.AddSong(song.MusicOrder);
                }
                MusicOrderViewer.SortSongs();
            }
            catch
            {
                foreach (var song in touched.AsEnumerable().Reverse())
                {
                    AddedMusic.Remove(song);
                    ImportedAdvancedMetadataIds.Remove(song.Id);
                    WordList.Items.Remove(song.Word);
                    WordList.Items.Remove(song.WordSub);
                    WordList.Items.Remove(song.WordDetail);
                    if (CurrentProject != null)
                    {
                        CurrentProject.WordList.Items.Remove(song.WordRow);
                        CurrentProject.WordList.Items.Remove(song.WordSubRow);
                        CurrentProject.WordList.Items.Remove(song.WordDetailRow);
                    }
                    MusicAttributes.Items.Remove(song.MusicAttribute);
                    if (song.MusicOrder != null)
                        MusicOrderViewer.RemoveAllSongs(song.MusicOrder.UniqueId);
                }
                MusicOrderViewer.SortSongs();
                AddedMusicBinding.ResetBindings(false);
                throw;
            }

            AddedMusicBinding.ResetBindings(false);
            NewSoundsBox.ClearSelected();
            NewSoundsBox.SelectedItem = batch[batch.Count - 1];
            TabControl.SelectedIndex = 1;
            FeedbackBox.Clear();

            RefreshProjectDiagnosticsState();
            RefreshCategoryEditorState();
            RefreshSongDeletionState();
            RefreshAdvancedMetadataState();
            NotifyUnifiedProjectStateChanged();
        }
    }
}
