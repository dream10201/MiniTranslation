using MiniTranslation.Core;

namespace MiniTranslation
{
    /// <summary>翻译接口设置：API 地址、Key、模型。</summary>
    public sealed class SettingsForm : Form
    {
        private static readonly Color TextMain = Color.FromArgb(32, 33, 36);
        private static readonly Color TextMuted = Color.FromArgb(128, 132, 139);
        private static readonly Color Accent = Color.FromArgb(25, 103, 210);

        private readonly AppSettings _settings;
        private readonly TextBox _urlBox;
        private readonly TextBox _keyBox;
        private readonly TextBox _modelBox;
        private readonly CheckBox _clipboardCheck;
        private readonly CheckBox _selectionCheck;
        private readonly Button _testButton;
        private readonly Button _saveButton;
        private readonly Label _statusLabel;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;

            Text = "设置";
            Font = new Font("Microsoft YaHei UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            ClientSize = new Size(460, 304);

            const int labelX = 24, inputX = 110, inputW = 320;
            int y = 24;

            AddLabel("API 地址", labelX, y + 4);
            _urlBox = AddTextBox(inputX, y, inputW, settings.ApiBaseUrl);
            _urlBox.PlaceholderText = "https://api.deepseek.com";
            y += 44;

            AddLabel("API Key", labelX, y + 4);
            _keyBox = AddTextBox(inputX, y, inputW, settings.ApiKey);
            _keyBox.UseSystemPasswordChar = true;
            y += 44;

            AddLabel("模型", labelX, y + 4);
            _modelBox = AddTextBox(inputX, y, inputW, settings.Model);
            _modelBox.PlaceholderText = "deepseek-chat";
            y += 44;

            _clipboardCheck = new CheckBox
            {
                AutoSize = true,
                Location = new Point(inputX, y),
                ForeColor = TextMain,
                Text = "显示窗口时自动翻译剪贴板内容",
                Checked = settings.AutoTranslateClipboard,
            };
            Controls.Add(_clipboardCheck);
            y += 36;

            _selectionCheck = new CheckBox
            {
                AutoSize = true,
                Location = new Point(inputX, y),
                ForeColor = TextMain,
                Text = "显示窗口时自动翻译选中的文本（模拟 Ctrl+C 获取）",
                Checked = settings.AutoTranslateSelection,
            };
            Controls.Add(_selectionCheck);
            y += 40;

            _statusLabel = new Label
            {
                AutoSize = true,
                Location = new Point(labelX, y + 8),
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = TextMuted,
                Text = "",
            };

            _testButton = CreateButton("测试", new Point(250, y), false);
            _testButton.Click += async (_, _) => await TestAsync();
            _saveButton = CreateButton("保存", new Point(350, y), true);
            _saveButton.Click += (_, _) => SaveAndClose();

            Controls.AddRange(new Control[] { _statusLabel, _testButton, _saveButton });
            AcceptButton = _saveButton;
        }

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

        private TextBox AddTextBox(int x, int y, int width, string value)
        {
            var box = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                ForeColor = TextMain,
                Text = value,
            };
            Controls.Add(box);
            return box;
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

        private AppSettings ReadInput() => new()
        {
            ApiBaseUrl = _urlBox.Text.Trim(),
            ApiKey = _keyBox.Text.Trim(),
            Model = _modelBox.Text.Trim(),
        };

        private async Task TestAsync()
        {
            var candidate = ReadInput();
            if (!candidate.IsConfigured)
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
            var candidate = ReadInput();
            _settings.ApiBaseUrl = candidate.ApiBaseUrl;
            _settings.ApiKey = candidate.ApiKey;
            _settings.Model = candidate.Model;
            _settings.AutoTranslateClipboard = _clipboardCheck.Checked;
            _settings.AutoTranslateSelection = _selectionCheck.Checked;
            _settings.Save();
            Close();
        }
    }
}
