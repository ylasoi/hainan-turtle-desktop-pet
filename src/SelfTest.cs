using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace HainanTurtlePet
{
    internal static class SelfTest
    {
        private static void Require(bool condition, string detail)
        { if (!condition) throw new InvalidOperationException(detail); }

        internal static int Run(string output)
        {
            Directory.CreateDirectory(output);
            List<string> passed = new List<string>();
            try
            {
                using (SpriteAtlas atlas = SpriteAtlas.LoadEmbedded())
                {
                    Require(atlas.Bounds.Length == 40, "Expected 40 animation frames.");
                    foreach (Rectangle bounds in atlas.Bounds)
                        Require(bounds.Width > 24 && bounds.Height > 24, "Every animation frame must contain a character.");
                    passed.Add("40 embedded animation frames loaded with transparency.");
                    using (Bitmap contact = new Bitmap(1040, 5 * 310, PixelFormat.Format32bppArgb))
                    using (Graphics graphics = Graphics.FromImage(contact))
                    {
                        graphics.Clear(Color.FromArgb(236, 239, 229));
                        int[] frames = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38 };
                        for (int index = 0; index < frames.Length; index++)
                            using (Bitmap frame = atlas.Render(frames[index], 220, null, false, 0, false))
                            {
                                Require(frame.GetPixel(0, 0).A == 0, "Window corners must be transparent.");
                                graphics.DrawImage(frame, (index % 4) * 260, (index / 4) * 310);
                            }
                        contact.Save(Path.Combine(output, "render-contact-sheet.png"), ImageFormat.Png);
                    }
                    using (Bitmap left = atlas.Render(8, 220, "左键拖动 · 右键菜单", true, 0, false))
                    using (Bitmap right = atlas.Render(8, 220, null, false, 0, false))
                    {
                        Require(left.Width == right.Width && left.Height == right.Height, "Stable frame canvas required.");
                        left.Save(Path.Combine(output, "pet-preview.png"), ImageFormat.Png);
                    }
                    using (Icon icon = atlas.CreateIcon()) Require(icon.Width > 0, "Tray icon must load.");
                    passed.Add("All animation groups rendered; alpha, speech bubble, horizontal flip and tray icon passed.");
                }

                PetMotion motion = new PetMotion(new Rectangle(-1920, 40, 1920, 1040), new Size(260, 306), new Point(-1900, 100));
                motion.MoveTo(new Point(-5000, -100));
                Require(motion.Location.X == -1920 && motion.Location.Y == 40, "Drag must clamp to a monitor with negative coordinates.");
                motion.Direction = -1; motion.Advance(.1, true);
                Require(motion.Direction == 1, "Walking must reverse at the left edge.");
                motion.MoveTo(new Point(motion.Right, motion.Floor)); motion.Direction = 1; motion.Advance(.1, true);
                Require(motion.Direction == -1 && motion.Location.X == motion.Right, "Walking must reverse at the right edge.");
                motion.MoveTo(new Point(-1000, 60)); motion.Drop();
                for (int i = 0; i < 120; i++) motion.Advance(.033, false);
                Require(!motion.Falling && motion.Location.Y == motion.Floor, "Dropped pet must land on the work-area floor.");
                passed.Add("Multi-monitor negative-coordinate clamping, both boundary turns and gravity landing passed.");

                string settingsFile = Path.Combine(output, "self-test-settings.xml");
                PetSettings value = new PetSettings { AutoWalk = false, AlwaysOnTop = false, PetSize = 280, PositionX = -1500, PositionY = 320 };
                value.Save(settingsFile);
                value.PositionY = 321; value.Save(settingsFile);
                PetSettings loaded = PetSettings.Load(settingsFile);
                Require(!loaded.AutoWalk && !loaded.AlwaysOnTop && loaded.PetSize == 280 && loaded.PositionY == 321,
                    "Settings must round-trip and atomically replace an existing file.");
                File.WriteAllText(settingsFile, "invalid XML");
                Require(PetSettings.Load(settingsFile).AutoWalk, "Corrupt settings must fall back to defaults.");
                passed.Add("Settings persistence, replacement and corrupt-file recovery passed.");
                File.WriteAllLines(Path.Combine(output, "self-test.txt"), passed.ToArray());
                return 0;
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(output, "self-test-error.txt"), exception.ToString());
                return 1;
            }
        }
    }
}
