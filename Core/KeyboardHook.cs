using System.Runtime.InteropServices;

namespace MiniTranslation.Core
{
    /// <summary>低级键盘钩子，检测指定修饰键连按两下并向目标窗口投递热键消息，不拦截原始消息。</summary>
    public static class KeyboardHook
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;

        private static IntPtr _hook;
        private static IntPtr _targetHwnd;
        private static Keys _target;
        // 委托必须保活，否则被 GC 回收后钩子回调会崩溃
        private static LowLevelKeyboardProc? _proc;
        private static bool _held;
        private static bool _tainted;
        private static bool _armed;
        private static bool _fired;
        private static uint _armedTime;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdllHookStruct
        {
            public uint VkCode, ScanCode, Flags, Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        public static bool Start(IntPtr targetHwnd, Keys target)
        {
            _targetHwnd = targetHwnd;
            _target = target;
            _held = _tainted = _armed = _fired = false;
            if (_hook != IntPtr.Zero) return true;
            _proc = HookProc;
            _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public static void Stop()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _proc = null;
        }

        private static bool IsTarget(uint vk) => _target switch
        {
            Keys.ControlKey => vk is 0x11 or 0xA2 or 0xA3,
            Keys.Menu => vk is 0x12 or 0xA4 or 0xA5,
            Keys.ShiftKey => vk is 0x10 or 0xA0 or 0xA1,
            _ => false,
        };

        private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
                int msg = (int)wParam;
                bool down = msg is WmKeyDown or WmSysKeyDown;
                bool up = msg is WmKeyUp or WmSysKeyUp;
                if (IsTarget(info.VkCode))
                {
                    if (down && !_held) // 长按产生的重复 down 不算新按下
                    {
                        _held = true;
                        _tainted = false;
                        if (_armed && info.Time - _armedTime <= GetDoubleClickTime())
                        {
                            _armed = false;
                            _fired = true;
                            PostMessage(_targetHwnd, HotKeyManager.WmHotKey, HotKeyManager.HotKeyId, IntPtr.Zero);
                        }
                    }
                    else if (up)
                    {
                        _held = false;
                        // 触发后的抬起不再蓄力，避免三连击触发两次；
                        // 按住期间敲过其他键（组合键用法）也不算一次点按
                        _armed = !_fired && !_tainted;
                        _fired = false;
                        if (_armed) _armedTime = info.Time;
                    }
                }
                else if (down)
                {
                    _tainted = true;
                    _armed = false;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}
