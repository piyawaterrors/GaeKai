using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GaeKai.Setup
{
    internal sealed class SetupForm : Form
    {
        private readonly byte[] payload;
        private readonly bool upgrade;
        private readonly CheckBox startMenuBox = new CheckBox();
        private readonly CheckBox desktopBox = new CheckBox();
        private readonly CheckBox startupBox = new CheckBox();
        private readonly CheckBox launchBox = new CheckBox();
        private readonly Button installButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Label statusLabel = new Label();

        public SetupForm(byte[] payload)
        {
            this.payload = payload;
            upgrade = InstallLayout.IsInstalled;
            Version version = Installer.PayloadVersion(payload);

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = "ติดตั้ง " + InstallLayout.DisplayName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            Icon appIcon = LoadIcon(SystemInformation.IconSize);
            if (appIcon != null) Icon = appIcon;

            TableLayoutPanel root = new TableLayoutPanel();
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.ColumnCount = 1;
            root.Padding = new Padding(16, 14, 16, 12);
            root.Dock = DockStyle.Fill;

            // ---- หัวเรื่อง: ไอคอน + ชื่อโปรแกรม ----
            TableLayoutPanel header = new TableLayoutPanel();
            header.AutoSize = true;
            header.ColumnCount = 2;
            header.RowCount = 2;
            header.Margin = new Padding(0, 0, 0, 12);
            PictureBox logo = new PictureBox();
            logo.Size = new Size(48, 48);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Margin = new Padding(0, 0, 12, 0);
            Icon big = LoadIcon(new Size(64, 64)); // ใหญ่กว่าที่แสดงนิดหน่อย เพื่อให้คมบนจอ DPI สูง
            if (big != null) logo.Image = big.ToBitmap();
            header.Controls.Add(logo, 0, 0);
            header.SetRowSpan(logo, 2);
            Label title = new Label();
            title.AutoSize = true;
            title.Text = InstallLayout.DisplayName + "  " + version.ToString(3);
            title.Font = new Font(Font.FontFamily, Font.Size * 1.5f, FontStyle.Bold);
            title.Margin = new Padding(0, 2, 0, 2);
            header.Controls.Add(title, 1, 0);
            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.ForeColor = SystemColors.GrayText;
            subtitle.Text = upgrade
                ? "มีโปรแกรมอยู่ในเครื่องแล้ว จะอัปเดตเป็นเวอร์ชันนี้ (การตั้งค่าเดิมยังอยู่ครบ)"
                : "แก้ข้อความที่พิมพ์ผิดภาษา ไทย ⇄ อังกฤษ ด้วยคีย์ลัดเดียว";
            subtitle.Margin = new Padding(0);
            header.Controls.Add(subtitle, 1, 1);
            root.Controls.Add(header);

            // ---- ตำแหน่งติดตั้ง ----
            Label folderLabel = new Label();
            folderLabel.AutoSize = true;
            folderLabel.Text = "ติดตั้งไปที่ (สำหรับผู้ใช้คนนี้ ไม่ต้องใช้สิทธิ์ Admin)";
            folderLabel.Margin = new Padding(3, 0, 3, 4);
            root.Controls.Add(folderLabel);
            TextBox folderBox = new TextBox();
            folderBox.ReadOnly = true;
            folderBox.TabStop = false;
            folderBox.Text = InstallLayout.InstallDir;
            folderBox.Dock = DockStyle.Fill;
            folderBox.Margin = new Padding(3, 0, 3, 12);
            root.Controls.Add(folderBox);

            // ---- ตัวเลือก ----
            InstallOptions defaults = InstallOptions.ForThisComputer();
            AddOption(root, startMenuBox, "สร้างทางลัดใน Start Menu", defaults.StartMenuShortcut);
            AddOption(root, desktopBox, "สร้างทางลัดบนหน้าจอ (Desktop)", defaults.DesktopShortcut);
            AddOption(root, startupBox, "เปิดโปรแกรมอัตโนมัติเมื่อเปิดเครื่อง", defaults.StartWithWindows);
            AddOption(root, launchBox, "เปิดโปรแกรมทันทีหลังติดตั้งเสร็จ", defaults.LaunchAfterInstall);

            // ---- ปุ่ม ----
            TableLayoutPanel footer = new TableLayoutPanel();
            footer.AutoSize = true;
            footer.ColumnCount = 3;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Dock = DockStyle.Fill;
            footer.Margin = new Padding(0, 16, 0, 0);
            statusLabel.AutoSize = true;
            statusLabel.Anchor = AnchorStyles.Left;
            statusLabel.ForeColor = SystemColors.GrayText;
            footer.Controls.Add(statusLabel, 0, 0);
            installButton.Text = upgrade ? "อัปเดต" : "ติดตั้ง";
            installButton.AutoSize = true;
            installButton.MinimumSize = new Size(88, 0);
            installButton.Click += OnInstall;
            footer.Controls.Add(installButton, 1, 0);
            cancelButton.Text = "ยกเลิก";
            cancelButton.AutoSize = true;
            cancelButton.MinimumSize = new Size(88, 0);
            cancelButton.Click += delegate { Close(); };
            footer.Controls.Add(cancelButton, 2, 0);
            root.Controls.Add(footer);

            Controls.Add(root);
            AcceptButton = installButton;
            CancelButton = cancelButton;
            ActiveControl = installButton;
            ResumeLayout(false);
            PerformLayout();
        }

        private static void AddOption(TableLayoutPanel root, CheckBox box, string text, bool isChecked)
        {
            box.AutoSize = true;
            box.Text = text;
            box.Checked = isChecked;
            root.Controls.Add(box);
        }

        private void OnInstall(object sender, EventArgs e)
        {
            InstallOptions options = new InstallOptions();
            options.StartMenuShortcut = startMenuBox.Checked;
            options.DesktopShortcut = desktopBox.Checked;
            options.StartWithWindows = startupBox.Checked;
            options.LaunchAfterInstall = launchBox.Checked;

            installButton.Enabled = false;
            cancelButton.Enabled = false;
            UseWaitCursor = true;
            statusLabel.Text = upgrade ? "กำลังอัปเดต..." : "กำลังติดตั้ง...";
            Refresh();

            try
            {
                Installer.Install(payload, options);
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                statusLabel.Text = "";
                installButton.Enabled = true;
                cancelButton.Enabled = true;
                MessageBox.Show(this, "ติดตั้งไม่สำเร็จ: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UseWaitCursor = false;
            statusLabel.Text = "";
            string message = upgrade
                ? "อัปเดต " + InstallLayout.DisplayName + " เรียบร้อยแล้ว"
                : "ติดตั้ง " + InstallLayout.DisplayName + " เรียบร้อยแล้ว\n\n" +
                  "วิธีใช้: คลุมข้อความที่พิมพ์ผิดภาษา แล้วกด Ctrl + Shift + Space\n\n" +
                  "เปลี่ยนคีย์ลัดได้ที่ไอคอน ก ในถาดไอคอนมุมขวาล่าง\n" +
                  "ถอนการติดตั้งได้ที่ Settings → Apps";
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private static Icon LoadIcon(Size size)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GaeKai.ico"))
            {
                return stream == null ? null : new Icon(stream, size);
            }
        }
    }
}
