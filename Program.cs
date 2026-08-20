namespace MiniTranslation
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // 有已下载的更新版本则改为运行它（A/B 版本目录），本进程直接退出
            if (Core.UpdateManager.TryLaunchNewerAtStartup())
            {
                return;
            }

            // 限时等待而非立即放弃：更新重启的新实例需要等旧实例退出释放互斥量
            using var mutex = new Mutex(initiallyOwned: false, "MiniTranslation_SingleInstance");
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(10)))
                {
                    return;
                }
            }
            catch (AbandonedMutexException)
            {
                // 旧实例异常退出，互斥量已归本进程
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
