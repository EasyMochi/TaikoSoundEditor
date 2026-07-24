using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using TaikoSoundEditor.Collections;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal sealed class EventFolderEditorForm : Form
    {
        private readonly TaikoProject project;
        private readonly EventFolderDataDocument serverData;
        private readonly MusicInfos musicInfos;
        private readonly WordList wordList;
        private readonly IReadOnlyCollection<NewSongData> importedSongs;

        private readonly ListBox folders = new ListBox();
        private readonly Button addFolder = new Button();
        private readonly Button createClientDefinition = new Button();
        private readonly Button deleteFolder = new Button();
        private readonly Label validation = new Label();

        private readonly TextBox internalId = new TextBox();
        private readonly NumericUpDown folderId = IntegerBox(0, int.MaxValue);
        private readonly NumericUpDown order = IntegerBox(int.MinValue, int.MaxValue);
        private readonly TextBox voiceToneId = new TextBox();
        private readonly TextBox title = new TextBox();
        private readonly TextBox introduction = new TextBox();
        private readonly CheckBox serverReleased = new CheckBox();
        private readonly CheckBox normalMode = new CheckBox();
        private readonly CheckBox aiMode = new CheckBox();
        private readonly CheckBox collabo025Mode = new CheckBox();
        private readonly CheckBox collabo026Mode = new CheckBox();
        private readonly CheckBox aoharuMode = new CheckBox();
        private readonly NumericUpDown version = IntegerBox(0, int.MaxValue);
        private readonly NumericUpDown priority = IntegerBox(0, int.MaxValue);

        private readonly TextBox songSearch = new TextBox();
        private readonly ListBox availableSongs = new ListBox();
        private readonly ListBox folderSongs = new ListBox();
        private readonly Button addSong = new Button();
        private readonly Button removeSong = new Button();
        private readonly Button moveSongUp = new Button();
        private readonly Button moveSongDown = new Button();
        private readonly Button exportServerFiles = new Button();
        private readonly Button closeButton = new Button();

        private readonly List<SongChoice> allSongs = new List<SongChoice>();
        private bool refreshing;

        public EventFolderEditorForm(TaikoProject project, EventFolderDataDocument serverData,
            MusicInfos musicInfos, WordList wordList, IReadOnlyCollection<NewSongData> importedSongs)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.serverData = serverData ?? throw new ArgumentNullException(nameof(serverData));
            this.musicInfos = musicInfos ?? throw new ArgumentNullException(nameof(musicInfos));
            this.wordList = wordList ?? throw new ArgumentNullException(nameof(wordList));
            this.importedSongs = importedSongs ?? Array.Empty<NewSongData>();

            Text = "Event folders";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1120, 700);
            Size = new Size(1280, 820);

            BuildLayout();
            BuildSongIndex();
            RefreshFolders();
        }

        public bool Changed { get; private set; }

        private FolderEntry SelectedFolder => folders.SelectedItem as FolderEntry;

        private void BuildLayout()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 280,
                FixedPanel = FixedPanel.Panel1
            };
            Controls.Add(split);

            split.Panel1.Controls.Add(BuildFolderPanel());
            split.Panel2.Controls.Add(BuildEditorPanel());

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                ColumnCount = 2,
                Padding = new Padding(8, 6, 8, 6)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var note = new Label
            {
                Text = "Client metadata is staged for validated project export. Server export writes JSON, GZip and Brotli together.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            exportServerFiles.Text = "Export server files...";
            exportServerFiles.AutoSize = true;
            exportServerFiles.Click += (_, _) => ExportServerFiles();
            closeButton.Text = "Close";
            closeButton.AutoSize = true;
            closeButton.Click += (_, _) => Close();
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(exportServerFiles);
            footer.Controls.Add(note, 0, 0);
            footer.Controls.Add(buttons, 1, 0);
            Controls.Add(footer);
        }

        private Control BuildFolderPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));

            panel.Controls.Add(Heading("Event folders"), 0, 0);
            folders.Dock = DockStyle.Fill;
            folders.HorizontalScrollbar = true;
            folders.SelectedIndexChanged += (_, _) => RefreshSelectedFolder();
            panel.Controls.Add(folders, 0, 1);

            addFolder.Text = "Add folder";
            createClientDefinition.Text = "Create client";
            deleteFolder.Text = "Delete folder";
            addFolder.AutoSize = createClientDefinition.AutoSize = deleteFolder.AutoSize = true;
            addFolder.Click += (_, _) => AddFolder();
            createClientDefinition.Click += (_, _) => CreateClientDefinitionForSelected();
            deleteFolder.Click += (_, _) => DeleteSelectedFolder();
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            buttons.Controls.Add(addFolder);
            buttons.Controls.Add(createClientDefinition);
            buttons.Controls.Add(deleteFolder);
            panel.Controls.Add(buttons, 0, 2);

            validation.Dock = DockStyle.Fill;
            validation.AutoEllipsis = true;
            validation.TextAlign = ContentAlignment.TopLeft;
            panel.Controls.Add(validation, 0, 3);
            return panel;
        }

        private Control BuildEditorPanel()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 255));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildMetadataPanel(), 0, 0);
            root.Controls.Add(Heading("Folder contents"), 0, 1);
            root.Controls.Add(BuildContentsPanel(), 0, 2);
            return root;
        }

        private Control BuildMetadataPanel()
        {
            var group = new GroupBox { Text = "Folder metadata", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (var i = 0; i < 7; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            folderId.Enabled = false;
            AddField(table, "Folder ID", folderId, 0, 0);
            AddField(table, "Internal ID", internalId, 2, 0);
            AddField(table, "Japanese name", title, 0, 1);
            table.SetColumnSpan(title, 3);
            AddField(table, "Japanese intro", introduction, 0, 2);
            table.SetColumnSpan(introduction, 3);
            AddField(table, "Carousel order", order, 0, 3);
            AddField(table, "Voice tone ID", voiceToneId, 2, 3);
            AddField(table, "Server version", version, 0, 4);
            AddField(table, "Server priority", priority, 2, 4);

            serverReleased.Text = "Server released";
            normalMode.Text = "Normal";
            aiMode.Text = "AI Battle";
            collabo025Mode.Text = "Collabo 025";
            collabo026Mode.Text = "Collabo 026";
            aoharuMode.Text = "Aoharu";
            var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            flags.Controls.AddRange(new Control[] { serverReleased, normalMode, aiMode, collabo025Mode, collabo026Mode, aoharuMode });
            table.Controls.Add(new Label { Text = "Availability", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 5);
            table.Controls.Add(flags, 1, 5);
            table.SetColumnSpan(flags, 3);

            var source = new Label
            {
                Name = "SourceNote",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            table.Controls.Add(source, 0, 6);
            table.SetColumnSpan(source, 4);

            internalId.Leave += (_, _) => CommitInternalId();
            title.TextChanged += (_, _) => CommitWord(false);
            introduction.TextChanged += (_, _) => CommitWord(true);
            order.ValueChanged += (_, _) => CommitClientInt("order", order);
            voiceToneId.Leave += (_, _) => CommitVoiceTone();
            serverReleased.CheckedChanged += (_, _) => CommitClientBool("isServerReleasedFlag", serverReleased.Checked);
            normalMode.CheckedChanged += (_, _) => CommitClientBool("isEnableEnsoGameFlag", normalMode.Checked);
            aiMode.CheckedChanged += (_, _) => CommitClientBool("isEnableAIEnsoGameFlag", aiMode.Checked);
            collabo025Mode.CheckedChanged += (_, _) => CommitClientBool("isEnableAICollabo025EnsoGameFlag", collabo025Mode.Checked);
            collabo026Mode.CheckedChanged += (_, _) => CommitClientBool("isEnableAICollabo026EnsoGameFlag", collabo026Mode.Checked);
            aoharuMode.CheckedChanged += (_, _) => CommitClientBool("isEnableAoharuEnsoGameFlag", aoharuMode.Checked);
            version.ValueChanged += (_, _) => CommitServerInt("verupNo", version);
            priority.ValueChanged += (_, _) => CommitServerInt("priority", priority);

            group.Controls.Add(table);
            return group;
        }

        private Control BuildContentsPanel()
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            table.Controls.Add(new Label { Text = "Songs in folder", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            songSearch.PlaceholderText = "Search available songs";
            songSearch.Dock = DockStyle.Fill;
            songSearch.TextChanged += (_, _) => RefreshAvailableSongs();
            table.Controls.Add(songSearch, 2, 0);

            folderSongs.Dock = DockStyle.Fill;
            folderSongs.SelectionMode = SelectionMode.MultiExtended;
            folderSongs.HorizontalScrollbar = true;
            folderSongs.DoubleClick += (_, _) => RemoveSelectedSongs();
            table.Controls.Add(folderSongs, 0, 1);

            availableSongs.Dock = DockStyle.Fill;
            availableSongs.SelectionMode = SelectionMode.MultiExtended;
            availableSongs.HorizontalScrollbar = true;
            availableSongs.DoubleClick += (_, _) => AddSelectedSongs();
            availableSongs.SelectedIndexChanged += (_, _) => RefreshButtons();
            folderSongs.SelectedIndexChanged += (_, _) => RefreshButtons();
            table.Controls.Add(availableSongs, 2, 1);

            addSong.Text = "← Add";
            removeSong.Text = "Remove →";
            moveSongUp.Text = "Move up";
            moveSongDown.Text = "Move down";
            addSong.Click += (_, _) => AddSelectedSongs();
            removeSong.Click += (_, _) => RemoveSelectedSongs();
            moveSongUp.Click += (_, _) => MoveSelectedSong(-1);
            moveSongDown.Click += (_, _) => MoveSelectedSong(1);
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(5, 28, 5, 5)
            };
            foreach (var button in new[] { addSong, removeSong, moveSongUp, moveSongDown })
            {
                button.Width = 88;
                buttons.Controls.Add(button);
            }
            table.Controls.Add(buttons, 1, 1);
            return table;
        }

        private void BuildSongIndex()
        {
            var byUid = new Dictionary<int, SongChoice>();
            foreach (var info in musicInfos.Items.Where(item => item.UniqueId > 0))
            {
                byUid[info.UniqueId] = new SongChoice
                {
                    UniqueId = info.UniqueId,
                    Id = info.Id,
                    Title = wordList.GetBySong(info.Id)?.JapaneseText ?? string.Empty
                };
            }

            foreach (var song in importedSongs.Where(item => item.UniqueId > 0))
            {
                byUid[song.UniqueId] = new SongChoice
                {
                    UniqueId = song.UniqueId,
                    Id = song.Id,
                    Title = song.Word?.JapaneseText ?? string.Empty
                };
            }

            allSongs.Clear();
            allSongs.AddRange(byUid.Values.OrderBy(item => item.UniqueId).ThenBy(item => item.Id, StringComparer.Ordinal));
            RefreshAvailableSongs();
        }

        private void RefreshFolders(int? selectFolderId = null)
        {
            var previous = selectFolderId ?? SelectedFolder?.FolderId;
            var clientRows = project.HasGenreFolderInfo
                ? project.GenreFolderInfo.Items.OfType<JsonObject>().ToList()
                : new List<JsonObject>();
            var serverRows = serverData.Items.OfType<JsonObject>().ToList();
            var serverIds = new HashSet<int>(serverRows.Select(row => GetInt(row, "folderId") ?? -1));

            var entries = new Dictionary<int, FolderEntry>();
            foreach (var row in clientRows)
            {
                var id = GetInt(row, "uniqueId");
                if (!id.HasValue) continue;
                if (GetBool(row, "isServerReleasedFlag") != true && !serverIds.Contains(id.Value)) continue;
                entries[id.Value] = new FolderEntry { FolderId = id.Value, ClientRow = row };
            }

            foreach (var row in serverRows)
            {
                var id = GetInt(row, "folderId");
                if (!id.HasValue) continue;
                if (!entries.TryGetValue(id.Value, out var entry))
                    entries[id.Value] = entry = new FolderEntry { FolderId = id.Value };
                entry.ServerRow ??= row;
            }

            foreach (var entry in entries.Values)
            {
                entry.InternalId = GetString(entry.ClientRow, "id") ??
                    "event" + entry.FolderId.ToString(CultureInfo.InvariantCulture);
                entry.Title = GetWordText(ResolveWordKey(entry.InternalId, entry.FolderId, false));
                entry.Order = GetInt(entry.ClientRow, "order") ?? int.MaxValue;
            }

            refreshing = true;
            folders.BeginUpdate();
            try
            {
                folders.Items.Clear();
                folders.Items.AddRange(entries.Values
                    .OrderBy(item => item.Order)
                    .ThenBy(item => item.FolderId)
                    .Cast<object>().ToArray());
                if (previous.HasValue)
                    folders.SelectedItem = folders.Items.Cast<FolderEntry>()
                        .FirstOrDefault(item => item.FolderId == previous.Value);
                if (folders.SelectedIndex < 0 && folders.Items.Count > 0) folders.SelectedIndex = 0;
            }
            finally
            {
                folders.EndUpdate();
                refreshing = false;
            }

            RefreshValidation();
            RefreshSelectedFolder();
        }

        private void RefreshSelectedFolder()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var entry = SelectedFolder;
                var client = entry?.ClientRow;
                var server = entry?.ServerRow;
                var hasClient = client != null;
                var hasServer = server != null;

                folderId.Value = Clamp(folderId, entry?.FolderId ?? 0);
                internalId.Text = entry?.InternalId ?? string.Empty;
                title.Text = GetWordText(ResolveWordKey(internalId.Text, entry?.FolderId ?? 0, false));
                introduction.Text = GetWordText(ResolveWordKey(internalId.Text, entry?.FolderId ?? 0, true));
                order.Value = Clamp(order, GetInt(client, "order") ?? 0);
                voiceToneId.Text = GetString(client, "vo_toneId") ?? string.Empty;
                serverReleased.Checked = GetBool(client, "isServerReleasedFlag") == true;
                normalMode.Checked = GetBool(client, "isEnableEnsoGameFlag") == true;
                aiMode.Checked = GetBool(client, "isEnableAIEnsoGameFlag") == true;
                collabo025Mode.Checked = GetBool(client, "isEnableAICollabo025EnsoGameFlag") == true;
                collabo026Mode.Checked = GetBool(client, "isEnableAICollabo026EnsoGameFlag") == true;
                aoharuMode.Checked = GetBool(client, "isEnableAoharuEnsoGameFlag") == true;
                version.Value = Clamp(version, GetInt(server, "verupNo") ?? 1);
                priority.Value = Clamp(priority, GetInt(server, "priority") ?? 1);

                foreach (var control in new Control[]
                {
                    internalId, title, introduction, order, voiceToneId, serverReleased, normalMode,
                    aiMode, collabo025Mode, collabo026Mode, aoharuMode
                }) control.Enabled = entry != null && hasClient;
                version.Enabled = priority.Enabled = entry != null;

                var source = Controls.Find("SourceNote", true).OfType<Label>().FirstOrDefault();
                if (source != null)
                {
                    source.Text = entry == null ? "Select a folder."
                        : hasClient && hasServer ? "Client definition + server membership"
                        : hasClient ? "Client definition only; a server row will be created when contents are edited."
                        : "Server membership only; no matching client definition is loaded.";
                }

                RefreshFolderSongs();
            }
            finally
            {
                refreshing = false;
            }
            RefreshButtons();
        }

        private void RefreshAvailableSongs()
        {
            var query = songSearch.Text.Trim();
            var filtered = allSongs.Where(item => string.IsNullOrEmpty(query) ||
                item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.UniqueId.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase));
            availableSongs.BeginUpdate();
            try
            {
                availableSongs.Items.Clear();
                availableSongs.Items.AddRange(filtered.Cast<object>().ToArray());
            }
            finally
            {
                availableSongs.EndUpdate();
            }
        }

        private void RefreshFolderSongs()
        {
            folderSongs.BeginUpdate();
            try
            {
                folderSongs.Items.Clear();
                var songNumbers = SelectedFolder?.ServerRow?["songNo"] as JsonArray;
                if (songNumbers == null) return;
                foreach (var node in songNumbers)
                {
                    var uid = NodeInt(node) ?? 0;
                    var song = allSongs.FirstOrDefault(item => item.UniqueId == uid);
                    folderSongs.Items.Add(new MembershipItem(uid, song));
                }
            }
            finally
            {
                folderSongs.EndUpdate();
            }
        }

        private void AddFolder()
        {
            if (!project.HasGenreFolderInfo)
            {
                MessageBox.Show(this,
                    "genre_folderinfo.bin is not present in this project. Add it to the datatable folder and reopen the project before creating client event folders.",
                    "Client folder table missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var used = new HashSet<int>();
            foreach (var row in project.GenreFolderInfo.Items.OfType<JsonObject>())
                if (GetInt(row, "uniqueId") is int id) used.Add(id);
            foreach (var row in serverData.Items.OfType<JsonObject>())
                if (GetInt(row, "folderId") is int id) used.Add(id);
            var next = Enumerable.Range(1, 999).FirstOrDefault(id => !used.Contains(id));
            if (next == 0) throw new InvalidOperationException("No free event folder ID remains between 1 and 999.");

            var maxOrder = project.GenreFolderInfo.Items.OfType<JsonObject>()
                .Select(row => GetInt(row, "order") ?? 0).DefaultIfEmpty().Max();
            var internalName = "event" + next.ToString(CultureInfo.InvariantCulture);
            var client = CreateClientRow(next, internalName, maxOrder + 100);
            var server = new JsonObject
            {
                ["folderId"] = next,
                ["verupNo"] = 1,
                ["priority"] = 1,
                ["songNo"] = new JsonArray()
            };
            project.GenreFolderInfo.Items.Add(client);
            serverData.Items.Add(server);
            SetWordText(WordKey(internalName, false), "New event folder");
            SetWordText(WordKey(internalName, true), string.Empty);
            MarkChanged();
            RefreshFolders(next);
        }

        private JsonObject CreateClientRow(int folderId, string internalName, int folderOrder)
        {
            var template = project.GenreFolderInfo.Items.OfType<JsonObject>()
                .FirstOrDefault(row => GetBool(row, "isServerReleasedFlag") == true);
            var row = template?.DeepClone() as JsonObject ?? new JsonObject();
            Set(row, "uniqueId", folderId);
            Set(row, "id", internalName);
            Set(row, "order", folderOrder);

            var voice = GetNode(row, "vo_toneId");
            if (voice is JsonValue value && value.TryGetValue<string>(out _))
                Set(row, "vo_toneId", "focus_tokusyu_loop");
            else
                Set(row, "vo_toneId", 0);

            Set(row, "isServerReleasedFlag", true);
            if (template == null)
            {
                Set(row, "isEnableEnsoGameFlag", true);
                Set(row, "isEnableAIEnsoGameFlag", true);
                Set(row, "isEnableAICollabo025EnsoGameFlag", false);
                Set(row, "isEnableAICollabo026EnsoGameFlag", true);
                Set(row, "isEnableAoharuEnsoGameFlag", true);
            }
            return row;
        }

        private void CreateClientDefinitionForSelected()
        {
            var entry = SelectedFolder;
            if (entry == null || entry.ClientRow != null) return;
            if (!project.HasGenreFolderInfo)
            {
                MessageBox.Show(this, "genre_folderinfo.bin is not loaded.", "Client folder table missing",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var maxOrder = project.GenreFolderInfo.Items.OfType<JsonObject>()
                .Select(row => GetInt(row, "order") ?? 0).DefaultIfEmpty().Max();
            var internalName = "event" + entry.FolderId.ToString(CultureInfo.InvariantCulture);
            if (project.GenreFolderInfo.Items.OfType<JsonObject>()
                .Any(row => string.Equals(GetString(row, "id"), internalName, StringComparison.Ordinal)))
                internalName += "_new";

            var client = CreateClientRow(entry.FolderId, internalName, maxOrder + 100);
            project.GenreFolderInfo.Items.Add(client);
            EnsureWordText(WordKey(internalName, false), "New event folder");
            EnsureWordText(WordKey(internalName, true), string.Empty);
            MarkChanged();
            RefreshFolders(entry.FolderId);
        }

        private void DeleteSelectedFolder()
        {
            var entry = SelectedFolder;
            if (entry == null) return;
            if (MessageBox.Show(this,
                    $"Delete event folder {entry.FolderId} and its server membership?\n\nThis only changes the in-memory project until export.",
                    "Delete event folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            var internalName = GetString(entry.ClientRow, "id");
            if (project.HasGenreFolderInfo)
            {
                foreach (var row in project.GenreFolderInfo.Items.OfType<JsonObject>()
                    .Where(row => GetInt(row, "uniqueId") == entry.FolderId).ToList())
                    project.GenreFolderInfo.Items.Remove(row);
            }
            foreach (var row in serverData.Items.OfType<JsonObject>()
                .Where(row => GetInt(row, "folderId") == entry.FolderId).ToList())
                serverData.Items.Remove(row);
            if (!string.IsNullOrWhiteSpace(internalName))
            {
                RemoveWord(ResolveWordKey(internalName, entry.FolderId, false));
                RemoveWord(ResolveWordKey(internalName, entry.FolderId, true));
            }
            MarkChanged();
            RefreshFolders();
        }

        private void CommitInternalId()
        {
            if (refreshing || SelectedFolder?.ClientRow == null) return;
            var desired = internalId.Text.Trim();
            var old = GetString(SelectedFolder.ClientRow, "id") ?? string.Empty;
            if (string.Equals(desired, old, StringComparison.Ordinal)) return;
            if (string.IsNullOrWhiteSpace(desired))
            {
                MessageBox.Show(this, "The internal folder ID cannot be empty.");
                internalId.Text = old;
                return;
            }
            var duplicate = project.GenreFolderInfo.Items.OfType<JsonObject>().Any(row =>
                row != SelectedFolder.ClientRow && string.Equals(GetString(row, "id"), desired, StringComparison.Ordinal));
            if (duplicate)
            {
                MessageBox.Show(this, $"A folder named '{desired}' already exists.");
                internalId.Text = old;
                return;
            }
            var oldTitleKey = ResolveWordKey(old, SelectedFolder.FolderId, false);
            var oldIntroKey = ResolveWordKey(old, SelectedFolder.FolderId, true);
            var newTitleKey = IsNumericFolderWordKey(oldTitleKey, SelectedFolder.FolderId)
                ? oldTitleKey : WordKey(desired, false);
            var newIntroKey = IsNumericFolderWordKey(oldIntroKey, SelectedFolder.FolderId)
                ? oldIntroKey : WordKey(desired, true);
            if (wordList.Items.Any(word =>
                    (!string.Equals(word.Key, oldTitleKey, StringComparison.Ordinal) && string.Equals(word.Key, newTitleKey, StringComparison.Ordinal)) ||
                    (!string.Equals(word.Key, oldIntroKey, StringComparison.Ordinal) && string.Equals(word.Key, newIntroKey, StringComparison.Ordinal))))
            {
                MessageBox.Show(this, "The destination wordlist keys already exist.");
                internalId.Text = old;
                return;
            }

            RenameWord(oldTitleKey, newTitleKey);
            RenameWord(oldIntroKey, newIntroKey);
            Set(SelectedFolder.ClientRow, "id", desired);
            MarkChanged();
            RefreshFolders(SelectedFolder.FolderId);
        }

        private void CommitWord(bool intro)
        {
            if (refreshing || SelectedFolder?.ClientRow == null) return;
            var id = GetString(SelectedFolder.ClientRow, "id");
            if (string.IsNullOrWhiteSpace(id)) return;
            SetWordText(ResolveWordKey(id, SelectedFolder.FolderId, intro), intro ? introduction.Text : title.Text);
            MarkChanged();
            if (!intro) RefreshFolderCaption();
        }

        private void CommitClientInt(string property, NumericUpDown control)
        {
            if (refreshing || SelectedFolder?.ClientRow == null) return;
            Set(SelectedFolder.ClientRow, property, decimal.ToInt32(control.Value));
            MarkChanged();
            if (property == "order") RefreshFolders(SelectedFolder.FolderId);
        }

        private void CommitClientBool(string property, bool value)
        {
            if (refreshing || SelectedFolder?.ClientRow == null) return;
            Set(SelectedFolder.ClientRow, property, value);
            MarkChanged();
        }

        private void CommitVoiceTone()
        {
            if (refreshing || SelectedFolder?.ClientRow == null) return;
            var row = SelectedFolder.ClientRow;
            var existing = GetNode(row, "vo_toneId");
            if (existing is JsonValue value &&
                (value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _)) &&
                int.TryParse(voiceToneId.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                Set(row, "vo_toneId", number);
            else
                Set(row, "vo_toneId", voiceToneId.Text);
            MarkChanged();
        }

        private void CommitServerInt(string property, NumericUpDown control)
        {
            if (refreshing || SelectedFolder == null) return;
            var server = EnsureServerRow(SelectedFolder);
            Set(server, property, decimal.ToInt32(control.Value));
            MarkChanged();
        }

        private void AddSelectedSongs()
        {
            var entry = SelectedFolder;
            if (entry == null || availableSongs.SelectedItems.Count == 0) return;
            var server = EnsureServerRow(entry);
            var songs = EnsureSongArray(server);
            foreach (var song in availableSongs.SelectedItems.Cast<SongChoice>())
                songs.Add(song.UniqueId);
            MarkChanged();
            RefreshFolderSongs();
            RefreshValidation();
        }

        private void RemoveSelectedSongs()
        {
            var array = SelectedFolder?.ServerRow?["songNo"] as JsonArray;
            if (array == null || folderSongs.SelectedIndices.Count == 0) return;
            foreach (var index in folderSongs.SelectedIndices.Cast<int>().OrderByDescending(index => index))
                array.RemoveAt(index);
            MarkChanged();
            RefreshFolderSongs();
            RefreshValidation();
        }

        private void MoveSelectedSong(int offset)
        {
            var array = SelectedFolder?.ServerRow?["songNo"] as JsonArray;
            if (array == null || folderSongs.SelectedIndices.Count != 1) return;
            var index = folderSongs.SelectedIndex;
            var target = index + offset;
            if (target < 0 || target >= array.Count) return;
            var moving = array[index]?.DeepClone();
            array.RemoveAt(index);
            array.Insert(target, moving);
            MarkChanged();
            RefreshFolderSongs();
            folderSongs.SelectedIndex = target;
        }

        private JsonObject EnsureServerRow(FolderEntry entry)
        {
            if (entry.ServerRow != null) return entry.ServerRow;
            entry.ServerRow = new JsonObject
            {
                ["folderId"] = entry.FolderId,
                ["verupNo"] = 1,
                ["priority"] = 1,
                ["songNo"] = new JsonArray()
            };
            serverData.Items.Add(entry.ServerRow);
            version.Value = 1;
            priority.Value = 1;
            return entry.ServerRow;
        }

        private static JsonArray EnsureSongArray(JsonObject server)
        {
            if (server["songNo"] is JsonArray array) return array;
            array = new JsonArray();
            Set(server, "songNo", array);
            return array;
        }

        private void ExportServerFiles()
        {
            using var picker = new FolderBrowserDialog
            {
                Description = "Choose the folder that should receive event_folder_data.json, .json.gz and .json.br",
                UseDescriptionForTitle = true,
                SelectedPath = serverData.SourcePath == null ? string.Empty : Path.GetDirectoryName(serverData.SourcePath) ?? string.Empty
            };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            serverData.WriteAll(picker.SelectedPath);
            MessageBox.Show(this,
                "Written:\n\nevent_folder_data.json\nevent_folder_data.json.gz\nevent_folder_data.json.br",
                "Server files exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RefreshValidation()
        {
            var messages = new List<string>();
            if (!project.HasGenreFolderInfo)
                messages.Add("genre_folderinfo.bin is not loaded; server memberships can still be edited.");

            var duplicateFolders = serverData.Items.OfType<JsonObject>()
                .Select(row => GetInt(row, "folderId"))
                .Where(id => id.HasValue)
                .GroupBy(id => id.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateFolders.Count > 0)
                messages.Add("Duplicate server folder IDs: " + string.Join(", ", duplicateFolders));

            var knownUids = new HashSet<int>(allSongs.Select(song => song.UniqueId));
            var missingSongCount = serverData.Items.OfType<JsonObject>()
                .SelectMany(row => (row["songNo"] as JsonArray)?.Select(NodeInt) ?? Enumerable.Empty<int?>())
                .Count(uid => uid.HasValue && !knownUids.Contains(uid.Value));
            if (missingSongCount > 0)
                messages.Add($"{missingSongCount} song reference(s) are not present in this project build.");

            if (project.HasGenreFolderInfo)
            {
                var clientIds = new HashSet<int>(project.GenreFolderInfo.Items.OfType<JsonObject>()
                    .Select(row => GetInt(row, "uniqueId") ?? -1));
                var serverOnly = serverData.Items.OfType<JsonObject>()
                    .Select(row => GetInt(row, "folderId") ?? -1)
                    .Where(id => id >= 0 && !clientIds.Contains(id)).Distinct().OrderBy(id => id).ToList();
                if (serverOnly.Count > 0)
                    messages.Add("Unreleased/server-only folder rows: " + string.Join(", ", serverOnly));
            }

            validation.Text = messages.Count == 0 ? "Client/server links look valid." : string.Join(Environment.NewLine, messages);
        }

        private void RefreshButtons()
        {
            var selected = SelectedFolder != null;
            deleteFolder.Enabled = selected;
            createClientDefinition.Enabled = selected && SelectedFolder.ClientRow == null && project.HasGenreFolderInfo;
            addSong.Enabled = selected && availableSongs.SelectedItems.Count > 0;
            removeSong.Enabled = selected && folderSongs.SelectedItems.Count > 0;
            moveSongUp.Enabled = selected && folderSongs.SelectedIndices.Count == 1 && folderSongs.SelectedIndex > 0;
            moveSongDown.Enabled = selected && folderSongs.SelectedIndices.Count == 1 && folderSongs.SelectedIndex >= 0 && folderSongs.SelectedIndex < folderSongs.Items.Count - 1;
        }

        private void RefreshFolderCaption()
        {
            var entry = SelectedFolder;
            if (entry == null) return;
            entry.Title = title.Text;
            folders.Refresh();
        }

        private void MarkChanged()
        {
            if (refreshing) return;
            Changed = true;
            RefreshValidation();
        }

        private string GetWordText(string key) =>
            string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : wordList.Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal))?.JapaneseText ?? string.Empty;

        private void EnsureWordText(string key, string defaultText)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (wordList.Items.Any(item => string.Equals(item.Key, key, StringComparison.Ordinal))) return;
            SetWordText(key, defaultText);
        }

        private void SetWordText(string key, string text)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var word = wordList.Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
            if (word == null)
            {
                word = DatatableTypes.CreateWord(key);
                wordList.Items.Add(word);
            }
            word.JapaneseText = text ?? string.Empty;
        }

        private void RemoveWord(string key)
        {
            wordList.Items.RemoveAll(item => string.Equals(item.Key, key, StringComparison.Ordinal));
            foreach (var row in project.WordList.Items.OfType<JsonObject>()
                .Where(row => string.Equals(GetString(row, "key"), key, StringComparison.Ordinal)).ToList())
                project.WordList.Items.Remove(row);
        }

        private void RenameWord(string oldKey, string newKey)
        {
            if (string.IsNullOrWhiteSpace(oldKey) || string.IsNullOrWhiteSpace(newKey)) return;
            var word = wordList.Items.FirstOrDefault(item => string.Equals(item.Key, oldKey, StringComparison.Ordinal));
            if (word != null) word.Key = newKey;
            foreach (var row in project.WordList.Items.OfType<JsonObject>()
                .Where(row => string.Equals(GetString(row, "key"), oldKey, StringComparison.Ordinal)))
                Set(row, "key", newKey);
        }

        private string ResolveWordKey(string internalName, int numericFolderId, bool intro)
        {
            var byInternalId = WordKey(internalName, intro);
            if (!string.IsNullOrWhiteSpace(byInternalId) &&
                wordList.Items.Any(word => string.Equals(word.Key, byInternalId, StringComparison.Ordinal)))
                return byInternalId;

            var byNumericId = (intro ? "folder_intro_" : "folder_") +
                              numericFolderId.ToString(CultureInfo.InvariantCulture);
            if (wordList.Items.Any(word => string.Equals(word.Key, byNumericId, StringComparison.Ordinal)))
                return byNumericId;
            return byInternalId;
        }

        private static bool IsNumericFolderWordKey(string key, int folderId) =>
            !string.IsNullOrWhiteSpace(key) && key.EndsWith("_" + folderId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        private static string WordKey(string internalName, bool intro) =>
            string.IsNullOrWhiteSpace(internalName) ? null : (intro ? "folder_intro_" : "folder_") + internalName;

        private static NumericUpDown IntegerBox(int minimum, int maximum) => new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            DecimalPlaces = 0,
            Dock = DockStyle.Fill,
            ThousandsSeparator = false
        };

        private static decimal Clamp(NumericUpDown control, int value) =>
            Math.Min(control.Maximum, Math.Max(control.Minimum, value));

        private static Label Heading(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static void AddField(TableLayoutPanel table, string label, Control control, int column, int row)
        {
            table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, row);
            control.Dock = DockStyle.Fill;
            table.Controls.Add(control, column + 1, row);
        }

        private static JsonNode GetNode(JsonObject row, string propertyName)
        {
            if (row == null) return null;
            foreach (var property in row)
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            return null;
        }

        private static string GetString(JsonObject row, string propertyName)
        {
            var node = GetNode(row, propertyName);
            if (node == null) return null;
            if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text;
            return node.ToString();
        }

        private static int? GetInt(JsonObject row, string propertyName) => NodeInt(GetNode(row, propertyName));

        private static int? NodeInt(JsonNode node)
        {
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<int>(out var integer)) return integer;
            if (value.TryGetValue<long>(out var longInteger) && longInteger >= int.MinValue && longInteger <= int.MaxValue)
                return (int)longInteger;
            if (value.TryGetValue<string>(out var text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) return integer;
            return null;
        }

        private static bool? GetBool(JsonObject row, string propertyName)
        {
            var node = GetNode(row, propertyName);
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<bool>(out var boolean)) return boolean;
            if (value.TryGetValue<int>(out var integer)) return integer != 0;
            if (value.TryGetValue<string>(out var text))
            {
                if (bool.TryParse(text, out boolean)) return boolean;
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) return integer != 0;
            }
            return null;
        }

        private static void Set(JsonObject row, string propertyName, JsonNode value)
        {
            var actual = row.Select(property => property.Key)
                .FirstOrDefault(name => string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase)) ?? propertyName;
            row[actual] = value;
        }

        private static void Set(JsonObject row, string propertyName, string value) => Set(row, propertyName, JsonValue.Create(value));
        private static void Set(JsonObject row, string propertyName, int value) => Set(row, propertyName, JsonValue.Create(value));
        private static void Set(JsonObject row, string propertyName, bool value) => Set(row, propertyName, JsonValue.Create(value));

        private sealed class FolderEntry
        {
            public int FolderId { get; init; }
            public JsonObject ClientRow { get; set; }
            public JsonObject ServerRow { get; set; }
            public string InternalId { get; set; }
            public string Title { get; set; }
            public int Order { get; set; }

            public override string ToString()
            {
                var label = string.IsNullOrWhiteSpace(Title) ? InternalId : Title;
                if (string.IsNullOrWhiteSpace(label)) label = "Unnamed folder";
                var source = ClientRow == null ? " [server only]" : ServerRow == null ? " [client only]" : string.Empty;
                return $"{FolderId}. {label}{source}";
            }
        }

        private sealed class SongChoice
        {
            public int UniqueId { get; init; }
            public string Id { get; init; }
            public string Title { get; init; }

            public override string ToString() =>
                string.IsNullOrWhiteSpace(Title) ? $"{UniqueId}. {Id}" : $"{UniqueId}. {Id}  {Title}";
        }

        private sealed class MembershipItem
        {
            private readonly SongChoice song;

            public MembershipItem(int uniqueId, SongChoice song)
            {
                UniqueId = uniqueId;
                this.song = song;
            }

            public int UniqueId { get; }

            public override string ToString() => song == null
                ? $"{UniqueId}. [missing from musicinfo]"
                : song.ToString();
        }
    }
}
