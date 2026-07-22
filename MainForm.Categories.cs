using System;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private ToolStripMenuItem categoriesToolStripMenuItem;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            InitializeCategoryEditorMenu();
            InitializeSongDeletionMenu();
            InitializeAdvancedMetadataMenu();
            InitializeProjectAwareImporter();
            InitializeProjectRepairsMenu();
            InitializeUnifiedWorkspace();
            RefreshCategoryEditorState();
            RefreshSongDeletionState();
            RefreshAdvancedMetadataState();
            RefreshProjectRepairsState();
        }

        private void InitializeCategoryEditorMenu()
        {
            if (categoriesToolStripMenuItem != null) return;

            categoriesToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "categoriesToolStripMenuItem",
                Text = "Categories...",
                Enabled = CurrentProject != null
            };
            categoriesToolStripMenuItem.Click += CategoriesToolStripMenuItem_Click;
            menuStrip1.Items.Add(categoriesToolStripMenuItem);
        }

        private void CategoriesToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null || MusicOrders == null || WordList == null)
            {
                MessageBox.Show("No data project is loaded.");
                return;
            }

            using (var form = new CategoryEditorForm(MusicInfos, MusicOrders, WordList, MusicOrderViewer))
                form.ShowDialog(this);
            MarkUnifiedCategoriesStaged();
            RefreshUnifiedSongList();
        });

        private void RefreshCategoryEditorState()
        {
            if (categoriesToolStripMenuItem != null)
                categoriesToolStripMenuItem.Enabled = CurrentProject != null;
        }
    }
}
