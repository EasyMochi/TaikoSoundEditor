using System;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem diagnosticsToolStripMenuItem;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
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

            using (var form = new DiagnosticsForm(CurrentProject.BuildIndex().Diagnostics))
                form.ShowDialog(this);
        });
    }
}
