using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongDeletionForm : Form
    {
        private readonly TaikoProject project;
        private readonly TextBox filterBox = new TextBox();
        private readonly ListBox songList = new ListBox();
        private readonly TextBox previewBox = new TextBox();
        private readonly Button deleteButton = new Button();
        private readonly List<SongChoice> allSongs;

        public SongDeletionForm(TaikoProject project)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            allSongs = project.BuildIndex().ById.Values
                .OrderBy(song => song.Id, StringComparer.OrdinalIgnoreCase)
                .Select(song => new SongChoice(song.Id, GetTitle(song)))
                .ToList();

            Text = "Delete song completely";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 560);
            Size = new Size(980, 680);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            filterBox.Dock = DockStyle.Fill;
            filterBox.PlaceholderText = "Filter by song ID or title";
            filterBox.TextChanged += (_, __) => RefreshSongs();
            layout.Controls.Add(filterBox, 0, 0);
            layout.SetColumnSpan(filterBox, 2);

            songList.Dock = DockStyle.Fill;
            songList.SelectedIndexChanged += (_, __) => RefreshPreview();
            layout.Controls.Add(songList, 0, 1);

            previewBox.Dock = DockStyle.Fill;
            previewBox.Multiline = true;
            previewBox.ReadOnly = true;
            previewBox.ScrollBars = ScrollBars.Both;
            previewBox.WordWrap = false;
            previewBox.Font = new Font(FontFamily.GenericMonospace, 9f);
            layout.Controls.Add(previewBox, 1, 1);

            var cancelButton = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };
            deleteButton.Text = "Delete from project";
            deleteButton.AutoSize = true;
            deleteButton.Enabled = false;
            deleteButton.Click += DeleteButton_Click;

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            buttons.Controls.Add(deleteButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 2);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
            AcceptButton = deleteButton;
            CancelButton = cancelButton;
            RefreshSongs();
        }

        public SongDeletionPlan SelectedPlan { get; private set; }

        private void RefreshSongs()
        {
            var filter = filterBox.Text.Trim();
            var selectedId = (songList.SelectedItem as SongChoice)?.Id;
            var visible = allSongs.Where(song =>
                filter.Length == 0 ||
                song.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                song.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            songList.BeginUpdate();
            songList.Items.Clear();
            songList.Items.AddRange(visible.Cast<object>().ToArray());
            songList.EndUpdate();

            if (selectedId != null)
            {
                var restored = visible.FindIndex(song => string.Equals(song.Id, selectedId, StringComparison.Ordinal));
                if (restored >= 0) songList.SelectedIndex = restored;
            }
            if (songList.SelectedIndex < 0 && songList.Items.Count > 0)
                songList.SelectedIndex = 0;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var choice = songList.SelectedItem as SongChoice;
            SelectedPlan = choice == null ? null : SongDeletionPlan.Create(project, choice.Id);
            previewBox.Text = SelectedPlan?.BuildPreview() ?? "Select a song to preview its complete deletion.";
            deleteButton.Enabled = SelectedPlan?.CanDelete == true;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (SelectedPlan == null || !SelectedPlan.CanDelete) return;
            var result = MessageBox.Show(
                $"Remove {SelectedPlan.Title ?? SelectedPlan.Id} ({SelectedPlan.Id}) from the project?\n\n" +
                "The original data folder will remain untouched until a complete project export succeeds.",
                "Confirm complete song deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes) return;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static string GetTitle(SongRecord song)
        {
            var title = song.TitleWord == null ? null : JsonRow.GetString(song.TitleWord, "japaneseText");
            return string.IsNullOrWhiteSpace(title) ? song.Id : title;
        }

        private sealed class SongChoice
        {
            public SongChoice(string id, string title)
            {
                Id = id;
                Title = title ?? id;
            }

            public string Id { get; }
            public string Title { get; }
            public override string ToString() => string.Equals(Id, Title, StringComparison.Ordinal)
                ? Id
                : $"{Id} — {Title}";
        }
    }
}
