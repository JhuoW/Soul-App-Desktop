using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SoulWebLauncher
{
    internal static class DesktopApplication
    {
        internal static int Run(string[] args)
        {
            if (args.Length > 1 || (args.Length == 1 && args[0] != "--open"))
            {
                MessageBox.Show("直接打开程序即可使用；也支持 --open。", "Soul 桌面", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (MainForm main = new MainForm()) Application.Run(main);
            return 0;
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Ink = Color.FromArgb(25, 48, 57);
        private static readonly Color Muted = Color.FromArgb(83, 108, 116);
        private readonly bool startWebView;
        private readonly Panel browserPanel;
        private readonly Panel fallbackPanel;
        private readonly Label fallbackTitle;
        private readonly Label fallbackText;
        private readonly Label siteLabel;
        private readonly ToolStripStatusLabel connectionLabel;
        private readonly ToolStripStatusLabel reminderLabel;
        private readonly ToolStripMenuItem reminderMenu;
        private readonly TrayController tray;
        private readonly Dictionary<string, CoreWebView2PermissionState> permissionChoices = new Dictionary<string, CoreWebView2PermissionState>();
        private WebView2 browser;
        private bool initializationInProgress;
        private bool exitRequested;
        private bool closePromptOpen;
        private bool remindersEnabled = true;
        private bool notificationApiAvailable;
        private int browserGeneration;
        private int reminderGeneration;
        private CancellationTokenSource avatarCancellation;

        internal MainForm() : this(true) { }

        internal MainForm(bool startWebView)
        {
            this.startWebView = startWebView;
            Text = startWebView ? "Soul 桌面 · 官方网页" : "Soul 桌面 · 本地界面测试";
            Icon = AppIcons.LoadMainIcon();
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1180, 820);
            MinimumSize = new Size(900, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 249, 250);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 77, BackColor = Color.White, Padding = new Padding(18, 10, 14, 8) };
            TableLayoutPanel headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(headerLayout);
            Label brand = new Label { Text = "Soul 桌面\r\n非官方桌面工具", ForeColor = Ink, Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
            headerLayout.Controls.Add(brand, 0, 0);
            siteLabel = new Label { Text = BrowserPolicy.HomeUrl, ForeColor = Muted, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = new Padding(8, 0, 8, 0) };
            headerLayout.Controls.Add(siteLabel, 1, 0);
            ToolStrip tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.None,
                Anchor = AnchorStyles.Right, BackColor = Color.White, Font = Font, Padding = new Padding(0) };
            tools.Items.Add(MakeTool("首页", delegate { NavigateHome(); }));
            tools.Items.Add(MakeTool("刷新", delegate { RefreshPage(); }));
            tools.Items.Add(MakeTool("浏览器", delegate { OpenInBrowser(); }));
            ToolStripDropDownButton options = new ToolStripDropDownButton("设置");
            reminderMenu = new ToolStripMenuItem("托盘新消息提醒") { Checked = true };
            reminderMenu.Click += delegate { SetRemindersEnabled(!remindersEnabled); };
            options.DropDownItems.Add(reminderMenu);
            ToolStripMenuItem explain = new ToolStripMenuItem("提醒说明");
            explain.Click += delegate { ShowReminderHelp(); };
            options.DropDownItems.Add(explain);
            ToolStripMenuItem notices = new ToolStripMenuItem("第三方组件许可");
            notices.Click += delegate { using (ThirdPartyNoticesDialog dialog = new ThirdPartyNoticesDialog(RuntimeFiles.ReadThirdPartyNotices())) dialog.ShowDialog(this); };
            options.DropDownItems.Add(notices);
            tools.Items.Add(options);
            tools.Items.Add(MakeTool("托盘", delegate { MinimizeToTray(); }));
            headerLayout.Controls.Add(tools, 2, 0);

            StatusStrip footer = new StatusStrip { SizingGrip = false, BackColor = Color.FromArgb(238, 245, 246), Font = new Font("Microsoft YaHei UI", 9F) };
            connectionLabel = new ToolStripStatusLabel("正在准备官方网页…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted };
            reminderLabel = new ToolStripStatusLabel("等待网页通知") { ForeColor = Muted };
            footer.Items.Add(connectionLabel);
            footer.Items.Add(reminderLabel);
            browserPanel = new Panel { Dock = DockStyle.Fill };
            fallbackPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor, Padding = new Padding(48) };
            TableLayoutPanel fallbackLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            fallbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fallbackTitle = new Label { Text = startWebView ? "正在打开 Soul 官方网页" : "本地界面测试", Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Ink, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 12, 0, 20) };
            fallbackLayout.Controls.Add(fallbackTitle);
            fallbackText = new Label { Text = startWebView ? "登录与聊天由 Soul 官方页面提供。\r\n首次使用时，需要在本窗口内完成官方登录。" : "此窗口用于验证关闭、托盘和恢复，不加载网页、不连接 Soul。",
                ForeColor = Muted, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 24) };
            fallbackLayout.Controls.Add(fallbackText);
            FlowLayoutPanel recoveryButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
            recoveryButtons.Controls.Add(MakeRecoveryButton("重试", delegate { InitializeBrowser(); }));
            recoveryButtons.Controls.Add(MakeRecoveryButton("用浏览器打开", delegate { OpenInBrowser(); }));
            recoveryButtons.Controls.Add(MakeRecoveryButton("获取网页运行组件", delegate { BrowserPolicy.TryOpenExternal(BrowserPolicy.RuntimeUrl); }));
            recoveryButtons.Visible = startWebView;
            fallbackLayout.Controls.Add(recoveryButtons);
            fallbackPanel.Controls.Add(fallbackLayout);
            browserPanel.Controls.Add(fallbackPanel);
            Controls.Add(browserPanel);
            Controls.Add(footer);
            Controls.Add(header);

            tray = new TrayController();
            tray.OpenRequested += delegate { RestoreFromTray(); };
            tray.HomeRequested += delegate { RestoreFromTray(); NavigateHome(); };
            tray.RefreshRequested += delegate { RefreshPage(); };
            tray.ReminderToggleRequested += delegate { SetRemindersEnabled(!remindersEnabled); };
            tray.StopReminderRequested += delegate { StopReminder(); };
            tray.ExitRequested += delegate { ExitApplication(); };
            Shown += delegate { if (startWebView) InitializeBrowser(); else connectionLabel.Text = "本地界面测试 · 未连接任何网页"; };
            Activated += delegate { if (Visible && WindowState != FormWindowState.Minimized) StopReminder(); };
        }

        private ToolStripButton MakeTool(string text, Action action)
        {
            ToolStripButton button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, Padding = new Padding(4, 4, 4, 4) };
            button.Click += delegate { action(); };
            return button;
        }

        private Button MakeRecoveryButton(string text, Action action)
        {
            Button button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(118, 40),
                Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 0, 12, 12) };
            button.Click += delegate { action(); };
            return button;
        }

        private async void InitializeBrowser()
        {
            if (!startWebView || initializationInProgress || exitRequested || IsDisposed) return;
            initializationInProgress = true;
            int generation = ++browserGeneration;
            try
            {
                if (browser != null)
                {
                    browserPanel.Controls.Remove(browser);
                    browser.Dispose();
                    browser = null;
                }
                fallbackTitle.Text = "正在打开 Soul 官方网页";
                fallbackText.Text = "请稍候。登录与聊天均在官方页面完成。";
                fallbackPanel.Visible = true;
                fallbackPanel.BringToFront();
                connectionLabel.Text = "正在准备官方网页…";
                WebView2 current = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
                browser = current;
                browserPanel.Controls.Add(current);
                current.SendToBack();
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, RuntimeFiles.DataDirectory, null);
                if (IsDisposed || exitRequested || generation != browserGeneration) return;
                await current.EnsureCoreWebView2Async(environment);
                if (IsDisposed || exitRequested || generation != browserGeneration) return;
                CoreWebView2 core = current.CoreWebView2;
                core.Settings.AreHostObjectsAllowed = false;
                core.Settings.IsWebMessageEnabled = false;
                core.NavigationStarting += OnNavigationStarting;
                core.NavigationCompleted += OnNavigationCompleted;
                core.SourceChanged += OnSourceChanged;
                core.NewWindowRequested += OnNewWindowRequested;
                core.PermissionRequested += OnPermissionRequested;
                core.ProcessFailed += OnProcessFailed;
                core.WindowCloseRequested += delegate { BeginSafe(delegate { Close(); }); };
                notificationApiAvailable = false;
                try { core.NotificationReceived += OnNotificationReceived; notificationApiAvailable = true; }
                catch (NotImplementedException) { }
                catch (COMException) { }
                UpdateReminderStatus();
                core.Navigate(BrowserPolicy.HomeUrl);
                current.BringToFront();
                fallbackPanel.Visible = false;
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ShowFailure("缺少网页运行组件", "请安装 Microsoft Edge WebView2 Runtime 后重试。\r\n也可以先用默认浏览器访问 Soul 官方网页。");
            }
            catch (Exception error)
            {
                if (error is OutOfMemoryException || error is StackOverflowException) throw;
                ShowFailure("暂时无法打开内置网页", "请重试，或通过默认浏览器使用官方网页。\r\n如果持续失败，请检查 WebView2 Runtime 和本机网络。");
            }
            finally { initializationInProgress = false; }
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (BrowserPolicy.IsOfficialUrl(e.Uri)) { connectionLabel.Text = "正在加载官方网页…"; return; }
            e.Cancel = true;
            if (e.IsUserInitiated && BrowserPolicy.IsSafeHttpsUrl(e.Uri))
            {
                string destination = e.Uri;
                BeginSafe(delegate { BrowserPolicy.TryOpenExternal(destination); });
                connectionLabel.Text = "外部链接已交给默认浏览器";
            }
            else connectionLabel.Text = "已阻止离开官方站点的自动跳转";
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            connectionLabel.Text = e.IsSuccess ? "Soul 官方网页 · 登录状态由网页管理" : "网页加载未完成，可刷新或用浏览器打开";
        }

        private void OnSourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (browser == null || browser.CoreWebView2 == null) return;
            siteLabel.Text = BrowserPolicy.GetOfficialOrigin(browser.CoreWebView2.Source) ?? BrowserPolicy.HomeUrl;
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (!e.IsUserInitiated || !BrowserPolicy.IsSafeHttpsUrl(e.Uri)) return;
            string destination = e.Uri;
            BeginSafe(delegate { BrowserPolicy.TryOpenExternal(destination); });
        }

        private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            BeginSafe(delegate { ShowFailure("网页进程已停止", "点击重试重新打开官方网页。\r\n也可以改用默认浏览器。"); });
        }

        private void OnPermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            e.SavesInProfile = false;
            if (!BrowserPolicy.IsOfficialUrl(e.Uri))
            { e.State = CoreWebView2PermissionState.Deny; return; }
            string description = PermissionDescription(e.PermissionKind);
            if (description == null || (!e.IsUserInitiated && e.PermissionKind != CoreWebView2PermissionKind.Notifications))
            { e.State = CoreWebView2PermissionState.Deny; return; }
            string origin = BrowserPolicy.GetOfficialOrigin(e.Uri);
            string choiceKey = origin + "|" + e.PermissionKind.ToString();
            CoreWebView2PermissionState existing;
            if (permissionChoices.TryGetValue(choiceKey, out existing)) { e.State = existing; return; }
            CoreWebView2Deferral deferral = e.GetDeferral();
            int permissionGeneration = browserGeneration;
            try
            {
                // Never show a modal dialog synchronously inside a WebView2 callback.
                BeginInvoke(new Action(delegate
                {
                    try
                    {
                        if (exitRequested || IsDisposed || permissionGeneration != browserGeneration) { e.State = CoreWebView2PermissionState.Deny; return; }
                        CoreWebView2PermissionState queuedChoice;
                        if (permissionChoices.TryGetValue(choiceKey, out queuedChoice)) { e.State = queuedChoice; return; }
                        RestoreFromTray();
                        string message = "允许以下官方网页" + description + "吗？\r\n\r\n" + origin;
                        if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
                            message += "\r\n\r\n允许后，网页发出新通知时可显示托盘提醒。\r\n是否产生通知及是否包含头像，由官方网页决定。";
                        DialogResult result = MessageBox.Show(this, message, "网页权限请求", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        e.State = result == DialogResult.Yes ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
                        permissionChoices[choiceKey] = e.State;
                    }
                    catch (COMException) { }
                    catch (InvalidOperationException) { }
                    finally { CompleteDeferral(deferral); }
                }));
            }
            catch (InvalidOperationException)
            {
                e.State = CoreWebView2PermissionState.Deny;
                CompleteDeferral(deferral);
            }
        }

        private static void CompleteDeferral(CoreWebView2Deferral deferral)
        {
            try { deferral.Complete(); }
            catch (COMException) { }
            catch (InvalidOperationException) { }
        }

        private static string PermissionDescription(CoreWebView2PermissionKind kind)
        {
            switch (kind)
            {
                case CoreWebView2PermissionKind.Notifications: return "发送通知";
                case CoreWebView2PermissionKind.Microphone: return "使用麦克风";
                case CoreWebView2PermissionKind.Camera: return "使用摄像头";
                case CoreWebView2PermissionKind.Geolocation: return "获取位置信息";
                case CoreWebView2PermissionKind.ClipboardRead: return "读取剪贴板";
                default: return null;
            }
        }

        private void OnNotificationReceived(object sender, CoreWebView2NotificationReceivedEventArgs e)
        {
            // Leave official notification presentation and lifecycle to WebView2.
            // Never inspect the notification title/body or the page's messages.
            e.Handled = false;
            if (!remindersEnabled || !BrowserPolicy.IsOfficialUrl(e.SenderOrigin)) return;
            string iconUri = e.Notification.IconUri;
            BeginSafe(delegate { HandleIncomingReminder(iconUri); });
        }

        private void HandleIncomingReminder(string iconUri)
        {
            if (!remindersEnabled || exitRequested || IsDisposed) return;
            if (Visible && WindowState != FormWindowState.Minimized && ContainsFocus) return;
            CancelAvatarLoad();
            tray.StartReminder(false);
            reminderLabel.Text = "有新的网页提醒";
            avatarCancellation = new CancellationTokenSource();
            LoadAvatar(iconUri, reminderGeneration, avatarCancellation.Token);
        }

        private async void LoadAvatar(string iconUri, int generation, CancellationToken cancellation)
        {
            Icon[] frames = null;
            try
            {
                frames = await NotificationAvatar.TryLoadAsync(iconUri, cancellation);
                if (frames == null) return;
                if (IsDisposed || exitRequested || cancellation.IsCancellationRequested || generation != reminderGeneration || !tray.HasReminder) return;
                tray.SetReminderFrames(frames);
                frames = null; // TrayController owns the frames now.
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (error is OutOfMemoryException || error is StackOverflowException) throw;
            }
            finally { if (frames != null) foreach (Icon frame in frames) frame.Dispose(); }
        }

        private void CancelAvatarLoad()
        {
            reminderGeneration++;
            if (avatarCancellation == null) return;
            avatarCancellation.Cancel();
            avatarCancellation.Dispose();
            avatarCancellation = null;
        }

        private void SetRemindersEnabled(bool enabled)
        {
            remindersEnabled = enabled;
            reminderMenu.Checked = enabled;
            tray.SetRemindersEnabled(enabled);
            if (!enabled) StopReminder();
            UpdateReminderStatus();
        }

        private void UpdateReminderStatus()
        {
            if (!remindersEnabled) reminderLabel.Text = "托盘提醒已关闭";
            else if (!notificationApiAvailable) reminderLabel.Text = "通知支持未就绪";
            else reminderLabel.Text = "等待官方网页通知";
        }

        private void StopReminder()
        {
            CancelAvatarLoad();
            tray.StopReminder();
            UpdateReminderStatus();
        }

        internal void TriggerTestNotification()
        {
            if (startWebView || exitRequested || IsDisposed) return;
            CancelAvatarLoad();
            tray.StartReminder(true);
            reminderLabel.Text = "本地测试提醒";
        }

        private void ShowReminderHelp()
        {
            MessageBox.Show(this,
                "最小化到托盘后，官方网页继续在本程序内运行。\r\n\r\n" +
                "新消息提醒取决于官方网页是否发送系统通知。请在网页请求通知权限时自行选择允许。网页没有发出通知时，本程序不会读取聊天内容来推断未读消息。\r\n\r\n" +
                "网页通知提供可用头像时会显示头像，否则使用应用图标；双击托盘图标或选择“停止本次提醒”可结束跳动。\r\n\r\n" +
                "“关闭托盘提醒”只关闭本程序的跳动提醒；网页已有通知权限由网页运行组件管理。",
                "托盘提醒说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void NavigateHome()
        {
            if (!startWebView) return;
            if (browser != null && browser.CoreWebView2 != null) browser.CoreWebView2.Navigate(BrowserPolicy.HomeUrl);
            else InitializeBrowser();
        }

        private void RefreshPage()
        {
            if (!startWebView) return;
            if (browser != null && browser.CoreWebView2 != null && !fallbackPanel.Visible) browser.CoreWebView2.Reload();
            else InitializeBrowser();
        }

        private void OpenInBrowser()
        {
            if (!startWebView) return;
            connectionLabel.Text = BrowserPolicy.TryOpenExternal(BrowserPolicy.HomeUrl)
                ? "已打开默认浏览器；该窗口不受本程序托盘控制"
                : "无法打开默认浏览器，请手动访问 home.soulapp.cn";
        }

        private void ShowFailure(string title, string explanation)
        {
            if (IsDisposed || exitRequested) return;
            fallbackTitle.Text = title;
            fallbackText.Text = explanation;
            fallbackPanel.Visible = true;
            fallbackPanel.BringToFront();
            connectionLabel.Text = title;
            if (!Visible) RestoreFromTray();
        }

        internal void RestoreFromTray()
        {
            if (exitRequested || IsDisposed) return;
            ShowInTaskbar = true;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            StopReminder();
        }

        private void MinimizeToTray()
        {
            if (exitRequested || IsDisposed) return;
            Hide();
            ShowInTaskbar = false;
            tray.ShowHiddenHint();
        }

        private void ExitApplication()
        {
            exitRequested = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (!closePromptOpen)
                {
                    closePromptOpen = true;
                    try
                    {
                        using (CloseChoiceDialog dialog = new CloseChoiceDialog())
                        {
                            dialog.ShowDialog(this);
                            if (dialog.Choice == CloseChoice.Tray) MinimizeToTray();
                            else if (dialog.Choice == CloseChoice.Exit) { exitRequested = true; e.Cancel = false; }
                        }
                    }
                    finally { closePromptOpen = false; }
                }
            }
            if (!e.Cancel)
            {
                exitRequested = true;
                browserGeneration++;
                CancelAvatarLoad();
                tray.Dispose();
            }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (ProgramActivation.ActivateMessage != 0 && m.Msg == ProgramActivation.ActivateMessage)
            {
                RestoreFromTray();
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private bool BeginSafe(Action action)
        {
            if (exitRequested || IsDisposed || !IsHandleCreated) return false;
            try { BeginInvoke(new Action(delegate { if (!IsDisposed && !exitRequested) action(); })); return true; }
            catch (InvalidOperationException) { return false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                exitRequested = true;
                browserGeneration++;
                CancelAvatarLoad();
                if (tray != null) tray.Dispose();
                if (browser != null) browser.Dispose();
                if (Icon != null) { Icon.Dispose(); Icon = null; }
            }
            base.Dispose(disposing);
        }
    }
}
