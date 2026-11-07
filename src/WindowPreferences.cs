using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

namespace SoulWebLauncher
{
    // Contains window geometry and display preferences only. No website state is read here.
    internal sealed class WindowPreferences
    {
        internal Rectangle NormalBounds = Rectangle.Empty;
        internal bool Maximized;
        internal bool AutoFit;
        internal double ManualZoom = 1.0;

        internal static WindowPreferences Load(string path)
        {
            WindowPreferences result = new WindowPreferences();
            if (String.IsNullOrEmpty(path)) return result;
            try
            {
                FileInfo file = new FileInfo(path);
                if (!file.Exists || file.Length > 4096) return result;
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
                }
                string text;
                if (!values.TryGetValue("version", out text) || text != "1") return result;
                int left, top, width, height;
                if (ReadInt(values, "left", out left) && ReadInt(values, "top", out top) &&
                    ReadInt(values, "width", out width) && ReadInt(values, "height", out height) &&
                    Math.Abs((long)left) <= 1000000 && Math.Abs((long)top) <= 1000000 && width > 0 && height > 0 && width <= 1000000 && height <= 1000000)
                    result.NormalBounds = new Rectangle(left, top, width, height);
                bool flag;
                if (values.TryGetValue("maximized", out text) && Boolean.TryParse(text, out flag)) result.Maximized = flag;
                if (values.TryGetValue("autoFit", out text) && Boolean.TryParse(text, out flag)) result.AutoFit = flag;
                double zoom;
                if (values.TryGetValue("manualZoom", out text) && Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out zoom) &&
                    !Double.IsNaN(zoom) && !Double.IsInfinity(zoom) && zoom >= 0.5 && zoom <= 1.5)
                    result.ManualZoom = zoom;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (SecurityException) { }
            catch (ArgumentException) { }
            return result;
        }

        internal void Save(string path)
        {
            if (String.IsNullOrEmpty(path)) return;
            string temporary = null;
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                temporary = Path.Combine(directory, ".window-" + Guid.NewGuid().ToString("N") + ".tmp");
                string text = "version=1\nleft=" + NormalBounds.Left.ToString(CultureInfo.InvariantCulture) +
                    "\ntop=" + NormalBounds.Top.ToString(CultureInfo.InvariantCulture) +
                    "\nwidth=" + NormalBounds.Width.ToString(CultureInfo.InvariantCulture) +
                    "\nheight=" + NormalBounds.Height.ToString(CultureInfo.InvariantCulture) +
                    "\nmaximized=" + Maximized.ToString() + "\nautoFit=" + AutoFit.ToString() +
                    "\nmanualZoom=" + DesktopUi.ClampManualZoom(ManualZoom).ToString("0.####", CultureInfo.InvariantCulture) + "\n";
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                temporary = null;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (SecurityException) { }
            catch (ArgumentException) { }
            finally
            {
                if (temporary != null)
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static bool ReadInt(Dictionary<string, string> values, string key, out int value)
        {
            string text;
            value = 0;
            return values.TryGetValue(key, out text) && Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        internal static Rectangle GetVisibleBounds(Rectangle desired, Rectangle[] workingAreas, Size minimumSize, Size defaultSize)
        {
            Rectangle screen = new Rectangle(0, 0, 1920, 1080);
            if (workingAreas != null)
                foreach (Rectangle area in workingAreas) if (area.Width > 0 && area.Height > 0) { screen = area; break; }
            long largestIntersection = 0;
            bool valid = desired.Width > 0 && desired.Height > 0 && Math.Abs((long)desired.Left) <= 1000000 && Math.Abs((long)desired.Top) <= 1000000;
            if (valid && workingAreas != null)
            {
                foreach (Rectangle area in workingAreas)
                {
                    if (area.Width <= 0 || area.Height <= 0) continue;
                    long width = Math.Max(0L, Math.Min((long)desired.Left + desired.Width, (long)area.Left + area.Width) - Math.Max((long)desired.Left, area.Left));
                    long height = Math.Max(0L, Math.Min((long)desired.Top + desired.Height, (long)area.Top + area.Height) - Math.Max((long)desired.Top, area.Top));
                    if (width * height > largestIntersection) { largestIntersection = width * height; screen = area; }
                }
            }
            bool center = !valid || largestIntersection == 0;
            int requestedWidth = valid ? desired.Width : defaultSize.Width;
            int requestedHeight = valid ? desired.Height : defaultSize.Height;
            int widthResult = Math.Min(screen.Width, Math.Max(Math.Min(minimumSize.Width, screen.Width), requestedWidth));
            int heightResult = Math.Min(screen.Height, Math.Max(Math.Min(minimumSize.Height, screen.Height), requestedHeight));
            int leftResult = center ? screen.Left + (screen.Width - widthResult) / 2 : Math.Max(screen.Left, Math.Min(desired.Left, screen.Right - widthResult));
            int topResult = center ? screen.Top + (screen.Height - heightResult) / 2 : Math.Max(screen.Top, Math.Min(desired.Top, screen.Bottom - heightResult));
            return new Rectangle(leftResult, topResult, widthResult, heightResult);
        }
    }
}
