using Microsoft.Win32;

namespace MiniTranslation.Core
{
    /// <summary>开机自启动，基于当前用户的 Run 注册表键。</summary>
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MiniTranslation";

        public static bool IsEnabled()
        {
            try
            {
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
            }
            catch
            {
                // 注册表不可写时忽略
            }
        }
    }
}
