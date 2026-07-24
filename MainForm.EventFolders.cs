using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TaikoSoundEditor.Commons.Utils;
using TaikoSoundEditor.Project;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private void EventFoldersToolStripMenuItem_Click(object sender, EventArgs e) => ExceptionGuard.Run(() =>
        {
            if (CurrentProject == null || MusicInfos == null || WordList == null)
                throw new InvalidOperationException("Open a data project first.");

            MergeEditableDatatables();
            EventFolderData ??= LoadEventFolderData();
            if (EventFolderData == null) return;

            using var editor = new EventFolderEditorForm(
                CurrentProject, EventFolderData, MusicInfos, WordList, AddedMusic);
            editor.ShowDialog(this);
            if (editor.Changed)
                MarkUnifiedEventFoldersStaged();
        });

        private EventFolderDataDocument LoadEventFolderData()
        {
            var candidates = new[]
            {
                Path.Combine(CurrentProject.Paths.Root, "server", "event_folder_data.json"),
                Path.Combine(CurrentProject.Paths.Root, "server", "event_folder_data.json.gz"),
                Path.Combine(CurrentProject.Paths.Root, "server", "event_folder_data.json.br"),
                Path.Combine(CurrentProject.Paths.Root, "event_folder_data.json"),
                Path.Combine(CurrentProject.Paths.Root, "event_folder_data.json.gz"),
                Path.Combine(CurrentProject.Paths.Root, "event_folder_data.json.br")
            };
            var existing = candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
            if (!string.IsNullOrWhiteSpace(existing))
            {
                Config.EventFolderDataPath = existing;
                return EventFolderDataDocument.Load(existing);
            }

            using var picker = new OpenFileDialog
            {
                Title = "Open event_folder_data",
                Filter = "Event folder data|event_folder_data.json;event_folder_data.json.gz;event_folder_data.json.br|JSON files (*.json)|*.json|Compressed JSON (*.gz;*.br)|*.gz;*.br|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                FileName = "event_folder_data.json"
            };
            if (!string.IsNullOrWhiteSpace(Config.EventFolderDataPath))
            {
                var initialDirectory = Path.GetDirectoryName(Config.EventFolderDataPath);
                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                    picker.InitialDirectory = initialDirectory;
            }

            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                Config.EventFolderDataPath = picker.FileName;
                return EventFolderDataDocument.Load(picker.FileName);
            }

            return MessageBox.Show(this,
                       "No server event-folder file was selected. Start with an empty server folder list?",
                       "Create event-folder data", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes
                ? EventFolderDataDocument.CreateEmpty()
                : null;
        }
    }
}
