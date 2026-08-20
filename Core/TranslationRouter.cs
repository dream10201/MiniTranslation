namespace MiniTranslation.Core
{
    /// <summary>
    /// 多接口故障切换：按“健康的在前（保持手动顺序）、失败过的在后”依次尝试，
    /// 失败即降权、成功即恢复。健康状态仅保存在内存中，失败标记在冷却期后自动解除。
    /// </summary>
    public static class TranslationRouter
    {
        private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(2);
        private static readonly Dictionary<string, DateTime> LastFailure = new();

        public static async Task<TranslationResult> TranslateAsync(string text, AppSettings settings, CancellationToken ct = default)
        {
            var candidates = settings.Profiles
                .Where(p => p.IsComplete)
                .OrderBy(p => IsHealthy(p) ? 0 : 1) // 稳定排序，组内保持手动顺序
                .ToList();
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("没有可用的接口配置。");
            }

            Exception? lastError = null;
            foreach (var profile in candidates)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await TranslationService.TranslateAsync(text, profile, ct).ConfigureAwait(false);
                    lock (LastFailure) LastFailure.Remove(profile.Key);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (LastFailure) LastFailure[profile.Key] = DateTime.UtcNow;
                    lastError = ex;
                }
            }

            throw candidates.Count == 1
                ? lastError!
                : new InvalidOperationException($"{candidates.Count} 个接口均不可用，最后错误：{lastError!.Message}", lastError);
        }

        private static bool IsHealthy(ApiProfile profile)
        {
            lock (LastFailure)
            {
                return !LastFailure.TryGetValue(profile.Key, out var at) ||
                       DateTime.UtcNow - at > FailureCooldown;
            }
        }
    }
}
