using System.Text.Json;

namespace MiniTranslation.Core
{
    public sealed record UpdateInfo(string Version, string? SetupUrl, string? ZipUrl);

    /// <summary>检查 GitHub Releases 是否有新版本。</summary>
    public static class UpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/dream10201/MiniTranslation/releases/latest";
        public const string ReleasesUrl = "https://github.com/dream10201/MiniTranslation/releases/latest";

        /// <summary>有新版本时返回版本号与下载地址，否则返回 null。失败静默。</summary>
        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MiniTranslation");
                string body = await http.GetStringAsync(ApiUrl).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                string tag = root.GetProperty("tag_name").GetString() ?? "";
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;

                string current = Application.ProductVersion.Split('+', '-')[0];
                if (!Version.TryParse(current, out var installed)) return null;
                if (latest <= installed) return null;

                string? setupUrl = null, zipUrl = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                        if (name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase)) setupUrl = url;
                        else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) zipUrl = url;
                    }
                }
                return new UpdateInfo(tag, setupUrl, zipUrl);
            }
            catch
            {
                return null;
            }
        }
    }
}
