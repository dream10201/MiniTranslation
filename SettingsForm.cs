using MiniTranslation.Core;

namespace MiniTranslation
{
    /// <summary>翻译接口设置：可维护多套 API 配置并手动排序（顺序即优先级）。</summary>
    public sealed class SettingsForm : Form
    {
        private static readonly Color TextMain = Color.FromArgb(32, 33, 36);
        private static readonly Color TextMuted = Color.FromArgb(128, 132, 139);
        private static readonly Color Accent = Color.FromArgb(25, 103, 210);

        private readonly AppSettings _settings;
        private readonly List<ApiProfile> _profiles;

        private readonly ListBox _profileList;
        private readonly TextBox _urlBox;
        private readonly TextBox _keyBox;
        private readonly TextBox _modelBox;
        private readonly CheckBox _clipboardCheck;
        private readonly CheckBox _selectionCheck;
        private readonly Button _testButton;
        private readonly Button _saveButton;
        private readonly Label _statusLabel;
        private bool _loadingFields;

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
            ClientSize = new Size(500, 478);

            const int labelX = 24, inputX = 110, inputW = 366, buttonW = 92;

            var listLabel = new Label
            {
                AutoSize = true,
                Location = new Point(labelX, 20),
                ForeColor = TextMuted,
                Text = "接口列表（顺序即优先级，失败的会自动降权重试其余）",
            };
            Controls.Add(listLabel);

            _profileList = new ListBox
            {
                Location = new Point(labelX, 48),
                Size = new Size(452 - buttonW - 10, 118),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                IntegralHeight = false,
            };
            _profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
            Controls.Add(_profileList);

            int buttonX = labelX + _profileList.Width + 10;
            Controls.Add(CreateSmallButton("添加", new Point(buttonX, 48), AddProfile));
            Controls.Add(CreateSmallButton("删除", new Point(buttonX, 80), RemoveProfile));
            Controls.Add(CreateSmallButton("上移", new Point(buttonX, 112), (_, _) => MoveProfile(-1)));
            Controls.Add(CreateSmallButton("下移", new Point(buttonX, 144), (_, _) => MoveProfile(1)));

            int y = 184;
            AddLabel("API 地址", labelX, y + 4);
            _urlBox = AddTextBox(inputX, y, inputW);
            _urlBox.PlaceholderText = "https://api.deepseek.com";
            y += 42;

            AddLabel("API Key", labelX, y + 4);
            _keyBox = AddTextBox(inputX, y, inputW);
            _keyBox.UseSystemPasswordChar = true;
            y += 42;

            AddLabel("模型", labelX, y + 4);
            _modelBox = AddTextBox(inputX, y, inputW);
            _modelBox.PlaceholderText = "deepseek-chat";
            y += 46;

            _clipboardCheck = new CheckBox
            {
                AutoSize = true,
                Location = new Point(labelX, y),
                ForeColor = TextMain,
                Text = "显示窗口时自动翻译剪贴板内容",
                Checked = settings.AutoTranslateClipboard,
            };
            Controls.Add(_clipboardCheck);
            y += 34;

            _selectionCheck = new CheckBox
            {
                AutoSize = true,
                Location = new Point(labelX, y),
                ForeColor = TextMain,
                Text = "显示窗口时自动翻译选中的文本（模拟 Ctrl+C 获取）",
                Checked = settings.AutoTranslateSelection,
            };
            Controls.Add(_selectionCheck);
            y += 44;

            _statusLabel = new Label
            {
                AutoSize = true,
                Location = new Point(labelX, y + 8),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = TextMuted,
                Text = "",
            };

            _testButton = CreateButton("测试", new Point(290, y), false);
            _testButton.Click += async (_, _) => await TestAsync();
            _saveButton = CreateButton("保存", new Point(390, y), true);
            _saveButton.Click += (_, _) => SaveAndClose();

            Controls.AddRange(new Control[] { _statusLabel, _testButton, _saveButton });

            _urlBox.TextChanged += (_, _) => ApplyFieldsToSelected();
            _keyBox.TextChanged += (_, _) => ApplyFieldsToSelected();
            _modelBox.TextChanged += (_, _) => ApplyFieldsToSelected();

            RefreshList(0);
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
            _settings.Save();
            Close();
        }
    }
}
