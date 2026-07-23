using System;
using System.Windows.Forms;

namespace TaikoSoundEditor
{
    partial class MainForm
    {
        private bool encryptionPreferenceEventsHooked;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            HookEncryptionPreferenceEvents();
            RefreshEncryptionPreferenceControls();
        }

        private void HookEncryptionPreferenceEvents()
        {
            if (encryptionPreferenceEventsHooked) return;
            encryptionPreferenceEventsHooked = true;

            UseEncryptionBox.CheckedChanged += EncryptionPreferenceChanged;
            DatatableKeyBox.TextChanged += DatatableKeyPreferenceChanged;
            FumenKeyBox.TextChanged += FumenKeyPreferenceChanged;
        }

        private void EncryptionPreferenceChanged(object sender, EventArgs e)
        {
            UseEncryptionBox_CheckedChanged(sender, e);
            RefreshEncryptionPreferenceControls();
        }

        private void DatatableKeyPreferenceChanged(object sender, EventArgs e) =>
            DatatableKeyBox_TextChanged(sender, e);

        private void FumenKeyPreferenceChanged(object sender, EventArgs e) =>
            FumenKeyBox_TextChanged(sender, e);

        private void RefreshEncryptionPreferenceControls()
        {
            var enabled = UseEncryptionBox.Checked;
            DatatableKeyBox.Enabled = enabled;
            FumenKeyBox.Enabled = enabled;
        }
    }
}
