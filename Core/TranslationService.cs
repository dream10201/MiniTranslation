using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MiniTranslation.Core
{
    public sealed record TranslationResult(string Text, bool SourceIsChinese);

    /// <summary>
    /// 通过 OpenAI 兼容的 /chat/completions 接口调用大模型完成翻译，
    /// 兼容 OpenAI / DeepSeek / Moonshot / 本地 Ollama 等服务。
    /// </summary>
    public static class TranslationService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

        private const string SystemPrompt =
            "你是一个专业的翻译引擎。如果用户输入的内容主要是中文，请翻译成英文；否则翻译成简体中文。" +
            "只输出译文本身，不要输出任何解释、注释或多余内容。";

        public static async Task<TranslationResult> TranslateAsync(string text, AppSettings settings, CancellationToken ct = default)
        {
            var payload = new
            {
                model = settings.Model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = text },
                },
                stream = false,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(settings.ApiBaseUrl))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"接口返回 {(int)response.StatusCode}：{Truncate(body, 300)}");
            }

            using var doc = JsonDocument.Parse(body);
            string content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return new TranslationResult(content.Trim(), IsMainlyChinese(text));
        }

        /// <summary>汉字占比高于字母时按中文处理，决定翻译方向与朗读内容。</summary>
        public static bool IsMainlyChinese(string text)
        {
            int zh = 0, en = 0;
            foreach (char c in text)
            {
                if (char.IsDigit(c) || char.IsWhiteSpace(c)) continue;
                if (c > 127) zh++;
                else en++;
            }
            return zh >= en;
        }

        private static string BuildEndpoint(string baseUrl)
        {
            string url = baseUrl.TrimEnd('/');
            return url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? url
                : url + "/chat/completions";
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    }
}
