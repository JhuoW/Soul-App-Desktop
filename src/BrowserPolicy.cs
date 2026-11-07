using System;
using System.Diagnostics;

namespace SoulWebLauncher
{
    internal static class BrowserPolicy
    {
        internal const string HomeUrl = "https://home.soulapp.cn/";
        internal const string RuntimeUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

        internal static bool IsSafeHttpsUrl(string value)
        {
            Uri uri;
            return !String.IsNullOrEmpty(value) && value.Length <= 8192 &&
                Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                String.IsNullOrEmpty(uri.UserInfo) && !String.IsNullOrWhiteSpace(uri.Host);
        }

        internal static bool IsOfficialUrl(string value)
        {
            if (!IsSafeHttpsUrl(value)) return false;
            Uri uri = new Uri(value);
            string host = uri.DnsSafeHost;
            return uri.Port == 443 &&
                (host.Equals("soulapp.cn", StringComparison.OrdinalIgnoreCase) ||
                 host.EndsWith(".soulapp.cn", StringComparison.OrdinalIgnoreCase));
        }

        internal static string GetOfficialOrigin(string value)
        {
            if (!IsOfficialUrl(value)) return null;
            return new Uri(value).GetLeftPart(UriPartial.Authority);
        }

        internal static bool TryOpenExternal(string value)
        {
            if (!IsSafeHttpsUrl(value)) return false;
            try
            {
                string normalized = new Uri(value).AbsoluteUri;
                using (Process process = Process.Start(new ProcessStartInfo(normalized) { UseShellExecute = true })) { }
                return true;
            }
            catch (System.ComponentModel.Win32Exception) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }
    }
}
