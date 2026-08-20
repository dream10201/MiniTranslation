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

        public static async Task<TranslationResult> TranslateAsync(string text, AppSettings settings, CancellationToken ct = default)
        {
            bool sourceIsChinese = IsMainlyChinese(text);
            // 腾讯混元 Hy-MT2 官方翻译模板（对通用聊天模型同样适用）
            string prompt = $"将以下文本翻译为 {(sourceIsChinese ? "英文" : "简体中文")}，" +
                            $"注意只需要输出翻译后的结果，不要额外解释： {text}";

            var payload = new Dictionary<string, object>
            {
                ["model"] = settings.Model,
                ["messages"] = new object[] { new { role = "user", content = prompt } },
                ["stream"] = false,
            };
            if (IsHunyuanMtModel(settings.Model))
            {
                // Hy-MT2 1.8B/7B 官方推荐采样参数；top_k/repetition_penalty 为
                // vLLM 等本地推理服务的扩展参数，OpenAI 官方接口不接受，故仅对该模型附加
                payload["temperature"] = 0.7;
                payload["top_p"] = 0.6;
                payload["top_k"] = 20;
                payload["repetition_penalty"] = 1.05;
            }

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

            return new TranslationResult(content.Trim(), sourceIsChinese);
        }

        private static bool IsHunyuanMtModel(string model) =>
            model.Contains("hy-mt", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("hunyuan-mt", StringComparison.OrdinalIgnoreCase);

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

        /// <summary>补全接口路径：地址没带版本段（如 /v1）时自动加上。</summary>
        private static string BuildEndpoint(string baseUrl)
        {
            string url = baseUrl.TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(url, @"/v\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                url += "/v1";
            }
            return url + "/chat/completions";
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    }
}
