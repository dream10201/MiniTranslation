using System.Runtime.InteropServices;

namespace MiniTranslation.Core
{
    /// <summary>低级鼠标钩子，检测鼠标中键双击并向目标窗口投递热键消息，不拦截原始消息。</summary>
    public static class MouseHook
    {
        private const int WhMouseLl = 14;
        private const int WmMButtonDown = 0x0207;
        private const int SmCxDoubleClk = 36;
        private const int SmCyDoubleClk = 37;

        private static IntPtr _hook;
        private static IntPtr _targetHwnd;
        // 委托必须保活，否则被 GC 回收后钩子回调会崩溃
        private static LowLevelMouseProc? _proc;
        private static uint _lastDownTime;
        private static int _lastX, _lastY;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MsllHookStruct
        {
            public int X, Y;
            public uint MouseData, Flags, Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        public static bool Start(IntPtr targetHwnd)
        {
            _targetHwnd = targetHwnd;
            if (_hook != IntPtr.Zero) return true;
            _proc = HookProc;
            _hook = SetWindowsHookEx(WhMouseLl, _proc, GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public static void Stop()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _proc = null;
        }

        private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == WmMButtonDown)
            {
                var info = Marshal.PtrToStructure<MsllHookStruct>(lParam);
                bool isDouble = info.Time - _lastDownTime <= GetDoubleClickTime()
                    && Math.Abs(info.X - _lastX) <= GetSystemMetrics(SmCxDoubleClk) / 2
                    && Math.Abs(info.Y - _lastY) <= GetSystemMetrics(SmCyDoubleClk) / 2;
                if (isDouble)
                {
                    _lastDownTime = 0; // 三连击不算第二次双击
                    PostMessage(_targetHwnd, HotKeyManager.WmHotKey, HotKeyManager.HotKeyId, IntPtr.Zero);
                }
                else
                {
                    _lastDownTime = info.Time;
                    _lastX = info.X;
                    _lastY = info.Y;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }
}
