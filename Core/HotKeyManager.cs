using System.Runtime.InteropServices;

namespace MiniTranslation.Core
{
    /// <summary>全局热键注册。</summary>
    public static class HotKeyManager
    {
        public const int WmHotKey = 0x0312;
        public const int WmQueryEndSession = 0x0011;
        public const int HotKeyId = 0x3572;
        public const string MouseMiddleDouble = "MouseMiddleDouble";

        /// <summary>快捷键配置是否为鼠标中键双击（此类触发不走 RegisterHotKey）。</summary>
        public static bool IsMouseTrigger(string? text) =>
            string.Equals(text?.Trim(), MouseMiddleDouble, StringComparison.OrdinalIgnoreCase);

        /// <summary>解析 "CtrlDouble" 形式的修饰键连按触发（此类触发不走 RegisterHotKey）。</summary>
        public static bool TryParseDoubleModifier(string? text, out Keys key)
        {
            key = text?.Trim().ToLowerInvariant() switch
            {
                "ctrldouble" => Keys.ControlKey,
                "altdouble" => Keys.Menu,
                "shiftdouble" => Keys.ShiftKey,
                _ => Keys.None,
            };
            return key != Keys.None;
        }

        public static string FormatDoubleModifier(Keys key) => key switch
        {
            Keys.ControlKey => "CtrlDouble",
            Keys.Menu => "AltDouble",
            Keys.ShiftKey => "ShiftDouble",
            _ => "",
        };

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

        /// <summary>解析 "Ctrl+Alt+Q" 形式的快捷键描述。</summary>
        public static bool TryParse(string text, out Modifiers modifiers, out Keys key)
        {
            modifiers = Modifiers.None;
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (string part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl" or "control": modifiers |= Modifiers.Ctrl; break;
                    case "alt": modifiers |= Modifiers.Alt; break;
                    case "shift": modifiers |= Modifiers.Shift; break;
                    case "win": modifiers |= Modifiers.Win; break;
                    default:
                        if (!Enum.TryParse(part, ignoreCase: true, out key)) return false;
                        break;
                }
            }
            return key != Keys.None;
        }

        public static string Format(Modifiers modifiers, Keys key)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(Modifiers.Ctrl)) parts.Add("Ctrl");
            if (modifiers.HasFlag(Modifiers.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(Modifiers.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(Modifiers.Win)) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }
    }
}
