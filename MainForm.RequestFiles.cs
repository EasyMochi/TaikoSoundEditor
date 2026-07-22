using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Collections;
using TaikoSoundEditor.Commons.Controls;
using TaikoSoundEditor.Commons.IO;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Data;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        #region Requesting Files

        private void WordListPathSelector_PathChanged(object sender, EventArgs args)
        {
            Logger.Info($"WordListPathSelector_PathChanged : {WordListPathSelector.Path}");
            WordListPath = WordListPathSelector.Path;
        }

        private void MusicInfoPathSelector_PathChanged(object sender, EventArgs args)
        {
            Logger.Info($"MusicInfoPathSelector_PathChanged : {MusicInfoPathSelector.Path}");
            MusicInfoPath = MusicInfoPathSelector.Path;
        }

        private void MusicOrderPathSelector_PathChanged(object sender, EventArgs args)
        {
            Logger.Info($"MusicOrderPathSelector_PathChanged : {MusicOrderPathSelector.Path}");
            MusicOrderPath = MusicOrderPathSelector.Path;
        }

        private void MusicAttributePathSelector_PathChanged(object sender, EventArgs args)
        {
            Logger.Info($"MusicAttributePathSelector_PathChanged : {MusicAttributePathSelector.Path}");
            MusicAttributePath = MusicAttributePathSelector.Path;
        }

        private void DirSelector_PathChanged(object sender, EventArgs args) => ExceptionGuard.Run(() =>
        {
            var root = DirSelector.Path;
            Logger.Info($"Data project root changed: {root}");
            if (string.IsNullOrWhiteSpace(root)) return;

            var paths = TaikoProject.CreateStructure(root);
            Config.DatatablesPath = root;

            MusicAttributePathSelector.Path = paths.DatatableFile("music_attribute.bin");
            MusicOrderPathSelector.Path = paths.DatatableFile("music_order.bin");
            MusicInfoPathSelector.Path = paths.DatatableFile("musicinfo.bin");
            WordListPathSelector.Path = paths.DatatableFile("wordlist.bin");

            var missing = paths.FindMissingDatatables();
            if (missing.Count > 0)
            {
                var message =
                    "The data folder structure is ready, but these datatables are missing:\n\n" +
                    string.Join("\n", missing) +
                    "\n\nExpected structure:\n" +
                    "datatable/\nsound/\nfumen/";
                Logger.Warning(message);
                MessageBox.Show(message);
            }
        });

        private void OkButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            Logger.Info("Clicked 'Looks good'");

            Config.DatatableIO.IsEncrypted = UseEncryptionBox.Checked;
            SSL.LoadKeys();

            CurrentProject = TaikoProject.Open(DirSelector.Path, UseEncryptionBox.Checked);
            RefreshProjectDiagnosticsState();

            try
            {
                MusicAttributes = Config.DatatableIO.DeserializeCollection<MusicAttributes, IMusicAttribute>(
                    CurrentProject.Paths.DatatableFile("music_attribute.bin"), DatatableTypes.MusicAttribute);
                MusicOrders = Config.DatatableIO.DeserializeCollection<MusicOrders, IMusicOrder>(
                    CurrentProject.Paths.DatatableFile("music_order.bin"), DatatableTypes.MusicOrder);
                MusicInfos = Config.DatatableIO.DeserializeCollection<MusicInfos, IMusicInfo>(
                    CurrentProject.Paths.DatatableFile("musicinfo.bin"), DatatableTypes.MusicInfo);
                WordList = Config.DatatableIO.DeserializeCollection<WordList, IWord>(
                    CurrentProject.Paths.DatatableFile("wordlist.bin"), DatatableTypes.Word);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Failed to load the editable song tables.", ex);
            }

            // Loading a project must never repair or mutate it implicitly. Missing rows are
            // surfaced by validation and will receive explicit repair actions in the project UI.
            LoadedMusicBinding = new BindingSource();
            var cleanList = MusicInfos.Items.Where(mi => mi.UniqueId != 0).OrderBy(mi => mi.UniqueId).ToList();
            LoadedMusicBinding.DataSource = cleanList;
            LoadedMusicBox.DataSource = LoadedMusicBinding;
            TabControl.SelectedIndex = 1;

            MusicOrderViewer.WordList = WordList;
            foreach (var musicOrder in MusicOrders.Items.Where(order =>
                         MusicInfos.Items.Any(info => info.UniqueId == order.UniqueId)))
            {
                MusicOrderViewer.AddSong(musicOrder);
            }
            MusicOrderViewer.SortSongs();
        });

        #endregion
    }
}
