using System.Drawing.Drawing2D;
using MiniTranslation.Core;

namespace MiniTranslation
{
    /// <summary>翻译接口设置：可维护多套 API 配置并手动排序（顺序即优先级）。</summary>
    public sealed class SettingsForm : Form
    {
        private static readonly Color TextMain = Color.FromArgb(32, 33, 36);
        private static readonly Color TextMuted = Color.FromArgb(128, 132, 139);
        private static readonly Color Accent = Color.FromArgb(25, 103, 210);
        private static readonly Color LineColor = Color.FromArgb(225, 228, 232);

        private readonly AppSettings _settings;
        private readonly List<ApiProfile> _profiles;
        private readonly List<int> _separatorYs = new();

        private readonly ListBox _profileList;
        private readonly TextBox _urlBox;
        private readonly TextBox _keyBox;
        private readonly TextBox _modelBox;
        private readonly TextBox _hotKeyBox;
        private readonly CheckBox _clipboardCheck;
        private readonly CheckBox _selectionCheck;
        private readonly CheckBox _hideOnFocusLostCheck;
        private readonly CheckBox _autoCopyCheck;
        private readonly CheckBox _autoStartCheck;
        private readonly CheckBox _autoUpdateCheck;
        private readonly Button _testButton;
        private readonly Button _saveButton;
        private readonly Button _updateButton;
        private readonly Label _statusLabel;
        private bool _loadingFields;
        private int _lastMiddleDownTick;
        private Keys _lastModUpKey;
        private int _lastModUpTick;
        private PendingUpdate? _pending;

        private const int LabelX = 24, InputX = 110, InputW = 366, RowW = 452;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            _profiles = settings.Profiles.Select(p => p.Clone()).ToList();
            if (_profiles.Count == 0) _profiles.Add(new ApiProfile());

            Text = "设置";
            Font = new Font("Microsoft YaHei UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;

            int y = 20;

            // ---- 接口 ----
            AddMutedLabel("接口列表", LabelX, y);
            y += 28;

            _profileList = new ListBox
            {
                Location = new Point(LabelX, y),
                Size = new Size(RowW - 102, 118),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                IntegralHeight = false,
            };
            _profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
            Controls.Add(_profileList);

            int buttonX = LabelX + _profileList.Width + 10;
            Controls.Add(CreateSmallButton("添加", new Point(buttonX, y), AddProfile));
            Controls.Add(CreateSmallButton("删除", new Point(buttonX, y + 32), RemoveProfile));
            Controls.Add(CreateSmallButton("上移", new Point(buttonX, y + 64), (_, _) => MoveProfile(-1)));
            Controls.Add(CreateSmallButton("下移", new Point(buttonX, y + 96), (_, _) => MoveProfile(1)));
            y += 136;

            AddLabel("API 地址", LabelX, y + 4);
            _urlBox = AddTextBox(InputX, y, InputW);
            _urlBox.PlaceholderText = "https://api.deepseek.com";
            y += 42;

            AddLabel("API Key", LabelX, y + 4);
            _keyBox = AddTextBox(InputX, y, InputW);
            _keyBox.UseSystemPasswordChar = true;
            y += 42;

            AddLabel("模型", LabelX, y + 4);
            _modelBox = AddTextBox(InputX, y, InputW - 100);
            _modelBox.PlaceholderText = "deepseek-chat";
            _testButton = CreateButton("测试", new Point(InputX + InputW - 86, y - 2), false);
            _testButton.Click += async (_, _) => await TestAsync();
            Controls.Add(_testButton);
            y += 40;

            y = AddSeparator(y);

            // ---- 选项 ----
            AddLabel("快捷键", LabelX, y + 4);
            _hotKeyBox = AddTextBox(InputX, y, 160);
            _hotKeyBox.Text = settings.HotKey;
            _hotKeyBox.ReadOnly = true;
            _hotKeyBox.BackColor = Color.White;
            _hotKeyBox.KeyDown += HotKeyBox_KeyDown;
            _hotKeyBox.KeyUp += HotKeyBox_KeyUp;
            _hotKeyBox.MouseDown += HotKeyBox_MouseDown;
            y += 44;

            _clipboardCheck = AddCheckBox("显示窗口时自动翻译剪贴板内容", ref y, settings.AutoTranslateClipboard);
            _selectionCheck = AddCheckBox("显示窗口时自动翻译选中的文本", ref y, settings.AutoTranslateSelection);
            _hideOnFocusLostCheck = AddCheckBox("失去焦点时自动隐藏窗口", ref y, settings.HideOnFocusLost);
            _autoCopyCheck = AddCheckBox("翻译完成后自动复制译文", ref y, settings.AutoCopyResult);
            _autoStartCheck = AddCheckBox("开机自启动", ref y, AutoStart.IsEnabled());
            _autoUpdateCheck = AddCheckBox("自动更新", ref y, settings.AutoCheckUpdate);
            y += 6;

            y = AddSeparator(y);

            // ---- 版本 ----
            AddMutedLabel($"版本 {Application.ProductVersion.Split('+', '-')[0]}", LabelX, y + 6);
            _updateButton = CreateButton("检查更新", new Point(InputX, y), false);
            _updateButton.Click += async (_, _) => await CheckUpdateAsync();
            Controls.Add(_updateButton);
            _pending = UpdateManager.GetPending();
            if (_pending != null) _updateButton.Text = "重启并更新";
            y += 52;

            // ---- 底部 ----
            _statusLabel = new Label
            {
                AutoSize = true,
                Location = new Point(LabelX, y + 8),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = TextMuted,
                Text = "",
            };
            _saveButton = CreateButton("保存", new Point(LabelX + RowW - 86, y), true);
            _saveButton.Click += (_, _) => SaveAndClose();
            Controls.AddRange(new Control[] { _statusLabel, _saveButton });
            AcceptButton = _saveButton;

            ClientSize = new Size(500, y + 52);

            _urlBox.TextChanged += (_, _) => ApplyFieldsToSelected();
            _keyBox.TextChanged += (_, _) => ApplyFieldsToSelected();
            _modelBox.TextChanged += (_, _) => ApplyFieldsToSelected();

            RefreshList(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(LineColor) { DashStyle = DashStyle.Dash };
            foreach (int y in _separatorYs)
            {
                e.Graphics.DrawLine(pen, LabelX, y, LabelX + RowW, y);
            }
        }

        private int AddSeparator(int y)
        {
            _separatorYs.Add(y + 8);
            return y + 24;
        }

        #region 列表操作

        private int SelectedIndex => _profileList.SelectedIndex;

        private void RefreshList(int selectIndex)
        {
            _profileList.BeginUpdate();
            _profileList.Items.Clear();
            foreach (var p in _profiles) _profileList.Items.Add(p.DisplayName);
            _profileList.EndUpdate();
            _profileList.SelectedIndex = Math.Clamp(selectIndex, 0, _profiles.Count - 1);
        }

        private void LoadSelectedProfile()
        {
            if (SelectedIndex < 0) return;
            var p = _profiles[SelectedIndex];
            _loadingFields = true;
            _urlBox.Text = p.ApiBaseUrl;
            _keyBox.Text = p.ApiKey;
            _modelBox.Text = p.Model;
            _loadingFields = false;
        }

        private void ApplyFieldsToSelected()
        {
            if (_loadingFields || SelectedIndex < 0) return;
            var p = _profiles[SelectedIndex];
            p.ApiBaseUrl = _urlBox.Text.Trim();
            p.ApiKey = _keyBox.Text.Trim();
            p.Model = _modelBox.Text.Trim();
            _profileList.Items[SelectedIndex] = p.DisplayName;
        }

        private void AddProfile(object? sender, EventArgs e)
        {
            _profiles.Add(new ApiProfile());
            RefreshList(_profiles.Count - 1);
            _urlBox.Focus();
        }

        private void RemoveProfile(object? sender, EventArgs e)
        {
            if (SelectedIndex < 0) return;
            int index = SelectedIndex;
            _profiles.RemoveAt(index);
            if (_profiles.Count == 0) _profiles.Add(new ApiProfile());
            RefreshList(Math.Min(index, _profiles.Count - 1));
        }

        private void MoveProfile(int delta)
        {
            int from = SelectedIndex, to = from + delta;
            if (from < 0 || to < 0 || to >= _profiles.Count) return;
            (_profiles[from], _profiles[to]) = (_profiles[to], _profiles[from]);
            RefreshList(to);
        }

        #endregion

        /// <summary>在输入框中直接按下组合键完成录制。</summary>
        private void HotKeyBox_KeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            var key = e.KeyCode;
            if (key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin or Keys.None)
            {
                // 同一修饰键松开后快速再按，录制为连按触发
                if (key == _lastModUpKey &&
                    Environment.TickCount - _lastModUpTick <= SystemInformation.DoubleClickTime &&
                    key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey)
                {
                    _lastModUpKey = Keys.None;
                    _hotKeyBox.Text = HotKeyManager.FormatDoubleModifier(key);
                }
                return; // 只按了修饰键，等待实际按键
            }
            _lastModUpKey = Keys.None;

            var modifiers = HotKeyManager.Modifiers.None;
            if (e.Control) modifiers |= HotKeyManager.Modifiers.Ctrl;
            if (e.Alt) modifiers |= HotKeyManager.Modifiers.Alt;
            if (e.Shift) modifiers |= HotKeyManager.Modifiers.Shift;

            if (modifiers == HotKeyManager.Modifiers.None && key is not (>= Keys.F1 and <= Keys.F24))
            {
                SetStatus("快捷键需要包含 Ctrl/Alt/Shift 修饰键。", isError: true);
                return;
            }
            _hotKeyBox.Text = HotKeyManager.Format(modifiers, key);
        }

        private void HotKeyBox_KeyUp(object? sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey)
            {
                _lastModUpKey = e.KeyCode;
                _lastModUpTick = Environment.TickCount;
            }
        }

        /// <summary>在输入框中连按两下鼠标中键录制为中键双击触发。</summary>
        private void HotKeyBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle) return;
            int now = Environment.TickCount;
            if (now - _lastMiddleDownTick <= SystemInformation.DoubleClickTime)
            {
                _lastMiddleDownTick = 0;
                _hotKeyBox.Text = HotKeyManager.MouseMiddleDouble;
            }
            else
            {
                _lastMiddleDownTick = now;
            }
        }

        #region 控件工厂

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(x, y),
                ForeColor = TextMain,
                Text = text,
            });
        }

        private void AddMutedLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(x, y),
                ForeColor = TextMuted,
                Text = text,
            });
        }

        private TextBox AddTextBox(int x, int y, int width)
        {
            var box = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                ForeColor = TextMain,
            };
            Controls.Add(box);
            return box;
        }

        private CheckBox AddCheckBox(string text, ref int y, bool isChecked)
        {
            var check = new CheckBox
            {
                AutoSize = true,
                Location = new Point(LabelX, y),
                ForeColor = TextMain,
                Text = text,
                Checked = isChecked,
            };
            Controls.Add(check);
            y += 36;
            return check;
        }

        private Button CreateSmallButton(string text, Point location, EventHandler onClick)
        {
            var button = CreateButton(text, location, false);
            button.Size = new Size(92, 28);
            button.Click += onClick;
            return button;
        }

        private Button CreateButton(string text, Point location, bool primary)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(86, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : TextMain,
                Cursor = Cursors.Hand,
            };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(218, 220, 224);
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        #endregion

        private async Task TestAsync()
        {
            if (SelectedIndex < 0) return;
            var candidate = _profiles[SelectedIndex];
            if (!candidate.IsComplete)
            {
                SetStatus("请先填写完整。", isError: true);
                return;
            }

            _testButton.Enabled = false;
            SetStatus("测试中…", isError: false);
            try
            {
                var result = await TranslationService.TranslateAsync("你好", candidate);
                SetStatus($"连接成功：你好 → {result.Text}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus("测试失败：" + ex.Message, isError: true);
            }
            finally
            {
                _testButton.Enabled = true;
            }
        }

        private async Task CheckUpdateAsync()
        {
            if (!UpdateManager.Enabled)
            {
                SetStatus("调试模式下已禁用更新。", isError: true);
                return;
            }
            if (_pending != null)
            {
                UpdateManager.Apply(_pending);
                Application.Exit();
                return;
            }

            _updateButton.Enabled = false;
            SetStatus("检查更新中…", isError: false);
            try
            {
                int lastPercent = -1;
                var pending = await UpdateManager.CheckAndDownloadAsync(percent =>
                {
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        SetStatus($"下载更新 {percent}%", isError: false);
                    }
                });
                if (pending == null)
                {
                    SetStatus("已是最新版本。", isError: false);
                }
                else
                {
                    _pending = pending;
                    _updateButton.Text = "重启并更新";
                    SetStatus($"已就绪 {pending.Version}，将在下次启动时启用。", isError: false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("更新失败：" + ex.Message, isError: true);
            }
            finally
            {
                _updateButton.Enabled = true;
            }
        }

        private void SetStatus(string text, bool isError)
        {
            _statusLabel.ForeColor = isError ? Color.FromArgb(197, 34, 31) : TextMuted;
            _statusLabel.Text = text.Length > 60 ? text[..60] + "…" : text;
        }

        private void SaveAndClose()
        {
            _settings.Profiles = _profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.ApiBaseUrl) ||
                            !string.IsNullOrWhiteSpace(p.ApiKey) ||
                            !string.IsNullOrWhiteSpace(p.Model))
                .ToList();
            _settings.AutoTranslateClipboard = _clipboardCheck.Checked;
            _settings.AutoTranslateSelection = _selectionCheck.Checked;
            _settings.HideOnFocusLost = _hideOnFocusLostCheck.Checked;
            _settings.AutoCopyResult = _autoCopyCheck.Checked;
            _settings.AutoCheckUpdate = _autoUpdateCheck.Checked;
            if (HotKeyManager.IsMouseTrigger(_hotKeyBox.Text) ||
                HotKeyManager.TryParseDoubleModifier(_hotKeyBox.Text, out _) ||
                HotKeyManager.TryParse(_hotKeyBox.Text, out _, out _))
            {
                _settings.HotKey = _hotKeyBox.Text;
            }
            AutoStart.SetEnabled(_autoStartCheck.Checked);
            _settings.Save();
            Close();
        }
    }
}
