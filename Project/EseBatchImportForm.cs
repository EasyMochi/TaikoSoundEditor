using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal sealed class EseBatchImportForm : Form
    {
        private readonly TaikoProject project;
        private readonly IReadOnlyList<NewSongData> pendingSongs;
        private readonly EseBatchScanResult scan;
        private readonly BindingList<EseBatchImportCandidate> candidates;
        private readonly DataGridView grid;
        private readonly Label summaryLabel;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly CheckBox markNewBox;
        private readonly CheckBox addSilenceBox;
        private readonly NumericUpDown extraSilenceBox;
        private readonly Button importButton;
        private readonly Button cancelButton;
        private bool converting;
        private bool suppressGridValidation;
        private bool validatingRows;

        public EseBatchImportForm(TaikoProject project, IEnumerable<NewSongData> pendingSongs,
            EseBatchScanResult scan)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.pendingSongs = (pendingSongs ?? Enumerable.Empty<NewSongData>()).ToList();
            this.scan = scan ?? throw new ArgumentNullException(nameof(scan));
            candidates = new BindingList<EseBatchImportCandidate>(scan.Candidates.ToList());

            Text = "ESE Batch TJA Import";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1040, 650);
            Size = new Size(1400, 820);

            var heading = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(10, 8, 10, 0),
                Font = new Font(Font, FontStyle.Bold),
                Text = "ESE folder batch importer"
            };
            var description = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(10, 4, 10, 4),
                Text = "Genre folders such as 01 Pop and 02 Anime are used as the category source. " +
                       "Each immediate song subfolder must contain one unambiguous TJA and OGG/audio file. " +
                       "Edit IDs or uncheck rows before conversion."
            };
            var rootLabel = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                ReadOnly = true,
                Text = scan.RootPath
            };

            grid = BuildGrid();
            grid.DataSource = candidates;
            grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += (_, e) =>
            {
                if (!suppressGridValidation && e.RowIndex >= 0 &&
                    (e.ColumnIndex == 0 || e.ColumnIndex == 3))
                {
                    candidates[e.RowIndex].RuntimeError = string.Empty;
                    ValidateRows();
                }
            };
            grid.CellEndEdit += (_, e) =>
            {
                if (suppressGridValidation) return;
                if (e.RowIndex >= 0) candidates[e.RowIndex].RuntimeError = string.Empty;
                ValidateRows();
            };
            grid.DataError += (_, e) => e.ThrowException = false;

            summaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var selectAllButton = new Button { Text = "Select valid", AutoSize = true };
            selectAllButton.Click += (_, _) =>
            {
                suppressGridValidation = true;
                try
                {
                    foreach (var item in candidates) item.Include = item.ScanErrors.Count == 0;
                }
                finally
                {
                    suppressGridValidation = false;
                }
                grid.Refresh();
                ValidateRows();
            };
            var selectNoneButton = new Button { Text = "Select none", AutoSize = true };
            selectNoneButton.Click += (_, _) =>
            {
                suppressGridValidation = true;
                try
                {
                    foreach (var item in candidates) item.Include = false;
                }
                finally
                {
                    suppressGridValidation = false;
                }
                grid.Refresh();
                ValidateRows();
            };

            markNewBox = new CheckBox
            {
                Text = "Set NEW flag on imported songs",
                Checked = true,
                AutoSize = true
            };
            addSilenceBox = new CheckBox
            {
                Text = "Add leading silence",
                AutoSize = true
            };
            extraSilenceBox = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 60,
                DecimalPlaces = 0,
                Width = 58,
                Enabled = false
            };
            addSilenceBox.CheckedChanged += (_, _) => extraSilenceBox.Enabled = addSilenceBox.Checked;

            var optionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(8, 5, 8, 5)
            };
            optionBar.Controls.Add(selectAllButton);
            optionBar.Controls.Add(selectNoneButton);
            optionBar.Controls.Add(new Label { Width = 18 });
            optionBar.Controls.Add(markNewBox);
            optionBar.Controls.Add(new Label { Width = 18 });
            optionBar.Controls.Add(addSilenceBox);
            optionBar.Controls.Add(extraSilenceBox);
            optionBar.Controls.Add(new Label { Text = "extra second(s)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = Math.Max(1, candidates.Count),
                Value = 0
            };

            importButton = new Button { Text = "Convert and stage selected songs", AutoSize = true };
            importButton.Click += ImportButton_Click;
            cancelButton = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(8, 5, 8, 5)
            };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(importButton);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 112,
                ColumnCount = 3,
                RowCount = 3
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            footer.Controls.Add(optionBar, 0, 0);
            footer.SetColumnSpan(optionBar, 3);
            footer.Controls.Add(statusLabel, 0, 1);
            footer.Controls.Add(progressBar, 1, 1);
            footer.SetColumnSpan(progressBar, 2);
            footer.Controls.Add(summaryLabel, 0, 2);
            footer.Controls.Add(buttons, 1, 2);
            footer.SetColumnSpan(buttons, 2);

            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(rootLabel);
            Controls.Add(description);
            Controls.Add(heading);

            AcceptButton = importButton;
            CancelButton = cancelButton;
            FormClosing += EseBatchImportForm_FormClosing;
            ValidateRows();
        }

        public IReadOnlyList<NewSongData> ImportedSongs { get; private set; } =
            Array.Empty<NewSongData>();

        private static DataGridView BuildGrid()
        {
            var result = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            };
            result.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.Include),
                HeaderText = "Import",
                Width = 55
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.Genre),
                HeaderText = "Genre",
                ReadOnly = true,
                Width = 105
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.SongFolder),
                HeaderText = "Song folder",
                ReadOnly = true,
                Width = 170
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.SongId),
                HeaderText = "Song ID",
                MaxInputLength = 6,
                Width = 76
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.JapaneseTitle),
                HeaderText = "Japanese title",
                ReadOnly = true,
                Width = 180
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.EnglishTitle),
                HeaderText = "English title",
                ReadOnly = true,
                Width = 170
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.Charts),
                HeaderText = "Charts",
                ReadOnly = true,
                Width = 72
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.TjaFileName),
                HeaderText = "TJA",
                ReadOnly = true,
                Width = 120
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.AudioFileName),
                HeaderText = "Audio",
                ReadOnly = true,
                Width = 120
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.EncodingName),
                HeaderText = "Encoding",
                ReadOnly = true,
                Width = 82
            });
            result.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(EseBatchImportCandidate.Status),
                HeaderText = "Status",
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 220
            });
            return result;
        }

        private bool ValidateRows(bool showDialog = false)
        {
            if (validatingRows) return importButton?.Enabled == true;
            validatingRows = true;
            try
            {
                grid.EndEdit();
                var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in project.MusicInfo.Items.OfType<JsonObject>())
                {
                    var id = JsonRow.GetString(row, "id");
                    if (!string.IsNullOrWhiteSpace(id)) existingIds.Add(id);
                }
                foreach (var pending in pendingSongs)
                    if (!string.IsNullOrWhiteSpace(pending.Id)) existingIds.Add(pending.Id);
                var existingWordKeys = new HashSet<string>(
                    project.WordList.Items.OfType<JsonObject>()
                        .Select(row => JsonRow.GetString(row, "key"))
                        .Where(key => !string.IsNullOrWhiteSpace(key)),
                    StringComparer.Ordinal);

                var selected = candidates.Where(item => item.Include).ToList();
                var duplicateIds = new HashSet<string>(
                    selected.Where(item => !string.IsNullOrWhiteSpace(item.SongId))
                        .GroupBy(item => item.SongId.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key),
                    StringComparer.OrdinalIgnoreCase);

                var invalidSelected = new List<EseBatchImportCandidate>();
                foreach (var item in candidates)
                {
                    var rowErrors = new List<string>(item.ScanErrors);
                    if (!string.IsNullOrWhiteSpace(item.RuntimeError))
                        rowErrors.Add(item.RuntimeError);
                    var id = item.SongId?.Trim() ?? string.Empty;
                    if (item.Include)
                    {
                        if (!EseBatchImportScanner.IsValidSongId(id))
                            rowErrors.Add("ID must be 1-6 ASCII letters, digits, or underscore.");
                        if (duplicateIds.Contains(id))
                            rowErrors.Add("Duplicate ID inside this batch.");
                        if (existingIds.Contains(id))
                            rowErrors.Add("ID already exists in the project or pending imports.");
                        if (File.Exists(project.Paths.SoundFile(id)))
                            rowErrors.Add("A source-project sound bank already exists for this ID.");
                        if (Directory.Exists(project.Paths.FumenDirectory(id)))
                            rowErrors.Add("A source-project fumen folder already exists for this ID.");
                        if (existingWordKeys.Contains("song_" + id) ||
                            existingWordKeys.Contains("song_sub_" + id) ||
                            existingWordKeys.Contains("song_detail_" + id))
                            rowErrors.Add("One or more wordlist keys already exist for this ID.");
                        if (item.Source == null)
                            rowErrors.Add("TJA was not parsed.");
                        if (string.IsNullOrWhiteSpace(item.AudioPath) || !File.Exists(item.AudioPath))
                            rowErrors.Add("Audio file is unavailable.");
                    }

                    if (rowErrors.Count > 0)
                    {
                        item.Status = string.Join("; ", rowErrors.Distinct(StringComparer.Ordinal));
                        if (item.Include) invalidSelected.Add(item);
                    }
                    else if (!item.Include)
                    {
                        item.Status = item.ScanWarnings.Count > 0
                            ? "Skipped; " + string.Join("; ", item.ScanWarnings)
                            : "Skipped";
                    }
                    else
                    {
                        item.Status = item.ScanWarnings.Count > 0
                            ? "Ready with warning: " + string.Join("; ", item.ScanWarnings)
                            : "Ready";
                    }
                }

                grid.Refresh();
                var warningCount = candidates.Count(item => item.ScanWarnings.Count > 0);
                summaryLabel.Text = $"Found {candidates.Count} song folder(s); selected {selected.Count}; " +
                                    $"invalid selected {invalidSelected.Count}; scan warnings {warningCount}.";
                if (scan.Warnings.Count > 0)
                    summaryLabel.Text += "  " + string.Join("  ", scan.Warnings);
                importButton.Text = selected.Count == 1
                    ? "Convert and stage 1 song"
                    : $"Convert and stage {selected.Count} songs";
                importButton.Enabled = !converting && selected.Count > 0 && invalidSelected.Count == 0;

                if (showDialog && invalidSelected.Count > 0)
                    MessageBox.Show(this,
                        "Some selected rows are not importable. Edit their IDs, repair their folders, or uncheck them.",
                        "Batch import blocked",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                return selected.Count > 0 && invalidSelected.Count == 0;
            }
            finally
            {
                validatingRows = false;
            }
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            if (!ValidateRows(true)) return;
            var selected = candidates.Where(item => item.Include).ToList();
            foreach (var item in selected) item.RuntimeError = string.Empty;
            EseBatchImportCandidate activeCandidate = null;
            converting = true;
            ToggleControls(false);
            progressBar.Maximum = Math.Max(1, selected.Count);
            progressBar.Value = 0;

            try
            {
                var nextUniqueId = SongImportPlan.FindNextUniqueId(project, pendingSongs);
                var reservations = selected.Select((item, index) => new NewSongData
                {
                    Id = item.SongId.Trim(),
                    UniqueId = checked(nextUniqueId + index)
                }).ToList();
                var plans = new List<(EseBatchImportCandidate Candidate, SongImportPlan Plan)>();

                for (var index = 0; index < selected.Count; index++)
                {
                    var candidate = selected[index];
                    activeCandidate = candidate;
                    var reservation = reservations[index];
                    var source = TjaImportSource.LoadAuto(candidate.TjaPath);
                    var otherPending = pendingSongs.Concat(reservations.Where((_, i) => i != index));
                    var silenceSeconds = addSilenceBox.Checked
                        ? Math.Max(0, (int)Math.Ceiling(source.Tja.Headers.Offset +
                                                       (int)extraSilenceBox.Value))
                        : 0;
                    var plan = SongImportPlan.Create(project, otherPending,
                        candidate.AudioPath, candidate.TjaPath, reservation.Id,
                        reservation.UniqueId, source.Tja, source.Lines,
                        silenceSeconds, candidate.Genre, candidate.GenreFolder, markNewBox.Checked);
                    plans.Add((candidate, plan));
                }

                var blocked = plans.Where(pair => !pair.Plan.CanImport).ToList();
                if (blocked.Count > 0)
                {
                    foreach (var pair in blocked)
                    {
                        pair.Candidate.RuntimeError = string.Join("; ", pair.Plan.Errors);
                        pair.Candidate.Status = pair.Candidate.RuntimeError;
                    }
                    grid.Refresh();
                    MessageBox.Show(this,
                        "The selected songs failed final project collision validation. No conversions were run.",
                        "Batch import blocked",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var converted = new List<NewSongData>();
                for (var index = 0; index < plans.Count; index++)
                {
                    var pair = plans[index];
                    activeCandidate = pair.Candidate;
                    pair.Candidate.Status = "Converting...";
                    grid.Refresh();
                    var prefix = $"{index + 1}/{plans.Count} {pair.Candidate.SongId}";
                    var song = pair.Plan.Convert(message =>
                    {
                        statusLabel.Text = prefix + ": " + message;
                        pair.Candidate.Status = message;
                        grid.Refresh();
                        statusLabel.Refresh();
                        Application.DoEvents();
                    });
                    converted.Add(song);
                    pair.Candidate.Status = "Converted and ready to stage";
                    progressBar.Value = index + 1;
                    grid.Refresh();
                }

                ImportedSongs = converted;
                statusLabel.Text = $"Converted {converted.Count} song(s). Staging into the project...";
                converting = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                if (activeCandidate != null)
                    activeCandidate.RuntimeError = "Conversion failed: " + ex.Message;
                ImportedSongs = Array.Empty<NewSongData>();
                statusLabel.Text = "Batch conversion failed. The project was not changed.";
                MessageBox.Show(this,
                    "Batch conversion stopped before any songs were staged.\n\n" + ex.Message,
                    "ESE batch import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                converting = false;
                if (!IsDisposed && DialogResult != DialogResult.OK) ToggleControls(true);
            }
        }

        private void ToggleControls(bool enabled)
        {
            grid.Enabled = enabled;
            markNewBox.Enabled = enabled;
            addSilenceBox.Enabled = enabled;
            extraSilenceBox.Enabled = enabled && addSilenceBox.Checked;
            cancelButton.Enabled = enabled;
            importButton.Enabled = enabled;
            UseWaitCursor = !enabled;
            if (enabled) ValidateRows();
        }

        private void EseBatchImportForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!converting) return;
            e.Cancel = true;
        }
    }
}
