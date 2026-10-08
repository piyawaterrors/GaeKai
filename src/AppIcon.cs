using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GaeKai
{
    internal static class AppIcon
    {
        private static Icon small, large, paused;

        /// <summary>ไอคอนสำหรับถาดไอคอน (ขนาดตาม DPI ของเครื่อง)</summary>
        public static Icon Small
        {
            get { return small ?? (small = Load(SystemInformation.SmallIconSize)); }
        }

        public static Icon Large
        {
            get { return large ?? (large = Load(SystemInformation.IconSize)); }
        }

        /// <summary>ไอคอนสีเทา ใช้ตอนหยุดชั่วคราว</summary>
        public static Icon Paused
        {
            get { return paused ?? (paused = Grayscale(Small)); }
        }

        private static Icon Load(Size size)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GaeKai.ico"))
            {
                if (stream == null) return SystemIcons.Application;
                return new Icon(stream, size);
            }
        }

        private static Icon Grayscale(Icon source)
        {
            using (Bitmap color = source.ToBitmap())
            using (Bitmap gray = new Bitmap(color.Width, color.Height, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(gray))
            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix(new[]
                {
                    new[] { 0.30f, 0.30f, 0.30f, 0, 0 },
                    new[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                    new[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                    new[] { 0f, 0, 0, 0.6f, 0 },
                    new[] { 0f, 0, 0, 0, 1 },
                }));
                g.DrawImage(color, new Rectangle(0, 0, color.Width, color.Height),
                    0, 0, color.Width, color.Height, GraphicsUnit.Pixel, attributes);
                return Icon.FromHandle(gray.GetHicon());
            }
        }
    }
}
