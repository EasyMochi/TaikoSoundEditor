using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TaikoSoundEditor.Project
{
    internal sealed class ProjectRepairForm : Form
    {
        private readonly TaikoProject project;
        private readonly CheckedListBox actionsList = new CheckedListBox();
        private readonly TextBox preview = new TextBox();
        private readonly Label summary = new Label();
        private readonly Button apply = new Button();
        private List<ProjectRepairAction> actions = new List<ProjectRepairAction>();

        public ProjectRepairForm(TaikoProject project)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            Text = "Explicit project repairs";
            Width = 1050;
            Height = 650;
            MinimumSize = new Size(820, 500);
            StartPosition = FormStartPosition.CenterParent;

            summary.Dock = DockStyle.Top;
            summary.Height = 48;
            summary.Padding = new Padding(9, 8, 9, 0);
            summary.Text = "Only deterministic repairs are listed. Nothing is selected or changed automatically.";

            actionsList.Dock = DockStyle.Fill;
            actionsList.CheckOnClick = true;
            actionsList.IntegralHeight = false;
            actionsList.SelectedIndexChanged += (sender, args) => RefreshPreview();
            actionsList.ItemCheck += (sender, args) => BeginInvoke(new Action(RefreshButtons));

            preview.Dock = DockStyle.Fill;
            preview.Multiline = true;
            preview.ReadOnly = true;
            preview.ScrollBars = ScrollBars.Both;
            preview.WordWrap = false;
            preview.Font = new Font(FontFamily.GenericMonospace, 9f);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 430
            };
            split.Panel1.Controls.Add(actionsList);
            split.Panel2.Controls.Add(preview);

            var selectAll = new Button { Text = "Select all", Dock = DockStyle.Left, Width = 100 };
            selectAll.Click += (sender, args) => SetAllChecked(true);
            var selectNone = new Button { Text = "Select none", Dock = DockStyle.Left, Width = 100 };
            selectNone.Click += (sender, args) => SetAllChecked(false);
            var refresh = new Button { Text = "Rescan", Dock = DockStyle.Left, Width = 90 };
            refresh.Click += (sender, args) => LoadActions();
            var close = new Button { Text = "Close", Dock = DockStyle.Right, Width = 90 };
            close.Click += (sender, args) => Close();
            apply.Text = "Apply selected repairs";
            apply.Dock = DockStyle.Right;
            apply.Width = 170;
            apply.Click += Apply_Click;

            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(5) };
            buttons.Controls.Add(close);
            buttons.Controls.Add(apply);
            buttons.Controls.Add(refresh);
            buttons.Controls.Add(selectNone);
            buttons.Controls.Add(selectAll);

            Controls.Add(split);
            Controls.Add(summary);
            Controls.Add(buttons);
            LoadActions();
        }

        public bool RepairsApplied { get; private set; }

        private void LoadActions()
        {
            actions = ProjectRepairPlanner.Build(project).ToList();
            actionsList.Items.Clear();
            foreach (var action in actions) actionsList.Items.Add(action, false);
            summary.Text = actions.Count == 0
                ? "No deterministic repairs are currently available. Diagnostics may still contain issues requiring manual decisions."
                : $"{actions.Count} deterministic repair(s) available. Review and select each change explicitly.";
            RefreshPreview();
            RefreshButtons();
        }

        private void RefreshPreview()
        {
            if (actionsList.SelectedItem is ProjectRepairAction action)
            {
                preview.Text = action.Title + Environment.NewLine + Environment.NewLine + action.Preview;
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Select a repair to inspect its exact change.");
            builder.AppendLine();
            builder.AppendLine("Repairs remain in memory until complete validated project export.");
            preview.Text = builder.ToString();
        }

        private void RefreshButtons()
        {
            apply.Enabled = actionsList.CheckedItems.Count > 0;
        }

        private void SetAllChecked(bool value)
        {
            for (var i = 0; i < actionsList.Items.Count; i++)
                actionsList.SetItemChecked(i, value);
            RefreshButtons();
        }

        private void Apply_Click(object sender, EventArgs e)
        {
            var selected = actionsList.CheckedItems.Cast<ProjectRepairAction>().ToList();
            if (selected.Count == 0) return;

            var confirmation = $"Apply {selected.Count} selected repair(s) to the in-memory project?\n\n" +
                               "The source data folder will remain untouched until complete project export.";
            if (MessageBox.Show(this, confirmation, "Confirm explicit repairs",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            foreach (var action in selected) action.Apply();
            RepairsApplied = true;
            MessageBox.Show(this,
                $"Applied {selected.Count} repair(s). The project has not been written to disk.",
                "Repairs staged", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadActions();
        }
    }
}
