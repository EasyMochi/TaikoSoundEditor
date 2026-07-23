using System;
using System.Drawing;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal sealed class AdvancedMetadataForm : Form
    {
        private readonly TaikoProject project;
        private readonly IMusicInfo[] songs;
        private readonly ComboBox songBox = new ComboBox();
        private readonly NumericUpDown easy = CreateCountBox();
        private readonly NumericUpDown normal = CreateCountBox();
        private readonly NumericUpDown hard = CreateCountBox();
        private readonly NumericUpDown oni = CreateCountBox();
        private readonly NumericUpDown ura = CreateCountBox();
        private readonly CheckBox oniLevel11 = new CheckBox { Text = "Oni 10★ flag (o)", AutoSize = true };
        private readonly CheckBox uraLevel11 = new CheckBox { Text = "Ura 10★ flag (o)", AutoSize = true };
        private readonly TextBox usbVer = new TextBox();
        private JsonObject aiRow;
        private JsonObject usbRow;
        private bool loading;

        public AdvancedMetadataForm(TaikoProject project, System.Collections.Generic.IEnumerable<IMusicInfo> songs)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.songs = songs?.Where(song => song != null && song.UniqueId != 0)
                .OrderBy(song => song.UniqueId).ToArray() ?? Array.Empty<IMusicInfo>();

            Text = "AI / USB Metadata";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(660, 480);
            Size = new Size(720, 540);

            songBox.DropDownStyle = ComboBoxStyle.DropDownList;
            songBox.Dock = DockStyle.Fill;
            songBox.DisplayMember = nameof(SongChoice.Display);
            songBox.SelectedIndexChanged += (_, _) => LoadSelectedSong();

            var choices = this.songs.Select(song => new SongChoice(song)).ToArray();
            if (choices.Length > 0)
                songBox.Items.AddRange(choices.Cast<object>().ToArray());

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 12,
                AutoScroll = true
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(grid, 0, "Song", songBox);
            AddHeading(grid, 1, "AI section counts");
            AddRow(grid, 2, "Easy", easy);
            AddRow(grid, 3, "Normal", normal);
            AddRow(grid, 4, "Hard", hard);
            AddRow(grid, 5, "Oni", oni);
            AddRow(grid, 6, "Ura", ura);
            AddRow(grid, 7, "Level 11 flags", new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Controls = { oniLevel11, uraLevel11 }
            });
            AddHeading(grid, 8, "USB setting");
            AddRow(grid, 9, "usbVer", usbVer);

            var note = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                Text = "Auto values for imported songs use the provisional deterministic rule from the research memo: 3 sections below 100 seconds, otherwise 5, calculated per difficulty. This is not labelled as Bandai Namco's exact official algorithm. USB version defaults to an empty string."
            };
            AddRow(grid, 10, "", note);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            var save = new Button { Text = "Save song", AutoSize = true, Enabled = choices.Length > 0 };
            save.Click += (_, _) => SaveSelectedSong();
            buttons.Controls.Add(close);
            buttons.Controls.Add(save);
            AddRow(grid, 11, "", buttons);

            Controls.Add(grid);
            AcceptButton = save;
            CancelButton = close;

            if (choices.Length > 0)
            {
                songBox.SelectedIndex = 0;
            }
            else
            {
                songBox.Enabled = false;
                songBox.Text = "No songs with a valid unique ID";
            }
        }

        private static NumericUpDown CreateCountBox() => new NumericUpDown
        {
            Minimum = 0,
            Maximum = 99,
            Value = 3,
            Width = 100
        };

        private static void AddHeading(TableLayoutPanel grid, int row, string text)
        {
            var label = new Label { Text = text, Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold), AutoSize = true };
            grid.Controls.Add(label, 0, row);
            grid.SetColumnSpan(label, 2);
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            grid.Controls.Add(control, 1, row);
        }

        private void LoadSelectedSong()
        {
            if (!(songBox.SelectedItem is SongChoice choice)) return;
            loading = true;
            try
            {
                aiRow = SongAdvancedMetadata.EnsureAiRow(project, choice.Song.Id, choice.Song.UniqueId, choice.Song);
                usbRow = SongAdvancedMetadata.EnsureUsbRow(project, choice.Song.Id, choice.Song.UniqueId);
                easy.Value = Clamp(JsonRow.GetInt(aiRow, "easy") ?? 3, easy);
                normal.Value = Clamp(JsonRow.GetInt(aiRow, "normal") ?? 3, normal);
                hard.Value = Clamp(JsonRow.GetInt(aiRow, "hard") ?? 3, hard);
                oni.Value = Clamp(JsonRow.GetInt(aiRow, "oni") ?? 3, oni);
                ura.Value = Clamp(JsonRow.GetInt(aiRow, "ura") ?? 3, ura);
                oniLevel11.Checked = string.Equals(JsonRow.GetString(aiRow, "oniLevel11"), "o", StringComparison.OrdinalIgnoreCase);
                uraLevel11.Checked = string.Equals(JsonRow.GetString(aiRow, "uraLevel11"), "o", StringComparison.OrdinalIgnoreCase);
                usbVer.Text = JsonRow.GetString(usbRow, "usbVer") ?? string.Empty;
            }
            finally
            {
                loading = false;
            }
        }

        private void SaveSelectedSong()
        {
            if (loading || aiRow == null || usbRow == null) return;
            aiRow["easy"] = (int)easy.Value;
            aiRow["normal"] = (int)normal.Value;
            aiRow["hard"] = (int)hard.Value;
            aiRow["oni"] = (int)oni.Value;
            aiRow["ura"] = (int)ura.Value;
            aiRow["oniLevel11"] = oniLevel11.Checked ? "o" : string.Empty;
            aiRow["uraLevel11"] = uraLevel11.Checked ? "o" : string.Empty;
            usbRow["usbVer"] = usbVer.Text ?? string.Empty;
            MessageBox.Show("AI and USB metadata saved in memory.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static decimal Clamp(int value, NumericUpDown control) => Math.Min(control.Maximum, Math.Max(control.Minimum, value));

        private sealed class SongChoice
        {
            public SongChoice(IMusicInfo song) { Song = song; }
            public IMusicInfo Song { get; }
            public string Display => $"{Song.UniqueId}. {Song.Id}";
        }
    }
}
