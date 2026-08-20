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

        private const int ContentWidth = 400;
        private const int Margin_ = 18;

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

            _inputBox = new TextBox
            {
                Location = new Point(Margin_, Margin_),
                Size = new Size(ContentWidth, 28),
                Font = new Font("Microsoft YaHei UI", 11.5F),
                BorderStyle = BorderStyle.None,
                BackColor = CardBack,
                ForeColor = TextMain,
                Multiline = true,
                WordWrap = false,
                AcceptsReturn = true,
                MaxLength = 99999999,
                HideSelection = false,
            };
            _inputBox.KeyDown += InputBox_KeyDown;

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
                Font = new Font("Microsoft YaHei UI", 11.5F),
                BorderStyle = BorderStyle.None,
                BackColor = CardBack,
                ForeColor = TextResult,
                Multiline = true,
                ReadOnly = true,
                TabStop = false,
            };

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

            Controls.AddRange(new Control[] { _inputBox, _separator, _resultBox, _speakLabel, _copyLabel, _statusLabel });

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示", null, (_, _) => SetVisible(true));
            trayMenu.Items.Add("设置", null, (_, _) => OpenSettings());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出", null, (_, _) => ExitApp());

            _notifyIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = "MiniTranslation（Alt+Q 显示/隐藏）",
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
            if (!HotKeyManager.Register(Handle, HotKeyManager.Modifiers.Alt, Keys.Q))
            {
                _notifyIcon?.ShowBalloonTip(3000, "MiniTranslation", "热键 Alt+Q 注册失败，可能被其他程序占用。", ToolTipIcon.Warning);
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

        /// <summary>向前台窗口模拟 Ctrl+C 抓取选中文本，再显示窗口并翻译。</summary>
        private async Task ShowWithSelectionCaptureAsync()
        {
            bool captured = false;
            try
            {
                string before = GetClipboardText();
                SendCopyShortcut();
                await Task.Delay(150);
                string after = GetClipboardText();
                captured = after.Length > 0 && after != before;
            }
            catch
            {
            }
            SetVisible(true); // 若开启了剪贴板自动翻译，常规逻辑已能接住新内容
            if (captured) TryTranslateClipboard(force: true);
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static void SendCopyShortcut()
        {
            const uint KeyUp = 0x2;
            // 热键 Alt+Q 的 Alt 此时可能仍被按住，先补发抬起，避免变成 Ctrl+Alt+C
            keybd_event(0x12, 0, KeyUp, UIntPtr.Zero); // Alt
            keybd_event(0x11, 0, 0, UIntPtr.Zero);     // Ctrl 按下
            keybd_event(0x43, 0, 0, UIntPtr.Zero);     // C 按下
            keybd_event(0x43, 0, KeyUp, UIntPtr.Zero);
            keybd_event(0x11, 0, KeyUp, UIntPtr.Zero);
        }

        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
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
            string text = System.Text.RegularExpressions.Regex
                .Replace(_inputBox.Text, @"\s+", " ").Trim();
            if (text.Length == 0) return;
            _inputBox.Text = text;
            _inputBox.SelectionStart = text.Length;

            if (!_settings.IsConfigured)
            {
                ShowResult("请先配置翻译接口（API 地址 / Key / 模型）。", isError: true);
                OpenSettings();
                return;
            }

            _translateCts?.Cancel();
            var cts = new CancellationTokenSource();
            _translateCts = cts;
            SetStatus("翻译中…");

            try
            {
                var result = await TranslationRouter.TranslateAsync(text, _settings, cts.Token);
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
            _resultBox.Text = text;
            LayoutContent();
        }

        /// <summary>根据结果行数自适应窗体高度。</summary>
        private void LayoutContent()
        {
            if (_resultBox.TextLength == 0)
            {
                _resultBox.Height = 0;
            }
            else
            {
                int lines = _resultBox.GetLineFromCharIndex(_resultBox.TextLength) + 1;
                _resultBox.Height = lines * (_resultBox.Font.Height + 4);
            }

            _actionBarTop = (_resultBox.TextLength == 0 ? _separator.Bottom : _resultBox.Bottom) + 12;
            _speakLabel.Location = new Point(Margin_ - 2, _actionBarTop);
            _copyLabel.Location = new Point(_speakLabel.Right + 14, _actionBarTop);
            PositionStatusLabel();
            ClientSize = new Size(ContentWidth + Margin_ * 2, _speakLabel.Bottom + 14);
        }

        /// <summary>状态文字右对齐；文字变化后需重新计算位置。</summary>
        private void PositionStatusLabel() =>
            _statusLabel.Location = new Point(Margin_ + ContentWidth - _statusLabel.PreferredWidth, _actionBarTop);

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
            try
            {
                dialog.ShowDialog(this);
            }
            finally
            {
                TopMost = true;
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
