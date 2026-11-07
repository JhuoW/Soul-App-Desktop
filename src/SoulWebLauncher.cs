using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
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
        private readonly TableLayoutPanel headerLayout;
        private readonly ToolStrip tools;
        private readonly ToolStripButton homeButton;
        private readonly ToolStripButton refreshButton;
        private readonly ToolStripButton browserButton;
        private readonly ToolStripButton smallerButton;
        private readonly ToolStripButton largerButton;
        private readonly ToolStripDropDownButton zoomMenu;
        private readonly ToolStripMenuItem autoFitMenu;
        private readonly LoadingIndicator loadingIndicator;
        private readonly FlowLayoutPanel recoveryButtons;
        private readonly Button retryButton;
        private readonly System.Windows.Forms.Timer resizeTimer;
        private readonly System.Windows.Forms.Timer navigationTimer;
        private readonly Bitmap brandImage;
        private readonly Icon mainIcon;
        private readonly WindowPreferences preferences;
        private readonly string preferencesPath;
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
        private bool autoFitEnabled;
        private double manualZoom = 1.0;
        private double currentZoomFactor = 1.0;
        private bool navigationInProgress;
        private bool pageHasLoaded;
        private bool requiresBrowserRestart;
        private bool restoringPreferences;
        private bool preferencesSaved;
        private bool applyingZoom;
        private bool resourcesDisposed;
        private Rectangle normalBounds;
        private FormWindowState lastNonMinimizedState = FormWindowState.Normal;
        private ulong activeNavigationId;
        private string officialOrigin = "https://home.soulapp.cn";

        internal bool AutoFitEnabled { get { return autoFitEnabled; } }
        internal double CurrentZoomFactor { get { return currentZoomFactor; } }

        internal MainForm() : this(true) { }

        internal MainForm(bool startWebView)
        {
            this.startWebView = startWebView;
            Text = startWebView ? "Soul 桌面 · 官方网页" : "Soul 桌面 · 本地界面测试";
            mainIcon = AppIcons.LoadMainIcon();
            Icon = mainIcon;
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1180, 820);
            MinimumSize = new Size(720, 540);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 249, 250);
            SuspendLayout();
            brandImage = AppIcons.LoadImage();
            preferencesPath = startWebView && !String.IsNullOrEmpty(RuntimeFiles.DataDirectory)
                ? Path.Combine(Path.GetDirectoryName(RuntimeFiles.DataDirectory), "window-preferences.ini") : null;
            preferences = startWebView ? WindowPreferences.Load(preferencesPath) : new WindowPreferences();
            autoFitEnabled = preferences.AutoFit;
            manualZoom = preferences.ManualZoom;

            SmoothPanel header = new SmoothPanel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White, Padding = new Padding(16, 9, 12, 8) };
            headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                AutoSize = false, GrowStyle = TableLayoutPanelGrowStyle.FixedSize, Margin = new Padding(0), Padding = new Padding(0) };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            // A default AutoSize row measures the docked panels at their preferred height
            // and can exceed this compact header. Give the single row its actual viewport.
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Controls.Add(headerLayout);
            Panel identity = new SmoothPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            PictureBox brandPicture = new PictureBox { Image = brandImage, SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Left, Width = 42, Margin = new Padding(0) };
            identity.Controls.Add(brandPicture);
            Panel brandText = new SmoothPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0) };
            Label brand = new Label { Text = "Soul 桌面", ForeColor = Ink, Dock = DockStyle.Top, Height = 28,
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
            siteLabel = new Label { Text = "home.soulapp.cn · 非官方工具", ForeColor = Muted, Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 8.5F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = new Padding(0) };
            brandText.Controls.Add(siteLabel);
            brandText.Controls.Add(brand);
            identity.Controls.Add(brandText);
            brandText.BringToFront();
            headerLayout.Controls.Add(identity, 0, 0);
            tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Fill, AutoSize = false,
                CanOverflow = true, BackColor = Color.White, Font = new Font("Microsoft YaHei UI", 9.5F), Padding = new Padding(0),
                Margin = new Padding(0), Renderer = new DesktopToolStripRenderer(), ShowItemToolTips = true, Stretch = true };
            homeButton = MakeTool("首页", delegate { NavigateHome(); });
            homeButton.ToolTipText = "返回官方首页  Alt+Home";
            refreshButton = MakeTool("刷新", delegate { RefreshPage(); });
            refreshButton.ToolTipText = "刷新当前页面  F5 / Ctrl+R";
            browserButton = MakeTool("浏览器", delegate { OpenInBrowser(); });
            browserButton.ToolTipText = "在默认浏览器中打开官方网页";
            tools.Items.Add(homeButton);
            tools.Items.Add(refreshButton);
            tools.Items.Add(browserButton);
            tools.Items.Add(new ToolStripSeparator());
            smallerButton = MakeTool("−", delegate { ChangeZoom(-0.1); });
            smallerButton.ToolTipText = "缩小网页  Ctrl+−";
            largerButton = MakeTool("+", delegate { ChangeZoom(0.1); });
            largerButton.ToolTipText = "放大网页  Ctrl++";
            zoomMenu = new ToolStripDropDownButton("100%") { ToolTipText = "网页大小与窗口适配", Overflow = ToolStripItemOverflow.Never };
            ToolStripMenuItem resetZoom = new ToolStripMenuItem("100% · 默认大小") { ShortcutKeyDisplayString = "Ctrl+0", ToolTipText = "页面跟随窗口大小调整布局" };
            resetZoom.Click += delegate { ResetZoom(); };
            zoomMenu.DropDownItems.Add(resetZoom);
            autoFitMenu = new ToolStripMenuItem("适配桌面宽度") { Checked = autoFitEnabled };
            autoFitMenu.Click += delegate { SetAutoFit(!autoFitEnabled); };
            zoomMenu.DropDownItems.Add(autoFitMenu);
            zoomMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem zoomOut = new ToolStripMenuItem("缩小网页") { ShortcutKeyDisplayString = "Ctrl+−" };
            zoomOut.Click += delegate { ChangeZoom(-0.1); };
            zoomMenu.DropDownItems.Add(zoomOut);
            ToolStripMenuItem zoomIn = new ToolStripMenuItem("放大网页") { ShortcutKeyDisplayString = "Ctrl++" };
            zoomIn.Click += delegate { ChangeZoom(0.1); };
            zoomMenu.DropDownItems.Add(zoomIn);
            tools.Items.Add(smallerButton);
            tools.Items.Add(zoomMenu);
            tools.Items.Add(largerButton);
            tools.Items.Add(new ToolStripSeparator());
            ToolStripDropDownButton options = new ToolStripDropDownButton("设置") { ToolTipText = "托盘提醒和软件信息" };
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
            ToolStripButton trayButton = MakeTool("托盘", delegate { MinimizeToTray(); });
            trayButton.ToolTipText = "隐藏到系统托盘，网页继续运行";
            trayButton.Overflow = ToolStripItemOverflow.Never;
            tools.Items.Add(trayButton);
            headerLayout.Controls.Add(tools, 1, 0);

            StatusStrip footer = new StatusStrip { SizingGrip = false, BackColor = Color.FromArgb(238, 245, 246), Font = new Font("Microsoft YaHei UI", 9F) };
            connectionLabel = new ToolStripStatusLabel("正在准备官方网页…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted };
            reminderLabel = new ToolStripStatusLabel("等待网页通知") { ForeColor = Muted };
            footer.Items.Add(connectionLabel);
            footer.Items.Add(reminderLabel);
            loadingIndicator = new LoadingIndicator { Dock = DockStyle.Top };
            browserPanel = new SmoothPanel { Dock = DockStyle.Fill };
            fallbackPanel = new SmoothPanel { Dock = DockStyle.Fill, BackColor = BackColor, Padding = new Padding(36), AutoScroll = true };
            TableLayoutPanel fallbackLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            fallbackLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fallbackTitle = new Label { Text = startWebView ? "正在打开 Soul 官方网页" : "本地界面测试", Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                ForeColor = Ink, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 12, 0, 20) };
            fallbackLayout.Controls.Add(fallbackTitle);
            fallbackText = new Label { Text = startWebView ? "登录与聊天由 Soul 官方页面提供。\r\n首次使用时，需要在本窗口内完成官方登录。" : "此窗口用于验证关闭、托盘和恢复，不加载网页、不连接 Soul。",
                ForeColor = Muted, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 24) };
            fallbackLayout.Controls.Add(fallbackText);
            recoveryButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
            retryButton = MakeRecoveryButton("重试", delegate { RetryPage(); });
            recoveryButtons.Controls.Add(retryButton);
            recoveryButtons.Controls.Add(MakeRecoveryButton("用浏览器打开", delegate { OpenInBrowser(); }));
            recoveryButtons.Controls.Add(MakeRecoveryButton("获取网页运行组件", delegate { BrowserPolicy.TryOpenExternal(BrowserPolicy.RuntimeUrl); }));
            recoveryButtons.Visible = false;
            fallbackLayout.Controls.Add(recoveryButtons);
            fallbackPanel.Controls.Add(fallbackLayout);
            browserPanel.Controls.Add(fallbackPanel);
            Controls.Add(browserPanel);
            Controls.Add(footer);
            Controls.Add(loadingIndicator);
            Controls.Add(header);

            resizeTimer = new System.Windows.Forms.Timer { Interval = 100 };
            resizeTimer.Tick += delegate { resizeTimer.Stop(); ApplyZoom(); };
            navigationTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            navigationTimer.Tick += delegate
            {
                navigationTimer.Stop();
                if (navigationInProgress) ShowFailure("网页加载时间较长", "网络连接较慢，可以稍候或重试。");
            };
            browserPanel.SizeChanged += delegate { OnViewportSizeChanged(); };
            ResizeEnd += delegate { resizeTimer.Stop(); ApplyZoom(); RememberNormalBounds(); };
            LocationChanged += delegate { RememberNormalBounds(); };
            SizeChanged += delegate { RememberNormalBounds(); };

            tray = new TrayController();
            tray.OpenRequested += delegate { RestoreFromTray(); };
            tray.HomeRequested += delegate { RestoreFromTray(); NavigateHome(); };
            tray.RefreshRequested += delegate { RefreshPage(); };
            tray.ReminderToggleRequested += delegate { SetRemindersEnabled(!remindersEnabled); };
            tray.StopReminderRequested += delegate { StopReminder(); };
            tray.ExitRequested += delegate { ExitApplication(); };
            Load += delegate { ApplyWindowPreferences(); };
            Shown += delegate
            {
                UpdateResponsiveHeader();
                ApplyZoom();
                if (startWebView) InitializeBrowser();
                else { connectionLabel.Text = "本地界面测试 · 未连接任何网页"; UpdateActionState(); }
            };
            Activated += delegate { if (Visible && WindowState != FormWindowState.Minimized) StopReminder(); };
            ResumeLayout(true);
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

        private void UpdateResponsiveHeader()
        {
            if (headerLayout == null || tools == null) return;
            float scale = Math.Max(1F, DeviceDpi / 96F);
            bool compact = ClientSize.Width / scale < 940;
            headerLayout.ColumnStyles[0].Width = (compact ? 174 : 240) * scale;
            siteLabel.Text = compact ? "非官方桌面工具" : officialOrigin.Replace("https://", "") + " · 非官方工具";
            browserButton.Overflow = compact ? ToolStripItemOverflow.Always : ToolStripItemOverflow.AsNeeded;
            smallerButton.Overflow = compact ? ToolStripItemOverflow.Always : ToolStripItemOverflow.AsNeeded;
            largerButton.Overflow = compact ? ToolStripItemOverflow.Always : ToolStripItemOverflow.AsNeeded;
            if (fallbackPanel != null) fallbackPanel.Padding = new Padding((int)((compact ? 24 : 36) * scale));
        }

        private void OnViewportSizeChanged()
        {
            if (resizeTimer == null || exitRequested || IsDisposed || WindowState == FormWindowState.Minimized) return;
            UpdateResponsiveHeader();
            // WinForms resizes the native WebView directly through Dock.Fill. Only optional
            // desktop-fit zoom is debounced; neither path reloads the page or changes its DOM.
            if (autoFitEnabled) { resizeTimer.Stop(); resizeTimer.Start(); }
        }

        internal void SetAutoFit(bool enabled)
        {
            autoFitEnabled = enabled;
            autoFitMenu.Checked = enabled;
            resizeTimer.Stop();
            ApplyZoom();
        }

        internal void SetManualZoom(double factor)
        {
            autoFitEnabled = false;
            autoFitMenu.Checked = false;
            manualZoom = DesktopUi.ClampManualZoom(factor);
            resizeTimer.Stop();
            ApplyZoom();
        }

        internal void ChangeZoom(double delta)
        {
            if (Double.IsNaN(delta) || Double.IsInfinity(delta)) return;
            SetManualZoom(Math.Round((currentZoomFactor + delta) * 10.0) / 10.0);
        }

        internal void ResetZoom() { SetManualZoom(1.0); }

        private void ApplyZoom()
        {
            if (exitRequested || IsDisposed || browserPanel == null) return;
            double desired = autoFitEnabled ? DesktopUi.CalculateAutoFitZoom(browserPanel.ClientSize.Width, DeviceDpi) : manualZoom;
            currentZoomFactor = desired;
            zoomMenu.Text = (autoFitEnabled ? "适配 " : "") + Math.Round(desired * 100).ToString(CultureInfo.InvariantCulture) + "%";
            autoFitMenu.Checked = autoFitEnabled;
            smallerButton.Enabled = desired > 0.5001;
            largerButton.Enabled = desired < 1.4999;
            if (browser == null || browser.CoreWebView2 == null) return;
            try
            {
                if (Math.Abs(browser.ZoomFactor - desired) < 0.005) return;
                applyingZoom = true;
                browser.ZoomFactor = desired;
            }
            catch (InvalidOperationException) { }
            catch (COMException) { }
            finally { applyingZoom = false; }
        }

        private void OnBrowserZoomFactorChanged(object sender, object e)
        {
            if (applyingZoom || navigationInProgress || initializationInProgress || browser == null || browser.CoreWebView2 == null) return;
            double changed = browser.ZoomFactor;
            if (Math.Abs(changed - currentZoomFactor) < 0.005) return;
            // Ctrl+mouse wheel is a deliberate user zoom; preserve it as manual mode.
            BeginSafe(delegate { SetManualZoom(changed); });
        }

        private Action GetShortcutAction(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            if ((key == Keys.F5 && modifiers == Keys.None) || (key == Keys.R && modifiers == Keys.Control)) return RefreshPage;
            if (key == Keys.Home && modifiers == Keys.Alt) return NavigateHome;
            if (key == Keys.D0 && modifiers == Keys.Control) return ResetZoom;
            if (key == Keys.NumPad0 && modifiers == Keys.Control) return ResetZoom;
            if ((modifiers == Keys.Control || modifiers == (Keys.Control | Keys.Shift)) && (key == Keys.Oemplus || key == Keys.Add))
                return delegate { ChangeZoom(0.1); };
            if (modifiers == Keys.Control && (key == Keys.OemMinus || key == Keys.Subtract))
                return delegate { ChangeZoom(-0.1); };
            return null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Action action = GetShortcutAction(keyData);
            if (action == null) return base.ProcessCmdKey(ref msg, keyData);
            BeginSafe(action);
            return true;
        }

        private void OnBrowserKeyDown(object sender, KeyEventArgs e)
        {
            // WebView2 WinForms forwards its accelerator callbacks as standard key events.
            Action action = GetShortcutAction(e.KeyData);
            if (action == null) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            BeginSafe(action);
        }

        private void ApplyWindowPreferences()
        {
            if (!startWebView) { normalBounds = Bounds; return; }
            restoringPreferences = true;
            try
            {
                List<Rectangle> areas = new List<Rectangle>();
                if (Screen.PrimaryScreen != null) areas.Add(Screen.PrimaryScreen.WorkingArea);
                foreach (Screen screen in Screen.AllScreens) if (!screen.Primary) areas.Add(screen.WorkingArea);
                Rectangle restored = WindowPreferences.GetVisibleBounds(preferences.NormalBounds, areas.ToArray(), MinimumSize, Size);
                Rectangle workArea = Screen.FromRectangle(restored).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, workArea.Width), Math.Min(MinimumSize.Height, workArea.Height));
                StartPosition = FormStartPosition.Manual;
                Bounds = restored;
                normalBounds = restored;
                if (preferences.Maximized) WindowState = FormWindowState.Maximized;
                lastNonMinimizedState = WindowState;
            }
            finally { restoringPreferences = false; }
        }

        private void RememberNormalBounds()
        {
            if (restoringPreferences || !IsHandleCreated || !Visible || WindowState == FormWindowState.Minimized) return;
            lastNonMinimizedState = WindowState;
            if (WindowState == FormWindowState.Normal) normalBounds = Bounds;
        }

        private void SaveWindowPreferences()
        {
            if (!startWebView || preferencesSaved || preferencesPath == null) return;
            preferencesSaved = true;
            preferences.NormalBounds = normalBounds.Width > 0 && normalBounds.Height > 0 ? normalBounds : RestoreBounds;
            preferences.Maximized = lastNonMinimizedState == FormWindowState.Maximized;
            preferences.AutoFit = autoFitEnabled;
            preferences.ManualZoom = manualZoom;
            preferences.Save(preferencesPath);
        }

        private void UpdateActionState()
        {
            bool available = startWebView && browser != null && browser.CoreWebView2 != null && !initializationInProgress && !navigationInProgress;
            homeButton.Enabled = available;
            refreshButton.Enabled = available;
            retryButton.Enabled = startWebView && !initializationInProgress;
        }

        private void SetPageLoading(bool loading)
        {
            navigationInProgress = loading;
            loadingIndicator.SetLoading(loading);
            if (loading) { navigationTimer.Stop(); navigationTimer.Start(); }
            else navigationTimer.Stop();
            UpdateActionState();
        }

        private async void InitializeBrowser()
        {
            if (!startWebView || initializationInProgress || exitRequested || IsDisposed) return;
            initializationInProgress = true;
            int generation = ++browserGeneration;
            pageHasLoaded = false;
            requiresBrowserRestart = false;
            SetPageLoading(true);
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
                recoveryButtons.Visible = false;
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
                current.KeyDown += OnBrowserKeyDown;
                current.ZoomFactorChanged += OnBrowserZoomFactorChanged;
                core.WindowCloseRequested += delegate { BeginSafe(delegate { Close(); }); };
                notificationApiAvailable = false;
                try { core.NotificationReceived += OnNotificationReceived; notificationApiAvailable = true; }
                catch (NotImplementedException) { }
                catch (COMException) { }
                UpdateReminderStatus();
                ApplyZoom();
                core.Navigate(BrowserPolicy.HomeUrl);
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
            finally
            {
                initializationInProgress = false;
                if (!IsDisposed && !exitRequested) UpdateActionState();
            }
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (BrowserPolicy.IsOfficialUrl(e.Uri))
            {
                activeNavigationId = e.NavigationId;
                SetPageLoading(true);
                connectionLabel.Text = "正在加载官方网页…";
                if (!pageHasLoaded)
                {
                    fallbackTitle.Text = "正在打开 Soul 官方网页";
                    fallbackText.Text = "登录与聊天由 Soul 官方页面提供。\r\n首次使用请在本窗口完成官方登录。";
                    recoveryButtons.Visible = false;
                    fallbackPanel.Visible = true;
                    fallbackPanel.BringToFront();
                }
                return;
            }
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
            if (e.NavigationId != activeNavigationId || exitRequested || IsDisposed) return;
            SetPageLoading(false);
            if (e.IsSuccess && e.HttpStatusCode < 400)
            {
                pageHasLoaded = true;
                requiresBrowserRestart = false;
                fallbackPanel.Visible = false;
                browser.BringToFront();
                connectionLabel.Text = "官方网页已加载 · 登录状态由网页管理";
                ApplyZoom();
            }
            else if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                ShowFailure("网页暂时无法加载", "请检查网络后重试，或用默认浏览器打开官方网页。");
            else if (!pageHasLoaded)
                ShowFailure("页面加载已取消", "可以重试，或用默认浏览器打开官方网页。");
        }

        private void OnSourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (browser == null || browser.CoreWebView2 == null) return;
            try { officialOrigin = BrowserPolicy.GetOfficialOrigin(browser.CoreWebView2.Source) ?? "https://home.soulapp.cn"; }
            catch (COMException) { officialOrigin = "https://home.soulapp.cn"; }
            catch (InvalidOperationException) { officialOrigin = "https://home.soulapp.cn"; }
            UpdateResponsiveHeader();
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
            if (e.ProcessFailedKind != CoreWebView2ProcessFailedKind.BrowserProcessExited &&
                e.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessExited &&
                e.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessUnresponsive) return;
            int failedGeneration = browserGeneration;
            BeginSafe(delegate
            {
                if (failedGeneration != browserGeneration) return;
                requiresBrowserRestart = true;
                ShowFailure("网页进程暂时不可用", "点击重试重新打开官方网页。\r\n也可以改用默认浏览器。");
            });
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
            if (!startWebView || initializationInProgress || navigationInProgress) return;
            if (browser == null || browser.CoreWebView2 == null || requiresBrowserRestart) { InitializeBrowser(); return; }
            try { browser.CoreWebView2.Navigate(BrowserPolicy.HomeUrl); }
            catch (InvalidOperationException) { requiresBrowserRestart = true; InitializeBrowser(); }
            catch (COMException) { requiresBrowserRestart = true; InitializeBrowser(); }
        }

        private void RefreshPage()
        {
            if (!startWebView || initializationInProgress || navigationInProgress) return;
            if (browser == null || browser.CoreWebView2 == null || requiresBrowserRestart) { InitializeBrowser(); return; }
            try { browser.CoreWebView2.Reload(); }
            catch (InvalidOperationException) { requiresBrowserRestart = true; InitializeBrowser(); }
            catch (COMException) { requiresBrowserRestart = true; InitializeBrowser(); }
        }

        private void RetryPage()
        {
            if (!startWebView || initializationInProgress) return;
            SetPageLoading(false);
            RefreshPage();
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
            recoveryButtons.Visible = startWebView;
            connectionLabel.Text = title;
            SetPageLoading(false);
        }

        internal void RestoreFromTray()
        {
            if (exitRequested || IsDisposed) return;
            ShowInTaskbar = true;
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = lastNonMinimizedState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
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
                SaveWindowPreferences();
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
            bool releaseOwnedImages = disposing && !resourcesDisposed;
            if (releaseOwnedImages)
            {
                resourcesDisposed = true;
                exitRequested = true;
                browserGeneration++;
                CancelAvatarLoad();
                if (tray != null) tray.Dispose();
                if (resizeTimer != null) resizeTimer.Dispose();
                if (navigationTimer != null) navigationTimer.Dispose();
                if (browser != null) browser.Dispose();
            }
            try { base.Dispose(disposing); }
            finally
            {
                // Controls must release their native handles before their images are freed.
                // Form.Icon can return a shared default after disposal; only dispose our own icon.
                if (releaseOwnedImages)
                {
                    if (brandImage != null) brandImage.Dispose();
                    if (mainIcon != null) mainIcon.Dispose();
                }
            }
        }
    }
}
