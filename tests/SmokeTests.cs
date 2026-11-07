// Local-only tests against the compiled product assembly. No website is loaded.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

internal static class SmokeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Assembly product;
    private static int assertions;
    private static string isolatedRoot;
    private static System.Threading.Timer watchdog;
    private static string currentStage = "startup";

    [STAThread]
    private static int Main(string[] args)
    {
        bool uiLaunch = args.Length == 3 && args[2] == "--ui-launch";
        if (args.Length != 2 && !uiLaunch)
        {
            Console.Error.WriteLine("Usage: SmokeTests.exe <product-exe> <isolated-test-directory> [--ui-launch]");
            return 2;
        }
        try
        {
            if (!uiLaunch)
            {
                // Let test failures in window callbacks reach the console runner instead
                // of opening WinForms' modal exception dialog and hiding the failure.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                watchdog = new System.Threading.Timer(delegate
                {
                    Console.Error.WriteLine("FAIL: offline test stage exceeded 30 seconds: " + currentStage);
                    Environment.Exit(3);
                }, null, 30000, Timeout.Infinite);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            product = Assembly.LoadFile(Path.GetFullPath(args[0]));
            isolatedRoot = Path.GetFullPath(args[1]);
            SetStage("isolated runtime");
            CheckRuntime(isolatedRoot);
            if (uiLaunch)
            {
                Console.WriteLine("Local UI test window only. No WebView2 browser or Soul connection is created.");
                using (Form localWindow = (Form)Activator.CreateInstance(ProductType("MainForm"), InstanceFlags, null, new object[] { false }, null))
                    Application.Run(localWindow);
                return 0;
            }
            SetStage("icons"); CheckIcons();
            SetStage("browser policy"); CheckBrowserPolicy();
            SetStage("close choices"); CheckCloseDialog();
            SetStage("local window lifecycle"); CheckLocalWindowLifecycle();
            SetStage("responsive layout and zoom"); CheckResponsiveLayoutAndZoom();
            SetStage("window preferences"); CheckWindowPreferences();
            Console.WriteLine("PASS: " + assertions + " assertions. No website, login or remote messaging was used.");
            return 0;
        }
        catch (Exception error)
        {
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            Console.Error.WriteLine("FAIL: " + error.GetType().Name + ": " + error.Message);
            Console.Error.WriteLine(error.StackTrace);
            return 1;
        }
        finally { if (watchdog != null) watchdog.Dispose(); }
    }

    private static void CheckRuntime(string root)
    {
        Directory.CreateDirectory(root);
        Type runtime = ProductType("RuntimeFiles");
        CallStatic(runtime, "Initialize", new Type[] { typeof(string) }, new object[] { root });
        string data = (string)GetStaticProperty(runtime, "DataDirectory");
        string loaderDirectory = (string)GetStaticProperty(runtime, "LoaderDirectory");
        Assert(data == Path.Combine(root, "WebView2"), "Runtime uses the isolated profile path.");
        Assert(loaderDirectory.StartsWith(Path.Combine(root, "Runtime") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "The native loader is extracted under the isolated root.");
        string loader = Path.Combine(loaderDirectory, "WebView2Loader.dll");
        Assert(File.Exists(loader), "Packaged native loader is present.");
        byte[] bytes = File.ReadAllBytes(loader);
        Assert(bytes.Length > 1024 && bytes[0] == 'M' && bytes[1] == 'Z', "Packaged loader is a Windows binary.");
        Assert(loaderDirectory.EndsWith(Hash(bytes).Substring(0, 16), StringComparison.Ordinal), "Loader directory includes its content hash.");
        Assert(!Directory.Exists(data), "Runtime initialization does not create a browser profile or start a browser.");
        CallStatic(runtime, "ConfigureWebViewLoader", Type.EmptyTypes, new object[0]);
        Assembly core = Assembly.Load("Microsoft.Web.WebView2.Core");
        Assembly winForms = Assembly.Load("Microsoft.Web.WebView2.WinForms");
        Assert(core.GetName().Name == "Microsoft.Web.WebView2.Core", "Embedded Core assembly resolves.");
        Assert(winForms.GetName().Name == "Microsoft.Web.WebView2.WinForms", "Embedded WinForms assembly resolves.");
        string notices = (string)CallStatic(runtime, "ReadThirdPartyNotices", Type.EmptyTypes, new object[0]);
        Assert(!String.IsNullOrWhiteSpace(notices) && notices.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0,
            "Packaged third-party notices are available.");
        Console.WriteLine("PASS: isolated runtime extraction and embedded dependencies.");
    }

    private static void CheckIcons()
    {
        Type icons = ProductType("AppIcons");
        using (Stream stream = product.GetManifestResourceStream("SoulWebLauncher.AppIcon.ico"))
        {
            Assert(stream != null, "ICO is embedded.");
            using (BinaryReader reader = new BinaryReader(stream))
            {
                Assert(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1, "ICO header is valid.");
                int count = reader.ReadUInt16();
                Assert(count == 9, "ICO contains nine resolutions.");
                int[] expected = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
                for (int i = 0; i < count; i++)
                {
                    int width = reader.ReadByte();
                    int height = reader.ReadByte();
                    if (width == 0) width = 256;
                    if (height == 0) height = 256;
                    reader.ReadByte(); reader.ReadByte();
                    reader.ReadUInt16();
                    int bitDepth = reader.ReadUInt16();
                    uint length = reader.ReadUInt32();
                    uint offset = reader.ReadUInt32();
                    Assert(width == expected[i] && height == expected[i] && bitDepth == 32 &&
                        length > 0 && (long)offset + length <= stream.Length, "Valid " + expected[i] + " px icon entry.");
                }
            }
        }
        using (Icon main = (Icon)CallStatic(icons, "LoadMainIcon", Type.EmptyTypes, new object[0]))
        using (Icon tray = (Icon)CallStatic(icons, "LoadTrayIcon", Type.EmptyTypes, new object[0]))
        using (Bitmap image = (Bitmap)CallStatic(icons, "LoadImage", Type.EmptyTypes, new object[0]))
        {
            Assert(main.Width >= 128, "A high-resolution window icon loads.");
            Assert(tray.Width == 32 && tray.Height == 32, "Tray base icon is 32 px.");
            Assert(image.Width == 512 && image.Height == 512, "The embedded PNG is 512 px.");
        }
        Icon[] normal = (Icon[])CallStatic(icons, "CreateTrayFrames", Type.EmptyTypes, new object[0]);
        try
        {
            CheckFrames(normal, "Application reminder");
            Assert(IconHash(normal[0]) != IconHash(normal[1]), "The reminder visibly moves between frames.");
            Assert(IconHash(normal[1]) == IconHash(normal[3]), "The four-frame bounce returns through the same midpoint.");
        }
        finally { DisposeFrames(normal); }
        Bitmap avatar = new Bitmap(80, 60);
        using (Graphics graphics = Graphics.FromImage(avatar)) graphics.Clear(Color.Gold);
        Icon[] avatarFrames = (Icon[])CallStatic(icons, "CreateTrayFrames", new Type[] { typeof(Image) }, new object[] { avatar });
        avatar.Dispose(); // Returned icons must remain independent from the supplied avatar.
        try { CheckFrames(avatarFrames, "Independent avatar reminder"); }
        finally { DisposeFrames(avatarFrames); }
        Icon[] fallback = (Icon[])CallStatic(icons, "CreateTrayFrames", new Type[] { typeof(Image) }, new object[] { avatar });
        try { CheckFrames(fallback, "Disposed-avatar fallback"); }
        finally { DisposeFrames(fallback); }
        Console.WriteLine("PASS: high-resolution resources and independent reminder frames.");
    }

    private static void CheckBrowserPolicy()
    {
        Type policy = ProductType("BrowserPolicy");
        string[] official = { "https://soulapp.cn/", "https://home.soulapp.cn/login", "https://THEMIS.SOULAPP.CN/policy", "https://home.soulapp.cn:443/" };
        foreach (string value in official) Assert(CheckUrl(policy, "IsOfficialUrl", value), "Official origin accepted: " + value);
        string[] rejected = { null, "", "http://home.soulapp.cn/", "https://soulapp.cn.evil.example/", "https://not-soulapp.cn/",
            "https://soulapp.cn@evil.example/", "https://evil.example@soulapp.cn/", "https://home.soulapp.cn:444/",
            "file:///C:/Windows/test.html", "javascript:alert(1)", "data:text/html,hello", "https://127.0.0.1/", "https://s\u043eulapp.cn/" };
        foreach (string value in rejected) Assert(!CheckUrl(policy, "IsOfficialUrl", value), "Nonofficial or unsafe origin rejected: " + (value ?? "<null>"));
        Assert(CheckUrl(policy, "IsSafeHttpsUrl", "https://example.com/page"), "Ordinary HTTPS can be offered to the external browser.");
        Assert(!CheckUrl(policy, "IsSafeHttpsUrl", "https://user:secret@example.com/"), "Credential-bearing external URLs are rejected.");
        Assert(!CheckUrl(policy, "IsSafeHttpsUrl", "https://example.com/" + new string('x', 8200)), "Oversized URLs are rejected.");
        string origin = (string)CallStatic(policy, "GetOfficialOrigin", new Type[] { typeof(string) }, new object[] { "https://home.soulapp.cn/login?source=test#section" });
        Assert(origin == "https://home.soulapp.cn", "Displayed official origin excludes the path, query and fragment.");
        Assert(CallStatic(policy, "GetOfficialOrigin", new Type[] { typeof(string) }, new object[] { "https://evil.example/" }) == null,
            "A nonofficial origin is not displayed as official.");
        Console.WriteLine("PASS: official-origin boundary and dangerous URL rejection.");
    }

    private static void CheckCloseDialog()
    {
        using (Form dialog = (Form)Activator.CreateInstance(ProductType("CloseChoiceDialog"), InstanceFlags, null, new object[0], null))
        {
            Assert(GetProperty(dialog, "Choice").ToString() == "Cancel", "Closing the prompt without a choice defaults to cancellation.");
            Control accept = dialog.AcceptButton as Control;
            Control cancel = dialog.CancelButton as Control;
            Assert(accept != null && accept.Text == "最小化到托盘", "Enter defaults to minimizing to tray.");
            Assert(cancel != null && cancel.Text == "取消", "Escape has an explicit Cancel button.");
            Assert(dialog.CancelButton.DialogResult == DialogResult.Cancel, "The cancel button preserves cancellation semantics.");
            bool foundExit = false;
            foreach (Control control in dialog.Controls) if (control is Button && control.Text == "直接退出") foundExit = true;
            Assert(foundExit, "The direct-exit choice is present.");
        }
        Console.WriteLine("PASS: close-choice defaults and labels.");
    }

    private static void CheckLocalWindowLifecycle()
    {
        Form window = (Form)Activator.CreateInstance(ProductType("MainForm"), InstanceFlags, null, new object[] { false }, null);
        object tray = GetField(window, "tray");
        NotifyIcon notificationIcon = (NotifyIcon)GetField(tray, "notifyIcon");
        System.Windows.Forms.Timer timer = (System.Windows.Forms.Timer)GetField(tray, "animationTimer");
        try
        {
            Assert(!(bool)GetField(window, "startWebView"), "The test window is explicitly offline.");
            window.Show();
            Application.DoEvents();
            Assert(window.Visible && window.ShowInTaskbar, "The local window opens normally.");
            Assert(GetField(window, "browser") == null, "Showing the test window does not create WebView2.");
            Assert(notificationIcon.Visible, "The notification-area icon is present.");
            Assert(MenuContains(notificationIcon.ContextMenuStrip, "打开聊天窗口") && MenuContains(notificationIcon.ContextMenuStrip, "停止本次提醒") &&
                MenuContains(notificationIcon.ContextMenuStrip, "退出"), "The tray menu contains restore, stop and exit actions.");
            CallInstance(window, "MinimizeToTray");
            Assert(!window.Visible && !window.ShowInTaskbar && notificationIcon.Visible, "Minimizing hides the window and keeps the tray icon.");
            CallInstance(window, "TriggerTestNotification");
            Assert((bool)GetProperty(tray, "HasReminder") && timer.Enabled, "The local notification starts animation.");
            Assert(notificationIcon.Text.IndexOf("本地测试", StringComparison.Ordinal) >= 0, "The test reminder is labelled as a local test.");
            Stopwatch watch = Stopwatch.StartNew();
            while ((int)GetField(tray, "animationFrame") == 0 && watch.ElapsedMilliseconds < 1000)
            { Application.DoEvents(); Thread.Sleep(10); }
            Assert((int)GetField(tray, "animationFrame") != 0, "The reminder timer advances the frame.");
            CallInstance(window, "RestoreFromTray");
            Application.DoEvents();
            Assert(window.Visible && window.ShowInTaskbar && window.WindowState != FormWindowState.Minimized, "Restoring makes the chat window available.");
            Assert(!(bool)GetProperty(tray, "HasReminder") && !timer.Enabled, "Restoring stops the current reminder.");
            CallInstance(window, "MinimizeToTray");
            CallInstance(window, "TriggerTestNotification");
            CallInstance(window, "SetRemindersEnabled", new object[] { false });
            Assert(!(bool)GetProperty(tray, "HasReminder") && !timer.Enabled, "Disabling reminders stops the current animation.");
            Assert(!((ToolStripMenuItem)GetField(tray, "enableItem")).Checked, "Tray toggle reflects disabled reminders.");
            CallInstance(window, "SetRemindersEnabled", new object[] { true });
            CallInstance(window, "TriggerTestNotification");
            CallInstance(window, "ExitApplication");
            Application.DoEvents();
            Assert(window.IsDisposed && !window.Visible, "Explicit exit closes and disposes the window.");
            Assert((bool)GetField(tray, "disposed") && !notificationIcon.Visible && !timer.Enabled, "Exit removes the tray icon and stops its timer.");
        }
        finally { window.Dispose(); Application.DoEvents(); }
        Console.WriteLine("PASS: local window -> tray -> reminder -> restore -> explicit exit.");
    }

    private static bool MenuContains(ContextMenuStrip menu, string label)
    {
        foreach (ToolStripItem item in menu.Items) if (item.Text == label) return true;
        return false;
    }

    private static void CheckResponsiveLayoutAndZoom()
    {
        Type desktopUi = ProductType("DesktopUi");
        double previous = 0;
        foreach (int width in new int[] { 400, 720, 1080, 1600, 2600 })
        {
            double zoom = (double)CallStatic(desktopUi, "CalculateAutoFitZoom", new Type[] { typeof(int), typeof(float) }, new object[] { width, 96F });
            Assert(IsFinite(zoom) && zoom >= 0.6 && zoom <= 1 && zoom >= previous,
                "Optional desktop-fit zoom is bounded and does not shrink as width grows.");
            previous = zoom;
        }
        Assert(Near(previous, 1), "Wide desktop-fit mode does not enlarge content above 100%.");
        double normalDpi = (double)CallStatic(desktopUi, "CalculateAutoFitZoom", new Type[] { typeof(int), typeof(float) }, new object[] { 800, 96F });
        double doubleDpi = (double)CallStatic(desktopUi, "CalculateAutoFitZoom", new Type[] { typeof(int), typeof(float) }, new object[] { 1600, 192F });
        Assert(Near(normalDpi, doubleDpi), "Equivalent logical widths have equivalent optional-fit zoom at different DPI.");

        SetStage("creating responsive test window");
        Form window = (Form)Activator.CreateInstance(ProductType("MainForm"), InstanceFlags, null, new object[] { false }, null);
        try
        {
            window.Text = "Soul 桌面 · 自动离线布局测试";
            SetStage("showing responsive test window");
            window.Show();
            Application.DoEvents();
            Assert(!(bool)GetProperty(window, "AutoFitEnabled") && Near((double)GetProperty(window, "CurrentZoomFactor"), 1),
                "The default is native website responsiveness at 100%, with desktop-fit disabled.");
            Size[] sizes = { new Size(760, 620), new Size(1100, 780), new Size(1540, 940) };
            foreach (Size size in sizes)
            {
                SetStage("resizing offline window to " + size.Width + "x" + size.Height);
                window.ClientSize = size;
                window.PerformLayout();
                Application.DoEvents();
                DumpHeaderLayout(window);
                CheckRemainingBrowserArea(window);
                CheckReachableTools(window);
                Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 1), "Native mode keeps 100% while the window resizes.");
                Assert(GetField(window, "browser") == null, "Layout changes do not create an online browser in the local test window.");
            }

            SetStage("minimum window dimensions");
            window.Size = window.MinimumSize;
            window.PerformLayout();
            Application.DoEvents();
            DumpHeaderLayout(window);
            CheckRemainingBrowserArea(window);
            CheckReachableTools(window);
            Assert(window.Width == window.MinimumSize.Width && window.Height == window.MinimumSize.Height,
                "The minimum window size itself remains usable.");

            SetStage("fit: resizing back to narrow viewport");
            window.ClientSize = sizes[0];
            Application.DoEvents();
            SetStage("fit: enabling optional fit");
            CallInstance(window, "SetAutoFit", new object[] { true });
            SetStage("fit: waiting for initial narrow factor");
            WaitForFit(window, "Optional fit reaches the current viewport scale.");
            double firstFit = (double)GetProperty(window, "CurrentZoomFactor");
            Assert((bool)GetProperty(window, "AutoFitEnabled") && firstFit >= 0.6 && firstFit <= 1, "Optional fit mode can be enabled explicitly.");
            SetStage("fit: repeating fit selection");
            for (int i = 0; i < 4; i++) CallInstance(window, "SetAutoFit", new object[] { true });
            Assert(Near(firstFit, (double)GetProperty(window, "CurrentZoomFactor")), "Repeated fit selection does not multiply the zoom factor.");
            SetStage("fit: resizing to wide viewport");
            window.ClientSize = sizes[2];
            Application.DoEvents();
            SetStage("fit: waiting for wide factor");
            WaitForFit(window, "Deferred resize updates optional fit for the wide viewport.");
            double wideFit = (double)GetProperty(window, "CurrentZoomFactor");
            SetStage("fit: resizing back to narrow after wide");
            window.ClientSize = sizes[0];
            Application.DoEvents();
            SetStage("fit: waiting for restored narrow factor");
            WaitForFit(window, "Deferred resize updates optional fit for the narrow viewport.");
            Assert(wideFit >= firstFit && Near(firstFit, (double)GetProperty(window, "CurrentZoomFactor")),
                "Narrow-wide-narrow resizing restores the same optional-fit scale.");

            SetStage("manual zoom: setting 125 percent");
            CallInstance(window, "SetManualZoom", new object[] { 1.25 });
            Assert(!(bool)GetProperty(window, "AutoFitEnabled") && Near((double)GetProperty(window, "CurrentZoomFactor"), 1.25),
                "Manual zoom exits fit mode and uses the selected factor.");
            SetStage("manual zoom: resizing to wide");
            window.ClientSize = sizes[2];
            Application.DoEvents();
            Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 1.25), "Resizing does not override a manual zoom selection.");
            SetStage("manual zoom: adjustment and boundaries");
            CallInstance(window, "ChangeZoom", new object[] { -0.25 });
            Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 1), "A manual zoom adjustment applies once.");
            CallInstance(window, "SetManualZoom", new object[] { -1000.0 });
            Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 0.5), "Very small manual zoom is clamped to 50%.");
            CallInstance(window, "SetManualZoom", new object[] { 1000.0 });
            Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 1.5), "Very large manual zoom is clamped to 150%.");
            SetStage("manual zoom: nonfinite inputs");
            CallInstance(window, "SetManualZoom", new object[] { Double.NaN });
            Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 1), "NaN resets to 100% instead of propagating to the browser.");
            CallInstance(window, "SetManualZoom", new object[] { Double.PositiveInfinity });
            Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), 1), "Infinity resets to 100% instead of propagating to the browser.");
            SetStage("manual zoom: reset to native mode");
            CallInstance(window, "SetAutoFit", new object[] { true });
            CallInstance(window, "ResetZoom");
            Assert(!(bool)GetProperty(window, "AutoFitEnabled") && Near((double)GetProperty(window, "CurrentZoomFactor"), 1),
                "Reset zoom returns to native 100% layout rather than re-enabling desktop-fit.");
            SetStage("calling responsive window ExitApplication");
            CallInstance(window, "ExitApplication");
            SetStage("pumping after responsive window exit");
            Application.DoEvents();
            Assert(window.IsDisposed, "The responsive test window disposes normally.");
        }
        finally
        {
            SetStage("final responsive window Dispose");
            window.Dispose();
            SetStage("final responsive window message pump");
            Application.DoEvents();
        }
        Console.WriteLine("PASS: narrow/normal/wide layout, native responsive mode, optional fit and bounded manual zoom.");
    }

    private static void CheckRemainingBrowserArea(Form window)
    {
        Control browserPanel = (Control)GetField(window, "browserPanel");
        Control parent = browserPanel.Parent;
        Assert(browserPanel.Dock == DockStyle.Fill && browserPanel.Width > 100 && browserPanel.Height > 100,
            "The browser area remains a substantial fill region.");
        Rectangle expected = parent.ClientRectangle;
        expected = Rectangle.FromLTRB(expected.Left + parent.Padding.Left, expected.Top + parent.Padding.Top,
            expected.Right - parent.Padding.Right, expected.Bottom - parent.Padding.Bottom);
        TableLayoutPanel table = parent as TableLayoutPanel;
        if (table != null)
        {
            TableLayoutPanelCellPosition cell = table.GetCellPosition(browserPanel);
            int[] widths = table.GetColumnWidths();
            int[] heights = table.GetRowHeights();
            int x = parent.Padding.Left;
            int y = parent.Padding.Top;
            for (int i = 0; i < cell.Column; i++) x += widths[i];
            for (int i = 0; i < cell.Row; i++) y += heights[i];
            int width = 0, height = 0;
            for (int i = cell.Column; i < cell.Column + table.GetColumnSpan(browserPanel); i++) width += widths[i];
            for (int i = cell.Row; i < cell.Row + table.GetRowSpan(browserPanel); i++) height += heights[i];
            expected = Rectangle.FromLTRB(x + browserPanel.Margin.Left, y + browserPanel.Margin.Top,
                x + width - browserPanel.Margin.Right, y + height - browserPanel.Margin.Bottom);
        }
        else
        {
            foreach (Control sibling in parent.Controls)
            {
                if (sibling == browserPanel || !sibling.Visible) continue;
                if (sibling.Dock == DockStyle.Top) expected = Rectangle.FromLTRB(expected.Left, Math.Max(expected.Top, sibling.Bottom), expected.Right, expected.Bottom);
                if (sibling.Dock == DockStyle.Bottom) expected = Rectangle.FromLTRB(expected.Left, expected.Top, expected.Right, Math.Min(expected.Bottom, sibling.Top));
                if (sibling.Dock == DockStyle.Left) expected = Rectangle.FromLTRB(Math.Max(expected.Left, sibling.Right), expected.Top, expected.Right, expected.Bottom);
                if (sibling.Dock == DockStyle.Right) expected = Rectangle.FromLTRB(expected.Left, expected.Top, Math.Min(expected.Right, sibling.Left), expected.Bottom);
            }
        }
        Assert(Math.Abs(browserPanel.Left - expected.Left) <= 2 && Math.Abs(browserPanel.Top - expected.Top) <= 2 &&
            Math.Abs(browserPanel.Right - expected.Right) <= 2 && Math.Abs(browserPanel.Bottom - expected.Bottom) <= 2,
            "The browser panel fills the area remaining after the desktop controls.");
    }

    private static void CheckReachableTools(Form window)
    {
        ToolStrip tools = (ToolStrip)GetField(window, "tools");
        AssertWithinAncestors(tools, window, "Toolbar");
        TableLayoutPanel header = (TableLayoutPanel)GetField(window, "headerLayout");
        Control identity = header.GetControlFromPosition(0, 0);
        Assert(identity != null, "The header identity area remains present.");
        CheckIdentityBounds(identity, window);
        List<ToolStripItem> displayed = new List<ToolStripItem>();
        int reachable = 0;
        foreach (ToolStripItem item in tools.Items)
        {
            if (!item.Available || item is ToolStripSeparator) continue;
            if (item.Placement == ToolStripItemPlacement.Overflow)
            {
                Assert(tools.CanOverflow && tools.OverflowButton.Visible, "Overflowed actions remain accessible through the overflow menu.");
                reachable++;
            }
            else
            {
                Assert(item.Placement == ToolStripItemPlacement.Main, "Available toolbar actions are not lost during resize.");
                Rectangle visible = tools.ClientRectangle;
                visible.Inflate(2, 2);
                Assert(visible.Contains(item.Bounds), "Displayed toolbar actions stay inside the toolbar.");
                displayed.Add(item);
                reachable++;
            }
        }
        Assert(reachable >= 3, "The main toolbar continues to expose its primary actions.");
        for (int i = 0; i < displayed.Count; i++)
            for (int j = i + 1; j < displayed.Count; j++)
                Assert(!displayed[i].Bounds.IntersectsWith(displayed[j].Bounds), "Displayed toolbar actions do not overlap.");
        Control site = (Control)GetField(window, "siteLabel");
        if (site.Visible && site.Width > 0)
        {
            Assert(site.Height >= site.Font.Height, "The site identity label retains enough vertical room for its text.");
            Rectangle siteOnScreen = site.RectangleToScreen(site.ClientRectangle);
            Rectangle toolsOnScreen = tools.RectangleToScreen(tools.ClientRectangle);
            Assert(!siteOnScreen.IntersectsWith(toolsOnScreen), "The official-site label does not overlap the toolbar.");
        }
    }

    private static void CheckIdentityBounds(Control control, Form window)
    {
        if (!control.Visible) return;
        AssertWithinAncestors(control, window, "Header " + control.GetType().Name);
        foreach (Control child in control.Controls) CheckIdentityBounds(child, window);
    }

    private static void AssertWithinAncestors(Control control, Form window, string description)
    {
        Assert(control.Visible && control.Width > 0 && control.Height > 0, description + " remains visible with nonzero bounds.");
        Rectangle bounds = new Rectangle(control.PointToScreen(Point.Empty), control.Size);
        for (Control ancestor = control.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            Rectangle clip = ancestor.RectangleToScreen(ancestor.ClientRectangle);
            clip.Inflate(2, 2);
            Assert(clip.Contains(bounds), description + " is not clipped by ancestor " + ancestor.GetType().Name +
                " (control=" + bounds + ", ancestor=" + clip + ").");
            if (ancestor == window) break;
        }
    }

    private static void DumpHeaderLayout(Form window)
    {
        TableLayoutPanel layout = (TableLayoutPanel)GetField(window, "headerLayout");
        ToolStrip tools = (ToolStrip)GetField(window, "tools");
        object dpi = typeof(Control).GetProperty("DeviceDpi", InstanceFlags).GetValue(window, null);
        Console.WriteLine("INFO: client=" + window.ClientSize + ", dpi=" + dpi + ", font=" + window.Font +
            ", header=" + layout.Parent.Bounds + ", layout=" + layout.Bounds + ", rows=" +
            String.Join(",", Array.ConvertAll(layout.GetRowHeights(), delegate(int height) { return height.ToString(); })) +
            ", tools=" + tools.Bounds + ", toolsDisplay=" + tools.DisplayRectangle);
    }

    private static void CheckWindowPreferences()
    {
        Type preferences = ProductType("WindowPreferences");
        string path = Path.Combine(isolatedRoot, "local-window-preferences-test.txt");
        object missing = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
        Assert(!(bool)GetField(missing, "AutoFit") && Near((double)GetField(missing, "ManualZoom"), 1),
            "Missing preferences preserve native 100% defaults.");
        SetField(missing, "NormalBounds", new Rectangle(50, 60, 900, 600));
        SetField(missing, "Maximized", true);
        SetField(missing, "AutoFit", false);
        SetField(missing, "ManualZoom", 1.25);
        CallInstance(missing, "Save", new object[] { path });
        object restored = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
        Assert((Rectangle)GetField(restored, "NormalBounds") == new Rectangle(50, 60, 900, 600) && (bool)GetField(restored, "Maximized"),
            "Normal bounds and maximized state survive a local save/load round trip.");
        Assert(!(bool)GetField(restored, "AutoFit") && Near((double)GetField(restored, "ManualZoom"), 1.25), "Manual zoom preferences survive a round trip.");
        SetField(missing, "AutoFit", true);
        CallInstance(missing, "Save", new object[] { path });
        object fitted = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
        Assert((bool)GetField(fitted, "AutoFit") && Near((double)GetField(fitted, "ManualZoom"), 1.25),
            "The optional desktop-fit preference persists without overwriting the manual factor.");
        File.WriteAllText(path, "not a valid settings file\0\ufffd");
        object corrupt = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
        Assert(!(bool)GetField(corrupt, "AutoFit") && Near((double)GetField(corrupt, "ManualZoom"), 1), "Corrupt preferences safely return to native defaults.");
        File.WriteAllText(path, "manualZoom=1.25\n" + new string('x', 4200));
        object oversized = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
        Assert(!(bool)GetField(oversized, "AutoFit") && Near((double)GetField(oversized, "ManualZoom"), 1), "Oversized settings files safely return to native defaults.");
        foreach (string invalid in new string[] { "NaN", "Infinity", "-Infinity", "-500", "500", "1e999", "not-a-number" })
        {
            File.WriteAllText(path, "version=1\nautoFit=false\nmanualZoom=" + invalid + "\n");
            object unreadable = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
            Assert(Near((double)GetField(unreadable, "ManualZoom"), 1), "Invalid zoom read directly from disk restores 100%.");
        }
        foreach (double invalid in new double[] { Double.NaN, Double.PositiveInfinity, -500, 500 })
        {
            SetField(missing, "ManualZoom", invalid);
            CallInstance(missing, "Save", new object[] { path });
            object sanitized = CallStatic(preferences, "Load", new Type[] { typeof(string) }, new object[] { path });
            Assert(BoundedManualZoom((double)GetField(sanitized, "ManualZoom")), "Invalid saved zoom is sanitized before use.");
        }
        Rectangle[] monitors = { new Rectangle(0, 0, 1920, 1040), new Rectangle(1920, 0, 1280, 984) };
        Size minimum = new Size(720, 540);
        Size defaults = new Size(1180, 820);
        Rectangle valid = new Rectangle(1950, 40, 850, 650);
        Assert(VisibleBounds(preferences, valid, monitors, minimum, defaults) == valid, "Valid secondary-monitor bounds are preserved.");
        Rectangle[] invalidBounds = { new Rectangle(5000, 4000, 800, 600), new Rectangle(30, 20, -40, -100),
            new Rectangle(10, 10, 10000, 10000), new Rectangle(Int32.MaxValue - 20, Int32.MaxValue - 20, Int32.MaxValue, Int32.MaxValue) };
        foreach (Rectangle invalid in invalidBounds)
        {
            Rectangle corrected = VisibleBounds(preferences, invalid, monitors, minimum, defaults);
            Assert(corrected.Width > 0 && corrected.Height > 0 && ContainedByAny(corrected, monitors),
                "Invalid, oversized or entirely offscreen bounds become visible on a working area.");
        }
        Rectangle[] negativeMonitor = { new Rectangle(-1920, 0, 1920, 1040), new Rectangle(0, 0, 1920, 1040) };
        Rectangle negativeValid = new Rectangle(-1800, 80, 900, 600);
        Assert(VisibleBounds(preferences, negativeValid, negativeMonitor, minimum, defaults) == negativeValid,
            "Negative coordinates on a real monitor are not mistaken for corruption.");
        Rectangle[] smallMonitor = { new Rectangle(0, 0, 640, 480) };
        Rectangle smallResult = VisibleBounds(preferences, new Rectangle(100, 100, 1180, 820), smallMonitor, minimum, defaults);
        Assert(smallResult.Width > 0 && smallResult.Height > 0 && smallMonitor[0].Contains(smallResult),
            "A small working area takes precedence over an impossible minimum window size.");
        Console.WriteLine("PASS: isolated preference persistence, corrupt settings and multi-monitor bounds recovery.");
    }

    private static Rectangle VisibleBounds(Type preferences, Rectangle desired, Rectangle[] workingAreas, Size minimum, Size defaults)
    { return (Rectangle)CallStatic(preferences, "GetVisibleBounds", new Type[] { typeof(Rectangle), typeof(Rectangle[]), typeof(Size), typeof(Size) }, new object[] { desired, workingAreas, minimum, defaults }); }

    private static bool ContainedByAny(Rectangle value, Rectangle[] areas)
    { foreach (Rectangle area in areas) if (area.Contains(value)) return true; return false; }

    private static bool IsFinite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
    private static bool Near(double left, double right) { return IsFinite(left) && IsFinite(right) && Math.Abs(left - right) < 0.00001; }
    private static bool BoundedManualZoom(double value) { return IsFinite(value) && value >= 0.5 && value <= 1.5; }

    private static void SetStage(string stage)
    {
        currentStage = stage;
        if (watchdog != null) watchdog.Change(30000, Timeout.Infinite);
    }

    private static void WaitForFit(Form window, string message)
    {
        Control viewport = (Control)GetField(window, "browserPanel");
        PropertyInfo dpiProperty = typeof(Control).GetProperty("DeviceDpi", InstanceFlags);
        if (dpiProperty == null) throw new MissingMemberException("The test runtime must support Control.DeviceDpi.");
        float dpi = Convert.ToSingle(dpiProperty.GetValue(window, null));
        double expected = (double)CallStatic(ProductType("DesktopUi"), "CalculateAutoFitZoom",
            new Type[] { typeof(int), typeof(float) }, new object[] { viewport.ClientSize.Width, dpi });
        Stopwatch timeout = Stopwatch.StartNew();
        while (!Near((double)GetProperty(window, "CurrentZoomFactor"), expected) && timeout.ElapsedMilliseconds < 1200)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Assert(Near((double)GetProperty(window, "CurrentZoomFactor"), expected), message);
    }

    private static void CheckFrames(Icon[] frames, string description)
    {
        Assert(frames != null && frames.Length == 4, description + " has four frames.");
        foreach (Icon frame in frames)
        {
            Assert(frame != null && frame.Width == 32 && frame.Height == 32, description + " uses 32 px frames.");
            using (Bitmap bitmap = frame.ToBitmap())
            {
                int visible = 0;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A > 100) visible++;
                Assert(visible > 150, description + " never contains an empty blink frame.");
            }
        }
    }

    private static string IconHash(Icon icon)
    {
        using (Bitmap bitmap = icon.ToBitmap())
        using (MemoryStream bytes = new MemoryStream())
        { bitmap.Save(bytes, ImageFormat.Png); return Hash(bytes.ToArray()); }
    }

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static void DisposeFrames(Icon[] frames)
    { if (frames != null) foreach (Icon frame in frames) if (frame != null) frame.Dispose(); }

    private static Type ProductType(string name)
    {
        Type type = product.GetType("SoulWebLauncher." + name, true);
        return type;
    }

    private static bool CheckUrl(Type policy, string method, string value)
    { return (bool)CallStatic(policy, method, new Type[] { typeof(string) }, new object[] { value }); }

    private static object CallStatic(Type type, string name, Type[] parameters, object[] args)
    {
        MethodInfo method = type.GetMethod(name, StaticFlags, null, parameters, null);
        if (method == null) throw new MissingMethodException(type.FullName, name);
        return method.Invoke(null, args);
    }

    private static object CallInstance(object target, string name)
    { return CallInstance(target, name, new object[0]); }

    private static object CallInstance(object target, string name, object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, InstanceFlags);
        if (method == null) throw new MissingMethodException(target.GetType().FullName, name);
        return method.Invoke(target, args);
    }

    private static object GetStaticProperty(Type type, string name)
    { return type.GetProperty(name, StaticFlags).GetValue(null, null); }

    private static object GetProperty(object target, string name)
    { return target.GetType().GetProperty(name, InstanceFlags).GetValue(target, null); }

    private static object GetField(object target, string name)
    { return target.GetType().GetField(name, InstanceFlags).GetValue(target); }

    private static void SetField(object target, string name, object value)
    { target.GetType().GetField(name, InstanceFlags).SetValue(target, value); }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
