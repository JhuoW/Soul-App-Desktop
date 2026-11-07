using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Soul Web Launcher")]
[assembly: AssemblyDescription("Unofficial launcher for the official Soul website")]
[assembly: AssemblyProduct("Soul Web Launcher")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

namespace SoulWebLauncher
{
    internal static class Program
    {
        internal const string HomeUrl = "https://home.soulapp.cn/";
        internal const string AnnouncementUrl = "https://www.soulapp.cn/media/news/article-32";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 1 && String.Equals(args[0], "--open", StringComparison.Ordinal))
            {
                string browserName;
                string error;
                if (BrowserLauncher.OpenApp(out browserName, out error))
                    return 0;
                MessageBox.Show(error + "\r\n\r\n请正常打开此助手，使用“在默认浏览器中打开”。",
                    "Soul 网页启动助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            if (args.Length != 0)
            {
                MessageBox.Show("可直接运行此程序，或使用 --open 打开官方独立窗口。",
                    "Soul 网页启动助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }

            Application.Run(new LauncherForm());
            return 0;
        }
    }

    internal sealed class BrowserChoice
    {
        internal readonly string Name;
        internal readonly string Path;

        internal BrowserChoice(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }

    internal static class BrowserLauncher
    {
        private static BrowserChoice[] FindCandidates()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new BrowserChoice[]
            {
                Candidate("Microsoft Edge", programFiles, @"Microsoft\Edge\Application\msedge.exe"),
                Candidate("Microsoft Edge", programFilesX86, @"Microsoft\Edge\Application\msedge.exe"),
                Candidate("Microsoft Edge", localAppData, @"Microsoft\Edge\Application\msedge.exe"),
                Candidate("Google Chrome", programFiles, @"Google\Chrome\Application\chrome.exe"),
                Candidate("Google Chrome", programFilesX86, @"Google\Chrome\Application\chrome.exe"),
                Candidate("Google Chrome", localAppData, @"Google\Chrome\Application\chrome.exe")
            };
        }

        private static BrowserChoice Candidate(string name, string root, string relativePath)
        {
            return new BrowserChoice(name,
                String.IsNullOrEmpty(root) ? String.Empty : System.IO.Path.Combine(root, relativePath));
        }

        internal static bool OpenApp(out string browserName, out string error)
        {
            browserName = String.Empty;
            error = String.Empty;
            bool found = false;
            foreach (BrowserChoice browser in FindCandidates())
            {
                if (String.IsNullOrEmpty(browser.Path) || !File.Exists(browser.Path))
                    continue;
                found = true;
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = browser.Path;
                    info.Arguments = "--app=https://home.soulapp.cn/ --window-size=1180,820";
                    info.UseShellExecute = false;
                    info.WorkingDirectory = System.IO.Path.GetDirectoryName(browser.Path);
                    using (Process process = Process.Start(info))
                    {
                        // Disposing our handle does not close the browser or its windows.
                    }
                    browserName = browser.Name;
                    return true;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // If this installed browser cannot start, try the next fixed location.
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.Security.SecurityException)
                {
                }
            }

            error = found
                ? "暂时无法启动独立窗口。请使用下方的默认浏览器按钮，或检查浏览器是否可正常运行。"
                : "未在标准安装目录找到 Microsoft Edge 或 Google Chrome。仍可使用下方的默认浏览器按钮。";
            return false;
        }

        internal static bool OpenOfficialPage(string url)
        {
            // Keep the shell operation restricted to these two compile-time official URLs.
            if (url != Program.HomeUrl && url != Program.AnnouncementUrl)
                return false;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = url;
                info.UseShellExecute = true;
                using (Process process = Process.Start(info))
                {
                }
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    internal sealed class LauncherForm : Form
    {
        private readonly Color ink = Color.FromArgb(26, 47, 56);
        private readonly Color muted = Color.FromArgb(87, 108, 117);
        private readonly Color accent = Color.FromArgb(0, 120, 115);
        private readonly Label status;
        private readonly Panel statusPanel;

        internal LauncherForm()
        {
            Text = "Soul 网页启动助手";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 10F);
            ClientSize = new Size(740, 630);
            MinimumSize = new Size(660, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 248, 250);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(28);
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.AutoScroll = true;
            Controls.Add(layout);

            Label title = MakeLabel("Soul 网页启动助手", 24F, FontStyle.Bold, ink);
            title.Margin = new Padding(0, 0, 0, 8);
            AddRow(layout, title);

            Label subtitle = MakeLabel("官方网页版启动助手 · 非 Soul 官方软件", 11F, FontStyle.Regular, muted);
            subtitle.Margin = new Padding(0, 0, 0, 22);
            AddRow(layout, subtitle);

            TableLayoutPanel card = new TableLayoutPanel();
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Dock = DockStyle.Top;
            card.ColumnCount = 1;
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.BackColor = Color.White;
            card.Padding = new Padding(18, 15, 18, 15);
            card.Margin = new Padding(0, 0, 0, 18);
            Label cardTitle = MakeLabel("打开 Soul 官方网页", 11F, FontStyle.Bold, ink);
            cardTitle.Margin = new Padding(0, 0, 0, 6);
            AddRow(card, cardTitle);
            Label address = MakeLabel(Program.HomeUrl, 17F, FontStyle.Regular, accent);
            address.Margin = new Padding(0, 0, 0, 8);
            AddRow(card, address);
            Label cardHint = MakeLabel("在电脑上使用官方聊天页面，登录由你在官方页面中完成。", 10F, FontStyle.Regular, muted);
            cardHint.Margin = new Padding(0);
            AddRow(card, cardHint);
            AddRow(layout, card);

            Button openApp = MakeButton("打开独立聊天窗口", true);
            openApp.Click += delegate
            {
                string browserName;
                string error;
                if (BrowserLauncher.OpenApp(out browserName, out error))
                    SetStatus("已请求 " + browserName + " 打开官方网页。\r\n请在新窗口中完成登录；此助手不检查登录或连接状态。", false);
                else
                    SetStatus(error, true);
            };
            AddRow(layout, openApp);
            AcceptButton = openApp;

            Button openDefault = MakeButton("在默认浏览器中打开", false);
            openDefault.Click += delegate
            {
                if (BrowserLauncher.OpenOfficialPage(Program.HomeUrl))
                    SetStatus("已请求默认浏览器打开官方网页。\r\n请在浏览器中完成登录；此助手不检查登录或连接状态。", false);
                else
                    SetStatus("未能打开默认浏览器。请手动在浏览器中访问上方显示的官方网址。", true);
            };
            AddRow(layout, openDefault);

            Label browserHint = MakeLabel("独立窗口优先使用已安装的 Edge，其次使用 Chrome。\r\n沿用浏览器现有设置；关闭此助手不会关闭聊天窗口。", 9.5F, FontStyle.Regular, muted);
            browserHint.Margin = new Padding(0, 0, 0, 18);
            AddRow(layout, browserHint);

            statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Top;
            statusPanel.Height = 78;
            statusPanel.BackColor = Color.FromArgb(228, 242, 239);
            statusPanel.Padding = new Padding(14, 12, 14, 12);
            statusPanel.Margin = new Padding(0, 0, 0, 18);
            status = new Label();
            status.Dock = DockStyle.Fill;
            status.Font = new Font("Microsoft YaHei UI", 10F);
            status.ForeColor = Color.FromArgb(36, 88, 80);
            status.Text = "准备就绪。\r\n登录、验证码与聊天均在 Soul 官方网页中完成。";
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.AccessibleName = "启动状态";
            statusPanel.Controls.Add(status);
            AddRow(layout, statusPanel);

            LinkLabel announcement = new LinkLabel();
            announcement.AutoSize = true;
            announcement.Text = "查看 Soul 官方网页版发布说明";
            announcement.Font = new Font("Microsoft YaHei UI", 10F);
            announcement.LinkColor = accent;
            announcement.ActiveLinkColor = Color.FromArgb(0, 86, 81);
            announcement.LinkBehavior = LinkBehavior.HoverUnderline;
            announcement.Margin = new Padding(0, 0, 0, 12);
            announcement.LinkClicked += delegate
            {
                if (BrowserLauncher.OpenOfficialPage(Program.AnnouncementUrl))
                    SetStatus("已请求默认浏览器打开 Soul 官方发布说明。", false);
                else
                    SetStatus("未能打开默认浏览器。请检查系统的默认浏览器设置。", true);
            };
            AddRow(layout, announcement);

            Label privacy = MakeLabel("此助手不读取浏览器资料，不保存账号、验证码或聊天记录。", 9F, FontStyle.Regular, muted);
            privacy.Margin = new Padding(0);
            AddRow(layout, privacy);
        }

        private Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Dock = DockStyle.Top;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", size, style);
            label.ForeColor = color;
            label.UseMnemonic = false;
            return label;
        }

        private Button MakeButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.Dock = DockStyle.Top;
            button.Height = 48;
            button.Margin = new Padding(0, 0, 0, 12);
            button.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.BackColor = primary ? accent : Color.White;
            button.ForeColor = primary ? Color.White : ink;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(207, 219, 224);
            button.FlatAppearance.MouseOverBackColor = primary
                ? Color.FromArgb(0, 102, 98) : Color.FromArgb(233, 240, 243);
            button.FlatAppearance.MouseDownBackColor = primary
                ? Color.FromArgb(0, 86, 81) : Color.FromArgb(222, 233, 237);
            return button;
        }

        private static void AddRow(TableLayoutPanel panel, Control control)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(control, 0, row);
        }

        private void SetStatus(string message, bool isError)
        {
            status.Text = message;
            statusPanel.BackColor = isError
                ? Color.FromArgb(255, 241, 224) : Color.FromArgb(228, 242, 239);
            status.ForeColor = isError
                ? Color.FromArgb(130, 75, 24) : Color.FromArgb(36, 88, 80);
        }
    }
}

