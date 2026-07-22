using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private bool unifiedWorkspaceInitialized;
        private bool unifiedSelectionChanging;
        private bool unifiedCategoryChangesStaged;
        private bool unifiedExportIsCurrent;
        private int unifiedAppliedRepairCount;
        private string unifiedLastExportPath;

        private readonly HashSet<string> unifiedEditedSongIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> unifiedRepairedSongIds = new(StringComparer.Ordinal);

        private ListBox unifiedSongsBox;
        private TextBox unifiedSearchBox;
        private Label unifiedProjectHeading;
        private Label unifiedSelectionHeading;
        private ToolStripStatusLabel unifiedProjectStatus;
        private ToolStripStatusLabel unifiedChangesStatus;
        private Button unifiedImportButton;
        private Button unifiedMetadataButton;
        private Button unifiedAdvancedButton;
        private Button unifiedCategoriesButton;
        private Button unifiedAiUsbButton;
        private Button unifiedDeleteButton;
        private Button unifiedDiagnosticsButton;
        private Button unifiedRepairsButton;
        private Button unifiedExportButton;

        private sealed class UnifiedSongItem
        {
            public string Id { get; init; }
            public int UniqueId { get; init; }
            public string Title { get; init; }
            public object Source { get; init; }
            public bool Imported { get; init; }
            public bool Edited { get; init; }
            public bool Repaired { get; init; }
            public bool Deleted { get; init; }

            public override string ToString()
            {
                var badges = new List<string>();
                if (Imported) badges.Add("NEW");
                if (Edited) badges.Add("EDITED");
                if (Repaired) badges.Add("REPAIRED");
                if (Deleted) badges.Add("DELETE");
                var badge = badges.Count == 0 ? string.Empty : $"[{string.Join(" · ", badges)}] ";
                var identity = UniqueId > 0 ? $"{UniqueId}. {Id}" : Id;
                return $"{badge}{identity}  {Title}".TrimEnd();
            }
        }

        private void InitializeUnifiedWorkspace()
        {
            if (unifiedWorkspaceInitialized) return;
            unifiedWorkspaceInitialized = true;

            SuspendLayout();
            Text = "Taiko Sound Editor";
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(1080, 680);
            Size = new Size(Math.Max(Width, 1280), Math.Max(Height, 760));

            BuildUnifiedLandingPage();
            BuildUnifiedImportPage();
            BuildUnifiedWorkspacePage();
            HookUnifiedWorkspaceEvents();
            ResetUnifiedStagedState();
            ResumeLayout(true);
        }

        private void BuildUnifiedLandingPage()
        {
            tabPage1.Controls.Clear();
            tabPage1.Padding = new Padding(24);

            groupBox1.Visible = false;
            groupBox2.Text = "Data project folder";
            groupBox12.Text = "Encryption settings (advanced)";
            groupBox13.Text = "Datatable definition (advanced)";
            groupBox2.Dock = groupBox12.Dock = groupBox13.Dock = DockStyle.Fill;
            DatatableKeyBox.UseSystemPasswordChar = true;
            FumenKeyBox.UseSystemPasswordChar = true;
            UseEncryptionBox.Text = "This project uses encrypted datatables and fumens";
            OkButton.Text = "Open data project";
            OkButton.AutoSize = true;
            OkButton.MinimumSize = new Size(150, 34);

            var showKeys = new CheckBox { Text = "Show keys", AutoSize = true, Location = new Point(360, 71) };
            showKeys.CheckedChanged += (_, _) =>
            {
                DatatableKeyBox.UseSystemPasswordChar = !showKeys.Checked;
                FumenKeyBox.UseSystemPasswordChar = !showKeys.Checked;
            };
            groupBox12.Controls.Add(showKeys);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(14)
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(Heading("Open a Taiko data project"), 0, 0);
            content.Controls.Add(BodyText("Select the folder containing datatable, sound, and fumen. The source folder remains untouched until validated export."), 0, 1);
            content.Controls.Add(groupBox2, 0, 2);
            content.Controls.Add(groupBox12, 0, 3);
            content.Controls.Add(groupBox13, 0, 4);
            content.Controls.Add(BodyText("All six datatables are loaded losslessly. Unknown fields are preserved, and opening a project never repairs or normalizes it automatically."), 0, 5);
            content.Controls.Add(RightButtons(OkButton), 0, 6);

            var frame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
            frame.Controls.Add(content, 1, 0);
            tabPage1.Controls.Add(frame);
        }

        private void BuildUnifiedImportPage()
        {
            tabPage3.Controls.Clear();
            tabPage3.Padding = new Padding(20);
            CreateBackButton.Text = "Back to songs";
            CreateOkButton.Text = "Preview and import";
            CreateBackButton.MinimumSize = new Size(120, 34);
            CreateOkButton.MinimumSize = new Size(160, 34);
            CreateBackButton.AutoSize = CreateOkButton.AutoSize = true;
            AudioFileSelector.Dock = TJASelector.Dock = SongNameBox.Dock = DockStyle.Fill;
            groupBox9.Dock = groupBox10.Dock = DockStyle.Fill;
            FeedbackBox.Dock = DockStyle.Fill;
            FeedbackBox.ReadOnly = true;
            FeedbackBox.ScrollBars = ScrollBars.Vertical;

            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 3; i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            fields.Controls.Add(FieldLabel("Audio file"), 0, 0);
            fields.Controls.Add(AudioFileSelector, 1, 0);
            fields.Controls.Add(FieldLabel("TJA file"), 0, 1);
            fields.Controls.Add(TJASelector, 1, 1);
            fields.Controls.Add(FieldLabel("Song ID"), 0, 2);
            fields.Controls.Add(SongNameBox, 1, 2);

            var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(8) };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            AddSilenceBox.Text = "Add leading silence";
            AddSilenceBox.AutoSize = true;
            var silencePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 24, 0, 0) };
            silencePanel.Controls.Add(AddSilenceBox);
            options.Controls.Add(groupBox9, 0, 0);
            options.Controls.Add(silencePanel, 1, 0);
            options.Controls.Add(groupBox10, 2, 0);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.Controls.Add(Heading("Import a TJA song"), 0, 0);
            root.Controls.Add(BodyText("The importer previews collisions and every staged row first. Conversion must finish completely before the in-memory project changes."), 0, 1);
            root.Controls.Add(fields, 0, 2);
            root.Controls.Add(options, 0, 3);
            root.Controls.Add(FeedbackBox, 0, 4);
            root.Controls.Add(RightButtons(CreateOkButton, CreateBackButton), 0, 5);
            tabPage3.Controls.Add(root);
        }

        private void BuildUnifiedWorkspacePage()
        {
            tabPage2.Controls.Clear();
            tabPage2.Padding = Padding.Empty;
            BuildUnifiedMenu();

            SoundViewerSimple.Text = "Metadata";
            SoundViewerExpert.Text = "Advanced fields";
            MusicOrderTab.Text = "Categories and order";
            SoundViewTab.Dock = DockStyle.Fill;

            unifiedProjectHeading = Heading("No data project loaded");
            var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 62, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 4, 12, 0) };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            header.Controls.Add(unifiedProjectHeading, 0, 0);
            header.Controls.Add(BodyText("Working copy only. The selected source folder is never modified; use validated export to create an output project."), 0, 1);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(8) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 275));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            body.Controls.Add(BuildUnifiedNavigator(), 0, 0);
            body.Controls.Add(SoundViewTab, 1, 0);
            body.Controls.Add(BuildUnifiedActionRail(), 2, 0);

            var status = new StatusStrip { SizingGrip = false };
            unifiedProjectStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            unifiedChangesStatus = new ToolStripStatusLabel();
            status.Items.Add(unifiedProjectStatus);
            status.Items.Add(unifiedChangesStatus);

            tabPage2.Controls.Add(body);
            tabPage2.Controls.Add(header);
            tabPage2.Controls.Add(menuStrip1);
            tabPage2.Controls.Add(status);
            menuStrip1.Dock = DockStyle.Top;
            status.Dock = DockStyle.Bottom;
        }

        private Control BuildUnifiedNavigator()
        {
            unifiedSearchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Search ID, title, UID, or status" };
            unifiedSongsBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
            unifiedSelectionHeading = BodyText("Select a song");
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(4) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            panel.Controls.Add(Heading("Songs"), 0, 0);
            panel.Controls.Add(unifiedSearchBox, 0, 1);
            panel.Controls.Add(unifiedSongsBox, 0, 2);
            panel.Controls.Add(unifiedSelectionHeading, 0, 3);
            panel.Controls.Add(BodyText("NEW: imported  ·  EDITED: metadata changed\nREPAIRED: repair applied  ·  DELETE: pending removal"), 0, 4);
            return panel;
        }

        private Control BuildUnifiedActionRail()
        {
            unifiedImportButton = ActionButton("Import TJA song...");
            unifiedMetadataButton = ActionButton("Edit metadata");
            unifiedAdvancedButton = ActionButton("Advanced fields");
            unifiedCategoriesButton = ActionButton("Edit categories...");
            unifiedAiUsbButton = ActionButton("AI / USB metadata...");
            unifiedDeleteButton = ActionButton("Delete song...");
            unifiedDiagnosticsButton = ActionButton("Diagnostics...");
            unifiedRepairsButton = ActionButton("Repairs...");
            unifiedExportButton = ActionButton("Validated export...");

            unifiedImportButton.Click += (_, _) => CreateButton_Click(this, EventArgs.Empty);
            unifiedMetadataButton.Click += (_, _) => SoundViewTab.SelectedTab = SoundViewerSimple;
            unifiedAdvancedButton.Click += (_, _) => SoundViewTab.SelectedTab = SoundViewerExpert;
            unifiedCategoriesButton.Click += (_, _) => CategoriesToolStripMenuItem_Click(this, EventArgs.Empty);
            unifiedAiUsbButton.Click += (_, _) => AdvancedMetadataToolStripMenuItem_Click(this, EventArgs.Empty);
            unifiedDeleteButton.Click += (_, _) => DeleteSongToolStripMenuItem_Click(this, EventArgs.Empty);
            unifiedDiagnosticsButton.Click += (_, _) => DiagnosticsToolStripMenuItem_Click(this, EventArgs.Empty);
            unifiedRepairsButton.Click += (_, _) => RepairsToolStripMenuItem_Click(this, EventArgs.Empty);
            unifiedExportButton.Click += (_, _) => ExportAllButton_Click(this, EventArgs.Empty);

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 11, Padding = new Padding(6) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            for (var i = 0; i < 9; i++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(Heading("Actions"), 0, 0);
            var buttons = new[] { unifiedImportButton, unifiedMetadataButton, unifiedAdvancedButton, unifiedCategoriesButton, unifiedAiUsbButton, unifiedDeleteButton, unifiedDiagnosticsButton, unifiedRepairsButton, unifiedExportButton };
            for (var i = 0; i < buttons.Length; i++) panel.Controls.Add(buttons[i], 0, i + 1);
            return panel;
        }

        private void BuildUnifiedMenu()
        {
            menuStrip1.Items.Clear();
            var project = new ToolStripMenuItem("&Project");
            project.DropDownItems.Add(MenuItem("Open another data project...", ReturnToUnifiedLandingPage));
            project.DropDownItems.Add(new ToolStripSeparator());
            project.DropDownItems.Add(MenuItem("Diagnostics...", () => DiagnosticsToolStripMenuItem_Click(this, EventArgs.Empty)));
            project.DropDownItems.Add(MenuItem("Repairs...", () => RepairsToolStripMenuItem_Click(this, EventArgs.Empty)));
            project.DropDownItems.Add(new ToolStripSeparator());
            project.DropDownItems.Add(MenuItem("Validated export...", () => ExportAllButton_Click(this, EventArgs.Empty)));
            project.DropDownItems.Add(new ToolStripSeparator());
            project.DropDownItems.Add(MenuItem("Exit", Close));

            var song = new ToolStripMenuItem("&Song");
            song.DropDownItems.Add(MenuItem("Import TJA song...", () => CreateButton_Click(this, EventArgs.Empty)));
            song.DropDownItems.Add(MenuItem("Edit metadata", () => SoundViewTab.SelectedTab = SoundViewerSimple));
            song.DropDownItems.Add(MenuItem("Advanced fields", () => SoundViewTab.SelectedTab = SoundViewerExpert));
            song.DropDownItems.Add(MenuItem("Categories...", () => CategoriesToolStripMenuItem_Click(this, EventArgs.Empty)));
            song.DropDownItems.Add(MenuItem("AI / USB metadata...", () => AdvancedMetadataToolStripMenuItem_Click(this, EventArgs.Empty)));
            song.DropDownItems.Add(new ToolStripSeparator());
            song.DropDownItems.Add(MenuItem("Delete song...", () => DeleteSongToolStripMenuItem_Click(this, EventArgs.Empty)));

            var view = new ToolStripMenuItem("&View");
            view.DropDownItems.Add(MenuItem("Metadata view", () => SoundViewTab.SelectedTab = SoundViewerSimple));
            view.DropDownItems.Add(MenuItem("Advanced fields view", () => SoundViewTab.SelectedTab = SoundViewerExpert));
            view.DropDownItems.Add(MenuItem("Categories and order view", () => SoundViewTab.SelectedTab = MusicOrderTab));
            view.DropDownItems.Add(new ToolStripSeparator());
            view.DropDownItems.Add(musicOrderToolStripMenuItem);

            menuStrip1.Items.Add(project);
            menuStrip1.Items.Add(song);
            menuStrip1.Items.Add(view);
        }

        private void HookUnifiedWorkspaceEvents()
        {
            unifiedSearchBox.TextChanged += (_, _) => RefreshUnifiedSongList();
            unifiedSongsBox.SelectedIndexChanged += UnifiedSongsBox_SelectedIndexChanged;
            LoadedMusicBox.SelectedIndexChanged += (_, _) => SyncUnifiedSelectionFromLegacy();
            NewSoundsBox.SelectedIndexChanged += (_, _) => SyncUnifiedSelectionFromLegacy();
            AddedMusicBinding.ListChanged += (_, _) => NotifyUnifiedProjectStateChanged();

            SimpleTitleBox.TextChanged += UnifiedMetadataControlChanged;
            SimpleSubtitleBox.TextChanged += UnifiedMetadataControlChanged;
            SimpleDetailBox.TextChanged += UnifiedMetadataControlChanged;
            SimpleGenreBox.SelectedIndexChanged += UnifiedMetadataControlChanged;
            SimpleStarEasyBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarNormalBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarHardBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarManiaBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarUraBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleTitleBox.Leave += (_, _) => RefreshUnifiedSongList();

            MusicInfoGrid.PropertyValueChanged += UnifiedPropertyGridChanged;
            MusicAttributesGrid.PropertyValueChanged += UnifiedPropertyGridChanged;
            MusicOrderGrid.PropertyValueChanged += UnifiedPropertyGridChanged;
            WordsGrid.PropertyValueChanged += UnifiedPropertyGridChanged;
            WordSubGrid.PropertyValueChanged += UnifiedPropertyGridChanged;
            WordDetailGrid.PropertyValueChanged += UnifiedPropertyGridChanged;
            FormClosing += UnifiedWorkspace_FormClosing;
        }

        private void UnifiedMetadataControlChanged(object sender, EventArgs e)
        {
            if (!simpleBoxLoading) MarkCurrentUnifiedSongEdited();
        }

        private void UnifiedPropertyGridChanged(object sender, PropertyValueChangedEventArgs e) => MarkCurrentUnifiedSongEdited();

        private void MarkCurrentUnifiedSongEdited()
        {
            if (LoadedMusicBox.SelectedItem is not IMusicInfo info) return;
            unifiedEditedSongIds.Add(info.Id);
            unifiedExportIsCurrent = false;
            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
        }

        private void UnifiedSongsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (unifiedSelectionChanging) return;
            unifiedSelectionChanging = true;
            try
            {
                var item = unifiedSongsBox.SelectedItem as UnifiedSongItem;
                if (item == null || item.Deleted)
                {
                    LoadedMusicBox.SelectedItem = null;
                    NewSoundsBox.SelectedItem = null;
                    unifiedSelectionHeading.Text = item?.Deleted == true ? $"{item.Id} is pending deletion" : "Select a song";
                    return;
                }

                if (item.Source is IMusicInfo info)
                {
                    NewSoundsBox.SelectedItem = null;
                    LoadedMusicBox.SelectedItem = info;
                }
                else if (item.Source is NewSongData song)
                {
                    LoadedMusicBox.SelectedItem = null;
                    NewSoundsBox.SelectedItem = song;
                }
                unifiedSelectionHeading.Text = $"Selected: {item.Id}  ·  UID {item.UniqueId}";
            }
            finally
            {
                unifiedSelectionChanging = false;
                UpdateUnifiedWorkspaceState();
            }
        }

        private void SyncUnifiedSelectionFromLegacy()
        {
            if (!unifiedWorkspaceInitialized || unifiedSelectionChanging || unifiedSongsBox == null) return;
            var id = string.Empty;
            var uid = 0;
            if (LoadedMusicBox.SelectedItem is IMusicInfo info) (id, uid) = (info.Id, info.UniqueId);
            else if (NewSoundsBox.SelectedItem is NewSongData song) (id, uid) = (song.Id, song.UniqueId);
            if (string.IsNullOrEmpty(id)) return;

            unifiedSelectionChanging = true;
            try
            {
                unifiedSongsBox.SelectedItem = unifiedSongsBox.Items.Cast<UnifiedSongItem>()
                    .FirstOrDefault(item => !item.Deleted && item.UniqueId == uid && string.Equals(item.Id, id, StringComparison.Ordinal));
            }
            finally
            {
                unifiedSelectionChanging = false;
            }
        }

        private void RefreshUnifiedSongList()
        {
            if (!unifiedWorkspaceInitialized || unifiedSongsBox == null) return;
            var previous = unifiedSongsBox.SelectedItem as UnifiedSongItem;
            var query = unifiedSearchBox?.Text?.Trim() ?? string.Empty;
            var pendingIds = new HashSet<string>(AddedMusic.Select(song => song.Id), StringComparer.Ordinal);
            var items = new List<UnifiedSongItem>();

            if (MusicInfos != null)
            {
                items.AddRange(MusicInfos.Items
                    .Where(info => info.UniqueId != 0 && !pendingIds.Contains(info.Id))
                    .Select(info => new UnifiedSongItem
                    {
                        Id = info.Id,
                        UniqueId = info.UniqueId,
                        Title = WordList?.GetBySong(info.Id)?.JapaneseText ?? string.Empty,
                        Source = info,
                        Edited = unifiedEditedSongIds.Contains(info.Id),
                        Repaired = unifiedRepairedSongIds.Contains(info.Id)
                    }));
            }

            items.AddRange(AddedMusic.Select(song => new UnifiedSongItem
            {
                Id = song.Id,
                UniqueId = song.UniqueId,
                Title = song.Word?.JapaneseText ?? string.Empty,
                Source = song,
                Imported = true,
                Repaired = unifiedRepairedSongIds.Contains(song.Id)
            }));

            if (CurrentProject != null)
            {
                items.AddRange(CurrentProject.DeletedSongIds.Select(id => new UnifiedSongItem
                {
                    Id = id,
                    Title = "Pending removal from exported project",
                    Deleted = true
                }));
            }

            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(item =>
                    item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.UniqueId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            items = items.OrderBy(item => item.Deleted)
                .ThenBy(item => item.UniqueId == 0 ? int.MaxValue : item.UniqueId)
                .ThenBy(item => item.Id, StringComparer.Ordinal).ToList();

            unifiedSelectionChanging = true;
            unifiedSongsBox.BeginUpdate();
            try
            {
                unifiedSongsBox.Items.Clear();
                unifiedSongsBox.Items.AddRange(items.Cast<object>().ToArray());
                if (previous != null)
                {
                    unifiedSongsBox.SelectedItem = unifiedSongsBox.Items.Cast<UnifiedSongItem>()
                        .FirstOrDefault(item => item.Deleted == previous.Deleted && item.UniqueId == previous.UniqueId && string.Equals(item.Id, previous.Id, StringComparison.Ordinal));
                }
            }
            finally
            {
                unifiedSongsBox.EndUpdate();
                unifiedSelectionChanging = false;
            }
        }

        private void UpdateUnifiedWorkspaceState()
        {
            if (!unifiedWorkspaceInitialized || unifiedProjectHeading == null) return;
            var loaded = CurrentProject != null && MusicInfos != null;
            var selected = unifiedSongsBox?.SelectedItem as UnifiedSongItem;
            var hasSong = loaded && selected != null && !selected.Deleted;

            unifiedProjectHeading.Text = loaded ? $"Project: {CurrentProject.Paths.Root}" : "No data project loaded";
            unifiedProjectStatus.Text = loaded ? $"Source: {CurrentProject.Paths.Root}" : "Open a complete data project to begin";
            var status = new List<string>
            {
                $"Imported {AddedMusic.Count}",
                $"Edited {unifiedEditedSongIds.Count}",
                $"Repairs {unifiedAppliedRepairCount}",
                $"Deleted {CurrentProject?.DeletedSongIds.Count ?? 0}"
            };
            if (unifiedCategoryChangesStaged) status.Add("Categories changed");
            if (unifiedExportIsCurrent && !string.IsNullOrWhiteSpace(unifiedLastExportPath)) status.Add("Exported");
            unifiedChangesStatus.Text = string.Join("  ·  ", status);

            unifiedImportButton.Enabled = loaded;
            unifiedMetadataButton.Enabled = hasSong;
            unifiedAdvancedButton.Enabled = hasSong;
            unifiedCategoriesButton.Enabled = loaded;
            unifiedAiUsbButton.Enabled = loaded;
            unifiedDeleteButton.Enabled = loaded;
            unifiedDiagnosticsButton.Enabled = loaded;
            unifiedRepairsButton.Enabled = loaded;
            unifiedExportButton.Enabled = loaded;
        }

        private void ResetUnifiedStagedState()
        {
            unifiedEditedSongIds.Clear();
            unifiedRepairedSongIds.Clear();
            unifiedAppliedRepairCount = 0;
            unifiedCategoryChangesStaged = false;
            unifiedExportIsCurrent = false;
            unifiedLastExportPath = null;
            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
        }

        private void RegisterUnifiedRepairs(IEnumerable<string> songIds, int count)
        {
            foreach (var id in songIds ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(id)) unifiedRepairedSongIds.Add(id);
            unifiedAppliedRepairCount += Math.Max(0, count);
            NotifyUnifiedProjectStateChanged();
        }

        private void MarkUnifiedCategoriesStaged()
        {
            unifiedCategoryChangesStaged = true;
            NotifyUnifiedProjectStateChanged();
        }

        private void MarkUnifiedExportComplete(string path)
        {
            unifiedLastExportPath = path;
            unifiedExportIsCurrent = true;
            UpdateUnifiedWorkspaceState();
        }

        private void NotifyUnifiedProjectStateChanged()
        {
            unifiedExportIsCurrent = false;
            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
        }

        private IReadOnlyCollection<string> GetUnifiedEditedSongIds() => unifiedEditedSongIds;
        private IReadOnlyCollection<string> GetUnifiedRepairedSongIds() => unifiedRepairedSongIds;
        private int GetUnifiedAppliedRepairCount() => unifiedAppliedRepairCount;
        private bool GetUnifiedCategoryChangesStaged() => unifiedCategoryChangesStaged;

        private bool HasUnifiedStagedChanges() => AddedMusic.Count > 0 || unifiedEditedSongIds.Count > 0 ||
            unifiedAppliedRepairCount > 0 || unifiedCategoryChangesStaged || (CurrentProject?.DeletedSongIds.Count ?? 0) > 0;

        private void ReturnToUnifiedLandingPage()
        {
            if (HasUnifiedStagedChanges() && !unifiedExportIsCurrent && MessageBox.Show(this,
                    "This project has in-memory changes that have not been exported. Return to the project picker anyway?",
                    "Unsaved staged changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            TabControl.SelectedIndex = 0;
        }

        private void UnifiedWorkspace_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!HasUnifiedStagedChanges() || unifiedExportIsCurrent) return;
            if (MessageBox.Show(this,
                    "The current project has in-memory changes that have not been exported. Close the editor and discard them?",
                    "Discard staged changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                e.Cancel = true;
        }

        private Label Heading(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        private static Label BodyText(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        private static Label FieldLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static FlowLayoutPanel RightButtons(params Control[] controls)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            panel.Controls.AddRange(controls);
            return panel;
        }

        private static Button ActionButton(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(0, 4, 0, 2)
        };

        private static ToolStripMenuItem MenuItem(string text, Action action)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) => action();
            return item;
        }
    }
}
