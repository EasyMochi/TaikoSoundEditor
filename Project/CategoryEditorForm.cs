using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Collections;
using TaikoSoundEditor.Commons.Controls;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal sealed class CategoryEditorForm : Form
    {
        private readonly MusicInfos musicInfos;
        private readonly MusicOrders musicOrders;
        private readonly WordList wordList;
        private readonly MusicOrderViewer orderViewer;
        private readonly ListBox songs = new ListBox();
        private readonly CheckedListBox categories = new CheckedListBox();
        private readonly ComboBox primaryGenre = new ComboBox();
        private readonly ListView placements = new ListView();
        private readonly NumericUpDown closeDisplayType = new NumericUpDown();
        private readonly Button moveUp = new Button();
        private readonly Button moveDown = new Button();
        private bool refreshing;

        public CategoryEditorForm(MusicInfos musicInfos, MusicOrders musicOrders, WordList wordList,
            MusicOrderViewer orderViewer)
        {
            this.musicInfos = musicInfos ?? throw new ArgumentNullException(nameof(musicInfos));
            this.musicOrders = musicOrders ?? throw new ArgumentNullException(nameof(musicOrders));
            this.wordList = wordList ?? throw new ArgumentNullException(nameof(wordList));
            this.orderViewer = orderViewer ?? throw new ArgumentNullException(nameof(orderViewer));

            Text = "Categories";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 520);
            Size = new Size(940, 620);

            BuildLayout();
            LoadSongs();
        }

        private IMusicInfo SelectedSong => songs.SelectedItem as IMusicInfo;
        private IMusicOrder SelectedPlacement => placements.SelectedItems.Count == 1
            ? placements.SelectedItems[0].Tag as IMusicOrder
            : null;

        private void BuildLayout()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 280 };
            Controls.Add(split);

            songs.Dock = DockStyle.Fill;
            songs.DisplayMember = nameof(IMusicInfo.Id);
            songs.SelectedIndexChanged += (sender, args) => RefreshSong();
            split.Panel1.Controls.Add(songs);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(8)
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            split.Panel2.Controls.Add(right);

            right.Controls.Add(new Label { Text = "Primary genre", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            primaryGenre.DropDownStyle = ComboBoxStyle.DropDownList;
            primaryGenre.Dock = DockStyle.Fill;
            primaryGenre.SelectedIndexChanged += PrimaryGenre_SelectedIndexChanged;
            right.Controls.Add(primaryGenre, 1, 0);

            right.Controls.Add(new Label { Text = "Appears in", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 1);
            categories.Dock = DockStyle.Fill;
            categories.CheckOnClick = true;
            categories.ItemCheck += Categories_ItemCheck;
            foreach (Genre genre in Enum.GetValues(typeof(Genre))) categories.Items.Add(genre);
            right.Controls.Add(categories, 1, 1);

            right.Controls.Add(new Label { Text = "Placements", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 2);
            placements.Dock = DockStyle.Fill;
            placements.View = View.Details;
            placements.FullRowSelect = true;
            placements.HideSelection = false;
            placements.Columns.Add("Category", 130);
            placements.Columns.Add("Order", 70);
            placements.Columns.Add("closeDispType", 110);
            placements.SelectedIndexChanged += Placements_SelectedIndexChanged;
            right.Controls.Add(placements, 1, 2);

            right.Controls.Add(new Label { Text = "closeDispType", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            closeDisplayType.Minimum = int.MinValue;
            closeDisplayType.Maximum = int.MaxValue;
            closeDisplayType.Dock = DockStyle.Fill;
            closeDisplayType.ValueChanged += CloseDisplayType_ValueChanged;
            right.Controls.Add(closeDisplayType, 1, 3);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            moveUp.Text = "Move up";
            moveDown.Text = "Move down";
            moveUp.Click += (sender, args) => MoveSelected(-1);
            moveDown.Click += (sender, args) => MoveSelected(1);
            buttons.Controls.Add(moveUp);
            buttons.Controls.Add(moveDown);
            right.Controls.Add(buttons, 1, 4);

            var note = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(8),
                Text = "Category membership is stored in music_order. Primary genre is stored separately in musicinfo."
            };
            Controls.Add(note);
        }

        private void LoadSongs()
        {
            songs.BeginUpdate();
            songs.Items.Clear();
            foreach (var song in musicInfos.Items.Where(item => item.UniqueId != 0).OrderBy(item => item.UniqueId))
                songs.Items.Add(song);
            songs.EndUpdate();
            if (songs.Items.Count > 0) songs.SelectedIndex = 0;
        }

        private List<IMusicOrder> GetPlacements(IMusicInfo song)
        {
            return song == null
                ? new List<IMusicOrder>()
                : musicOrders.Items.Where(item => item.UniqueId == song.UniqueId || item.Id == song.Id).ToList();
        }

        private void RefreshSong()
        {
            refreshing = true;
            try
            {
                var song = SelectedSong;
                var songPlacements = GetPlacements(song);

                for (var i = 0; i < categories.Items.Count; i++)
                {
                    var genre = (Genre)categories.Items[i];
                    categories.SetItemChecked(i, songPlacements.Any(item => item.GenreNo == (int)genre));
                }

                primaryGenre.Items.Clear();
                foreach (var genre in songPlacements.Select(item => item.Genre).Distinct().OrderBy(item => (int)item))
                    primaryGenre.Items.Add(genre);
                if (song != null && primaryGenre.Items.Contains(song.Genre))
                    primaryGenre.SelectedItem = song.Genre;

                placements.Items.Clear();
                if (song != null)
                {
                    foreach (var placement in songPlacements)
                    {
                        var categoryRows = musicOrders.Items.Where(item => item.GenreNo == placement.GenreNo).ToList();
                        var order = categoryRows.IndexOf(placement) + 1;
                        var row = new ListViewItem(new[]
                        {
                            placement.Genre.ToString(), order.ToString(), placement.CloseDispType.ToString()
                        }) { Tag = placement };
                        placements.Items.Add(row);
                    }
                }
            }
            finally
            {
                refreshing = false;
            }
            RefreshPlacementControls();
        }

        private void Categories_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (refreshing || SelectedSong == null) return;
            BeginInvoke(new Action(() =>
            {
                var song = SelectedSong;
                var genre = (Genre)categories.Items[e.Index];
                var existing = GetPlacements(song).FirstOrDefault(item => item.GenreNo == (int)genre);

                if (e.NewValue == CheckState.Checked && existing == null)
                {
                    var placement = DatatableTypes.CreateMusicOrder(genre, song.Id, song.UniqueId);
                    musicOrders.Items.Add(placement);
                    orderViewer.AddSong(placement);
                }
                else if (e.NewValue != CheckState.Checked && existing != null)
                {
                    var remaining = GetPlacements(song).Where(item => item != existing).ToList();
                    if (song.GenreNo == existing.GenreNo)
                    {
                        MessageBox.Show("Choose another primary genre before removing this category.");
                        RefreshSong();
                        return;
                    }
                    if (remaining.Count == 0)
                    {
                        MessageBox.Show("A song must remain in at least one category.");
                        RefreshSong();
                        return;
                    }
                    musicOrders.Items.Remove(existing);
                    orderViewer.RemoveSong(existing);
                }
                RefreshSong();
            }));
        }

        private void PrimaryGenre_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (refreshing || SelectedSong == null || !(primaryGenre.SelectedItem is Genre genre)) return;
            SelectedSong.Genre = genre;
            orderViewer.MusicOrdersPanel_Update();
        }

        private void Placements_SelectedIndexChanged(object sender, EventArgs e)
        {
            refreshing = true;
            try
            {
                var placement = SelectedPlacement;
                closeDisplayType.Enabled = placement != null;
                closeDisplayType.Value = placement?.CloseDispType ?? 0;
            }
            finally
            {
                refreshing = false;
            }
            RefreshPlacementControls();
        }

        private void CloseDisplayType_ValueChanged(object sender, EventArgs e)
        {
            if (refreshing || SelectedPlacement == null) return;
            SelectedPlacement.CloseDispType = (int)closeDisplayType.Value;
            RefreshSong();
        }

        private void MoveSelected(int offset)
        {
            var selected = SelectedPlacement;
            if (selected == null) return;
            var categoryRows = musicOrders.Items.Where(item => item.GenreNo == selected.GenreNo).ToList();
            var position = categoryRows.IndexOf(selected);
            var target = position + offset;
            if (position < 0 || target < 0 || target >= categoryRows.Count) return;

            var targetRow = categoryRows[target];
            var currentGlobal = musicOrders.Items.IndexOf(selected);
            var targetGlobal = musicOrders.Items.IndexOf(targetRow);
            musicOrders.Items.RemoveAt(currentGlobal);
            if (targetGlobal > currentGlobal) targetGlobal--;
            musicOrders.Items.Insert(targetGlobal, selected);

            var card = orderViewer.SongCards.FirstOrDefault(item => item.MusicOrder == selected);
            var targetCard = orderViewer.SongCards.FirstOrDefault(item => item.MusicOrder == targetRow);
            if (card != null && targetCard != null)
            {
                orderViewer.SongCards.Remove(card);
                var cardTarget = orderViewer.SongCards.IndexOf(targetCard);
                if (offset > 0) cardTarget++;
                orderViewer.SongCards.Insert(cardTarget, card);
                orderViewer.MusicOrdersPanel_Update();
            }
            RefreshSong();
        }

        private void RefreshPlacementControls()
        {
            var placement = SelectedPlacement;
            moveUp.Enabled = placement != null;
            moveDown.Enabled = placement != null;
            closeDisplayType.Enabled = placement != null;
        }
    }
}
