using System.Runtime.InteropServices;
using MiniTranslation.Core;

namespace MiniTranslation
{
    public sealed class MainForm : Form
    {
        private static readonly Color CardBack = Color.White;
        private static readonly Color TextMain = Color.FromArgb(32, 33, 36);
        private static readonly Color TextResult = Color.FromArgb(25, 103, 210);
        private static readonly Color TextMuted = Color.FromArgb(128, 132, 139);
        private static readonly Color TextError = Color.FromArgb(197, 34, 31);
        private static readonly Color LineColor = Color.FromArgb(232, 234, 237);
        private static readonly Font UiFont = new("Microsoft YaHei UI", 10.5F);

        private readonly TextBox _inputBox;
        private readonly TextBox _resultBox;
        private readonly Panel _separator;
        private readonly Panel _inputScrollBar;
        private readonly Panel _resultScrollBar;
        private readonly Label _speakLabel;
        private readonly Label _copyLabel;
        private readonly Label _statusLabel;
        private readonly NotifyIcon _notifyIcon;

        private readonly AppSettings _settings = AppSettings.Load();
        private readonly SpeechService _speech = new();
        private CancellationTokenSource? _translateCts;
        private string _speakText = "";
        private string _lastClipboardText = "";
        private bool _isShown;
        private int _actionBarTop;

        private const int ContentWidth = 400; // 内容区最小宽度
        private const int Margin_ = 18;
        private int _contentWidth = ContentWidth;

        public MainForm()
        {
            SuspendLayout();

            Text = "MiniTranslation";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = CardBack;
            ClientSize = new Size(ContentWidth + Margin_ * 2, 96);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = false;
            ShowIcon = false;
            KeyPreview = true;
            Font = UiFont;
            DoubleBuffered = true;
            // 尺寸变化时整体重绘，否则 OnPaint 画的边框会在原位置留下残影
            SetStyle(ControlStyles.ResizeRedraw, true);

            _inputBox = new TextBox
            {
                Location = new Point(Margin_, Margin_),
                Size = new Size(ContentWidth, 28),
                // 拉丁字形用 Segoe UI，中文经系统 font linking 回落到雅黑
                Font = new Font("Segoe UI", 11F),
                BorderStyle = BorderStyle.None,
                BackColor = CardBack,
                ForeColor = TextMain,
                Multiline = true,
                WordWrap = true,
                AcceptsReturn = true,
                MaxLength = 99999999,
                HideSelection = false,
            };
            _inputBox.KeyDown += InputBox_KeyDown;
            _inputBox.TextChanged += (_, _) => LayoutContent();

            _separator = new Panel
            {
                Location = new Point(Margin_, _inputBox.Bottom + 10),
                Size = new Size(ContentWidth, 1),
                BackColor = LineColor,
            };

            _resultBox = new TextBox
            {
                Location = new Point(Margin_, _separator.Bottom + 12),
                Size = new Size(ContentWidth, 0),
                Font = new Font("Segoe UI", 11F),
                BorderStyle = BorderStyle.None,
                BackColor = CardBack,
                ForeColor = TextResult,
                Multiline = true,
                ReadOnly = true,
                TabStop = false,
            };

            _inputScrollBar = CreateScrollIndicator();
            _resultScrollBar = CreateScrollIndicator();

            // 无滚动条样式的多行 TextBox 不响应滚轮，手动接管；
            // 按光标位置分发，悬停在译文上也能滚
            MouseWheel += (_, e) => HandleWheel(e);
            _inputBox.MouseWheel += (_, e) => HandleWheel(e);
            _resultBox.MouseWheel += (_, e) => HandleWheel(e);
            _inputBox.KeyUp += (_, _) => UpdateScrollIndicators();

            _speakLabel = CreateActionLabel("\U0001F50A 朗读", (_, _) => Speak());
            _copyLabel = CreateActionLabel("复制", (_, _) => CopyResult());
            _statusLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = TextMuted,
                BackColor = CardBack,
                Text = "",
            };

            Controls.AddRange(new Control[]
            {
                _inputBox, _separator, _resultBox,
                _inputScrollBar, _resultScrollBar,
                _speakLabel, _copyLabel, _statusLabel,
            });

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示", null, (_, _) => SetVisible(true));
            trayMenu.Items.Add("设置", null, (_, _) => OpenSettings());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出", null, (_, _) => ExitApp());

            _notifyIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = "MiniTranslation",
                Visible = true,
                ContextMenuStrip = trayMenu,
            };
            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) SetVisible(true);
            };

            KeyPress += (_, e) =>
            {
                if (e.KeyChar == (char)27) SetVisible(false);
            };
            Activated += (_, _) =>
            {
                _inputBox.Focus();
                _inputBox.SelectionStart = _inputBox.TextLength;
            };
            Deactivate += (_, _) =>
            {
                if (_settings.HideOnFocusLost && _isShown) SetVisible(false);
            };
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    SetVisible(false);
                }
            };
            Shown += (_, _) => Hide();
            MouseDown += Form_MouseDown;

            LayoutContent();
            ResumeLayout(false);
        }

        private Label CreateActionLabel(string text, EventHandler onClick)
        {
            var label = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = TextMuted,
                BackColor = CardBack,
                Text = text,
                Cursor = Cursors.Hand,
            };
            label.Click += onClick;
            label.MouseEnter += (_, _) => label.ForeColor = TextResult;
            label.MouseLeave += (_, _) => label.ForeColor = TextMuted;
            return label;
        }

        #region 窗体外观（圆角 + 阴影 + 拖动）

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        private static void ApplyRoundedCorners(IntPtr hwnd)
        {
            try
            {
                int preference = 2; // DWMWCP_ROUND，Windows 11 生效，旧系统忽略
                DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
            }
            catch
            {
            }
        }

        private void Form_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0x00A1 /*WM_NCLBUTTONDOWN*/, 0x2 /*HTCAPTION*/, 0);
            }
        }

        #endregion

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedCorners(Handle);
            ApplyHotKey();
        }

        private void ApplyHotKey()
        {
            HotKeyManager.Unregister(Handle);
            if (!HotKeyManager.TryParse(_settings.HotKey, out var modifiers, out var key))
            {
                HotKeyManager.TryParse("Alt+Q", out modifiers, out key);
            }
            if (!HotKeyManager.Register(Handle, modifiers, key))
            {
                _notifyIcon?.ShowBalloonTip(3000, "MiniTranslation",
                    $"热键 {_settings.HotKey} 注册失败，可能被其他程序占用。", ToolTipIcon.Warning);
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case HotKeyManager.WmHotKey when m.WParam.ToInt32() == HotKeyManager.HotKeyId:
                    if (_isShown)
                    {
                        SetVisible(false);
                    }
                    else if (_settings.AutoTranslateSelection && _settings.IsConfigured)
                    {
                        _ = ShowWithSelectionCaptureAsync();
                    }
                    else
                    {
                        SetVisible(true);
                    }
                    break;
                case HotKeyManager.WmQueryEndSession:
                    m.Result = (IntPtr)1;
                    ExitApp();
                    return;
            }
            base.WndProc(ref m);
        }

        private void SetVisible(bool visible)
        {
            if (visible)
            {
                Show();
                Activate();
                _inputBox.SelectAll();
                TryTranslateClipboard();
            }
            else
            {
                _speech.Stop();
                Hide();
            }
            _isShown = visible;
        }

        /// <summary>显示窗口时，若剪贴板有新的文本则自动填入并翻译。</summary>
        private void TryTranslateClipboard(bool force = false)
        {
            if (!force && !_settings.AutoTranslateClipboard) return;
            if (!_settings.IsConfigured) return;
            string clip = GetClipboardText();
            if (clip.Length == 0 || clip == _lastClipboardText) return;
            _lastClipboardText = clip;
            _inputBox.Text = clip;
            StartTranslate();
        }

        private static string GetClipboardText()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
            }
            catch
            {
                return ""; // 剪贴板被其他程序占用
            }
        }

        /// <summary>向前台窗口模拟 Ctrl+C 抓取选中文本，窗口先显示，剪贴板在后台轮询。</summary>
        private async Task ShowWithSelectionCaptureAsync()
        {
            uint seqBefore = 0;
            bool sent = false;
            try
            {
                // 稍等热键的物理按键松开；等不到也没关系，注入时会
                // 强制抬起残留的修饰键。注入必须在本窗口抢焦点之前完成
                for (int i = 0; i < 10 && AnyHotKeyKeyDown(); i++)
                {
                    await Task.Delay(30);
                }
                seqBefore = GetClipboardSequenceNumber();
                SendCopyShortcut();
                sent = true;
                await Task.Delay(60); // 让目标窗口先处理注入的按键，再抢焦点
            }
            catch
            {
            }

            SetVisible(true);
            if (!sent) return;

            // 剪贴板序号只要发生过复制就会变化，与内容是否相同无关；
            // 放宽到 1.5s 兼容写剪贴板慢的程序
            for (int i = 0; i < 30; i++)
            {
                if (GetClipboardSequenceNumber() != seqBefore)
                {
                    TryTranslateClipboard(force: true);
                    return;
                }
                await Task.Delay(50);
            }
        }

        private bool AnyHotKeyKeyDown()
        {
            if (IsKeyDown(0x12) || IsKeyDown(0x11) || IsKeyDown(0x10)) // Alt/Ctrl/Shift
            {
                return true;
            }
            return HotKeyManager.TryParse(_settings.HotKey, out _, out var key) && IsKeyDown((int)key);
        }

        private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int Dx, Dy;
            public uint MouseData, Flags, Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort Vk, Scan;
            public uint Flags, Time;
            public IntPtr ExtraInfo;
        }

        private static Input Key(ushort vk, bool up) => new()
        {
            Type = 1, // INPUT_KEYBOARD
            U = new InputUnion { Keyboard = new KeyboardInput { Vk = vk, Flags = up ? 0x2u : 0u } },
        };

        private static void SendCopyShortcut()
        {
            var inputs = new List<Input>();
            // 把仍被物理按住的修饰键强制抬起，与 Ctrl+C 同批原子注入，
            // 避免目标程序收到 Ctrl+Shift+C / Ctrl+Alt+C 之类被污染的组合键
            foreach (ushort vk in new ushort[] { 0x12, 0x10, 0x5B, 0x5C, 0x11 }) // Alt/Shift/LWin/RWin/Ctrl
            {
                if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                {
                    inputs.Add(Key(vk, up: true));
                }
            }
            inputs.Add(Key(0x11, up: false)); // Ctrl down
            inputs.Add(Key(0x43, up: false)); // C down
            inputs.Add(Key(0x43, up: true));
            inputs.Add(Key(0x11, up: true));
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        }

        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter when !e.Shift: // Shift+Enter 换行
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    StartTranslate();
                    break;
                case Keys.A when e.Control:
                    _inputBox.SelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Tab:
                    Speak();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        private async void StartTranslate()
        {
            // 只合并行内多余空白，保留换行以保住段落结构
            string text = _inputBox.Text.Replace("\r\n", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, "[ \t]+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, " ?\n ?", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, "\n{3,}", "\n\n").Trim();
            if (text.Length == 0) return;
            _inputBox.Text = text.Replace("\n", "\r\n");
            _inputBox.SelectionStart = _inputBox.TextLength;

            if (!_settings.IsConfigured)
            {
                ShowResult("请先配置翻译接口（API 地址 / Key / 模型）。", isError: true);
                OpenSettings();
                return;
            }

            _translateCts?.Cancel();
            var cts = new CancellationTokenSource();
            _translateCts = cts;
            // 先清空上一次的结果，避免误当成本次译文
            _speakText = "";
            ShowResult("", isError: false);
            SetStatus("翻译中…");

            try
            {
                // 流式刷新节流到 80ms 一次，避免高频调整窗体导致闪烁；
                // 完成后仍会用完整译文收尾，不会丢字
                var throttle = System.Diagnostics.Stopwatch.StartNew();
                var result = await TranslationRouter.TranslateAsync(text, _settings, partial =>
                {
                    if (throttle.ElapsedMilliseconds < 80) return;
                    throttle.Restart();
                    // 回调来自线程池，需切回 UI 线程；换接口重试时全量覆盖
                    BeginInvoke(() =>
                    {
                        if (_translateCts == cts) ShowResult(partial, isError: false);
                    });
                }, cts.Token);
                if (cts.IsCancellationRequested) return;
                ShowResult(result.Text, isError: false);
                _speakText = result.SourceIsChinese ? result.Text : text;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested)
                {
                    ShowResult("翻译失败：" + ex.Message, isError: true);
                }
            }
            finally
            {
                if (_translateCts == cts) SetStatus("");
            }
        }

        private void ShowResult(string text, bool isError)
        {
            _resultBox.ForeColor = isError ? TextError : TextResult;
            // edit 控件对裸 \n 的显示与行数计算不可靠，统一为 \r\n
            _resultBox.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            LayoutContent();
        }

        /// <summary>
        /// 根据输入与译文内容双向自适应窗体尺寸：宽度取最宽一行（有上下限），
        /// 高度按换行后实际所需（有上限），超限时显示滚动条，并保证不超出工作区。
        /// </summary>
        private void LayoutContent()
        {
            var workArea = Screen.FromControl(this).WorkingArea;
            int maxContentWidth = Math.Min(760, workArea.Width - 120);

            int desired = Math.Max(WidestLine(_inputBox.Text, _inputBox.Font),
                                   WidestLine(_resultBox.Text, _resultBox.Font)) + 10;
            _contentWidth = Math.Clamp(desired, ContentWidth, maxContentWidth);

            // 先定宽，再用控件自身的实际换行行数算高度，保证所见即所得
            _inputBox.SetBounds(Margin_, Margin_, _contentWidth, _inputBox.Height);
            int maxInputHeight = _inputBox.Font.Height * 8 + 8;
            int inputNeeded = (_inputBox.GetLineFromCharIndex(_inputBox.TextLength) + 1) * _inputBox.Font.Height + 8;
            _inputBox.Height = Math.Min(inputNeeded, maxInputHeight);

            _separator.SetBounds(Margin_, _inputBox.Bottom + 10, _contentWidth, 1);

            int resultHeight = 0;
            if (_resultBox.TextLength > 0)
            {
                _resultBox.SetBounds(Margin_, _separator.Bottom + 12, _contentWidth, _resultBox.Height);
                int maxResultHeight = Math.Min(420, workArea.Height / 2);
                int resultNeeded = (_resultBox.GetLineFromCharIndex(_resultBox.TextLength) + 1) * _resultBox.Font.Height + 8;
                resultHeight = Math.Min(resultNeeded, maxResultHeight);
            }
            _resultBox.SetBounds(Margin_, _separator.Bottom + 12, _contentWidth, resultHeight);

            _actionBarTop = (resultHeight == 0 ? _separator.Bottom : _resultBox.Bottom) + 12;
            _speakLabel.Location = new Point(Margin_ - 2, _actionBarTop);
            _copyLabel.Location = new Point(_speakLabel.Right + 14, _actionBarTop);
            PositionStatusLabel();
            var newSize = new Size(_contentWidth + Margin_ * 2, _speakLabel.Bottom + 14);
            if (ClientSize != newSize)
            {
                ClientSize = newSize;
                Invalidate(); // 强制整面重绘，清掉旧边框线残影
            }

            if (Visible)
            {
                Location = new Point(
                    Math.Max(workArea.Left, Math.Min(Left, workArea.Right - Width)),
                    Math.Max(workArea.Top, Math.Min(Top, workArea.Bottom - Height)));
            }

            UpdateScrollIndicators();
        }

        private static int WidestLine(string text, Font font)
        {
            int widest = 0;
            foreach (string line in text.Split('\n'))
            {
                widest = Math.Max(widest, TextRenderer.MeasureText(line.TrimEnd('\r'), font).Width);
            }
            return widest;
        }

        #region 滚轮滚动与滚动指示条

        private const int EmLineScroll = 0x00B6;
        private const int EmGetFirstVisibleLine = 0x00CE;

        private static Panel CreateScrollIndicator() => new()
        {
            BackColor = Color.FromArgb(203, 206, 212),
            Size = new Size(3, 0),
            Visible = false,
            Enabled = false,
        };

        private void HandleWheel(MouseEventArgs e)
        {
            var pt = PointToClient(Cursor.Position);
            var target = _resultBox.Bounds.Contains(pt) && _resultBox.Height > 0 ? _resultBox : _inputBox;
            SendMessage(target.Handle, EmLineScroll, 0, -(e.Delta / 120) * 3);
            UpdateScrollIndicators();
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        private void UpdateScrollIndicators()
        {
            UpdateScrollIndicator(_inputBox, _inputScrollBar);
            UpdateScrollIndicator(_resultBox, _resultScrollBar);
        }

        /// <summary>内容超高时在右侧显示 3px 指示条，长度与位置对应可视比例。</summary>
        private void UpdateScrollIndicator(TextBox box, Panel bar)
        {
            int totalLines = box.GetLineFromCharIndex(box.TextLength) + 1;
            int visibleLines = Math.Max(1, box.Height / box.Font.Height);
            if (box.Height == 0 || box.TextLength == 0 || totalLines <= visibleLines)
            {
                bar.Visible = false;
                return;
            }
            int firstLine = (int)SendMessage(box.Handle, EmGetFirstVisibleLine, 0, 0);
            int maxFirst = Math.Max(1, totalLines - visibleLines);
            int barHeight = Math.Max(24, box.Height * visibleLines / totalLines);
            int barTop = box.Top + (box.Height - barHeight) * Math.Min(firstLine, maxFirst) / maxFirst;
            bar.SetBounds(box.Right + 6, barTop, 3, barHeight);
            bar.Visible = true;
        }

        #endregion

        /// <summary>状态文字右对齐；文字变化后需重新计算位置。</summary>
        private void PositionStatusLabel() =>
            _statusLabel.Location = new Point(Margin_ + _contentWidth - _statusLabel.PreferredWidth, _actionBarTop);

        private void SetStatus(string text)
        {
            _statusLabel.Text = text;
            PositionStatusLabel();
        }

        private void Speak()
        {
            if (!string.IsNullOrWhiteSpace(_speakText))
            {
                _speech.Speak(_speakText);
            }
        }

        private void CopyResult()
        {
            if (_resultBox.TextLength > 0)
            {
                Clipboard.SetText(_resultBox.Text);
                _lastClipboardText = _resultBox.Text.Trim(); // 复制的译文不触发自动翻译
                SetStatus("已复制");
            }
        }

        private void OpenSettings()
        {
            using var dialog = new SettingsForm(_settings);
            TopMost = false;
            HotKeyManager.Unregister(Handle); // 设置期间注销，避免录制快捷键时被全局热键拦截
            try
            {
                dialog.ShowDialog(this);
            }
            finally
            {
                TopMost = true;
                ApplyHotKey();
            }
        }

        private void ExitApp()
        {
            HotKeyManager.Unregister(Handle);
            _translateCts?.Cancel();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _speech.Dispose();
            Application.Exit();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(LineColor);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
