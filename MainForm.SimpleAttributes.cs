using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private GroupBox simpleAttributeGroup;
        private CheckBox simpleNewFlagBox;
        private ComboBox simpleLumenPresetBox;
        private bool simpleAttributeControlsLoading;

        private sealed class LumenPresetChoice
        {
            public string Name { get; init; }
            public string[] Values { get; init; }
            public override string ToString() => Name;
        }

        private void InitializeSimpleAttributeControls()
        {
            if (simpleAttributeGroup != null) return;

            simpleAttributeGroup = new GroupBox
            {
                Text = "Flags and collaboration visuals",
                Location = new Point(6, 136),
                Size = new Size(Math.Max(320, SoundViewerSimple.ClientSize.Width - 12), 72),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            simpleNewFlagBox = new CheckBox
            {
                Text = "NEW flag",
                AutoSize = true,
                Location = new Point(12, 31),
                Enabled = false
            };
            simpleNewFlagBox.CheckedChanged += SimpleNewFlagBox_CheckedChanged;

            var lumenLabel = new Label
            {
                Text = "Lumen preset",
                AutoSize = true,
                Location = new Point(125, 33)
            };

            simpleLumenPresetBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(210, 28),
                Size = new Size(Math.Max(180, simpleAttributeGroup.ClientSize.Width - 224), 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = false
            };
            simpleLumenPresetBox.SelectedIndexChanged += SimpleLumenPresetBox_SelectedIndexChanged;

            simpleAttributeGroup.Controls.Add(simpleNewFlagBox);
            simpleAttributeGroup.Controls.Add(lumenLabel);
            simpleAttributeGroup.Controls.Add(simpleLumenPresetBox);
            SoundViewerSimple.Controls.Add(simpleAttributeGroup);
            simpleAttributeGroup.BringToFront();
        }

        private void LoadSimpleAttributeControls(IMusicAttribute attribute)
        {
            if (simpleAttributeGroup == null) return;

            simpleAttributeControlsLoading = true;
            try
            {
                simpleNewFlagBox.Enabled = attribute != null;
                simpleLumenPresetBox.Enabled = attribute != null;
                simpleNewFlagBox.Checked = attribute?.New == true;

                simpleLumenPresetBox.BeginUpdate();
                try
                {
                    simpleLumenPresetBox.Items.Clear();
                    if (attribute == null) return;

                    var currentValues = ReadLumenValues(attribute);
                    var presets = BuildLumenPresets(currentValues);
                    simpleLumenPresetBox.Items.AddRange(presets.Cast<object>().ToArray());
                    simpleLumenPresetBox.SelectedItem = presets.FirstOrDefault(preset =>
                        LumenValuesEqual(preset.Values, currentValues));
                }
                finally
                {
                    simpleLumenPresetBox.EndUpdate();
                }
            }
            finally
            {
                simpleAttributeControlsLoading = false;
            }
        }

        private List<LumenPresetChoice> BuildLumenPresets(string[] currentValues)
        {
            var result = new List<LumenPresetChoice>();
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            void Add(string baseName, string[] values)
            {
                values = NormalizeLumenValues(values);
                if (!signatures.Add(LumenSignature(values))) return;

                nameCounts.TryGetValue(baseName, out var count);
                count++;
                nameCounts[baseName] = count;
                var name = count == 1 ? baseName : $"{baseName} (variant {count})";
                result.Add(new LumenPresetChoice { Name = name, Values = values });
            }

            Add("None / normal visuals", new string[16]);
            Add("Hatsune Miku", BuiltInMikuLumenValues());
            Add("Touhou Project", BuiltInTouhouLumenValues());

            if (MusicAttributes?.Items != null)
            {
                foreach (var attribute in MusicAttributes.Items.Where(item => item != null)
                             .OrderBy(item => GetLumenFriendlyName(ReadLumenValues(item)), StringComparer.OrdinalIgnoreCase)
                             .ThenBy(item => item.UniqueId))
                {
                    var values = ReadLumenValues(attribute);
                    if (values.All(string.IsNullOrEmpty)) continue;
                    Add(GetLumenFriendlyName(values), values);
                }
            }

            if (!signatures.Contains(LumenSignature(currentValues)))
                Add("Custom / mixed", currentValues);

            return result;
        }

        private void SimpleNewFlagBox_CheckedChanged(object sender, EventArgs e)
        {
            if (simpleAttributeControlsLoading) return;
            var attribute = GetSelectedMusicAttribute();
            if (attribute == null) return;

            attribute.New = simpleNewFlagBox.Checked;
            MusicAttributesGrid.Refresh();
            MarkCurrentUnifiedSongEdited();
        }

        private void SimpleLumenPresetBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (simpleAttributeControlsLoading || simpleLumenPresetBox.SelectedItem is not LumenPresetChoice preset)
                return;

            var attribute = GetSelectedMusicAttribute();
            if (attribute == null) return;

            WriteLumenValues(attribute, preset.Values);
            MusicAttributesGrid.Refresh();
            MarkCurrentUnifiedSongEdited();
        }

        private IMusicAttribute GetSelectedMusicAttribute()
        {
            if (LoadedMusicBox.SelectedItem is IMusicInfo info)
                return MusicAttributes?.GetByUniqueId(info.UniqueId);
            if (NewSoundsBox.SelectedItem is NewSongData song)
                return song.MusicAttribute;
            return null;
        }

        private static string[] ReadLumenValues(IMusicAttribute attribute) => NormalizeLumenValues(new[]
        {
            attribute?.DonBg1p, attribute?.DonBg2p, attribute?.DancerDai, attribute?.Dancer,
            attribute?.DanceNormalBg, attribute?.DanceFeverBg, attribute?.RendaEffect, attribute?.Fever,
            attribute?.DonBg1p1, attribute?.DonBg2p1, attribute?.DancerDai1, attribute?.Dancer1,
            attribute?.DanceNormalBg1, attribute?.DanceFeverBg1, attribute?.RendaEffect1, attribute?.Fever1
        });

        private static void WriteLumenValues(IMusicAttribute attribute, string[] values)
        {
            values = NormalizeLumenValues(values);
            attribute.DonBg1p = values[0];
            attribute.DonBg2p = values[1];
            attribute.DancerDai = values[2];
            attribute.Dancer = values[3];
            attribute.DanceNormalBg = values[4];
            attribute.DanceFeverBg = values[5];
            attribute.RendaEffect = values[6];
            attribute.Fever = values[7];
            attribute.DonBg1p1 = values[8];
            attribute.DonBg2p1 = values[9];
            attribute.DancerDai1 = values[10];
            attribute.Dancer1 = values[11];
            attribute.DanceNormalBg1 = values[12];
            attribute.DanceFeverBg1 = values[13];
            attribute.RendaEffect1 = values[14];
            attribute.Fever1 = values[15];
        }

        private static string[] NormalizeLumenValues(IEnumerable<string> values)
        {
            var normalized = (values ?? Enumerable.Empty<string>()).Take(16)
                .Select(value => value ?? string.Empty).ToList();
            while (normalized.Count < 16) normalized.Add(string.Empty);
            return normalized.ToArray();
        }

        private static bool LumenValuesEqual(string[] left, string[] right) =>
            NormalizeLumenValues(left).SequenceEqual(NormalizeLumenValues(right), StringComparer.Ordinal);

        private static string LumenSignature(string[] values) =>
            string.Join("\u001f", NormalizeLumenValues(values));

        private static string GetLumenFriendlyName(string[] values)
        {
            var path = NormalizeLumenValues(values).FirstOrDefault(value =>
                value.Contains("lumen/", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(path)) return "Custom collaboration";

            var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var lumenIndex = Array.FindIndex(parts, part => string.Equals(part, "lumen", StringComparison.OrdinalIgnoreCase));
            var root = lumenIndex >= 0 && lumenIndex + 1 < parts.Length ? parts[lumenIndex + 1] : string.Empty;
            return root.ToLowerInvariant() switch
            {
                "000_default" => "Default Taiko",
                "001_miku" => "Hatsune Miku",
                "002_toho" => "Touhou Project",
                "003_gumi" => "GUMI",
                "004_ia" => "IA",
                "005_lovelive" => "Love Live!",
                "006_i7_id7" => "IDOLiSH7",
                "010_imas" => "THE IDOLM@STER",
                "011_imas_cg" => "Cinderella Girls",
                "012_imas_ml" => "Million Live!",
                "013_imas_sidem" => "SideM",
                "014_yokai" => "Yo-kai Watch",
                "015_yokai_mb" => "Yo-kai Watch MB",
                "016_yokai_ht" => "Yo-kai Watch HT",
                "019_mario" => "Mario",
                "020_a3" => "A3!",
                _ => string.IsNullOrEmpty(root) ? "Custom collaboration" : root
            };
        }

        private static string[] BuiltInMikuLumenValues()
        {
            const string root = "lumen/001_miku/enso_normal/enso/";
            var values = new[]
            {
                root + "donbg/donbg_b_001_1p.nulstb",
                root + "donbg/donbg_b_001_2p.nulstb",
                root + "background/dodai_b_01.nulstb",
                root + "dancer/dance_b_001.nulstb",
                root + "background/bg_nomal_b_001.nulstb",
                root + "background/bg_fever_b_001.nulstb",
                root + "renda_effect/renda_b_001.nulstb",
                root + "fever/fever_b_001.nulstb"
            };
            return values.Concat(values).ToArray();
        }

        private static string[] BuiltInTouhouLumenValues()
        {
            const string root = "lumen/002_toho/enso_normal/enso/";
            var values = new[]
            {
                root + "donbg/donbg_b_002_1p.nulstb",
                root + "donbg/donbg_b_002_2p.nulstb",
                "lumen/000_default/enso_normal/enso/background/bg_dai_a_00.nulstb",
                root + "dancer/dance_b_002.nulstb",
                root + "background/bg_nomal_b_002.nulstb",
                root + "background/bg_fever_b_002.nulstb",
                root + "renda_effect/renda_b_002.nulstb",
                root + "fever/fever_b_002.nulstb"
            };
            return values.Concat(values).ToArray();
        }
    }
}
