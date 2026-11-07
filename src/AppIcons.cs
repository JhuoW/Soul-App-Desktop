using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SoulWebLauncher
{
    // Every returned Icon/Image belongs to the caller and must be disposed.
    internal static class AppIcons
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

        internal static Icon LoadMainIcon()
        {
            using (Stream stream = OpenResource("SoulWebLauncher.AppIcon.ico"))
            using (Icon icon = new Icon(stream, new Size(256, 256)))
                return (Icon)icon.Clone();
        }

        internal static Icon LoadTrayIcon()
        {
            using (Stream stream = OpenResource("SoulWebLauncher.AppIcon.ico"))
            using (Icon icon = new Icon(stream, new Size(32, 32)))
                return (Icon)icon.Clone();
        }

        internal static Bitmap LoadImage()
        {
            using (Stream stream = OpenResource("SoulWebLauncher.AppIcon.png"))
            using (Image image = Image.FromStream(stream))
                return new Bitmap(image);
        }

        internal static Icon[] CreateTrayFrames()
        {
            using (Bitmap image = LoadImage())
                return CreateFrames(image, false);
        }

        // The avatar is only drawn during this call. It is never retained or disposed here.
        // Invalid/disposed source images safely fall back to the application artwork.
        internal static Icon[] CreateTrayFrames(Image avatar)
        {
            if (avatar == null) return CreateTrayFrames();
            try
            {
                using (Bitmap square = new Bitmap(128, 128, PixelFormat.Format32bppArgb))
                {
                    int sourceSize = Math.Min(avatar.Width, avatar.Height);
                    if (sourceSize <= 0) return CreateTrayFrames();
                    using (Graphics graphics = Graphics.FromImage(square))
                    {
                        graphics.Clear(Color.White);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(avatar, new Rectangle(0, 0, 128, 128),
                            (avatar.Width - sourceSize) / 2, (avatar.Height - sourceSize) / 2,
                            sourceSize, sourceSize, GraphicsUnit.Pixel);
                    }
                    return CreateFrames(square, true);
                }
            }
            catch (ArgumentException) { return CreateTrayFrames(); }
            catch (ObjectDisposedException) { return CreateTrayFrames(); }
            catch (ExternalException) { return CreateTrayFrames(); }
        }

        private static Icon[] CreateFrames(Image image, bool roundAvatar)
        {
            int[] offsets = { 0, 2, 4, 2 };
            Icon[] frames = new Icon[offsets.Length];
            try
            {
                for (int i = 0; i < offsets.Length; i++)
                    frames[i] = RenderTrayFrame(image, offsets[i], roundAvatar);
                return frames;
            }
            catch
            {
                foreach (Icon frame in frames) if (frame != null) frame.Dispose();
                throw;
            }
        }

        private static Icon RenderTrayFrame(Image image, int upwardOffset, bool roundAvatar)
        {
            using (Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    Rectangle body = new Rectangle(3, 5 - upwardOffset, 26, 26);
                    if (roundAvatar)
                    {
                        GraphicsState state = graphics.Save();
                        using (GraphicsPath clip = new GraphicsPath())
                        {
                            clip.AddEllipse(body);
                            graphics.SetClip(clip);
                            graphics.DrawImage(image, body);
                        }
                        graphics.Restore(state);
                        using (Pen border = new Pen(Color.White, 1.4F))
                            graphics.DrawEllipse(border, body);
                    }
                    else graphics.DrawImage(image, body);
                    using (SolidBrush badge = new SolidBrush(Color.FromArgb(242, 78, 86)))
                    using (Pen outline = new Pen(Color.White, 1.5F))
                    {
                        RectangleF dot = new RectangleF(21, 5 - upwardOffset, 9, 9);
                        graphics.FillEllipse(badge, dot);
                        graphics.DrawEllipse(outline, dot);
                    }
                }
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon borrowed = Icon.FromHandle(handle))
                        return (Icon)borrowed.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        private static Stream OpenResource(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) throw new InvalidOperationException("Missing embedded icon resource: " + name);
            return stream;
        }
    }
}
