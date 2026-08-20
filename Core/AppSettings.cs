using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniTranslation.Core
{
    /// <summary>一套翻译接口配置。</summary>
    public sealed class ApiProfile
    {
        public string ApiBaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";

        [JsonIgnore]
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(ApiBaseUrl) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(Model);

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Model) && string.IsNullOrWhiteSpace(ApiBaseUrl))
                {
                    return "（未填写）";
                }
                string host = ApiBaseUrl;
                if (Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri)) host = uri.Host;
                return $"{Model} @ {host}";
            }
        }

        /// <summary>用于健康度追踪的稳定标识。</summary>
        [JsonIgnore]
        public string Key => $"{ApiBaseUrl}|{ApiKey}|{Model}";

        public ApiProfile Clone() => new() { ApiBaseUrl = ApiBaseUrl, ApiKey = ApiKey, Model = Model };
    }

    /// <summary>
    /// 应用配置，持久化到 %AppData%\MiniTranslation\settings.json。
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>接口配置列表，顺序即用户手动排定的优先级。</summary>
        public List<ApiProfile> Profiles { get; set; } = new();
        public bool AutoTranslateClipboard { get; set; } = true;
        public bool AutoTranslateSelection { get; set; } = false;
        public string HotKey { get; set; } = "Alt+Q";
        public bool HideOnFocusLost { get; set; } = false;
        public bool AutoCheckUpdate { get; set; } = true;
        public bool AutoCopyResult { get; set; } = false;

        [JsonIgnore]
        public bool IsConfigured => Profiles.Any(p => p.IsComplete);

        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniTranslation");
        private static readonly string FilePath = Path.Combine(Dir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                    MigrateLegacy(json, settings);
                    return settings;
                }
            }
            catch
            {
                // 配置损坏时回退到默认值
            }
            return new AppSettings();
        }

        /// <summary>旧版单接口配置（顶层 apiBaseUrl/apiKey/model）迁移为第一个 profile。</summary>
        private static void MigrateLegacy(string json, AppSettings settings)
        {
            if (settings.Profiles.Count > 0) return;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var legacy = new ApiProfile
            {
                ApiBaseUrl = root.TryGetProperty("apiBaseUrl", out var u) ? u.GetString() ?? "" : "",
                ApiKey = root.TryGetProperty("apiKey", out var k) ? k.GetString() ?? "" : "",
                Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "",
            };
            if (legacy.IsComplete)
            {
                settings.Profiles.Add(legacy);
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
    }
}
