using System;
using System.Drawing;
using System.Windows.Forms;

namespace SoulWebLauncher
{
    internal enum CloseChoice { Cancel, Tray, Exit }

    internal sealed class CloseChoiceDialog : Form
    {
        internal CloseChoice Choice { get; private set; }

        internal CloseChoiceDialog()
        {
            Choice = CloseChoice.Cancel;
            Text = "关闭 Soul 桌面";
            Font = new Font("Microsoft YaHei UI", 10F);
            Icon = AppIcons.LoadMainIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(510, 255);
            BackColor = Color.White;

            Label title = new Label();
            title.Text = "要继续在托盘中接收提醒吗？";
            title.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(25, 48, 57);
            title.Location = new Point(24, 22);
            title.Size = new Size(460, 42);
            Controls.Add(title);
            Label hint = new Label();
            hint.Text = "最小化到托盘：保留官方网页，双击图标可恢复。\r\n提醒取决于官方网页是否发出通知。\r\n\r\n直接退出：关闭本程序，停止托盘提醒。";
            hint.Location = new Point(24, 78);
            hint.Size = new Size(460, 90);
            hint.ForeColor = Color.FromArgb(75, 101, 111);
            Controls.Add(hint);
            Button tray = MakeButton("最小化到托盘", 24, 176, 170, CloseChoice.Tray);
            tray.BackColor = Color.FromArgb(0, 120, 115);
            tray.ForeColor = Color.White;
            tray.FlatStyle = FlatStyle.Flat;
            tray.FlatAppearance.BorderSize = 0;
            Controls.Add(tray);
            Controls.Add(MakeButton("直接退出", 206, 176, 130, CloseChoice.Exit));
            Button cancel = MakeButton("取消", 348, 176, 130, CloseChoice.Cancel);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            AcceptButton = tray;
            CancelButton = cancel;
        }

        private Button MakeButton(string text, int x, int y, int width, CloseChoice choice)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 44);
            button.Click += delegate { Choice = choice; DialogResult = choice == CloseChoice.Cancel ? DialogResult.Cancel : DialogResult.OK; Close(); };
            return button;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Icon != null) { Icon.Dispose(); Icon = null; }
            base.Dispose(disposing);
        }
    }

    internal sealed class ThirdPartyNoticesDialog : Form
    {
        internal ThirdPartyNoticesDialog(string notices)
        {
            Text = "第三方组件许可";
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(780, 560);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            TextBox text = new TextBox();
            text.Multiline = true;
            text.ReadOnly = true;
            text.ScrollBars = ScrollBars.Both;
            text.Dock = DockStyle.Fill;
            text.WordWrap = true;
            text.Text = notices;
            text.BackColor = Color.White;
            text.SelectionStart = 0;
            Controls.Add(text);
            Button close = new Button();
            close.Text = "关闭";
            close.Dock = DockStyle.Bottom;
            close.Height = 42;
            close.DialogResult = DialogResult.Cancel;
            Controls.Add(close);
            CancelButton = close;
        }
    }
}
