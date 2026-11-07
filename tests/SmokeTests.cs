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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            product = Assembly.LoadFile(Path.GetFullPath(args[0]));
            CheckRuntime(Path.GetFullPath(args[1]));
            if (uiLaunch)
            {
                Console.WriteLine("Local UI test window only. No WebView2 browser or Soul connection is created.");
                using (Form localWindow = (Form)Activator.CreateInstance(ProductType("MainForm"), InstanceFlags, null, new object[] { false }, null))
                    Application.Run(localWindow);
                return 0;
            }
            CheckIcons();
            CheckBrowserPolicy();
            CheckCloseDialog();
            CheckLocalWindowLifecycle();
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

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
