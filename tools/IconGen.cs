// สร้างไฟล์ไอคอน src\GaeKai.ico (สั่งรัน: build.bat icon)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

internal static class IconGen
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "GaeKai.ico";
        string preview = args.Length > 1 ? args[1] : null;

        List<byte[]> images = new List<byte[]>();
        foreach (int size in Sizes)
        {
            using (Bitmap bmp = Draw(size))
            {
                // ขนาดเล็กเก็บเป็น BMP ให้ทุกโปรแกรมอ่านได้ ขนาด 256 เก็บเป็น PNG เพื่อให้ไฟล์เล็ก
                images.Add(size >= 256 ? ToPng(bmp) : ToDib(bmp));
                if (preview != null && size == 256) bmp.Save(preview, ImageFormat.Png);
            }
        }

        using (BinaryWriter w = new BinaryWriter(File.Create(output)))
        {
            w.Write((short)0);            // reserved
            w.Write((short)1);            // type = icon
            w.Write((short)Sizes.Length); // count
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                byte dim = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                w.Write(dim);
                w.Write(dim);
                w.Write((byte)0);   // palette
                w.Write((byte)0);   // reserved
                w.Write((short)1);  // planes
                w.Write((short)32); // bpp
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) w.Write(image);
        }
        Console.WriteLine("Wrote " + output);
        return 0;
    }

    private static Bitmap Draw(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float inset = size <= 20 ? 0.5f : size * 0.04f;
            RectangleF box = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);
            using (GraphicsPath background = RoundedRect(box, size * 0.22f))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                box, Color.FromArgb(59, 130, 246), Color.FromArgb(29, 78, 216), LinearGradientMode.ForwardDiagonal))
            {
                g.FillPath(brush, background);
            }

            using (FontFamily family = ThaiFontFamily())
            using (GraphicsPath glyph = new GraphicsPath())
            {
                glyph.AddString("ก", family, (int)FontStyle.Bold, 100f, PointF.Empty, StringFormat.GenericTypographic);
                RectangleF bounds = glyph.GetBounds();

                float targetHeight = size * (size <= 20 ? 0.62f : 0.56f);
                float scale = targetHeight / bounds.Height;
                using (Matrix m = new Matrix())
                {
                    m.Translate(size / 2f, size / 2f);
                    m.Scale(scale, scale);
                    m.Translate(-(bounds.X + bounds.Width / 2f), -(bounds.Y + bounds.Height / 2f));
                    glyph.Transform(m);
                }
                g.FillPath(Brushes.White, glyph);
            }
        }
        return bmp;
    }

    private static FontFamily ThaiFontFamily()
    {
        foreach (string name in new[] { "Leelawadee UI", "Leelawadee", "Tahoma" })
        {
            try { return new FontFamily(name); }
            catch (ArgumentException) { }
        }
        return new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static byte[] ToPng(Bitmap bmp)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    /// <summary>BITMAPINFOHEADER + พิกเซล BGRA (ล่างขึ้นบน) + AND mask</summary>
    private static byte[] ToDib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        byte[] pixels = new byte[w * h * 4];
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * w * 4, w * 4);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        int maskStride = ((w + 31) / 32) * 4;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(40);                          // biSize
            bw.Write(w);                           // biWidth
            bw.Write(h * 2);                       // biHeight (XOR + AND)
            bw.Write((short)1);                    // biPlanes
            bw.Write((short)32);                   // biBitCount
            bw.Write(0);                           // biCompression = BI_RGB
            bw.Write(w * h * 4 + maskStride * h);  // biSizeImage
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            for (int y = h - 1; y >= 0; y--) bw.Write(pixels, y * w * 4, w * 4);

            for (int y = h - 1; y >= 0; y--)
            {
                byte[] row = new byte[maskStride];
                for (int x = 0; x < w; x++)
                {
                    if (pixels[(y * w + x) * 4 + 3] == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                }
                bw.Write(row);
            }
            bw.Flush();
            return ms.ToArray();
        }
    }
}
