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
        private static readonly HttpClient Http = new(new SocketsHttpHandler
        {
            // 连不上的接口快速失败，便于路由及时降权切换
            ConnectTimeout = TimeSpan.FromSeconds(5),
        })
        {
            // 覆盖建连到收到响应头；流式响应体的读取由下方逐行看门狗控制
            Timeout = TimeSpan.FromSeconds(20),
        };

        private static readonly TimeSpan StreamIdleTimeout = TimeSpan.FromSeconds(30);

        /// <summary>流式翻译；每收到一段增量，回调完整的已生成文本。</summary>
        public static async Task<TranslationResult> TranslateAsync(
            string text, ApiProfile profile, Action<string>? onProgress = null, CancellationToken ct = default)
        {
            try
            {
                return await TranslateCoreAsync(text, profile, onProgress, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // HttpClient 超时或流式读取空闲超时，按接口故障处理而非用户取消
                throw new TimeoutException("接口响应超时。");
            }
        }

        private static async Task<TranslationResult> TranslateCoreAsync(
            string text, ApiProfile profile, Action<string>? onProgress, CancellationToken ct)
        {
            bool sourceIsChinese = IsMainlyChinese(text);
            // 腾讯混元 Hy-MT2 官方模板：指令与待译文本以空行分隔（对通用聊天模型同样适用）。
            // 原文里的连续换行压成单个，否则模型会把空行当作文本结束而截断后续段落
            text = System.Text.RegularExpressions.Regex
                .Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "\n{3,}", "\n\n").Trim();
            // prompt 语言跟随原文语言，与官方训练数据的分布一致
            string prompt = sourceIsChinese
                ? $"将以下文本翻译为 英语，注意只需要输出翻译后的结果，不要额外解释：\n\n{text}"
                : "Translate the following text into Simplified Chinese. " +
                  $"Note that you should only output the translated result without any additional explanation:\n\n{text}";

            var payload = new Dictionary<string, object>
            {
                ["model"] = profile.Model,
                ["messages"] = new object[] { new { role = "user", content = prompt } },
                ["stream"] = true,
            };
            if (IsHunyuanMtModel(profile.Model))
            {
                // Hy-MT2 1.8B/7B 官方推荐采样参数；top_k/repetition_penalty 为
                // vLLM 等本地推理服务的扩展参数，OpenAI 官方接口不接受，故仅对该模型附加
                payload["temperature"] = 0.7;
                payload["top_p"] = 0.6;
                payload["top_k"] = 20;
                payload["repetition_penalty"] = 1.05;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(profile.ApiBaseUrl))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"接口返回 {(int)response.StatusCode}：{Truncate(body, 300)}");
            }

            string content = await ReadStreamedContentAsync(response, onProgress, ct).ConfigureAwait(false);
            content = content.Trim();
            if (content.Length == 0)
            {
                // 空结果视为接口故障，让路由降权并切换下一个接口
                throw new InvalidOperationException("接口返回了空结果。");
            }
            return new TranslationResult(content, sourceIsChinese);
        }

        /// <summary>解析 SSE 流；服务端不支持流式而直接返回完整 JSON 时自动兼容。</summary>
        private static async Task<string> ReadStreamedContentAsync(
            HttpResponseMessage response, Action<string>? onProgress, CancellationToken ct)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var content = new StringBuilder();
            var rawBody = new StringBuilder();
            bool sawSse = false;

            while (true)
            {
                idleCts.CancelAfter(StreamIdleTimeout);
                string? line = await reader.ReadLineAsync(idleCts.Token).ConfigureAwait(false);
                if (line == null) break;

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    sawSse = true;
                    string data = line[5..].Trim();
                    if (data == "[DONE]") break;
                    if (data.Length == 0) continue;

                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                        choices.GetArrayLength() > 0 &&
                        choices[0].TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var piece) &&
                        piece.ValueKind == JsonValueKind.String)
                    {
                        content.Append(piece.GetString());
                        onProgress?.Invoke(content.ToString());
                    }
                }
                else if (!sawSse)
                {
                    rawBody.Append(line);
                }
            }

            if (!sawSse && rawBody.Length > 0)
            {
                using var doc = JsonDocument.Parse(rawBody.ToString());
                content.Append(doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString());
            }
            return content.ToString();
        }

        private static bool IsHunyuanMtModel(string model) =>
            model.Contains("hy-mt", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("hunyuan-mt", StringComparison.OrdinalIgnoreCase);

        /// <summary>判断输入是否以中文为主，决定翻译方向与朗读内容。</summary>
        public static bool IsMainlyChinese(string text)
        {
            int zh = 0, en = 0;
            foreach (char c in text)
            {
                if (c is >= (char)0x4E00 and <= (char)0x9FFF) zh++;   // CJK 汉字
                else if (char.IsAsciiLetter(c)) en++;
            }
            // 汉字信息密度高，加权计票，中文夹英文术语时仍判为中文
            return zh > 0 && zh * 3 >= en;
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
