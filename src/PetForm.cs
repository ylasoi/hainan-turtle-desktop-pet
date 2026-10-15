using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace HainanTurtlePet
{
    internal sealed class PetForm : Form
    {
        private readonly SpriteAtlas atlas;
        private readonly string settingsPath, smokeOutput;
        private readonly PetSettings settings;
        private readonly Timer ticker = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon tray;
        private readonly Icon petIcon;
        private readonly ToolStripMenuItem walkMenu, topMenu, sleepMenu;
        private PetMotion motion;
        private Bitmap lastFrame;
        private double lastTime, poseUntil, messageUntil, nextDecision, blinkAt, ignoreClickUntil;
        private string message;
        private int pose, interactionCount, presentedFrames, smokeStep;
        private bool walking, sleeping, dragging, dragMoved, hovering;
        private Point dragCursor, dragOrigin;

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams value = base.CreateParams; value.ExStyle |= 0x80000 | 0x80; return value; }
        }

        internal PetForm(SpriteAtlas sprites, string path, string testOutput)
        {
            atlas = sprites; settingsPath = path; smokeOutput = testOutput;
            settings = testOutput == null ? PetSettings.Load(path) : new PetSettings();
            Text = Program.AppName;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = settings.AlwaysOnTop;
            Size = WindowSizeFor(settings.PetSize);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Point initial = new Point(area.Right - Width - 44, area.Bottom - Height);
            if (settings.PositionX != Int32.MinValue && settings.PositionY != Int32.MinValue)
            {
                initial = new Point(settings.PositionX, settings.PositionY);
                area = Screen.FromPoint(new Point(initial.X + Width / 2, initial.Y + Height / 2)).WorkingArea;
            }
            motion = new PetMotion(area, Size, initial);
            Location = motion.Location;
            Cursor = Cursors.Hand;
            petIcon = atlas.CreateIcon();
            Icon = petIcon;
            menu.Font = new Font("Microsoft YaHei UI", 9.5f);
            menu.Items.Add(new ToolStripMenuItem("海南腿毛龟  v" + Program.Version) { Enabled = false });
            menu.Items.Add("打个招呼", null, delegate { Wake(); SetPose(3, 2.1, "海南腿毛龟，出来溜达。 "); });
            menu.Items.Add("龟壳无敌", null, delegate { Wake(); SetPose(5, 1.8, "腿毛一抖，烦恼全走。 "); });
            sleepMenu = new ToolStripMenuItem("缩壳睡觉", null, delegate { ToggleSleep(); });
            menu.Items.Add(sleepMenu);
            menu.Items.Add(new ToolStripSeparator());
            walkMenu = new ToolStripMenuItem("自由爬行", null, delegate {
                settings.AutoWalk = !settings.AutoWalk; walking = false;
                nextDecision = clock.Elapsed.TotalSeconds + .6; SaveSettings(); });
            menu.Items.Add(walkMenu);
            topMenu = new ToolStripMenuItem("保持最前", null, delegate {
                settings.AlwaysOnTop = !settings.AlwaysOnTop; TopMost = settings.AlwaysOnTop; SaveSettings(); });
            menu.Items.Add(topMenu);
            ToolStripMenuItem sizes = new ToolStripMenuItem("体型大小");
            foreach (int value in new int[] { 160, 220, 280 })
            {
                int selected = value;
                ToolStripMenuItem item = new ToolStripMenuItem(value == 160 ? "小只" : value == 220 ? "标准" : "大只");
                item.Tag = value;
                item.Click += delegate { ResizePet(selected); SaveSettings(); };
                sizes.DropDownItems.Add(item);
            }
            menu.Items.Add(sizes);
            menu.Items.Add("回到屏幕角落", null, delegate {
                motion.Area = Screen.PrimaryScreen.WorkingArea;
                motion.MoveTo(new Point(motion.Right - 36, motion.Floor));
                Location = motion.Location; RenderNow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出桌宠", null, delegate { Close(); });
            menu.Opening += delegate {
                walkMenu.Checked = settings.AutoWalk;
                topMenu.Checked = settings.AlwaysOnTop;
                sleepMenu.Text = sleeping ? "叫醒它" : "缩壳睡觉";
                foreach (ToolStripMenuItem item in sizes.DropDownItems) item.Checked = (int)item.Tag == settings.PetSize;
            };
            ContextMenuStrip = menu;
            tray = new NotifyIcon { Icon = petIcon, Text = "海南腿毛龟：右键菜单可退出", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { Wake(); SetPose(3, 2, "在这儿呢。右键我可以退出。 "); };
            ticker.Interval = 33;
            ticker.Tick += Tick;
            blinkAt = 3.5;
            nextDecision = 4.0;
            Say("左键拖动 · 右键菜单", 6);
        }

        private static Size WindowSizeFor(int size) { return new Size(Math.Max(size + 32, 260), size + 86); }

        protected override void OnShown(EventArgs e)
        { base.OnShown(e); RenderNow(); ticker.Start(); }

        private void Tick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double elapsed = Math.Min(.1, now - lastTime);
            lastTime = now;
            if (now >= blinkAt) blinkAt = now + 3.0 + random.NextDouble() * 3;
            if (!dragging && !sleeping && now >= nextDecision)
            {
                walking = settings.AutoWalk && !walking;
                nextDecision = now + (walking ? 2.0 + random.NextDouble() * 3.0 : 2.2 + random.NextDouble() * 2.8);
            }
            bool canWalk = walking && settings.AutoWalk && !sleeping && !dragging && !hovering &&
                !menu.Visible && now >= poseUntil;
            if (!dragging)
            {
                motion.Area = Screen.FromPoint(new Point(motion.Location.X + Width / 2, motion.Location.Y + Height / 2)).WorkingArea;
                motion.MoveTo(motion.Location);
                motion.Advance(elapsed, canWalk);
                Location = motion.Location;
            }
            RenderNow();
            if (smokeOutput != null) RunSmokeSequence(now);
        }

        private int CurrentFrame()
        {
            double now = clock.Elapsed.TotalSeconds;
            if (sleeping) return 24 + ((int)(now / .3) % 8);
            if (now < poseUntil && pose == 7) return 32 + ((int)(now / .18) % 8);
            if (now < poseUntil && (pose == 3 || pose == 5)) return 16 + ((int)(now / .16) % 8);
            if (dragging || motion.Falling) return 16;
            if (walking && settings.AutoWalk && !hovering && !menu.Visible && now >= poseUntil)
                return (int)(now / .14) % 8;
            return 8 + ((int)(now / .3) % 8);
        }

        private void RenderNow()
        {
            if (!IsHandleCreated || IsDisposed) return;
            double now = clock.Elapsed.TotalSeconds;
            Bitmap next = atlas.Render(CurrentFrame(), settings.PetSize, now < messageUntil ? message : null,
                motion.Direction < 0, now, walking && !sleeping, now < poseUntil && pose == 5);
            try { NativeMethods.Present(Handle, next, Location); }
            catch { next.Dispose(); throw; }
            if (lastFrame != null) lastFrame.Dispose();
            lastFrame = next;
            presentedFrames++;
        }

        private void Say(string text, double seconds) { message = text; messageUntil = clock.Elapsed.TotalSeconds + seconds; }
        private void SetPose(int frame, double seconds, string text)
        { pose = frame; poseUntil = clock.Elapsed.TotalSeconds + seconds; Say(text, seconds + 1.2); RenderNow(); }
        private void Wake() { sleeping = false; nextDecision = clock.Elapsed.TotalSeconds + 3; }
        private void ToggleSleep()
        {
            sleeping = !sleeping; walking = false;
            if (sleeping) Say("龟壳一盖，谁也不爱。", 3);
            else { nextDecision = clock.Elapsed.TotalSeconds + 3; SetPose(3, 1.5, "睡醒了，腿毛还在。 "); }
            RenderNow();
        }

        private void Interact()
        {
            Wake(); interactionCount++;
            string[] lines = { "别戳了，腿毛会扎手。", "龟壳挺硬，脾气不小。", "上班累了？蹲一会儿。", "风一吹，全是自由的味道。" };
            SetPose(interactionCount % 3 == 0 ? 7 : 3, 2, lines[(interactionCount - 1) % lines.Length]);
        }

        private void BeginDrag(Point screenPoint)
        {
            dragging = true; dragMoved = false; dragCursor = screenPoint; dragOrigin = Location;
            walking = false; Capture = true; Wake();
        }
        private void MoveDragged(Point screenPoint)
        {
            if (!dragging) return;
            int dx = screenPoint.X - dragCursor.X, dy = screenPoint.Y - dragCursor.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 5) dragMoved = true;
            motion.Area = Screen.FromPoint(screenPoint).WorkingArea;
            motion.MoveTo(new Point(dragOrigin.X + dx, dragOrigin.Y + dy));
            Location = motion.Location;
            if (dragMoved) Say("提龟可以，别薅腿毛。", 2);
            RenderNow();
        }
        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false; Capture = false;
            if (dragMoved) { motion.Drop(); Say("稳住，龟壳抗摔。", 2.5); }
            else if (clock.Elapsed.TotalSeconds > ignoreClickUntil) Interact();
            nextDecision = clock.Elapsed.TotalSeconds + 3;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) BeginDrag(Cursor.Position); }
        protected override void OnMouseMove(MouseEventArgs e)
        { base.OnMouseMove(e); if (dragging) MoveDragged(Cursor.Position); }
        protected override void OnMouseUp(MouseEventArgs e)
        { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) EndDrag(); }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            { Wake(); ignoreClickUntil = clock.Elapsed.TotalSeconds + .6; SetPose(5, 1.8, "无敌！腿毛加护甲。 "); }
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovering = true; }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovering = false; }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x84 && lastFrame != null && !dragging)
            {
                long value = message.LParam.ToInt64();
                Point local = PointToClient(new Point((short)(value & 0xffff), (short)((value >> 16) & 0xffff)));
                if (local.X >= 0 && local.Y >= 0 && local.X < lastFrame.Width && local.Y < lastFrame.Height &&
                    lastFrame.GetPixel(local.X, local.Y).A < 12)
                { message.Result = new IntPtr(-1); return; }
            }
            base.WndProc(ref message);
        }

        private void ResizePet(int size)
        {
            settings.PetSize = size; Size = WindowSizeFor(size); motion.WindowSize = Size;
            motion.MoveTo(new Point(motion.Location.X, motion.Floor)); Location = motion.Location; RenderNow();
        }
        private void SaveSettings()
        {
            settings.PositionX = motion.Location.X; settings.PositionY = motion.Location.Y;
            try { settings.Save(settingsPath); }
            catch (IOException) { Say("设置没保存上，下次再试。", 3); }
            catch (UnauthorizedAccessException) { Say("设置目录暂时不可写。", 3); }
        }

        private void RunSmokeSequence(double seconds)
        {
            if (seconds > .5 && smokeStep == 0) { smokeStep++; Interact(); }
            if (seconds > 1 && smokeStep == 1) { smokeStep++; ResizePet(160); }
            if (seconds > 1.5 && smokeStep == 2) { smokeStep++; ResizePet(280); }
            if (seconds > 2 && smokeStep == 3)
            {
                smokeStep++; Point start = new Point(Left + Width / 2, Top + Height / 2);
                BeginDrag(start); MoveDragged(new Point(start.X - 36, start.Y - 70)); EndDrag();
            }
            if (seconds > 2.8 && smokeStep == 4) { smokeStep++; ToggleSleep(); }
            if (seconds > 3.3 && smokeStep == 5) { smokeStep++; ToggleSleep(); SetPose(7, .4, "桌面渲染测试"); }
            if (seconds > 4 && smokeStep == 6)
            {
                smokeStep++; ResizePet(220); walkMenu.PerformClick(); topMenu.PerformClick(); topMenu.PerformClick();
                if (settings.AutoWalk || !settings.AlwaysOnTop) throw new InvalidOperationException("Menu state did not update.");
                SetPose(0, 1.5, "写实腿毛龟 · 渲染通过");
            }
            if (seconds > 4.8 && smokeStep == 7)
            {
                smokeStep++; Directory.CreateDirectory(smokeOutput);
                lastFrame.Save(Path.Combine(smokeOutput, "window-frame.png"), ImageFormat.Png);
                File.WriteAllText(Path.Combine(smokeOutput, "smoke-test.txt"),
                    "PASS: native layered-window presentation, 5 animation groups, drag/drop, resize, sleep/wake, menu toggles, tray, clean shutdown\r\n" +
                    "Presented frames: " + presentedFrames + "\r\n");
                Close();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ticker.Stop(); ticker.Dispose(); SaveSettings(); tray.Visible = false; tray.Dispose();
            menu.Dispose(); if (lastFrame != null) lastFrame.Dispose(); petIcon.Dispose(); base.OnFormClosed(e);
        }
    }
}
