using System;
using System.Drawing;
using System.Windows.Forms;

namespace GaeKai
{
    internal sealed class SettingsForm : Form
    {
        private readonly HotkeyBox hotkeyBox = new HotkeyBox();
        private readonly CheckBox switchLayoutBox = new CheckBox();
        private readonly CheckBox restoreClipboardBox = new CheckBox();
        private readonly CheckBox startupBox = new CheckBox();
        private readonly Func<Hotkey, bool> tryRegisterHotkey;

        /// <param name="tryRegisterHotkey">ใช้ทดสอบว่าคีย์ลัดใหม่ไม่ชนกับโปรแกรมอื่นก่อนบันทึก</param>
        public SettingsForm(AppSettings settings, bool startWithWindows, Func<Hotkey, bool> tryRegisterHotkey)
        {
            this.tryRegisterHotkey = tryRegisterHotkey;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Program.DisplayName + " — ตั้งค่า";
            Icon = AppIcon.Large;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel root = new TableLayoutPanel();
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.ColumnCount = 1;
            root.Padding = new Padding(14, 12, 14, 10);
            root.Dock = DockStyle.Fill;

            Label hotkeyLabel = new Label();
            hotkeyLabel.AutoSize = true;
            hotkeyLabel.Text = "คีย์ลัดสำหรับแปลงข้อความที่คลุมไว้";
            hotkeyLabel.Font = new Font(Font, FontStyle.Bold);
            hotkeyLabel.Margin = new Padding(3, 0, 3, 6);
            root.Controls.Add(hotkeyLabel);

            FlowLayoutPanel hotkeyRow = new FlowLayoutPanel();
            hotkeyRow.AutoSize = true;
            hotkeyRow.WrapContents = false;
            hotkeyRow.Margin = new Padding(0);
            hotkeyBox.Width = 230;
            hotkeyBox.Hotkey = settings.Hotkey;
            hotkeyRow.Controls.Add(hotkeyBox);
            Button defaultButton = new Button();
            defaultButton.AutoSize = true;
            defaultButton.Text = "ค่าเริ่มต้น";
            defaultButton.Click += delegate { hotkeyBox.Hotkey = Hotkey.Default; };
            hotkeyRow.Controls.Add(defaultButton);
            root.Controls.Add(hotkeyRow);

            Label hint = new Label();
            hint.AutoSize = true;
            hint.ForeColor = SystemColors.GrayText;
            hint.Text = "คลิกที่ช่องด้านบน แล้วกดคีย์ลัดใหม่ที่ต้องการ เช่น Ctrl + Alt + K";
            hint.Margin = new Padding(3, 4, 3, 12);
            root.Controls.Add(hint);

            switchLayoutBox.AutoSize = true;
            switchLayoutBox.Text = "สลับภาษาแป้นพิมพ์ให้อัตโนมัติหลังแปลง (พิมพ์ต่อได้ทันที)";
            switchLayoutBox.Checked = settings.SwitchKeyboardLayout;
            root.Controls.Add(switchLayoutBox);

            restoreClipboardBox.AutoSize = true;
            restoreClipboardBox.Text = "คืนค่าคลิปบอร์ดเดิมหลังแปลง";
            restoreClipboardBox.Checked = settings.RestoreClipboard;
            root.Controls.Add(restoreClipboardBox);

            startupBox.AutoSize = true;
            startupBox.Text = "เปิดโปรแกรมอัตโนมัติเมื่อเปิดเครื่อง";
            startupBox.Checked = startWithWindows;
            root.Controls.Add(startupBox);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0, 14, 0, 0);
            Button cancelButton = new Button();
            cancelButton.Text = "ยกเลิก";
            cancelButton.AutoSize = true;
            cancelButton.Click += delegate { Close(); };
            Button okButton = new Button();
            okButton.Text = "บันทึก";
            okButton.AutoSize = true;
            okButton.Click += OnSave;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = okButton;
            CancelButton = cancelButton;
            ResumeLayout(false);
            PerformLayout();
        }

        public Hotkey SelectedHotkey
        {
            get { return hotkeyBox.Hotkey; }
        }

        public bool SwitchKeyboardLayout
        {
            get { return switchLayoutBox.Checked; }
        }

        public bool RestoreClipboard
        {
            get { return restoreClipboardBox.Checked; }
        }

        public bool StartWithWindows
        {
            get { return startupBox.Checked; }
        }

        private void OnSave(object sender, EventArgs e)
        {
            Hotkey hotkey = hotkeyBox.Hotkey;
            if (hotkey == null || !hotkey.IsValid)
            {
                MessageBox.Show(this, "กรุณากดคีย์ลัดที่ต้องการในช่องคีย์ลัด", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                hotkeyBox.Focus();
                return;
            }
            if (!tryRegisterHotkey(hotkey))
            {
                MessageBox.Show(this,
                    "คีย์ลัด " + hotkey + " ถูกใช้งานโดยโปรแกรมอื่นหรือโดย Windows อยู่แล้ว\nกรุณาเลือกคีย์ลัดอื่น",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                hotkeyBox.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            ActiveControl = null;
        }
    }
}
