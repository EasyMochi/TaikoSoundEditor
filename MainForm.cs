using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Controls;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            // The WinForms designer instantiates the form but must not execute runtime
            // initialization, file access, data binding, or UI reparenting.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            Load += MainForm_RuntimeLoad;
            Shown += MainForm_RuntimeShown;

            //Init button event handlers
            MusicAttributePathSelector.PathChanged += MusicAttributePathSelector_PathChanged;
            MusicOrderPathSelector.PathChanged += MusicOrderPathSelector_PathChanged;
            MusicInfoPathSelector.PathChanged += MusicInfoPathSelector_PathChanged;
            WordListPathSelector.PathChanged += WordListPathSelector_PathChanged;
            DirSelector.PathChanged += DirSelector_PathChanged;

            //Other stuff
            AddedMusicBinding.DataSource = AddedMusic;
            NewSoundsBox.DataSource = AddedMusicBinding;
            TabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            SimpleGenreBox.DataSource = Enum.GetValues(typeof(Genre));
            LoadPreferences();
        }

        private bool IsInDesignMode =>
            LicenseManager.UsageMode == LicenseUsageMode.Designtime ||
            DesignMode ||
            Site?.DesignMode == true;

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e) =>
            Logger.Info($"Commuted to tab {TabControl.SelectedIndex}.{TabControl.Name}");


        #region Editor

        private void LoadMusicInfo(IMusicInfo item)
        {
            Logger.Info($"Showing properties for MusicInfo: {item}");

            if (item == null)
            {
                ClearSimpleEditor();
                return;
            }

            var attribute = MusicAttributes?.GetByUniqueId(item.UniqueId);
            var order = MusicOrders?.GetByUniqueId(item.UniqueId);
            var title = WordList?.GetBySong(item.Id);
            var subtitle = WordList?.GetBySongSub(item.Id);
            var detail = WordList?.GetBySongDetail(item.Id);

            MusicInfoGrid.SelectedObject = item;
            MusicAttributesGrid.SelectedObject = attribute;
            MusicOrderGrid.SelectedObject = order;
            WordsGrid.SelectedObject = title;
            WordSubGrid.SelectedObject = subtitle;
            WordDetailGrid.SelectedObject = detail;

            simpleBoxLoading = true;
            try
            {
                SimpleIdBox.Text = item.Id ?? string.Empty;
                SimpleTitleBox.Text = title?.JapaneseText ?? string.Empty;
                SimpleSubtitleBox.Text = subtitle?.JapaneseText ?? string.Empty;
                SimpleDetailBox.Text = detail?.JapaneseText ?? string.Empty;
                SimpleGenreBox.SelectedItem = order?.Genre ?? item.Genre;
                SimpleStarEasyBox.Value = item.StarEasy;
                SimpleStarNormalBox.Value = item.StarNormal;
                SimpleStarHardBox.Value = item.StarHard;
                SimpleStarManiaBox.Value = item.StarMania;
                SimpleStarUraBox.Value = item.StarUra;
                SimpleStarUraBox.Enabled = attribute?.CanPlayUra == true;
            }
            finally
            {
                simpleBoxLoading = false;
            }

            LoadMultilingualWordEditor(item.Id);
        }

        private void LoadNewSongData(NewSongData item)
        {
            Logger.Info($"Selection Changed NewSongData: {item}");
            Logger.Info($"Showing properties for NewSongData: {item}");

            if (item == null)
            {
                ClearSimpleEditor();
                return;
            }

            MusicInfoGrid.SelectedObject = item.MusicInfo;
            MusicAttributesGrid.SelectedObject = item.MusicAttribute;
            MusicOrderGrid.SelectedObject = item.MusicOrder;
            WordsGrid.SelectedObject = item.Word;
            WordSubGrid.SelectedObject = item.WordSub;
            WordDetailGrid.SelectedObject = item.WordDetail;

            simpleBoxLoading = true;
            try
            {
                SimpleIdBox.Text = item.MusicInfo?.Id ?? item.Id ?? string.Empty;
                SimpleTitleBox.Text = item.Word?.JapaneseText ?? string.Empty;
                SimpleSubtitleBox.Text = item.WordSub?.JapaneseText ?? string.Empty;
                SimpleDetailBox.Text = item.WordDetail?.JapaneseText ?? string.Empty;
                SimpleGenreBox.SelectedItem = item.MusicOrder?.Genre ?? item.MusicInfo?.Genre ?? Genre.Pop;
                SimpleStarEasyBox.Value = item.MusicInfo?.StarEasy ?? 0;
                SimpleStarNormalBox.Value = item.MusicInfo?.StarNormal ?? 0;
                SimpleStarHardBox.Value = item.MusicInfo?.StarHard ?? 0;
                SimpleStarManiaBox.Value = item.MusicInfo?.StarMania ?? 0;
                SimpleStarUraBox.Value = item.MusicInfo?.StarUra ?? 0;
                SimpleStarUraBox.Enabled = item.MusicAttribute?.CanPlayUra == true;
            }
            finally
            {
                simpleBoxLoading = false;
            }

            LoadMultilingualWordEditor(item.Id);
        }

        private void ClearSimpleEditor()
        {
            MusicInfoGrid.SelectedObject = null;
            MusicAttributesGrid.SelectedObject = null;
            MusicOrderGrid.SelectedObject = null;
            WordsGrid.SelectedObject = null;
            WordSubGrid.SelectedObject = null;
            WordDetailGrid.SelectedObject = null;

            simpleBoxLoading = true;
            try
            {
                SimpleIdBox.Clear();
                SimpleTitleBox.Clear();
                SimpleSubtitleBox.Clear();
                SimpleDetailBox.Clear();
                SimpleGenreBox.SelectedIndex = -1;
                SimpleStarEasyBox.Value = 0;
                SimpleStarNormalBox.Value = 0;
                SimpleStarHardBox.Value = 0;
                SimpleStarManiaBox.Value = 0;
                SimpleStarUraBox.Value = 0;
                SimpleStarUraBox.Enabled = false;
            }
            finally
            {
                simpleBoxLoading = false;
            }

            ClearMultilingualWordEditor();
        }

        private bool indexChanging = false;

        private void LoadedMusicBox_SelectedIndexChanged(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (indexChanging) return;
            indexChanging = true;
            try
            {
                NewSoundsBox.SelectedItem = null;
                var item = LoadedMusicBox.SelectedItem as IMusicInfo;
                Logger.Info($"Selection Changed MusicItem: {item}");
                LoadMusicInfo(item);
                if (SoundViewTab.SelectedTab == MusicOrderTab)
                    SoundViewTab.SelectedTab = SoundViewerSimple;
            }
            finally
            {
                indexChanging = false;
            }
        });

        private void EditorTable_Resize(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            WordsGB.Height = WordSubGB.Height = WordDetailGB.Height = EditorTable.Height / 3;
        });

        private void NewSoundsBox_SelectedIndexChanged(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (indexChanging) return;
            indexChanging = true;
            try
            {
                LoadedMusicBox.SelectedItem = null;
                var item = NewSoundsBox.SelectedItem as NewSongData;
                LoadNewSongData(item);
                if (SoundViewTab.SelectedTab == MusicOrderTab)
                    SoundViewTab.SelectedTab = SoundViewerSimple;
            }
            finally
            {
                indexChanging = false;
            }
        });

        #endregion        

        private void RemoveNewSong(NewSongData ns)
        {
            AddedMusic.Remove(ns);
            Logger.Info("Refreshing list");
            AddedMusicBinding.ResetBindings(false);
            MusicOrderViewer.RemoveAllSongs(ns.MusicOrder.UniqueId);

            Logger.Info("Removing from wordlist & music_attributes");
            WordList.Items.Remove(ns.Word);
            WordList.Items.Remove(ns.WordDetail);
            WordList.Items.Remove(ns.WordSub);
            MusicAttributes.Items.Remove(ns.MusicAttribute);
        }

        private void RemoveExistingSong(IMusicInfo mi)
        {
            var ma = MusicAttributes.GetByUniqueId(mi.UniqueId);
            var mo = MusicOrders.GetByUniqueId(mi.UniqueId);
            var w = WordList.GetBySong(mi.Id);
            var ws = WordList.GetBySongSub(mi.Id);
            var wd = WordList.GetBySongDetail(mi.Id);

            Logger.Info("Removing music info");
            MusicInfos.Items.RemoveAll(x => x.UniqueId == mi.UniqueId);
            Logger.Info("Removing music attribute");
            MusicAttributes.Items.Remove(ma);
            Logger.Info("Removing music order");
            MusicOrders.Items.Remove(mo);
            Logger.Info("Removing word");
            WordList.Items.Remove(w);
            Logger.Info("Removing word sub");
            WordList.Items.Remove(ws);
            Logger.Info("Removing word detail");
            WordList.Items.Remove(wd);

            Logger.Info("Refreshing list");
            RefreshExistingSongsListView();

            var sel = LoadedMusicBox.SelectedIndex;

            if (sel >= MusicInfos.Items.Count)
                sel = MusicInfos.Items.Count - 1;

            LoadedMusicBox.SelectedItem = null;
            LoadedMusicBox.SelectedIndex = sel;

            Logger.Info("Removing from music orders");
            MusicOrderViewer.RemoveAllSongs(mo.UniqueId);
        }

        private void RemoveSongButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            Logger.Info("Clicked remove song");
            if (NewSoundsBox.SelectedItem != null)
            {
                Logger.Info("Removing newly added song");
                var ns = NewSoundsBox.SelectedItem as NewSongData;
                RemoveNewSong(ns);
                return;
            }

            if (LoadedMusicBox.SelectedItem != null)
            {
                Logger.Info("Removing existing song");
                var mi = LoadedMusicBox.SelectedItem as IMusicInfo;
                RemoveExistingSong(mi);
                return;
            }
        });

        private void SoundViewTab_SelectedIndexChanged(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (SoundViewTab.SelectedTab == MusicOrderTab)
            {
                MusicOrderViewer.Invalidate();
                return;
            }

            if (!(SoundViewTab.SelectedTab == SoundViewerExpert || SoundViewTab.SelectedTab == SoundViewerSimple))
                return;

            if (LoadedMusicBox.SelectedItem != null)
            {
                var item = LoadedMusicBox.SelectedItem as IMusicInfo;
                Logger.Info($"Tab switched, reloading MusicItem: {item}");
                LoadMusicInfo(item);
                return;
            }

            if (NewSoundsBox.SelectedItem != null)
            {
                var item = NewSoundsBox.SelectedItem as NewSongData;
                Logger.Info($"Tab switched, reloading NewSongData: {item}");
                LoadNewSongData(item);
                return;
            }
        });

        private bool simpleBoxLoading = false;
        private void SimpleBoxChanged(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (simpleBoxLoading) return;

            if (LoadedMusicBox.SelectedItem is IMusicInfo item)
            {
                Logger.Info($"Simple Box changed : {(sender as Control)?.Name} to value {(sender as Control)?.Text}");

                ApplyJapaneseWordEditFromSimpleControl(item.Id, sender);

                var order = MusicOrders?.GetByUniqueId(item.UniqueId);
                var genre = (Genre)(SimpleGenreBox.SelectedItem ?? Genre.Pop);
                if (order != null) order.Genre = genre;
                item.Genre = genre;
                item.StarEasy = (int)SimpleStarEasyBox.Value;
                item.StarNormal = (int)SimpleStarNormalBox.Value;
                item.StarHard = (int)SimpleStarHardBox.Value;
                item.StarMania = (int)SimpleStarManiaBox.Value;
                item.StarUra = (int)SimpleStarUraBox.Value;
                return;
            }

            if (NewSoundsBox.SelectedItem is NewSongData newSong)
            {
                Logger.Info($"Simple Box changed : {(sender as Control)?.Name} to value {(sender as Control)?.Text}");

                if (newSong.Word != null) newSong.Word.JapaneseText = SimpleTitleBox.Text;
                if (newSong.WordSub != null) newSong.WordSub.JapaneseText = SimpleSubtitleBox.Text;
                if (newSong.WordDetail != null) newSong.WordDetail.JapaneseText = SimpleDetailBox.Text;
                ApplyJapaneseWordEditFromSimpleControl(newSong.Id, sender);

                var genre = (Genre)(SimpleGenreBox.SelectedItem ?? Genre.Pop);
                if (newSong.MusicOrder != null) newSong.MusicOrder.Genre = genre;
                if (newSong.MusicInfo != null)
                {
                    newSong.MusicInfo.Genre = genre;
                    newSong.MusicInfo.StarEasy = (int)SimpleStarEasyBox.Value;
                    newSong.MusicInfo.StarNormal = (int)SimpleStarNormalBox.Value;
                    newSong.MusicInfo.StarHard = (int)SimpleStarHardBox.Value;
                    newSong.MusicInfo.StarMania = (int)SimpleStarManiaBox.Value;
                    newSong.MusicInfo.StarUra = (int)SimpleStarUraBox.Value;
                }
            }
        });

        private void MusicOrderViewer_SongRemoved(MusicOrderViewer sender, IMusicOrder mo) => ExceptionGuard.Run(() =>
        {
            var uniqId = mo.UniqueId;
            if (MusicOrderViewer.SongExists(uniqId))
                return;

            var mi = MusicInfos.Items.Where(x => x.UniqueId == uniqId).FirstOrDefault();

            if (mi != null)
            {
                RemoveExistingSong(mi);
                return;
            }

            var ns = AddedMusic.Where(x => x.UniqueId == uniqId).FirstOrDefault();
            if (ns != null)
            {
                RemoveNewSong(ns);
                return;
            }
            throw new InvalidOperationException("Nothing to remove.");
        });

        private void LocateInMusicOrderButton_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (LoadedMusicBox.SelectedItem != null)
            {
                var item = LoadedMusicBox.SelectedItem as IMusicInfo;
                var mo = MusicOrders.GetByUniqueId(item.UniqueId);
                if (MusicOrderViewer.Locate(mo))
                {
                    SoundViewTab.SelectedTab = MusicOrderTab;
                }
                return;
            }
            else if (NewSoundsBox.SelectedItem != null)
            {
                var item = NewSoundsBox.SelectedItem as NewSongData;
                if (MusicOrderViewer.Locate(item.MusicOrder))
                {
                    SoundViewTab.SelectedTab = MusicOrderTab;
                }
                return;
            }
        });

        private void MusicOrderViewer_SongDoubleClick(MusicOrderViewer sender, IMusicOrder mo)
        {
            var uid = mo.UniqueId;
            var mi = LoadedMusicBox.Items.Cast<IMusicInfo>().Where(_ => _.UniqueId == uid).FirstOrDefault();
            if (mi != null)
            {
                LoadedMusicBox.SelectedItem = mi;
                return;
            }
            var ns = AddedMusic.Where(_ => _.UniqueId == uid).FirstOrDefault();
            if (ns != null)
            {
                NewSoundsBox.SelectedItem = ns;
                return;
            }

        }

        private void SearchBox_TextChanged(object sender, EventArgs e) => RefreshExistingSongsListView();

        private void RefreshExistingSongsListView()
        {
            var list = MusicInfos.Items.Where(mi => mi.UniqueId != 0)
                .Where(mi =>
                {
                    if (string.IsNullOrEmpty(SearchBox.Text))
                        return true;
                    if (int.TryParse(SearchBox.Text, out int uid) && mi.UniqueId == uid)
                    {
                        return true;
                    }
                    return mi.Id.Contains(SearchBox.Text)
                        || WordList.GetBySong(mi.Id).JapaneseText.Contains(SearchBox.Text)
                        || WordList.GetBySongSub(mi.Id).JapaneseText.Contains(SearchBox.Text);
                })
                .ToList();
            LoadedMusicBox.DataSource = list;
            LoadedMusicBinding.ResetBindings(false);
        }

        private void MusicOrderSortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SortByGenreToolStripMenuItem.Checked = SortByIdToolStripMenuItem.Checked = sortByTitleToolStripMenuItem.Checked = noSortToolStripMenuItem.Checked = false;

            if (sender == SortByGenreToolStripMenuItem)
            {
                SortByGenreToolStripMenuItem.Checked = true;
                Config.SetMusicOrderSortByGenre();
            }
            else if (sender == SortByIdToolStripMenuItem)
            {
                SortByIdToolStripMenuItem.Checked = true;
                Config.SetMusicOrderSortById();
            }
            else if (sender == sortByTitleToolStripMenuItem)
            {
                sortByTitleToolStripMenuItem.Checked = true;
                Config.SetMusicOrderSortByTitle();
            }
            else  //(sender == noSortToolStripMenuItem)
            {
                noSortToolStripMenuItem.Checked = true;
                Config.SetMusicOrderNoSort();
            }

            MusicOrderViewer.SortSongs();
            MusicOrderViewer.MusicOrdersPanel_Update();
        }

        private void LoadPreferences()
        {
            UseEncryptionBox.Checked = Config.UseEncryption;
            UseEncryptionBox_CheckedChanged(null, null); // update state of the key two text boxes

            //If path is set for the datatable folder, update paths for all the files.
            if (Config.DatatablesPath != "") DirSelector.Path = Config.DatatablesPath;


            DatatableKeyBox.Text = Config.DatatableKey;
            FumenKeyBox.Text = Config.FumenKey;

            DatatableDef.Path = Config.DatatableDefPath;

            var musicOrderSort = Config.IniFile.Read(Config.MusicOrderSortProperty);
            if (musicOrderSort == Config.MusicOrderSortValueGenre) SortByGenreToolStripMenuItem.PerformClick();
            else if (musicOrderSort == Config.MusicOrderSortValueId) SortByIdToolStripMenuItem.PerformClick();
            else if (musicOrderSort == Config.MusicOrderSortValueTitle) sortByTitleToolStripMenuItem.PerformClick();
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            //var rel = await Updates.GetLatestTja2Fumen();
        });

        private void DatatableKeyBox_TextChanged(object sender, EventArgs e) => Config.DatatableKey = DatatableKeyBox.Text;

        private void FumenKeyBox_TextChanged(object sender, EventArgs e) => Config.FumenKey = FumenKeyBox.Text;

        private void LoadedMusicBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= LoadedMusicBox.Items.Count)
                return;
            var selItem = LoadedMusicBox.Items[e.Index] as IMusicInfo;
            TextRenderer.DrawText(e.Graphics, $"{selItem.UniqueId}. {selItem.Id}", Font, e.Bounds, e.ForeColor, e.BackColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private void DatatableDef_PathChanged(object sender, EventArgs args)
        {
            try
            {
                var json = File.ReadAllText(DatatableDef.Path);
                DatatableTypes.LoadFromJson(json);
                Config.DatatableDefPath = DatatableDef.Path;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void UseEncryptionBox_CheckedChanged(object sender, EventArgs e)
        {
            Config.UseEncryption = UseEncryptionBox.Checked;
            FumenKeyBox.Enabled = UseEncryptionBox.Checked;
            DatatableKeyBox.Enabled = UseEncryptionBox.Checked;
        }
    }
}