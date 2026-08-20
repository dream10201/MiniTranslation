using System.Text.Json;

namespace MiniTranslation.Core
{
    /// <summary>检查 GitHub Releases 是否有新版本。</summary>
    public static class UpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/dream10201/MiniTranslation/releases/latest";
        public const string ReleasesUrl = "https://github.com/dream10201/MiniTranslation/releases/latest";

        /// <summary>有新版本时返回其版本号（如 v2.1.0），否则返回 null。失败静默。</summary>
        public static async Task<string?> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MiniTranslation");
                string body = await http.GetStringAsync(ApiUrl).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;

                string current = Application.ProductVersion.Split('+', '-')[0];
                if (!Version.TryParse(current, out var installed)) return null;

                return latest > installed ? tag : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
