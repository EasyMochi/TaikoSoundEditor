using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using TaikoSoundEditor.Data;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private enum WordRowKind
        {
            Title,
            Subtitle,
            Detail
        }

        private sealed class WordLanguageDescriptor
        {
            public string DisplayName { get; init; }
            public string TextProperty { get; init; }
            public string FontProperty { get; init; }

            public bool IsJapanese =>
                TextProperty.StartsWith("japanese", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class WordGridBinding
        {
            public WordLanguageDescriptor Language { get; init; }
            public WordRowKind Kind { get; init; }
        }

        private GroupBox multilingualWordGroup;
        private DataGridView multilingualWordGrid;
        private bool multilingualWordGridLoading;
        private string multilingualSongId;
        private List<WordLanguageDescriptor> multilingualLanguages;

        private void InitializeMultilingualWordEditor()
        {
            if (multilingualWordGrid != null) return;

            multilingualWordGroup = new GroupBox
            {
                Text = "All languages",
                Location = new Point(6, 214),
                Size = new Size(
                    Math.Max(320, SoundViewerSimple.ClientSize.Width - 12),
                    Math.Max(150, SoundViewerSimple.ClientSize.Height - 220)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            multilingualWordGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                EditMode = DataGridViewEditMode.EditOnEnter,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };

            multilingualWordGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Language",
                Name = "LanguageColumn",
                ReadOnly = true,
                Width = 125,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            multilingualWordGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Field",
                Name = "FieldColumn",
                ReadOnly = true,
                Width = 78,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            multilingualWordGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Text",
                Name = "TextColumn",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            multilingualWordGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Font type",
                Name = "FontColumn",
                Width = 78,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            multilingualWordGrid.CellValueChanged += MultilingualWordGrid_CellValueChanged;
            multilingualWordGrid.CellValidating += MultilingualWordGrid_CellValidating;
            multilingualWordGrid.DataError += (_, e) => e.ThrowException = false;

            multilingualWordGroup.Controls.Add(multilingualWordGrid);
            SoundViewerSimple.Controls.Add(multilingualWordGroup);
            multilingualWordGroup.BringToFront();
        }

        private void ResetMultilingualWordSchema()
        {
            multilingualLanguages = null;
            ClearMultilingualWordEditor();
        }

        private void ClearMultilingualWordEditor()
        {
            multilingualSongId = null;
            if (multilingualWordGrid == null) return;

            multilingualWordGridLoading = true;
            try
            {
                multilingualWordGrid.Rows.Clear();
                multilingualWordGroup.Text = "All languages";
            }
            finally
            {
                multilingualWordGridLoading = false;
            }
        }

        private void LoadMultilingualWordEditor(string songId)
        {
            if (multilingualWordGrid == null || CurrentProject == null || string.IsNullOrWhiteSpace(songId))
            {
                ClearMultilingualWordEditor();
                return;
            }

            multilingualSongId = songId;
            multilingualLanguages ??= DiscoverWordLanguages();

            multilingualWordGridLoading = true;
            try
            {
                multilingualWordGrid.Rows.Clear();
                multilingualWordGroup.Text = $"All languages · {songId}";

                foreach (var language in multilingualLanguages)
                {
                    AddWordGridRow(language, WordRowKind.Title);
                    AddWordGridRow(language, WordRowKind.Subtitle);
                    AddWordGridRow(language, WordRowKind.Detail);
                }
            }
            finally
            {
                multilingualWordGridLoading = false;
            }
        }

        private void AddWordGridRow(WordLanguageDescriptor language, WordRowKind kind)
        {
            var rawRow = GetRawWordRow(multilingualSongId, kind, false);
            var text = GetJsonString(rawRow, language.TextProperty);
            var fontType = GetJsonInt(rawRow, language.FontProperty);

            if (language.IsJapanese)
            {
                var typedWord = GetTypedWord(multilingualSongId, kind);
                text ??= typedWord?.JapaneseText;
                fontType ??= typedWord?.JapaneseFontType;
            }

            var rowIndex = multilingualWordGrid.Rows.Add(
                language.DisplayName,
                GetWordKindLabel(kind),
                text ?? string.Empty,
                fontType?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            multilingualWordGrid.Rows[rowIndex].Tag = new WordGridBinding
            {
                Language = language,
                Kind = kind
            };
        }

        private void MultilingualWordGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (multilingualWordGridLoading || e.RowIndex < 0 || e.ColumnIndex != 3) return;

            var value = e.FormattedValue?.ToString();
            if (string.IsNullOrWhiteSpace(value) || int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out _))
            {
                multilingualWordGrid.Rows[e.RowIndex].ErrorText = string.Empty;
                return;
            }

            multilingualWordGrid.Rows[e.RowIndex].ErrorText = "Font type must be an integer.";
            e.Cancel = true;
        }

        private void MultilingualWordGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (multilingualWordGridLoading || e.RowIndex < 0 || e.ColumnIndex < 2 ||
                string.IsNullOrWhiteSpace(multilingualSongId))
                return;

            if (multilingualWordGrid.Rows[e.RowIndex].Tag is not WordGridBinding binding)
                return;

            var rawRow = GetRawWordRow(multilingualSongId, binding.Kind, true);
            if (rawRow == null) return;

            if (e.ColumnIndex == 2)
            {
                SetJsonValue(rawRow, binding.Language.TextProperty,
                    multilingualWordGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty);
            }
            else if (e.ColumnIndex == 3)
            {
                var value = multilingualWordGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                if (string.IsNullOrWhiteSpace(value))
                    SetJsonValue(rawRow, binding.Language.FontProperty, 0);
                else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fontType))
                    SetJsonValue(rawRow, binding.Language.FontProperty, fontType);
                else
                    return;
            }

            if (binding.Language.IsJapanese)
                SyncTypedJapaneseWord(binding.Kind, rawRow, binding.Language);

            MarkMultilingualWordEdited(multilingualSongId);
        }

        private void ApplyJapaneseWordEditFromSimpleControl(string songId, object sender)
        {
            if (ReferenceEquals(sender, SimpleTitleBox))
                ApplyJapaneseWordEdit(songId, WordRowKind.Title, SimpleTitleBox.Text);
            else if (ReferenceEquals(sender, SimpleSubtitleBox))
                ApplyJapaneseWordEdit(songId, WordRowKind.Subtitle, SimpleSubtitleBox.Text);
            else if (ReferenceEquals(sender, SimpleDetailBox))
                ApplyJapaneseWordEdit(songId, WordRowKind.Detail, SimpleDetailBox.Text);
            else
                return;

            SyncJapaneseGridFromSimpleEditor();
        }

        private void ApplyJapaneseWordEdit(string songId, WordRowKind kind, string text)
        {
            var typedWord = GetOrCreateTypedWord(songId, kind);
            if (typedWord != null) typedWord.JapaneseText = text ?? string.Empty;

            if (CurrentProject == null) return;
            var rawRow = GetRawWordRow(songId, kind, true);
            if (rawRow == null) return;

            var japanese = GetJapaneseLanguageDescriptor();
            SetJsonValue(rawRow, japanese.TextProperty, text ?? string.Empty);
            if (typedWord != null)
                SetJsonValue(rawRow, japanese.FontProperty, typedWord.JapaneseFontType);
        }

        private void SyncJapaneseGridFromSimpleEditor()
        {
            if (multilingualWordGrid == null ||
                !string.Equals(multilingualSongId, SimpleIdBox.Text, StringComparison.Ordinal))
                return;

            multilingualWordGridLoading = true;
            try
            {
                foreach (DataGridViewRow row in multilingualWordGrid.Rows)
                {
                    if (row.Tag is not WordGridBinding binding || !binding.Language.IsJapanese)
                        continue;

                    row.Cells[2].Value = binding.Kind switch
                    {
                        WordRowKind.Title => SimpleTitleBox.Text,
                        WordRowKind.Subtitle => SimpleSubtitleBox.Text,
                        WordRowKind.Detail => SimpleDetailBox.Text,
                        _ => string.Empty
                    };
                }
            }
            finally
            {
                multilingualWordGridLoading = false;
            }
        }

        private void SyncTypedJapaneseWord(WordRowKind kind, JsonObject rawRow,
            WordLanguageDescriptor language)
        {
            var typedWord = GetOrCreateTypedWord(multilingualSongId, kind);
            if (typedWord == null) return;

            typedWord.JapaneseText = GetJsonString(rawRow, language.TextProperty) ?? string.Empty;
            typedWord.JapaneseFontType = GetJsonInt(rawRow, language.FontProperty) ?? 0;

            simpleBoxLoading = true;
            try
            {
                switch (kind)
                {
                    case WordRowKind.Title:
                        SimpleTitleBox.Text = typedWord.JapaneseText;
                        WordsGrid.SelectedObject = typedWord;
                        WordsGrid.Refresh();
                        break;
                    case WordRowKind.Subtitle:
                        SimpleSubtitleBox.Text = typedWord.JapaneseText;
                        WordSubGrid.SelectedObject = typedWord;
                        WordSubGrid.Refresh();
                        break;
                    case WordRowKind.Detail:
                        SimpleDetailBox.Text = typedWord.JapaneseText;
                        WordDetailGrid.SelectedObject = typedWord;
                        WordDetailGrid.Refresh();
                        break;
                }
            }
            finally
            {
                simpleBoxLoading = false;
            }
        }

        private void MarkMultilingualWordEdited(string songId)
        {
            unifiedExportIsCurrent = false;
            if (LoadedMusicBox.SelectedItem is IMusicInfo info &&
                string.Equals(info.Id, songId, StringComparison.Ordinal))
                unifiedEditedSongIds.Add(songId);

            RefreshUnifiedSongList();
            UpdateUnifiedWorkspaceState();
        }

        private List<WordLanguageDescriptor> DiscoverWordLanguages()
        {
            var propertyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (CurrentProject != null)
            {
                foreach (var row in CurrentProject.WordList.Items.OfType<JsonObject>())
                {
                    foreach (var property in row)
                    {
                        if (!propertyNames.ContainsKey(property.Key))
                            propertyNames.Add(property.Key, property.Key);
                    }
                }
            }

            var prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var propertyName in propertyNames.Values)
            {
                if (propertyName.EndsWith("Text", StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = propertyName[..^4];
                    if (!prefixes.ContainsKey(prefix)) prefixes.Add(prefix, prefix);
                }
                else if (propertyName.EndsWith("FontType", StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = propertyName[..^8];
                    if (!prefixes.ContainsKey(prefix)) prefixes.Add(prefix, prefix);
                }
            }

            if (!prefixes.ContainsKey("japanese"))
                prefixes.Add("japanese", "japanese");

            return prefixes.Values
                .Select(prefix => new WordLanguageDescriptor
                {
                    DisplayName = GetLanguageDisplayName(prefix),
                    TextProperty = FindPropertyName(propertyNames, prefix + "Text"),
                    FontProperty = FindPropertyName(propertyNames, prefix + "FontType")
                })
                .OrderBy(language => language.IsJapanese ? 0 : 1)
                .ThenBy(language => language.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private WordLanguageDescriptor GetJapaneseLanguageDescriptor()
        {
            multilingualLanguages ??= DiscoverWordLanguages();
            return multilingualLanguages.First(language => language.IsJapanese);
        }

        private static string FindPropertyName(Dictionary<string, string> properties, string desiredName) =>
            properties.TryGetValue(desiredName, out var exactName) ? exactName : desiredName;

        private static string GetLanguageDisplayName(string prefix)
        {
            var normalized = prefix.Replace("_", string.Empty).Replace("-", string.Empty);
            if (normalized.Equals("japanese", StringComparison.OrdinalIgnoreCase)) return "Japanese";
            if (normalized.Equals("englishus", StringComparison.OrdinalIgnoreCase)) return "English (US)";
            if (normalized.Equals("englishuk", StringComparison.OrdinalIgnoreCase)) return "English (UK)";
            if (normalized.Equals("chineset", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("traditionalchinese", StringComparison.OrdinalIgnoreCase))
                return "Chinese (Traditional)";
            if (normalized.Equals("chineses", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("simplifiedchinese", StringComparison.OrdinalIgnoreCase))
                return "Chinese (Simplified)";
            if (normalized.Equals("portuguesebr", StringComparison.OrdinalIgnoreCase)) return "Portuguese (Brazil)";

            var builder = new StringBuilder();
            for (var i = 0; i < prefix.Length; i++)
            {
                var character = prefix[i];
                if (i > 0 && char.IsUpper(character) && !char.IsUpper(prefix[i - 1]))
                    builder.Append(' ');
                builder.Append(character);
            }

            var result = builder.ToString().Replace('_', ' ').Replace('-', ' ').Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(result.ToLowerInvariant());
        }

        private JsonObject GetRawWordRow(string songId, WordRowKind kind, bool create)
        {
            if (CurrentProject == null || string.IsNullOrWhiteSpace(songId)) return null;

            var key = GetWordKey(songId, kind);
            var row = CurrentProject.WordList.Items.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetJsonString(item, "key"), key,
                    StringComparison.Ordinal));
            if (row != null || !create) return row;

            row = new JsonObject { ["key"] = key };
            // A raw-only row would be discarded by MergeKnownItems because exports are
            // driven by the typed word collection. Creating both halves keeps the edit lossless.
            var typedWord = GetOrCreateTypedWord(songId, kind);
            if (typedWord != null)
            {
                var japanese = GetJapaneseLanguageDescriptor();
                row[japanese.TextProperty] = typedWord.JapaneseText ?? string.Empty;
                row[japanese.FontProperty] = typedWord.JapaneseFontType;
            }

            CurrentProject.WordList.Items.Add(row);
            return row;
        }

        private IWord GetTypedWord(string songId, WordRowKind kind)
        {
            if (WordList == null) return null;
            return kind switch
            {
                WordRowKind.Title => WordList.GetBySong(songId),
                WordRowKind.Subtitle => WordList.GetBySongSub(songId),
                WordRowKind.Detail => WordList.GetBySongDetail(songId),
                _ => null
            };
        }

        private IWord GetOrCreateTypedWord(string songId, WordRowKind kind)
        {
            var word = GetTypedWord(songId, kind);
            if (word != null || WordList == null || DatatableTypes.Word == null) return word;

            word = DatatableTypes.CreateWord(GetWordKey(songId, kind));
            WordList.Items.Add(word);
            return word;
        }

        private static string GetWordKey(string songId, WordRowKind kind) => kind switch
        {
            WordRowKind.Title => "song_" + songId,
            WordRowKind.Subtitle => "song_sub_" + songId,
            WordRowKind.Detail => "song_detail_" + songId,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        private static string GetWordKindLabel(WordRowKind kind) => kind switch
        {
            WordRowKind.Title => "Title",
            WordRowKind.Subtitle => "Subtitle",
            WordRowKind.Detail => "Detail",
            _ => kind.ToString()
        };

        private static string GetJsonString(JsonObject row, string propertyName)
        {
            var node = GetJsonNode(row, propertyName);
            if (node == null) return null;
            if (node is JsonValue value && value.TryGetValue<string>(out var text)) return text;
            return node.ToString();
        }

        private static int? GetJsonInt(JsonObject row, string propertyName)
        {
            var node = GetJsonNode(row, propertyName);
            if (node is not JsonValue value) return null;
            if (value.TryGetValue<int>(out var integer)) return integer;
            if (value.TryGetValue<long>(out var longInteger) &&
                longInteger >= int.MinValue && longInteger <= int.MaxValue)
                return (int)longInteger;
            if (value.TryGetValue<string>(out var text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                return integer;
            return null;
        }

        private static JsonNode GetJsonNode(JsonObject row, string propertyName)
        {
            if (row == null || string.IsNullOrEmpty(propertyName)) return null;
            foreach (var property in row)
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            return null;
        }

        private static void SetJsonValue(JsonObject row, string propertyName, string value)
        {
            var actualName = FindJsonPropertyName(row, propertyName);
            row[actualName] = value ?? string.Empty;
        }

        private static void SetJsonValue(JsonObject row, string propertyName, int value)
        {
            var actualName = FindJsonPropertyName(row, propertyName);
            row[actualName] = value;
        }

        private static string FindJsonPropertyName(JsonObject row, string desiredName)
        {
            foreach (var property in row)
                if (string.Equals(property.Key, desiredName, StringComparison.OrdinalIgnoreCase))
                    return property.Key;
            return desiredName;
        }
    }
}
