using System.Windows.Forms;
using TaikoSoundEditor.Commons.Controls;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            TabControl = new TabControl();
            tabPage1 = new TabPage();
            tabPage2 = new TabPage();
            tabPage3 = new TabPage();

            panel1 = new Panel();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            groupBox12 = new GroupBox();
            groupBox13 = new GroupBox();
            label1 = new Label();
            label5 = new Label();
            label6 = new Label();
            label7 = new Label();
            label8 = new Label();
            label20 = new Label();
            label21 = new Label();
            label22 = new Label();
            DirSelector = new PathSelector();
            MusicAttributePathSelector = new PathSelector();
            MusicOrderPathSelector = new PathSelector();
            MusicInfoPathSelector = new PathSelector();
            WordListPathSelector = new PathSelector();
            DatatableDef = new PathSelector();
            DatatableKeyBox = new TextBox();
            FumenKeyBox = new TextBox();
            UseEncryptionBox = new CheckBox();
            OkButton = new Button();

            menuStrip1 = new MenuStrip();
            preferencesToolStripMenuItem1 = new ToolStripMenuItem();
            musicOrderToolStripMenuItem = new ToolStripMenuItem();
            sortByTitleToolStripMenuItem = new ToolStripMenuItem();
            SortByGenreToolStripMenuItem = new ToolStripMenuItem();
            SortByIdToolStripMenuItem = new ToolStripMenuItem();
            noSortToolStripMenuItem = new ToolStripMenuItem();
            checkForUpdatesToolStripMenuItem = new ToolStripMenuItem();

            SoundViewTab = new TabControl();
            SoundViewerSimple = new TabPage();
            SoundViewerExpert = new TabPage();
            MusicOrderTab = new TabPage();
            LocateInMusicOrderButton = new Button();
            groupBox11 = new GroupBox();
            label19 = new Label();
            label17 = new Label();
            label18 = new Label();
            label16 = new Label();
            label15 = new Label();
            label14 = new Label();
            label13 = new Label();
            label12 = new Label();
            label11 = new Label();
            label4 = new Label();
            SimpleStarUraBox = new NumericUpDown();
            SimpleStarManiaBox = new NumericUpDown();
            SimpleStarHardBox = new NumericUpDown();
            SimpleStarNormalBox = new NumericUpDown();
            SimpleStarEasyBox = new NumericUpDown();
            SimpleIdBox = new TextBox();
            SimpleGenreBox = new ComboBox();
            SimpleDetailBox = new TextBox();
            SimpleSubtitleBox = new TextBox();
            SimpleTitleBox = new TextBox();

            groupBox4 = new GroupBox();
            EditorTable = new TableLayoutPanel();
            panel3 = new Panel();
            groupBox6 = new GroupBox();
            groupBox7 = new GroupBox();
            groupBox5 = new GroupBox();
            panel2 = new Panel();
            WordDetailGB = new GroupBox();
            WordSubGB = new GroupBox();
            WordsGB = new GroupBox();
            MusicInfoGrid = new PropertyGrid();
            MusicAttributesGrid = new PropertyGrid();
            MusicOrderGrid = new PropertyGrid();
            WordsGrid = new PropertyGrid();
            WordSubGrid = new PropertyGrid();
            WordDetailGrid = new PropertyGrid();
            MusicOrderViewer = new MusicOrderViewer();

            groupBox8 = new GroupBox();
            groupBox3 = new GroupBox();
            LoadedMusicBox = new ListBox();
            NewSoundsBox = new ListBox();
            SearchBox = new TextBox();
            CreateButton = new Button();
            RemoveSongButton = new Button();
            ExportDatatableButton = new Button();
            ExportSoundFoldersButton = new Button();
            ExportSoundBanksButton = new Button();
            ExportAllButton = new Button();
            ExportOpenOnFinished = new CheckBox();
            DatatableSpaces = new CheckBox();

            panel4 = new Panel();
            groupBox10 = new GroupBox();
            groupBox9 = new GroupBox();
            label2 = new Label();
            label3 = new Label();
            label9 = new Label();
            label10 = new Label();
            TJASelector = new PathSelector();
            AudioFileSelector = new PathSelector();
            SongNameBox = new TextBox();
            CreateOkButton = new Button();
            CreateBackButton = new Button();
            FeedbackBox = new TextBox();
            AddSilenceBox = new CheckBox();
            TjaEncShiftJIS = new RadioButton();
            TjaEncUTF8 = new RadioButton();
            TjaEncAuto = new RadioButton();
            SilenceBox = new NumericUpDown();

            SuspendLayout();

            TabControl.Dock = DockStyle.Fill;
            TabControl.Appearance = TabAppearance.FlatButtons;
            TabControl.ItemSize = new System.Drawing.Size(0, 1);
            TabControl.SizeMode = TabSizeMode.Fixed;
            TabControl.Controls.Add(tabPage1);
            TabControl.Controls.Add(tabPage2);
            TabControl.Controls.Add(tabPage3);

            tabPage1.Text = "Project";
            tabPage2.Text = "Editor";
            tabPage3.Text = "Import";

            panel1.Dock = DockStyle.Fill;
            tabPage1.Controls.Add(panel1);

            groupBox2.Text = "Data project folder";
            groupBox2.Dock = DockStyle.Top;
            groupBox2.Height = 74;
            DirSelector.Dock = DockStyle.Fill;
            DirSelector.SelectsFolder = true;
            groupBox2.Controls.Add(DirSelector);

            groupBox12.Text = "Encryption";
            groupBox12.Dock = DockStyle.Top;
            groupBox12.Height = 110;
            DatatableKeyBox.Dock = DockStyle.Top;
            FumenKeyBox.Dock = DockStyle.Top;
            UseEncryptionBox.Text = "Use encryption";
            UseEncryptionBox.Dock = DockStyle.Top;
            groupBox12.Controls.Add(UseEncryptionBox);
            groupBox12.Controls.Add(FumenKeyBox);
            groupBox12.Controls.Add(DatatableKeyBox);

            groupBox13.Text = "Datatable definition";
            groupBox13.Dock = DockStyle.Top;
            groupBox13.Height = 74;
            DatatableDef.Dock = DockStyle.Fill;
            groupBox13.Controls.Add(DatatableDef);

            groupBox1.Text = "Legacy table paths";
            groupBox1.Dock = DockStyle.Top;
            groupBox1.Height = 120;
            MusicAttributePathSelector.Dock = DockStyle.Top;
            MusicOrderPathSelector.Dock = DockStyle.Top;
            MusicInfoPathSelector.Dock = DockStyle.Top;
            WordListPathSelector.Dock = DockStyle.Top;
            groupBox1.Controls.Add(WordListPathSelector);
            groupBox1.Controls.Add(MusicInfoPathSelector);
            groupBox1.Controls.Add(MusicOrderPathSelector);
            groupBox1.Controls.Add(MusicAttributePathSelector);

            OkButton.Text = "Open data project";
            OkButton.Dock = DockStyle.Bottom;
            OkButton.Height = 36;
            OkButton.Click += OkButton_Click;

            panel1.Controls.Add(OkButton);
            panel1.Controls.Add(groupBox1);
            panel1.Controls.Add(groupBox13);
            panel1.Controls.Add(groupBox12);
            panel1.Controls.Add(groupBox2);

            menuStrip1.Items.Add(preferencesToolStripMenuItem1);
            menuStrip1.Items.Add(checkForUpdatesToolStripMenuItem);
            preferencesToolStripMenuItem1.Text = "Preferences";
            preferencesToolStripMenuItem1.DropDownItems.Add(musicOrderToolStripMenuItem);
            musicOrderToolStripMenuItem.Text = "Music order";
            musicOrderToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                sortByTitleToolStripMenuItem,
                SortByGenreToolStripMenuItem,
                SortByIdToolStripMenuItem,
                noSortToolStripMenuItem
            });
            sortByTitleToolStripMenuItem.Text = "Sort by title";
            SortByGenreToolStripMenuItem.Text = "Sort by genre";
            SortByIdToolStripMenuItem.Text = "Sort by ID";
            noSortToolStripMenuItem.Text = "No sort";
            sortByTitleToolStripMenuItem.Click += MusicOrderSortToolStripMenuItem_Click;
            SortByGenreToolStripMenuItem.Click += MusicOrderSortToolStripMenuItem_Click;
            SortByIdToolStripMenuItem.Click += MusicOrderSortToolStripMenuItem_Click;
            noSortToolStripMenuItem.Click += MusicOrderSortToolStripMenuItem_Click;
            checkForUpdatesToolStripMenuItem.Text = "Check for updates";
            checkForUpdatesToolStripMenuItem.Visible = false;
            checkForUpdatesToolStripMenuItem.Click += checkForUpdatesToolStripMenuItem_Click;

            SoundViewTab.Dock = DockStyle.Fill;
            SoundViewTab.Controls.Add(SoundViewerSimple);
            SoundViewTab.Controls.Add(SoundViewerExpert);
            SoundViewTab.Controls.Add(MusicOrderTab);
            SoundViewTab.SelectedIndexChanged += SoundViewTab_SelectedIndexChanged;

            SoundViewerSimple.Text = "Metadata";
            SoundViewerExpert.Text = "Advanced fields";
            MusicOrderTab.Text = "Categories and order";

            var simpleTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(8)
            };
            simpleTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            simpleTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 7; i++) simpleTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            label14.Text = "ID";
            label4.Text = "Title";
            label11.Text = "Subtitle";
            label12.Text = "Detail";
            label13.Text = "Genre";
            label14.Dock = label4.Dock = label11.Dock = label12.Dock = label13.Dock = DockStyle.Fill;
            SimpleIdBox.Dock = SimpleTitleBox.Dock = SimpleSubtitleBox.Dock = SimpleDetailBox.Dock = SimpleGenreBox.Dock = DockStyle.Fill;
            SimpleIdBox.ReadOnly = true;
            SimpleTitleBox.TextChanged += SimpleBoxChanged;
            SimpleSubtitleBox.TextChanged += SimpleBoxChanged;
            SimpleDetailBox.TextChanged += SimpleBoxChanged;
            SimpleGenreBox.SelectedIndexChanged += SimpleBoxChanged;

            simpleTable.Controls.Add(label14, 0, 0);
            simpleTable.Controls.Add(SimpleIdBox, 1, 0);
            simpleTable.Controls.Add(label4, 0, 1);
            simpleTable.Controls.Add(SimpleTitleBox, 1, 1);
            simpleTable.Controls.Add(label11, 0, 2);
            simpleTable.Controls.Add(SimpleSubtitleBox, 1, 2);
            simpleTable.Controls.Add(label12, 0, 3);
            simpleTable.Controls.Add(SimpleDetailBox, 1, 3);
            simpleTable.Controls.Add(label13, 0, 4);
            simpleTable.Controls.Add(SimpleGenreBox, 1, 4);

            groupBox11.Text = "Stars";
            groupBox11.Dock = DockStyle.Fill;
            var stars = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            label15.Text = "Easy";
            label16.Text = "Normal";
            label18.Text = "Hard";
            label17.Text = "Oni";
            label19.Text = "Ura";
            foreach (var box in new[] { SimpleStarEasyBox, SimpleStarNormalBox, SimpleStarHardBox, SimpleStarManiaBox, SimpleStarUraBox })
            {
                box.Minimum = 0;
                box.Maximum = 10;
                box.Width = 48;
                box.ValueChanged += SimpleBoxChanged;
            }
            stars.Controls.AddRange(new Control[]
            {
                label15, SimpleStarEasyBox,
                label16, SimpleStarNormalBox,
                label18, SimpleStarHardBox,
                label17, SimpleStarManiaBox,
                label19, SimpleStarUraBox
            });
            groupBox11.Controls.Add(stars);
            simpleTable.Controls.Add(groupBox11, 0, 5);
            simpleTable.SetColumnSpan(groupBox11, 2);

            LocateInMusicOrderButton.Text = "Locate in category order";
            LocateInMusicOrderButton.Dock = DockStyle.Fill;
            LocateInMusicOrderButton.Click += LocateInMusicOrderButton_Click;
            simpleTable.Controls.Add(LocateInMusicOrderButton, 0, 6);
            simpleTable.SetColumnSpan(LocateInMusicOrderButton, 2);
            SoundViewerSimple.Controls.Add(simpleTable);

            groupBox4.Dock = DockStyle.Fill;
            groupBox4.Text = "Song data";
            EditorTable.Dock = DockStyle.Fill;
            EditorTable.ColumnCount = 3;
            EditorTable.RowCount = 1;
            EditorTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            EditorTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            EditorTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            EditorTable.Resize += EditorTable_Resize;

            groupBox5.Text = "Music info";
            groupBox6.Text = "Music attributes";
            groupBox7.Text = "Music order";
            WordsGB.Text = "Title word";
            WordSubGB.Text = "Subtitle word";
            WordDetailGB.Text = "Detail word";
            foreach (var grid in new[] { MusicInfoGrid, MusicAttributesGrid, MusicOrderGrid, WordsGrid, WordSubGrid, WordDetailGrid })
            {
                grid.Dock = DockStyle.Fill;
                grid.HelpVisible = false;
                grid.ToolbarVisible = false;
            }
            groupBox5.Dock = DockStyle.Fill;
            groupBox6.Dock = DockStyle.Fill;
            groupBox7.Dock = DockStyle.Bottom;
            groupBox7.Height = 150;
            WordsGB.Dock = DockStyle.Top;
            WordSubGB.Dock = DockStyle.Top;
            WordDetailGB.Dock = DockStyle.Fill;
            WordsGB.Height = WordSubGB.Height = 120;
            groupBox5.Controls.Add(MusicInfoGrid);
            groupBox6.Controls.Add(MusicAttributesGrid);
            groupBox7.Controls.Add(MusicOrderGrid);
            WordsGB.Controls.Add(WordsGrid);
            WordSubGB.Controls.Add(WordSubGrid);
            WordDetailGB.Controls.Add(WordDetailGrid);
            panel3.Dock = DockStyle.Fill;
            panel3.Controls.Add(groupBox6);
            panel3.Controls.Add(groupBox7);
            panel2.Dock = DockStyle.Fill;
            panel2.Controls.Add(WordDetailGB);
            panel2.Controls.Add(WordSubGB);
            panel2.Controls.Add(WordsGB);
            EditorTable.Controls.Add(groupBox5, 0, 0);
            EditorTable.Controls.Add(panel3, 1, 0);
            EditorTable.Controls.Add(panel2, 2, 0);
            groupBox4.Controls.Add(EditorTable);
            SoundViewerExpert.Controls.Add(groupBox4);

            MusicOrderViewer.Dock = DockStyle.Fill;
            MusicOrderViewer.SongRemoved += MusicOrderViewer_SongRemoved;
            MusicOrderViewer.SongDoubleClick += MusicOrderViewer_SongDoubleClick;
            MusicOrderTab.Controls.Add(MusicOrderViewer);

            var editorRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            editorRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            editorRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editorRoot.Controls.Add(menuStrip1, 0, 0);
            editorRoot.Controls.Add(SoundViewTab, 0, 1);
            tabPage2.Controls.Add(editorRoot);

            groupBox3.Text = "Existing songs";
            groupBox8.Text = "Imported songs";
            SearchBox.TextChanged += SearchBox_TextChanged;
            LoadedMusicBox.DrawMode = DrawMode.OwnerDrawFixed;
            LoadedMusicBox.DrawItem += LoadedMusicBox_DrawItem;
            LoadedMusicBox.SelectedIndexChanged += LoadedMusicBox_SelectedIndexChanged;
            NewSoundsBox.SelectedIndexChanged += NewSoundsBox_SelectedIndexChanged;
            CreateButton.Text = "Import";
            CreateButton.Click += CreateButton_Click;
            RemoveSongButton.Text = "Remove";
            RemoveSongButton.Click += RemoveSongButton_Click;
            ExportDatatableButton.Text = "Export datatables";
            ExportDatatableButton.Click += ExportDatatableButton_Click;
            ExportSoundFoldersButton.Text = "Export fumens";
            ExportSoundFoldersButton.Click += ExportSoundFoldersButton_Click;
            ExportSoundBanksButton.Text = "Export sound banks";
            ExportSoundBanksButton.Click += ExportSoundBanksButton_Click;
            ExportAllButton.Text = "Export project";
            ExportAllButton.Click += ExportAllButton_Click;
            ExportOpenOnFinished.Text = "Open output folder";
            ExportOpenOnFinished.Checked = true;
            DatatableSpaces.Text = "Compact datatables";

            groupBox9.Text = "TJA encoding";
            TjaEncAuto.Text = "Auto";
            TjaEncUTF8.Text = "UTF-8";
            TjaEncShiftJIS.Text = "Shift-JIS";
            TjaEncAuto.Checked = true;
            groupBox9.Controls.Add(TjaEncShiftJIS);
            groupBox9.Controls.Add(TjaEncUTF8);
            groupBox9.Controls.Add(TjaEncAuto);

            groupBox10.Text = "Import options";
            label2.Text = "Song ID";
            label3.Text = "seconds";
            label9.Text = "TJA file";
            label10.Text = "Audio file";
            TJASelector.Filter = ".tja files(*.tja)|*.tja|All files(*.*)|*.*";
            AudioFileSelector.Filter = "Audio files|*.ogg;*.mp3;*.wav|All files(*.*)|*.*";
            TJASelector.PathChanged += TJASelector_PathChanged;
            AddSilenceBox.Text = "Add leading silence";
            AddSilenceBox.CheckedChanged += AddSilenceBox_CheckedChanged;
            SilenceBox.Minimum = 0;
            SilenceBox.Maximum = 10;
            SilenceBox.Value = 3;
            CreateOkButton.Text = "Preview and import";
            CreateOkButton.Click += CreateOkButton_Click;
            CreateBackButton.Text = "Back";
            CreateBackButton.Click += CreateBackButton_Click;
            FeedbackBox.Multiline = true;
            FeedbackBox.ScrollBars = ScrollBars.Vertical;

            var importTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(12)
            };
            importTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            importTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 6; i++) importTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            importTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            importTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            importTable.Controls.Add(label10, 0, 0);
            importTable.Controls.Add(AudioFileSelector, 1, 0);
            importTable.Controls.Add(label9, 0, 1);
            importTable.Controls.Add(TJASelector, 1, 1);
            importTable.Controls.Add(label2, 0, 2);
            importTable.Controls.Add(SongNameBox, 1, 2);
            importTable.Controls.Add(groupBox9, 0, 3);
            importTable.SetColumnSpan(groupBox9, 2);
            importTable.Controls.Add(AddSilenceBox, 0, 4);
            importTable.Controls.Add(SilenceBox, 1, 4);
            importTable.Controls.Add(FeedbackBox, 0, 6);
            importTable.SetColumnSpan(FeedbackBox, 2);
            var importButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            importButtons.Controls.Add(CreateOkButton);
            importButtons.Controls.Add(CreateBackButton);
            importTable.Controls.Add(importButtons, 0, 7);
            importTable.SetColumnSpan(importButtons, 2);
            tabPage3.Controls.Add(importTable);

            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1200, 760);
            MinimumSize = new System.Drawing.Size(1000, 600);
            Controls.Add(TabControl);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            Text = "Taiko Sound Editor";

            ResumeLayout(false);
        }

        #endregion

        private TabControl TabControl;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private TabPage tabPage3;
        private Panel panel1;
        private GroupBox groupBox1;
        private Label label5;
        private Label label6;
        private Label label7;
        private PathSelector WordListPathSelector;
        private PathSelector MusicInfoPathSelector;
        private PathSelector MusicOrderPathSelector;
        private PathSelector MusicAttributePathSelector;
        private Label label8;
        private GroupBox groupBox2;
        private Button OkButton;
        private PathSelector DirSelector;
        private Label label1;
        private ListBox LoadedMusicBox;
        private GroupBox groupBox3;
        private PropertyGrid MusicInfoGrid;
        private GroupBox groupBox4;
        private GroupBox groupBox5;
        private GroupBox groupBox6;
        private PropertyGrid MusicAttributesGrid;
        private GroupBox WordsGB;
        private PropertyGrid WordsGrid;
        private TableLayoutPanel EditorTable;
        private Panel panel2;
        private GroupBox WordDetailGB;
        private PropertyGrid WordDetailGrid;
        private GroupBox WordSubGB;
        private PropertyGrid WordSubGrid;
        private Panel panel3;
        private GroupBox groupBox7;
        private PropertyGrid MusicOrderGrid;
        private GroupBox groupBox8;
        private ListBox NewSoundsBox;
        private Button CreateButton;
        private Panel panel4;
        private GroupBox groupBox10;
        private Label label9;
        private PathSelector TJASelector;
        private PathSelector AudioFileSelector;
        private Label label10;
        private TextBox SongNameBox;
        private Label label2;
        private Button CreateOkButton;
        private Button CreateBackButton;
        private TextBox FeedbackBox;
        private Button ExportAllButton;
        private Button ExportSoundBanksButton;
        private Button ExportSoundFoldersButton;
        private Button ExportDatatableButton;
        private CheckBox ExportOpenOnFinished;
        private CheckBox AddSilenceBox;
        private CheckBox DatatableSpaces;
        private Button RemoveSongButton;
        private GroupBox groupBox9;
        private RadioButton TjaEncShiftJIS;
        private RadioButton TjaEncUTF8;
        private RadioButton TjaEncAuto;
        private NumericUpDown SilenceBox;
        private Label label3;
        private TabControl SoundViewTab;
        private TabPage SoundViewerExpert;
        private TabPage SoundViewerSimple;
        private TextBox SimpleSubtitleBox;
        private TextBox SimpleTitleBox;
        private Label label4;
        private TextBox SimpleDetailBox;
        private Label label12;
        private Label label11;
        private ComboBox SimpleGenreBox;
        private Label label13;
        private Label label14;
        private TextBox SimpleIdBox;
        private TabPage MusicOrderTab;
        private GroupBox groupBox11;
        private Label label17;
        private NumericUpDown SimpleStarManiaBox;
        private Label label18;
        private NumericUpDown SimpleStarHardBox;
        private Label label16;
        private NumericUpDown SimpleStarNormalBox;
        private Label label15;
        private NumericUpDown SimpleStarEasyBox;
        private Label label19;
        private NumericUpDown SimpleStarUraBox;
        private Button LocateInMusicOrderButton;
        private MusicOrderViewer MusicOrderViewer;
        private TextBox SearchBox;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem preferencesToolStripMenuItem1;
        private ToolStripMenuItem musicOrderToolStripMenuItem;
        private ToolStripMenuItem SortByGenreToolStripMenuItem;
        private ToolStripMenuItem SortByIdToolStripMenuItem;
        private ToolStripMenuItem checkForUpdatesToolStripMenuItem;
        private GroupBox groupBox12;
        private Label label20;
        private TextBox FumenKeyBox;
        private Label label21;
        private TextBox DatatableKeyBox;
        private CheckBox UseEncryptionBox;
        private GroupBox groupBox13;
        private Label label22;
        private PathSelector DatatableDef;
        private ToolStripMenuItem sortByTitleToolStripMenuItem;
        private ToolStripMenuItem noSortToolStripMenuItem;
    }
}
