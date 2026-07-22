using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TaikoSoundEditor.Project
{
    internal sealed class DiagnosticsForm : Form
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly Label summary = new Label();
        private readonly IReadOnlyList<ProjectDiagnostic> diagnostics;

        public DiagnosticsForm(IReadOnlyList<ProjectDiagnostic> diagnostics)
        {
            this.diagnostics = diagnostics ?? new List<ProjectDiagnostic>();
            Text = "Project diagnostics";
            Width = 900;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            summary.Dock = DockStyle.Top;
            summary.Height = 28;
            summary.Padding = new Padding(8, 7, 0, 0);

            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Severity", DataPropertyName = "Severity", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Song", DataPropertyName = "SongId", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Component", DataPropertyName = "Component", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", DataPropertyName = "Message", FillWeight = 45 });

            var copy = new Button { Text = "Copy report", Dock = DockStyle.Right, Width = 110 };
            copy.Click += (sender, args) => Clipboard.SetText(BuildReport());
            var close = new Button { Text = "Close", Dock = DockStyle.Right, Width = 90 };
            close.Click += (sender, args) => Close();
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(5) };
            buttons.Controls.Add(close);
            buttons.Controls.Add(copy);

            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(buttons);
            RefreshReport();
        }

        private void RefreshReport()
        {
            var errors = diagnostics.Count(item => item.Severity == DiagnosticSeverity.Error);
            var warnings = diagnostics.Count(item => item.Severity == DiagnosticSeverity.Warning);
            var info = diagnostics.Count(item => item.Severity == DiagnosticSeverity.Info);
            summary.Text = $"Errors: {errors}    Warnings: {warnings}    Info: {info}";
            grid.DataSource = diagnostics
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.SongId, StringComparer.Ordinal)
                .ThenBy(item => item.Component, StringComparer.Ordinal)
                .ToList();
        }

        private string BuildReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine(summary.Text);
            builder.AppendLine();
            foreach (var item in diagnostics)
                builder.AppendLine(item.ToString());
            return builder.ToString();
        }
    }
}
