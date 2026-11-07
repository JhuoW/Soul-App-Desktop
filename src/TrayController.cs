using System;
using System.Drawing;
using System.Windows.Forms;

namespace SoulWebLauncher
{
    internal sealed class TrayController : IDisposable
    {
        internal event EventHandler OpenRequested;
        internal event EventHandler HomeRequested;
        internal event EventHandler RefreshRequested;
        internal event EventHandler ReminderToggleRequested;
        internal event EventHandler StopReminderRequested;
        internal event EventHandler ExitRequested;
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem enableItem;
        private readonly ToolStripMenuItem stopItem;
        private readonly Timer animationTimer;
        private readonly Icon normalIcon;
        private readonly Icon[] defaultFrames;
        private Icon[] customFrames;
        private int animationFrame;
        private bool hiddenHintShown;
        private bool disposed;
        internal bool HasReminder { get; private set; }

        internal TrayController()
        {
            normalIcon = AppIcons.LoadTrayIcon();
            defaultFrames = AppIcons.CreateTrayFrames();
            menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 10F);
            menu.Items.Add(MakeItem("打开聊天窗口", delegate { Raise(OpenRequested); }));
            menu.Items.Add(MakeItem("返回官方首页", delegate { Raise(HomeRequested); }));
            menu.Items.Add(MakeItem("刷新网页", delegate { Raise(RefreshRequested); }));
            menu.Items.Add(new ToolStripSeparator());
            enableItem = MakeItem("托盘新消息提醒", delegate { Raise(ReminderToggleRequested); });
            enableItem.Checked = true;
            menu.Items.Add(enableItem);
            stopItem = MakeItem("停止本次提醒", delegate { Raise(StopReminderRequested); });
            stopItem.Enabled = false;
            menu.Items.Add(stopItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MakeItem("退出", delegate { Raise(ExitRequested); }));
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = normalIcon;
            notifyIcon.Text = "Soul 桌面 · 双击打开聊天";
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.MouseDoubleClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Raise(OpenRequested); };
            notifyIcon.BalloonTipClicked += delegate { Raise(OpenRequested); };
            notifyIcon.Visible = true;
            animationTimer = new Timer();
            animationTimer.Interval = 180;
            animationTimer.Tick += delegate
            {
                if (disposed || !HasReminder) return;
                Icon[] frames = customFrames ?? defaultFrames;
                if (frames.Length == 0) return;
                animationFrame = (animationFrame + 1) % frames.Length;
                notifyIcon.Icon = frames[animationFrame];
            };
        }

        private ToolStripMenuItem MakeItem(string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { if (!disposed) action(); };
            return item;
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null && !disposed) handler(this, EventArgs.Empty);
        }

        internal void SetRemindersEnabled(bool enabled)
        {
            if (disposed) return;
            enableItem.Checked = enabled;
            if (!enabled) StopReminder();
        }

        internal void StartReminder(bool preview)
        {
            if (disposed) return;
            StopReminder();
            HasReminder = true;
            stopItem.Enabled = true;
            animationFrame = 0;
            notifyIcon.Icon = defaultFrames[0];
            notifyIcon.Text = preview ? "Soul 桌面 · 本地测试提醒" : "Soul 桌面 · 有新的网页提醒";
            animationTimer.Start();
            if (preview)
                notifyIcon.ShowBalloonTip(3500, "本地托盘提醒测试",
                    "这是本地测试，不代表收到真实消息。双击图标可恢复窗口。", ToolTipIcon.Info);
        }

        internal void SetReminderFrames(Icon[] frames)
        {
            if (frames == null || frames.Length == 0) return;
            if (disposed || !HasReminder)
            {
                foreach (Icon frame in frames) frame.Dispose();
                return;
            }
            notifyIcon.Icon = normalIcon;
            DisposeCustomFrames();
            customFrames = frames;
            animationFrame = 0;
            notifyIcon.Icon = frames[0];
        }

        internal void StopReminder()
        {
            if (disposed) return;
            animationTimer.Stop();
            HasReminder = false;
            stopItem.Enabled = false;
            notifyIcon.Icon = normalIcon;
            notifyIcon.Text = "Soul 桌面 · 双击打开聊天";
            DisposeCustomFrames();
        }

        internal void ShowHiddenHint()
        {
            if (disposed || hiddenHintShown) return;
            hiddenHintShown = true;
            notifyIcon.ShowBalloonTip(3000, "Soul 已最小化到托盘", "网页继续运行。双击托盘图标恢复，右键可打开操作菜单。", ToolTipIcon.Info);
        }

        private void DisposeCustomFrames()
        {
            if (customFrames == null) return;
            foreach (Icon frame in customFrames) frame.Dispose();
            customFrames = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            animationTimer.Stop();
            animationTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            menu.Dispose();
            DisposeCustomFrames();
            foreach (Icon frame in defaultFrames) frame.Dispose();
            normalIcon.Dispose();
        }
    }
}
