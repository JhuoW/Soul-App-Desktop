// Original project artwork, rendered with GDI+. No external images are used.
// Keep the paths/colors aligned with assets/app-icon.svg.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class GenerateIcons
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: GenerateIcons.exe <assets-directory>");
            return 2;
        }
        string output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        List<byte[]> images = new List<byte[]>();
        foreach (int size in Sizes)
        {
            using (Bitmap bitmap = Render(size))
            {
                bitmap.Save(Path.Combine(output, "app-icon-" + size + ".png"), ImageFormat.Png);
                // Classic 32-bit DIB entries keep Icon.ToBitmap reliable on Framework.
                images.Add(ToIconBitmap(bitmap));
            }
        }
        using (Bitmap large = Render(512))
            large.Save(Path.Combine(output, "app-icon.png"), ImageFormat.Png);
        using (FileStream file = File.Create(Path.Combine(output, "app-icon.ico")))
        using (BinaryWriter writer = new BinaryWriter(file))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
        }
        using (Icon verify = new Icon(Path.Combine(output, "app-icon.ico"), new Size(256, 256)))
        using (Bitmap verifyBitmap = verify.ToBitmap())
        {
            if (verify.Width < 128 || verifyBitmap.Width < 128)
                throw new InvalidOperationException("The generated ICO could not load a high-resolution entry.");
        }
        Console.WriteLine("Generated original 16-256 px ICO and 512 px PNG; .NET Framework icon loading verified.");
        return 0;
    }

    private static Bitmap Render(int size)
    {
        // Supersampling keeps diagonal tails and small circular marks clean at 16 px.
        int renderSize = size < 128 ? size * 4 : size * 2;
        using (Bitmap canvas = new Bitmap(renderSize, renderSize, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                graphics.ScaleTransform(renderSize / 64F, renderSize / 64F);
                using (GraphicsPath tile = RoundedRectangle(2, 2, 60, 60, 15))
                using (LinearGradientBrush gradient = new LinearGradientBrush(
                    new PointF(0, 2), new PointF(0, 62),
                    Color.FromArgb(0, 182, 170), Color.FromArgb(5, 118, 111)))
                    graphics.FillPath(gradient, tile);
                using (GraphicsPath back = BackBubble())
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(139, 228, 217)))
                    graphics.FillPath(brush, back);
                using (GraphicsPath front = FrontBubble())
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(252, 255, 255)))
                    graphics.FillPath(brush, front);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(8, 124, 116)))
                {
                    graphics.FillEllipse(brush, 21.2F, 34.2F, 4.6F, 4.6F);
                    graphics.FillEllipse(brush, 31.2F, 34.2F, 4.6F, 4.6F);
                }
            }
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            using (ImageAttributes attributes = new ImageAttributes())
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(canvas, new Rectangle(0, 0, size, size),
                    0, 0, renderSize, renderSize, GraphicsUnit.Pixel, attributes);
            }
            return result;
        }
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + width - d, y, d, d, 270, 90);
        p.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        p.AddArc(x, y + height - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static GraphicsPath BackBubble()
    {
        GraphicsPath p = new GraphicsPath();
        p.AddLine(34, 15, 43, 15);
        p.AddBezier(43, 15, 48, 15, 52, 19, 52, 24);
        p.AddLine(52, 24, 52, 30);
        p.AddBezier(52, 30, 52, 34, 49, 37, 45, 38);
        p.AddLine(45, 38, 49, 44);
        p.AddLine(49, 44, 37, 38);
        p.AddLine(37, 38, 34, 38);
        p.AddBezier(34, 38, 29, 38, 25, 34, 25, 30);
        p.AddLine(25, 30, 25, 24);
        p.AddBezier(25, 24, 25, 19, 29, 15, 34, 15);
        p.CloseFigure();
        return p;
    }

    private static GraphicsPath FrontBubble()
    {
        GraphicsPath p = new GraphicsPath();
        p.AddLine(21, 25, 36, 25);
        p.AddBezier(36, 25, 41, 25, 45, 29, 45, 34);
        p.AddLine(45, 34, 45, 39);
        p.AddBezier(45, 39, 45, 44, 41, 48, 36, 48);
        p.AddLine(36, 48, 27, 48);
        p.AddLine(27, 48, 18, 54);
        p.AddLine(18, 54, 20, 48);
        p.AddBezier(20, 48, 15, 47, 12, 44, 12, 39);
        p.AddLine(12, 39, 12, 34);
        p.AddBezier(12, 34, 12, 29, 16, 25, 21, 25);
        p.CloseFigure();
        return p;
    }

    private static byte[] ToIconBitmap(Bitmap bitmap)
    {
        int size = bitmap.Width;
        int maskStride = ((size + 31) / 32) * 4;
        using (MemoryStream memory = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(memory))
        {
            writer.Write(40);
            writer.Write(size);
            writer.Write(size * 2);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0);
            writer.Write(size * size * 4 + maskStride * size);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            for (int y = size - 1; y >= 0; y--)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = bitmap.GetPixel(x, y);
                    writer.Write(c.B);
                    writer.Write(c.G);
                    writer.Write(c.R);
                    writer.Write(c.A);
                }
            }
            for (int y = size - 1; y >= 0; y--)
            {
                byte[] mask = new byte[maskStride];
                for (int x = 0; x < size; x++)
                    if (bitmap.GetPixel(x, y).A == 0)
                        mask[x / 8] |= (byte)(0x80 >> (x % 8));
                writer.Write(mask);
            }
            return memory.ToArray();
        }
    }
}
