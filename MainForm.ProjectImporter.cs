using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Data;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private bool projectImporterInitialized;

        private void InitializeProjectAwareImporter()
        {
            if (projectImporterInitialized) return;
            CreateOkButton.Click -= CreateOkButton_Click;
            CreateOkButton.Click += ProjectAwareCreateOkButton_Click;
            projectImporterInitialized = true;
        }

        private void ProjectAwareCreateOkButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null || MusicAttributes == null ||
                MusicOrders == null || WordList == null)
            {
                MessageBox.Show("Load a complete data project before importing a TJA song.");
                return;
            }

            var audioPath = AudioFileSelector.Path;
            var tjaPath = TJASelector.Path;
            var songId = SongNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(tjaPath) || !File.Exists(tjaPath))
            {
                MessageBox.Show("Select an existing TJA file.");
                return;
            }

            TJA tja;
            string[] sourceLines;
            if (TjaEncUTF8.Checked)
            {
                tja = TJA.ReadAsUTF8(tjaPath);
                sourceLines = File.ReadAllLines(tjaPath, Encoding.UTF8);
            }
            else if (TjaEncAuto.Checked)
            {
                tja = TJA.ReadDefault(tjaPath);
                sourceLines = ReadTjaLinesUsingDetectedEncoding(tjaPath);
            }
            else
            {
                var shiftJis = Encoding.GetEncoding("shift_jis");
                tja = TJA.ReadAsShiftJIS(tjaPath);
                sourceLines = File.ReadAllLines(tjaPath, shiftJis);
            }

            var silenceSeconds = AddSilenceBox.Checked
                ? Math.Max(0, (int)Math.Ceiling(tja.Headers.Offset + (int)SilenceBox.Value))
                : 0;
            var uniqueId = SongImportPlan.FindNextUniqueId(CurrentProject, AddedMusic);
            var plan = SongImportPlan.Create(CurrentProject, AddedMusic, audioPath, tjaPath,
                songId, uniqueId, tja, sourceLines, silenceSeconds);

            using var preview = new SongImportPreviewForm(plan);
            if (preview.ShowDialog(this) != DialogResult.OK || preview.ImportedSong == null)
                return;

            StageImportedSong(preview.ImportedSong);
            MessageBox.Show(this,
                $"{preview.ImportedSong.Id} was converted and added to the in-memory project.\n\n" +
                "Use complete project export to produce a validated data folder containing the new song.",
                "Song import staged",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });

        private void StageImportedSong(NewSongData song)
        {
            if (song == null) throw new ArgumentNullException(nameof(song));

            AddedMusic.Add(song);
            ImportedAdvancedMetadataIds.Add(song.Id);
            WordList.Items.Add(song.Word);
            WordList.Items.Add(song.WordSub);
            WordList.Items.Add(song.WordDetail);
            MusicAttributes.Items.Add(song.MusicAttribute);
            MusicOrderViewer.AddSong(song.MusicOrder);
            MusicOrderViewer.SortSongs();

            AddedMusicBinding.ResetBindings(false);
            NewSoundsBox.ClearSelected();
            NewSoundsBox.SelectedItem = song;
            TabControl.SelectedIndex = 1;
            FeedbackBox.Clear();

            RefreshProjectDiagnosticsState();
            RefreshCategoryEditorState();
            RefreshSongDeletionState();
            RefreshAdvancedMetadataState();
        }

        private static string[] ReadTjaLinesUsingDetectedEncoding(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return File.ReadAllLines(path, Encoding.UTF8);
            return File.ReadAllLines(path, Encoding.GetEncoding("shift_jis"));
        }
    }
}
