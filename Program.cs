namespace MiniTranslation
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // 有已下载的更新则先安装（安装完成后会自动重启新版本）
            if (Core.UpdateManager.TryApplyPendingAtStartup())
            {
                return;
            }

            using var mutex = new Mutex(true, "MiniTranslation_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
