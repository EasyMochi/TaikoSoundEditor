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

        private readonly HashSet<string> unifiedEditedSongIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unifiedRepairedSongIds =
            new HashSet<string>(StringComparer.Ordinal);

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
                var prefix = badges.Count == 0 ? string.Empty : "[" + string.Join(" · ", badges) + "] ";
                var identity = UniqueId > 0 ? $"{UniqueId}. {Id}" : Id;
                return $"{prefix}{identity}  {Title}".TrimEnd();
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
            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
            ResumeLayout(true);
        }

        private void BuildUnifiedLandingPage()
        {
            tabPage1.Controls.Clear();
            tabPage1.Padding = new Padding(24);

            groupBox2.Text = "Data project folder";
            groupBox2.Dock = DockStyle.Fill;
            groupBox12.Text = "Encryption settings (advanced)";
            groupBox12.Dock = DockStyle.Fill;
            groupBox13.Text = "Datatable definition (advanced)";
            groupBox13.Dock = DockStyle.Fill;
            groupBox1.Visible = false;

            DatatableKeyBox.UseSystemPasswordChar = true;
            FumenKeyBox.UseSystemPasswordChar = true;
            UseEncryptionBox.Text = "This project uses encrypted datatables and fumens";

            var showKeys = new CheckBox
            {
                Text = "Show keys",
                AutoSize = true,
                Location = new Point(360, 71)
            };
            showKeys.CheckedChanged += (sender, args) =>
            {
                DatatableKeyBox.UseSystemPasswordChar = !showKeys.Checked;
                FumenKeyBox.UseSystemPasswordChar = !showKeys.Checked;
            };
            groupBox12.Controls.Add(showKeys);

            OkButton.Text = "Open data project";
            OkButton.AutoSize = true;
            OkButton.MinimumSize = new Size(150, 34);

            var title = new Label
            {
                Text = "Open a Taiko data project",
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft
            };
            var subtitle = new Label
            {
                Text = "Select the folder containing datatable, sound, and fumen. The source folder remains untouched until validated export.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft
            };
            var note = new Label
            {
                Text = "All six datatables are loaded losslessly. Unknown fields are preserved, and opening a project never repairs or normalizes it automatically.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttonPanel.Controls.Add(OkButton);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(14)
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(title, 0, 0);
            content.Controls.Add(subtitle, 0, 1);
            content.Controls.Add(groupBox2, 0, 2);
            content.Controls.Add(groupBox12, 0, 3);
            content.Controls.Add(groupBox13, 0, 4);
            content.Controls.Add(note, 0, 5);
            content.Controls.Add(buttonPanel, 0, 6);

            var frame = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
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
            CreateBackButton.AutoSize = true;
            CreateBackButton.MinimumSize = new Size(120, 34);
            CreateOkButton.Text = "Preview and import";
            CreateOkButton.AutoSize = true;
            CreateOkButton.MinimumSize = new Size(160, 34);

            AudioFileSelector.Dock = DockStyle.Fill;
            TJASelector.Dock = DockStyle.Fill;
            SongNameBox.Dock = DockStyle.Fill;
            groupBox9.Dock = DockStyle.Fill;
            groupBox10.Dock = DockStyle.Fill;
            FeedbackBox.Dock = DockStyle.Fill;
            FeedbackBox.ReadOnly = true;
            FeedbackBox.ScrollBars = ScrollBars.Vertical;

            var title = new Label
            {
                Text = "Import a TJA song",
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var safety = new Label
            {
                Text = "The importer previews collisions and every staged row first. Conversion must finish completely before the in-memory project changes.",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(8)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var row = 0; row < 3; row++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            fields.Controls.Add(CreateFieldLabel("Audio file"), 0, 0);
            fields.Controls.Add(AudioFileSelector, 1, 0);
            fields.Controls.Add(CreateFieldLabel("TJA file"), 0, 1);
            fields.Controls.Add(TJASelector, 1, 1);
            fields.Controls.Add(CreateFieldLabel("Song ID"), 0, 2);
            fields.Controls.Add(SongNameBox, 1, 2);

            var options = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8)
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            var silenceTogglePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 24, 0, 0) };
            AddSilenceBox.AutoSize = true;
            AddSilenceBox.Text = "Add leading silence";
            silenceTogglePanel.Controls.Add(AddSilenceBox);
            options.Controls.Add(groupBox9, 0, 0);
            options.Controls.Add(silenceTogglePanel, 1, 0);
            options.Controls.Add(groupBox10, 2, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(CreateOkButton);
            buttons.Controls.Add(CreateBackButton);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.Controls.Add(title, 0, 0);
            root.Controls.Add(safety, 0, 1);
            root.Controls.Add(fields, 0, 2);
            root.Controls.Add(options, 0, 3);
            root.Controls.Add(FeedbackBox, 0, 4);
            root.Controls.Add(buttons, 0, 5);
            tabPage3.Controls.Add(root);
        }

        private static Label CreateFieldLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private void BuildUnifiedWorkspacePage()
        {
            tabPage2.Controls.Clear();
            tabPage2.Padding = new Padding(0);
            BuildUnifiedMenu();

            SoundViewerSimple.Text = "Metadata";
            SoundViewerExpert.Text = "Advanced fields";
            MusicOrderTab.Text = "Categories and order";
            SoundViewTab.Dock = DockStyle.Fill;

            unifiedProjectHeading = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                AutoEllipsis = true
            };
            var safety = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Working copy only. The selected source folder is never modified; use validated export to create an output project.",
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true
            };
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 4, 12, 0)
            };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            header.Controls.Add(unifiedProjectHeading, 0, 0);
            header.Controls.Add(safety, 0, 1);

            var navigator = BuildUnifiedNavigator();
            var actions = BuildUnifiedActionRail();
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 275));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            body.Controls.Add(navigator, 0, 0);
            body.Controls.Add(SoundViewTab, 1, 0);
            body.Controls.Add(actions, 2, 0);

            var status = new StatusStrip { SizingGrip = false };
            unifiedProjectStatus = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
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
            unifiedSearchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Search ID, title, UID, or status"
            };
            unifiedSongsBox = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            unifiedSelectionHeading = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Select a song",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            var legend = new Label
            {
                Dock = DockStyle.Fill,
                Text = "NEW: imported  ·  EDITED: metadata changed\nREPAIRED: repair applied  ·  DELETE: pending removal",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(4)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            panel.Controls.Add(new Label
            {
                Text = "Songs",
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            panel.Controls.Add(unifiedSearchBox, 0, 1);
            panel.Controls.Add(unifiedSongsBox, 0, 2);
            panel.Controls.Add(unifiedSelectionHeading, 0, 3);
            panel.Controls.Add(legend, 0, 4);
            return panel;
        }

        private Control BuildUnifiedActionRail()
        {
            unifiedImportButton = CreateUnifiedActionButton("Import TJA song...");
            unifiedMetadataButton = CreateUnifiedActionButton("Edit metadata");
            unifiedAdvancedButton = CreateUnifiedActionButton("Advanced fields");
            unifiedCategoriesButton = CreateUnifiedActionButton("Edit categories...");
            unifiedAiUsbButton = CreateUnifiedActionButton("AI / USB metadata...");
            unifiedDeleteButton = CreateUnifiedActionButton("Delete song...");
            unifiedDiagnosticsButton = CreateUnifiedActionButton("Diagnostics...");
            unifiedRepairsButton = CreateUnifiedActionButton("Repairs...");
            unifiedExportButton = CreateUnifiedActionButton("Validated export...");

            unifiedImportButton.Click += (sender, args) => CreateButton.PerformClick();
            unifiedMetadataButton.Click += (sender, args) => SoundViewTab.SelectedTab = SoundViewerSimple;
            unifiedAdvancedButton.Click += (sender, args) => SoundViewTab.SelectedTab = SoundViewerExpert;
            unifiedCategoriesButton.Click += (sender, args) => categoriesToolStripMenuItem?.PerformClick();
            unifiedAiUsbButton.Click += (sender, args) => advancedMetadataToolStripMenuItem?.PerformClick();
            unifiedDeleteButton.Click += (sender, args) => deleteSongToolStripMenuItem?.PerformClick();
            unifiedDiagnosticsButton.Click += (sender, args) => diagnosticsToolStripMenuItem?.PerformClick();
            unifiedRepairsButton.Click += (sender, args) => repairsToolStripMenuItem?.PerformClick();
            unifiedExportButton.Click += (sender, args) => ExportAllButton.PerformClick();

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 11,
                Padding = new Padding(6)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            for (var i = 0; i < 9; i++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label
            {
                Text = "Actions",
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            panel.Controls.Add(unifiedImportButton, 0, 1);
            panel.Controls.Add(unifiedMetadataButton, 0, 2);
            panel.Controls.Add(unifiedAdvancedButton, 0, 3);
            panel.Controls.Add(unifiedCategoriesButton, 0, 4);
            panel.Controls.Add(unifiedAiUsbButton, 0, 5);
            panel.Controls.Add(unifiedDeleteButton, 0, 6);
            panel.Controls.Add(unifiedDiagnosticsButton, 0, 7);
            panel.Controls.Add(unifiedRepairsButton, 0, 8);
            panel.Controls.Add(unifiedExportButton, 0, 9);
            return panel;
        }

        private static Button CreateUnifiedActionButton(string text) => new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(0, 4, 0, 2)
        };

        private void BuildUnifiedMenu()
        {
            menuStrip1.Items.Clear();
            var projectMenu = new ToolStripMenuItem("&Project");
            var openAnother = new ToolStripMenuItem("Open another data project...");
            openAnother.Click += (sender, args) => ReturnToUnifiedLandingPage();
            var export = new ToolStripMenuItem("Validated export...");
            export.Click += (sender, args) => ExportAllButton.PerformClick();
            var exit = new ToolStripMenuItem("Exit");
            exit.Click += (sender, args) => Close();
            projectMenu.DropDownItems.Add(openAnother);
            projectMenu.DropDownItems.Add(new ToolStripSeparator());
            if (diagnosticsToolStripMenuItem != null) projectMenu.DropDownItems.Add(diagnosticsToolStripMenuItem);
            if (repairsToolStripMenuItem != null) projectMenu.DropDownItems.Add(repairsToolStripMenuItem);
            projectMenu.DropDownItems.Add(new ToolStripSeparator());
            projectMenu.DropDownItems.Add(export);
            projectMenu.DropDownItems.Add(new ToolStripSeparator());
            projectMenu.DropDownItems.Add(exit);

            var songMenu = new ToolStripMenuItem("&Song");
            var import = new ToolStripMenuItem("Import TJA song...");
            import.Click += (sender, args) => CreateButton.PerformClick();
            var metadata = new ToolStripMenuItem("Edit metadata");
            metadata.Click += (sender, args) => SoundViewTab.SelectedTab = SoundViewerSimple;
            var advanced = new ToolStripMenuItem("Advanced fields");
            advanced.Click += (sender, args) => SoundViewTab.SelectedTab = SoundViewerExpert;
            songMenu.DropDownItems.Add(import);
            songMenu.DropDownItems.Add(metadata);
            songMenu.DropDownItems.Add(advanced);
            if (categoriesToolStripMenuItem != null) songMenu.DropDownItems.Add(categoriesToolStripMenuItem);
            if (advancedMetadataToolStripMenuItem != null) songMenu.DropDownItems.Add(advancedMetadataToolStripMenuItem);
            songMenu.DropDownItems.Add(new ToolStripSeparator());
            if (deleteSongToolStripMenuItem != null) songMenu.DropDownItems.Add(deleteSongToolStripMenuItem);

            var viewMenu = new ToolStripMenuItem("&View");
            var metadataView = new ToolStripMenuItem("Metadata view");
            metadataView.Click += (sender, args) => SoundViewTab.SelectedTab = SoundViewerSimple;
            var advancedView = new ToolStripMenuItem("Advanced fields view");
            advancedView.Click += (sender, args) => SoundViewTab.SelectedTab = SoundViewerExpert;
            var categoryView = new ToolStripMenuItem("Categories and order view");
            categoryView.Click += (sender, args) => SoundViewTab.SelectedTab = MusicOrderTab;
            viewMenu.DropDownItems.Add(metadataView);
            viewMenu.DropDownItems.Add(advancedView);
            viewMenu.DropDownItems.Add(categoryView);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(musicOrderToolStripMenuItem);

            menuStrip1.Items.Add(projectMenu);
            menuStrip1.Items.Add(songMenu);
            menuStrip1.Items.Add(viewMenu);
        }

        private void HookUnifiedWorkspaceEvents()
        {
            unifiedSearchBox.TextChanged += (sender, args) => RefreshUnifiedSongList();
            unifiedSongsBox.SelectedIndexChanged += UnifiedSongsBox_SelectedIndexChanged;
            LoadedMusicBox.SelectedIndexChanged += (sender, args) => SyncUnifiedSelectionFromLegacy();
            NewSoundsBox.SelectedIndexChanged += (sender, args) => SyncUnifiedSelectionFromLegacy();
            AddedMusicBinding.ListChanged += (sender, args) =>
            {
                RefreshUnifiedSongList();
                UpdateUnifiedWorkspaceState();
            };

            SimpleTitleBox.TextChanged += UnifiedMetadataControlChanged;
            SimpleSubtitleBox.TextChanged += UnifiedMetadataControlChanged;
            SimpleDetailBox.TextChanged += UnifiedMetadataControlChanged;
            SimpleGenreBox.SelectedIndexChanged += UnifiedMetadataControlChanged;
            SimpleStarEasyBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarNormalBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarHardBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarManiaBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleStarUraBox.ValueChanged += UnifiedMetadataControlChanged;
            SimpleTitleBox.Leave += (sender, args) => RefreshUnifiedSongList();

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
            if (simpleBoxLoading) return;
            MarkCurrentUnifiedSongEdited();
        }

        private void UnifiedPropertyGridChanged(object sender, PropertyValueChangedEventArgs e) =>
            MarkCurrentUnifiedSongEdited();

        private void MarkCurrentUnifiedSongEdited()
        {
            if (LoadedMusicBox.SelectedItem is not IMusicInfo musicInfo) return;
            unifiedEditedSongIds.Add(musicInfo.Id);
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
                if (unifiedSongsBox.SelectedItem is not UnifiedSongItem item || item.Deleted)
                {
                    LoadedMusicBox.SelectedItem = null;
                    NewSoundsBox.SelectedItem = null;
                    unifiedSelectionHeading.Text = item?.Deleted == true
                        ? $"{item.Id} is pending deletion"
                        : "Select a song";
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
            string id = null;
            int uniqueId = 0;
            if (LoadedMusicBox.SelectedItem is IMusicInfo info)
            {
                id = info.Id;
                uniqueId = info.UniqueId;
            }
            else if (NewSoundsBox.SelectedItem is NewSongData song)
            {
                id = song.Id;
                uniqueId = song.UniqueId;
            }
            if (id == null) return;

            unifiedSelectionChanging = true;
            try
            {
                unifiedSongsBox.SelectedItem = unifiedSongsBox.Items.Cast<UnifiedSongItem>()
                    .FirstOrDefault(item => !item.Deleted && item.UniqueId == uniqueId &&
                                            string.Equals(item.Id, id, StringComparison.Ordinal));
            }
            finally
            {
                unifiedSelectionChanging = false;
            }
        }

        private void RefreshUnifiedSongList()
        {
            if (!unifiedWorkspaceInitialized || unifiedSongsBox == null) return;
            var selected = unifiedSongsBox.SelectedItem as UnifiedSongItem;
            var query = unifiedSearchBox?.Text?.Trim() ?? string.Empty;
            var pendingIds = new HashSet<string>(AddedMusic.Select(song => song.Id), StringComparer.Ordinal);
            var items = new List<UnifiedSongItem>();

            if (MusicInfos != null)
            {
                foreach (var info in MusicInfos.Items.Where(info => info.UniqueId != 0 && !pendingIds.Contains(info.Id)))
                {
                    var title = WordList?.GetBySong(info.Id)?.JapaneseText ?? string.Empty;
                    items.Add(new UnifiedSongItem
                    {
                        Id = info.Id,
                        UniqueId = info.UniqueId,
                        Title = title,
                        Source = info,
                        Edited = unifiedEditedSongIds.Contains(info.Id),
                        Repaired = unifiedRepairedSongIds.Contains(info.Id)
                    });
                }
            }

            foreach (var song in AddedMusic)
            {
                items.Add(new UnifiedSongItem
                {
                    Id = song.Id,
                    UniqueId = song.UniqueId,
                    Title = song.Word?.JapaneseText ?? string.Empty,
                    Source = song,
                    Imported = true,
                    Repaired = unifiedRepairedSongIds.Contains(song.Id)
                });
            }

            if (CurrentProject != null)
            {
                foreach (var id in CurrentProject.DeletedSongIds)
                {
                    items.Add(new UnifiedSongItem
                    {
                        Id = id,
                        Title = "Pending removal from exported project",
                        Deleted = true
                    });
                }
            }

            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(item =>
                        item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.UniqueId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            items = items
                .OrderBy(item => item.Deleted)
                .ThenBy(item => item.UniqueId == 0 ? int.MaxValue : item.UniqueId)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToList();

            unifiedSelectionChanging = true;
            unifiedSongsBox.BeginUpdate();
            try
            {
                unifiedSongsBox.Items.Clear();
                foreach (var item in items) unifiedSongsBox.Items.Add(item);
                if (selected != null)
                {
                    unifiedSongsBox.SelectedItem = unifiedSongsBox.Items.Cast<UnifiedSongItem>()
                        .FirstOrDefault(item => item.Deleted == selected.Deleted &&
                                                item.UniqueId == selected.UniqueId &&
                                                string.Equals(item.Id, selected.Id, StringComparison.Ordinal));
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
            if (!unifiedWorkspaceInitialized) return;
            var loaded = CurrentProject != null && MusicInfos != null;
            var selected = unifiedSongsBox?.SelectedItem as UnifiedSongItem;
            var selectableSong = loaded && selected != null && !selected.Deleted;

            unifiedProjectHeading.Text = loaded
                ? $"Project: {CurrentProject.Paths.Root}"
                : "No data project loaded";
            unifiedProjectStatus.Text = loaded
                ? $"Source: {CurrentProject.Paths.Root}"
                : "Open a complete data project to begin";

            var changes = new List<string>
            {
                $"Imported {AddedMusic.Count}",
                $"Edited {unifiedEditedSongIds.Count}",
                $"Repairs {unifiedAppliedRepairCount}",
                $"Deleted {CurrentProject?.DeletedSongIds.Count ?? 0}"
            };
            if (unifiedCategoryChangesStaged) changes.Add("Categories changed");
            if (unifiedExportIsCurrent && !string.IsNullOrWhiteSpace(unifiedLastExportPath))
                changes.Add("Exported");
            unifiedChangesStatus.Text = string.Join("  ·  ", changes);

            unifiedImportButton.Enabled = loaded;
            unifiedMetadataButton.Enabled = selectableSong;
            unifiedAdvancedButton.Enabled = selectableSong;
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

        private void RegisterUnifiedRepairs(IEnumerable<string> songIds, int appliedCount)
        {
            foreach (var id in songIds ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(id)) unifiedRepairedSongIds.Add(id);
            unifiedAppliedRepairCount += Math.Max(0, appliedCount);
            unifiedExportIsCurrent = false;
            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
        }

        private void MarkUnifiedCategoriesStaged()
        {
            unifiedCategoryChangesStaged = true;
            unifiedExportIsCurrent = false;
            UpdateUnifiedWorkspaceState();
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

        private bool HasUnifiedStagedChanges() =>
            AddedMusic.Count > 0 || unifiedEditedSongIds.Count > 0 || unifiedAppliedRepairCount > 0 ||
            unifiedCategoryChangesStaged || (CurrentProject?.DeletedSongIds.Count ?? 0) > 0;

        private void ReturnToUnifiedLandingPage()
        {
            if (HasUnifiedStagedChanges() && !unifiedExportIsCurrent)
            {
                var answer = MessageBox.Show(this,
                    "This project has in-memory changes that have not been exported. Return to the project picker anyway?",
                    "Unsaved staged changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }
            TabControl.SelectedIndex = 0;
        }

        private void UnifiedWorkspace_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!HasUnifiedStagedChanges() || unifiedExportIsCurrent) return;
            var answer = MessageBox.Show(this,
                "The current project has in-memory changes that have not been exported. Close the editor and discard them?",
                "Discard staged changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) e.Cancel = true;
        }
    }
}
