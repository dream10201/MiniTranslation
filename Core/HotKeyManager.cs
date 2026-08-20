using System.Runtime.InteropServices;

namespace MiniTranslation.Core
{
    /// <summary>全局热键注册。</summary>
    public static class HotKeyManager
    {
        public const int WmHotKey = 0x0312;
        public const int WmQueryEndSession = 0x0011;
        public const int HotKeyId = 0x3572;

        [Flags]
        public enum Modifiers : uint
        {
            None = 0,
            Alt = 1,
            Ctrl = 2,
            Shift = 4,
            Win = 8,
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, Modifiers fsModifiers, Keys vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool Register(IntPtr hwnd, Modifiers modifiers, Keys key) =>
            RegisterHotKey(hwnd, HotKeyId, modifiers, key);

        public static void Unregister(IntPtr hwnd) => UnregisterHotKey(hwnd, HotKeyId);
    }
}
