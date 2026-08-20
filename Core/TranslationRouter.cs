namespace MiniTranslation.Core
{
    /// <summary>
    /// 多接口故障切换：失败的接口从翻译候选中剔除，由后台探活任务
    /// 定期检测，恢复可用后重新按手动顺序参与调度。
    /// </summary>
    public static class TranslationRouter
    {
        private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(60);
        private static readonly object Gate = new();
        private static readonly HashSet<string> Failed = new();
        private static readonly HashSet<string> Probing = new();
        private static AppSettings? _settings;

        public static async Task<TranslationResult> TranslateAsync(string text, AppSettings settings, CancellationToken ct = default)
        {
            _settings = settings;
            var complete = settings.Profiles.Where(p => p.IsComplete).ToList();
            if (complete.Count == 0)
            {
                throw new InvalidOperationException("没有可用的接口配置。");
            }

            List<ApiProfile> candidates;
            lock (Gate)
            {
                candidates = complete.Where(p => !Failed.Contains(p.Key)).ToList();
            }
            if (candidates.Count == 0)
            {
                candidates = complete; // 全部被降权时兜底：仍按手动顺序全试一遍
            }

            Exception? lastError = null;
            foreach (var profile in candidates)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await TranslationService.TranslateAsync(text, profile, ct).ConfigureAwait(false);
                    MarkHealthy(profile.Key);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    MarkFailed(profile);
                    lastError = ex;
                }
            }

            throw candidates.Count == 1
                ? lastError!
                : new InvalidOperationException($"{candidates.Count} 个接口均不可用，最后错误：{lastError!.Message}", lastError);
        }

        private static void MarkHealthy(string key)
        {
            lock (Gate) Failed.Remove(key);
        }

        private static void MarkFailed(ApiProfile profile)
        {
            lock (Gate)
            {
                Failed.Add(profile.Key);
                if (!Probing.Add(profile.Key)) return; // 已有探活任务在跑
            }
            _ = ProbeLoopAsync(profile);
        }

        /// <summary>后台探活：定期发一条极短请求，成功即恢复该接口。</summary>
        private static async Task ProbeLoopAsync(ApiProfile profile)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(ProbeInterval).ConfigureAwait(false);

                    // 配置已被删除或修改时停止探测
                    var settings = _settings;
                    if (settings == null || !settings.Profiles.Any(p => p.Key == profile.Key))
                    {
                        MarkHealthy(profile.Key);
                        return;
                    }

                    try
                    {
                        await TranslationService.TranslateAsync("hi", profile).ConfigureAwait(false);
                        MarkHealthy(profile.Key);
                        return;
                    }
                    catch
                    {
                        // 仍不可用，继续下一轮探测
                    }
                }
            }
            finally
            {
                lock (Gate) Probing.Remove(profile.Key);
            }
        }
    }
}
