using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace TaikoSoundEditor.Commons.Controls
{
    [DefaultEvent("PathChanged")]
    public partial class PathSelector : UserControl
    {
        public PathSelector()
        {
            InitializeComponent();
        }

        protected override void SetBoundsCore(
            int x, int y, int width, int height, BoundsSpecified specified)
        {
            // A docked control must always accept a bounds update. The previous
            // implementation ignored height changes, which caused WinForms layout
            // to retry forever when this control used DockStyle.Fill.
            var preferredHeight = DisplayBox?.PreferredHeight ?? height;
            var mayGrowVertically = Dock == DockStyle.Fill
                || Dock == DockStyle.Left
                || Dock == DockStyle.Right;

            base.SetBoundsCore(
                x,
                y,
                width,
                mayGrowVertically ? height : preferredHeight,
                specified);

            if (Button != null)
                Button.Width = 2 * preferredHeight;
        }

        [DefaultValue(false)]
        public bool SelectsFolder { get; set; }

        [DefaultValue("")]
        public string Path
        {
            get => DisplayBox.Text;
            set
            {
                DisplayBox.Text = value;
                PathChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public delegate void OnPathChanged(object sender, EventArgs args);
        public event OnPathChanged PathChanged;

        [DefaultValue("All files(*.*)|*.*")]
        public string Filter { get; set; } = "All files(*.*)|*.*";

        private void Button_Click(object sender, EventArgs e)
        {
            if (SelectsFolder)
            {
                var dialog = new FolderPicker { InputPath = DisplayBox.Text };
                if (dialog.ShowDialog() == true)
                    Path = dialog.ResultPath;
                return;
            }

            using var dialogFile = new OpenFileDialog { Filter = Filter };
            if (dialogFile.ShowDialog() == DialogResult.OK)
                Path = dialogFile.FileName;
        }
    }
}
