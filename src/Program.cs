using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace HainanTurtlePet
{
    internal static class Program
    {
        public const string Version = "1.0.0";
        public const string AppName = "海南腿毛龟 · 野生版";

        [STAThread]
        private static int Main(string[] args)
        {
            try { NativeMethods.SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--quit")
            {
                try
                {
                    using (EventWaitHandle request = EventWaitHandle.OpenExisting("Local\\HainanTurtleDesktopPet.Exit"))
                        request.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { }
                return 0;
            }
            if (args.Length > 0 && args[0] == "--self-test")
                return SelfTest.Run(args.Length > 1 ? args[1] : "test-results");

            string smokeOutput = args.Length > 1 && args[0] == "--smoke-test" ? args[1] : null;
            bool created;
            using (Mutex singleInstance = new Mutex(true, "Local\\HainanTurtleDesktopPet", out created))
            {
                if (!created)
                {
                    if (smokeOutput == null)
                        MessageBox.Show("桌宠已经在运行啦。\n可以在任务栏右下角的托盘里找到它。", AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 2;
                }
                try
                {
                    string settingsPath = smokeOutput == null ? PetSettings.DefaultPath :
                        Path.Combine(Path.GetFullPath(smokeOutput), "smoke-settings.xml");
                    using (SpriteAtlas atlas = SpriteAtlas.LoadEmbedded())
                    using (PetForm pet = new PetForm(atlas, settingsPath, smokeOutput))
                        Application.Run(pet);
                    return 0;
                }
                finally { singleInstance.ReleaseMutex(); }
            }
        }
    }
}
