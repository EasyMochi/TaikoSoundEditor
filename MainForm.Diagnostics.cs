using System;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem diagnosticsToolStripMenuItem;

        private void MainForm_RuntimeLoad(object sender, EventArgs e)
        {
            if (IsInDesignMode) return;

            InitializeProjectDiagnosticsMenu();
            RefreshProjectDiagnosticsState();
        }

        private void InitializeProjectDiagnosticsMenu()
        {
            if (diagnosticsToolStripMenuItem != null) return;

            diagnosticsToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "diagnosticsToolStripMenuItem",
                Text = "Diagnostics",
                Enabled = false
            };
            diagnosticsToolStripMenuItem.Click += DiagnosticsToolStripMenuItem_Click;
            menuStrip1.Items.Add(diagnosticsToolStripMenuItem);
        }

        private void RefreshProjectDiagnosticsState()
        {
            if (diagnosticsToolStripMenuItem != null)
                diagnosticsToolStripMenuItem.Enabled = CurrentProject != null;
        }

        private void DiagnosticsToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null)
            {
                MessageBox.Show("No data project is loaded.");
                return;
            }

            // Diagnostics should describe current unsaved edits, not only the original project snapshot.
            MergeEditableDatatables();
            using (var form = new DiagnosticsForm(CurrentProject.BuildIndex().Diagnostics))
                form.ShowDialog(this);
        });
    }
}
