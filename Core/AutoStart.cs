using Microsoft.Win32;

namespace MiniTranslation.Core
{
    /// <summary>开机自启动，基于当前用户的 Run 注册表键。</summary>
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MiniTranslation";

        // 旧版安装包用启动文件夹快捷方式实现自启动，需一并识别与清理
        private static string StartupShortcut =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MiniTranslation.lnk");

        public static bool IsEnabled()
        {
            try
            {
                if (File.Exists(StartupShortcut)) return true;
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string path &&
                       string.Equals(path.Trim('"'), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (enabled)
                {
                    key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
                // 统一到注册表方案，无论开关都清掉旧快捷方式，避免双重自启
                if (File.Exists(StartupShortcut)) File.Delete(StartupShortcut);
            }
            catch
            {
                // 注册表/文件不可写时忽略
            }
        }
    }
}
