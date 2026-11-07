using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Soul Web Launcher")]
[assembly: AssemblyDescription("Unofficial desktop window for the official Soul website")]
[assembly: AssemblyProduct("Soul Web Launcher")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]

namespace SoulWebLauncher
{
    internal static class Bootstrap
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 1 || (args.Length == 1 && args[0] != "--open"))
            {
                MessageBox.Show("请直接运行此程序，或使用 --open 打开聊天窗口。", "Soul 桌面窗口");
                return 2;
            }
            string sid = WindowsIdentity.GetCurrent().User.Value;
            bool created;
            using (Mutex single = new Mutex(true, "Local\\SoulWebLauncher." + sid, out created))
            {
                if (!created)
                {
                    ProgramActivation.ActivateExisting();
                    return 0;
                }
                try
                {
                    RuntimeFiles.Initialize(null);
                    return RunDesktop(args);
                }
                catch (Exception error)
                {
                    MessageBox.Show("无法启动桌面窗口。请确认系统已安装 .NET Framework 4.7.2 或更新版本，以及 Microsoft Edge WebView2 Runtime。\r\n\r\n" + error.GetType().Name,
                        "Soul 桌面窗口", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { single.ReleaseMutex(); }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunDesktop(string[] args)
        {
            RuntimeFiles.ConfigureWebViewLoader();
            return DesktopApplication.Run(args);
        }
    }

    internal static class ProgramActivation
    {
        internal static readonly int ActivateMessage = unchecked((int)RegisterWindowMessage("SoulWebLauncher.Activate.v1"));
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(uint processId);

        internal static void ActivateExisting()
        {
            // Broadcast contains no user information and only our window handles this registered message.
            AllowSetForegroundWindow(0xffffffff);
            PostMessage(new IntPtr(0xffff), ActivateMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    internal static class RuntimeFiles
    {
        private static readonly Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        internal static string DataDirectory { get; private set; }
        internal static string LoaderDirectory { get; private set; }

        internal static void Initialize(string testRoot)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            string root = testRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoulWebLauncher");
            root = Path.GetFullPath(root);
            DataDirectory = Path.Combine(root, "WebView2");
            string architecture = IntPtr.Size == 8 ? "x64" : "x86";
            byte[] loader = ReadCompressedResource("SoulWebLauncher.WebView2Loader." + architecture + ".gz");
            string expected = Hash(loader);
            LoaderDirectory = Path.Combine(root, "Runtime", architecture + "-" + expected.Substring(0, 16));
            Directory.CreateDirectory(LoaderDirectory);
            string target = Path.Combine(LoaderDirectory, "WebView2Loader.dll");
            if (!File.Exists(target))
            {
                string temporary = Path.Combine(LoaderDirectory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllBytes(temporary, loader);
                    try { File.Move(temporary, target); }
                    catch (IOException) { if (!File.Exists(target)) throw; }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            if (!String.Equals(Hash(File.ReadAllBytes(target)), expected, StringComparison.Ordinal))
                throw new InvalidDataException("WebView2 loader integrity check failed.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ConfigureWebViewLoader()
        {
            Microsoft.Web.WebView2.Core.CoreWebView2Environment.SetLoaderDllFolderPath(LoaderDirectory);
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            if (name != "Microsoft.Web.WebView2.Core" && name != "Microsoft.Web.WebView2.WinForms") return null;
            lock (assemblies)
            {
                Assembly cached;
                if (assemblies.TryGetValue(name, out cached)) return cached;
                Assembly loaded = Assembly.Load(ReadCompressedResource("SoulWebLauncher." + name + ".gz"));
                assemblies.Add(name, loaded);
                return loaded;
            }
        }

        private static byte[] ReadCompressedResource(string name)
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (resource == null) throw new FileNotFoundException("A packaged runtime component is missing.");
                using (GZipStream zip = new GZipStream(resource, CompressionMode.Decompress))
                using (MemoryStream bytes = new MemoryStream())
                {
                    zip.CopyTo(bytes);
                    return bytes.ToArray();
                }
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static string ReadThirdPartyNotices()
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("SoulWebLauncher.ThirdPartyNotices.txt"))
            {
                if (resource == null) return "许可文件缺失，请查看仓库 THIRD_PARTY_NOTICES.txt。";
                using (StreamReader reader = new StreamReader(resource)) return reader.ReadToEnd();
            }
        }
    }
}
