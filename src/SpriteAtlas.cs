using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace HainanTurtlePet
{
    internal sealed class SpriteAtlas : IDisposable
    {
        private readonly Bitmap sheet;
        internal readonly Rectangle[] Bounds = new Rectangle[40];
        internal readonly int CellWidth, CellHeight;
        internal readonly float CharacterScale;
        private readonly Rectangle commonBounds;

        private SpriteAtlas(Bitmap bitmap)
        {
            sheet = bitmap;
            CellWidth = sheet.Width / 4;
            CellHeight = sheet.Height / 10;
            if (sheet.Width % 4 != 0 || sheet.Height % 10 != 0)
                throw new InvalidDataException("Sprite sheet must have 4 columns and 10 rows (40 animation frames).");
            Rectangle union = Rectangle.Empty;
            for (int index = 0; index < Bounds.Length; index++)
            {
                int baseX = (index % 4) * CellWidth, baseY = (index / 4) * CellHeight;
                int left = CellWidth, top = CellHeight, right = -1, bottom = -1;
                for (int y = 0; y < CellHeight; y++)
                    for (int x = 0; x < CellWidth; x++)
                        if (sheet.GetPixel(baseX + x, baseY + y).A > 24)
                        { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
                if (right < left || bottom < top) throw new InvalidDataException("A sprite frame is empty.");
                Bounds[index] = new Rectangle(baseX + left, baseY + top, right - left + 1, bottom - top + 1);
                Rectangle local = new Rectangle(left, top, right - left + 1, bottom - top + 1);
                union = union.IsEmpty ? local : Rectangle.Union(union, local);
            }
            commonBounds = union;
            CharacterScale = 1f / Math.Max(union.Width, union.Height);
        }

        internal static SpriteAtlas LoadEmbedded()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HainanTurtlePet.sprites.png"))
            {
                if (stream == null) throw new FileNotFoundException("The embedded character sprites are missing.");
                using (Bitmap loaded = new Bitmap(stream)) return new SpriteAtlas(new Bitmap(loaded));
            }
        }

        internal static SpriteAtlas FromFile(string path) { return new SpriteAtlas(new Bitmap(path)); }

        internal Bitmap Render(int frame, int size, string message, bool facingLeft, double seconds, bool moving, bool bouncing = false)
        {
            int width = Math.Max(size + 32, 260), height = size + 86;
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int index = Math.Max(0, Math.Min(Bounds.Length - 1, frame));
                Rectangle source = new Rectangle((index % 4) * CellWidth + commonBounds.X,
                    (index / 4) * CellHeight + commonBounds.Y, commonBounds.Width, commonBounds.Height);
                float scale = size * CharacterScale;
                float targetWidth = source.Width * scale, targetHeight = source.Height * scale;
                float lift = moving ? (float)Math.Abs(Math.Sin(seconds * 12)) * 2.2f :
                    (frame >= 24 && frame < 32 ? 0 : (float)Math.Sin(seconds * 2.4) * 1.1f);
                float x = (width - targetWidth) / 2f;
                float y = height - 10 - targetHeight - lift;
                if (bouncing) y -= (float)Math.Abs(Math.Sin(seconds * 7)) * 16;
                if (facingLeft)
                {
                    graphics.TranslateTransform(width, 0);
                    graphics.ScaleTransform(-1, 1);
                    graphics.DrawImage(sheet, new RectangleF(x, y, targetWidth, targetHeight), source, GraphicsUnit.Pixel);
                    graphics.ResetTransform();
                }
                else graphics.DrawImage(sheet, new RectangleF(x, y, targetWidth, targetHeight), source, GraphicsUnit.Pixel);
                if (!String.IsNullOrEmpty(message)) DrawBubble(graphics, message, width);
                if (frame >= 24 && frame < 32)
                    using (Font font = new Font("Segoe UI", 15, FontStyle.Bold))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(205, 52, 107, 133)))
                        graphics.DrawString("z Z", font, brush, width * .66f, 86);
            }
            return bitmap;
        }

        private static void DrawBubble(Graphics graphics, string text, int width)
        {
            RectangleF rect = new RectangleF(9, 7, width - 18, 58);
            using (GraphicsPath path = Rounded(rect, 16))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(248, 250, 252, 246)))
            using (Pen border = new Pen(Color.FromArgb(225, 137, 185, 160), 1.3f))
            using (Font font = new Font("Microsoft YaHei UI", 10.5f))
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(40, 70, 60)))
            using (StringFormat format = new StringFormat())
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                graphics.DrawString(text, font, ink, new RectangleF(18, 12, width - 36, 46), format);
            }
            PointF[] tail = { new PointF(width / 2f - 6, 64), new PointF(width / 2f, 73), new PointF(width / 2f + 7, 64) };
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(248, 250, 252, 246))) graphics.FillPolygon(brush, tail);
        }

        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            float diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal Icon CreateIcon()
        {
            using (Bitmap frame = Render(8, 200, null, false, 0, false))
            using (Bitmap iconBitmap = new Bitmap(48, 48, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(iconBitmap))
                    graphics.DrawImage(frame, new Rectangle(0, 0, 48, 48), new Rectangle(28, 76, 204, 204), GraphicsUnit.Pixel);
                IntPtr handle = iconBitmap.GetHicon();
                try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
                finally { NativeMethods.DestroyIcon(handle); }
            }
        }

        public void Dispose() { sheet.Dispose(); }
    }
}
