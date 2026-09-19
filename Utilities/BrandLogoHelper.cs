using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace MissionPlanner.Utilities
{
    public static class BrandLogoHelper
    {
        /// <summary>Max width/height for the top-menu brand mark (preserves aspect ratio).</summary>
        public const int MenuLogoMaxWidth = 220;
        public const int MenuLogoMaxHeight = 36;

        public static Bitmap LoadMenuLogoBitmap(bool darkTheme)
        {
            if (Program.Logo != null)
            {
                var src = darkTheme ? Program.Logo : (Program.Logo2 ?? Program.Logo);
                return new Bitmap(src);
            }

            var running = Settings.GetRunningDirectory();
            var vfsPath = Path.Combine(running, "vfs_purdue_logo.png");
            if (File.Exists(vfsPath))
                return new Bitmap(vfsPath);

            if (File.Exists(Path.Combine(running, "logo.png")))
                return new Bitmap(Path.Combine(running, "logo.png"));

            if (Program.IconFile != null)
                return new Bitmap(Program.IconFile);

            throw new FileNotFoundException("VFS logo not found (expected vfs_purdue_logo.png or Program.Logo).");
        }

        public static void ApplyMenuLogo(ToolStripItem menuItem, bool darkTheme)
        {
            if (menuItem == null)
                return;

            using (var source = LoadMenuLogoBitmap(darkTheme))
            {
                var sized = ScaleToFit(source, MenuLogoMaxWidth, MenuLogoMaxHeight);
                menuItem.ImageScaling = ToolStripItemImageScaling.None;
                menuItem.BackgroundImage = null;
                menuItem.Image = sized;
                menuItem.AutoSize = false;
                menuItem.Size = new Size(sized.Width + 6, Math.Max(40, sized.Height + 4));
            }
        }

        public static Bitmap ScaleToFit(Image source, int maxWidth, int maxHeight)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var scale = Math.Min(maxWidth / (double)source.Width, maxHeight / (double)source.Height);
            if (scale > 1)
                scale = 1;

            var w = Math.Max(1, (int)Math.Round(source.Width * scale));
            var h = Math.Max(1, (int)Math.Round(source.Height * scale));

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(source, 0, 0, w, h);
            }

            return bmp;
        }

        /// <summary>Square canvas with the logo centered (letterboxed) — required for a valid WinForms/app icon.</summary>
        public static Bitmap CreateSquareLogoBitmap(Image source, int size = 256, Color? background = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var bg = background ?? Color.Black;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(bg);

                var scale = Math.Min(size / (double)source.Width, size / (double)source.Height);
                var w = Math.Max(1, (int)Math.Round(source.Width * scale));
                var h = Math.Max(1, (int)Math.Round(source.Height * scale));
                var x = (size - w) / 2;
                var y = (size - h) / 2;
                g.DrawImage(source, x, y, w, h);
            }

            return bmp;
        }

        public static Icon CreateIconFromImage(Image source, int size = 256)
        {
            using (var bmp = CreateSquareLogoBitmap(source, size, Color.Black))
            {
                var handle = bmp.GetHicon();
                using (var temp = Icon.FromHandle(handle))
                    return (Icon)temp.Clone();
            }
        }

        public static Icon LoadAppIcon()
        {
            var running = Settings.GetRunningDirectory();
            var icoPath = Path.Combine(running, "vfs_purdue.ico");
            if (File.Exists(icoPath))
            {
                try
                {
                    return new Icon(icoPath);
                }
                catch
                {
                }
            }

            if (Program.IconFile != null)
                return CreateIconFromImage(Program.IconFile);

            return null;
        }
    }
}
