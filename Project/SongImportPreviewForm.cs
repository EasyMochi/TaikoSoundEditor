using System;
using System.Drawing;
using System.Windows.Forms;
using TaikoSoundEditor.Data;

namespace TaikoSoundEditor.Project
{
    internal sealed class SongImportPreviewForm : Form
    {
        private readonly SongImportPlan plan;
        private readonly TextBox previewBox;
        private readonly Label statusLabel;
        private readonly Button importButton;
        private readonly Button cancelButton;

        public SongImportPreviewForm(SongImportPlan plan)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));

            Text = "TJA Import Preview";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 620);
            Size = new Size(860, 760);

            previewBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = plan.BuildPreview()
            };

            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(8),
                Text = plan.CanImport
                    ? "Review the complete import plan before running conversion."
                    : "Import is blocked. Resolve the listed collisions or invalid inputs."
            };

            importButton = new Button
            {
                Text = "Convert and stage song",
                AutoSize = true,
                Enabled = plan.CanImport
            };
            importButton.Click += ImportButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(importButton);

            Controls.Add(previewBox);
            Controls.Add(statusLabel);
            Controls.Add(buttons);

            AcceptButton = importButton;
            CancelButton = cancelButton;
        }

        public NewSongData ImportedSong { get; private set; }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            importButton.Enabled = false;
            cancelButton.Enabled = false;
            UseWaitCursor = true;
            try
            {
                ImportedSong = plan.Convert(message =>
                {
                    statusLabel.Text = message;
                    statusLabel.Refresh();
                    Application.DoEvents();
                });
                statusLabel.Text = "Conversion completed. The complete song is ready to be staged.";
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ImportedSong = null;
                statusLabel.Text = "Conversion failed. The project was not changed.";
                MessageBox.Show(this,
                    "Import conversion failed before any project changes were made.\n\n" + ex.Message,
                    "TJA import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                importButton.Enabled = plan.CanImport;
                cancelButton.Enabled = true;
            }
            finally
            {
                UseWaitCursor = false;
            }
        }
    }
}
