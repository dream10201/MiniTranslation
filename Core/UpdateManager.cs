using System.Diagnostics;
using System.IO.Compression;

namespace MiniTranslation.Core
{
    public sealed record PendingUpdate(string Version, string ExePath);

    /// <summary>
    /// 自动更新（A/B 版本目录）：后台下载 zip 解压到本地版本目录即完成安装，
    /// 启动时发现更高版本的目录就改为运行它。调试状态下整体禁用。
    /// </summary>
    public static class UpdateManager
    {
        private const string ExeName = "MiniTranslation.exe";

        // 版本目录：%LocalAppData%\MiniTranslation\app\<版本号>\MiniTranslation.exe
        private static readonly string AppDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiniTranslation", "app");

        private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

        public static bool Enabled
        {
            get
            {
#if DEBUG
                return false;
#else
                return !Debugger.IsAttached;
#endif
            }
        }

        private static Version CurrentVersion =>
            Version.TryParse(Application.ProductVersion.Split('+', '-')[0], out var v) ? v : new Version(0, 0);

        /// <summary>已下载就绪、比当前运行版本新的版本；没有返回 null。</summary>
        public static PendingUpdate? GetPending()
        {
            try
            {
                if (!Directory.Exists(AppDir)) return null;
                Version best = CurrentVersion;
                string? exe = null;
                foreach (string dir in Directory.GetDirectories(AppDir))
                {
                    if (!Version.TryParse(Path.GetFileName(dir), out var version) || version <= best) continue;
                    string candidate = Path.Combine(dir, ExeName);
                    if (!File.Exists(candidate)) continue;
                    best = version;
                    exe = candidate;
                }
                return exe == null ? null : new PendingUpdate(best.ToString(), exe);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>启动时如有更新版本则改为运行它；返回 true 表示调用方应直接退出。</summary>
        public static bool TryLaunchNewerAtStartup()
        {
            if (!Enabled) return false;
            var pending = GetPending();
            if (pending == null)
            {
                CleanupOldVersions();
                return false;
            }
            try
            {
                Apply(pending);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>启动新版本；调用方随后应退出应用（新实例会等本进程释放单实例互斥量）。</summary>
        public static void Apply(PendingUpdate pending)
        {
            Process.Start(new ProcessStartInfo(pending.ExePath) { UseShellExecute = true });
        }

        /// <summary>
        /// 把已就绪的新版本复制到本 exe 所在的安装目录（开机计划任务以管理员权限调用，
        /// 让 Program Files 基底跟上版本）。只复制、不执行用户可写目录里的文件。
        /// </summary>
        public static void SyncBase()
        {
            try
            {
                string baseExe = Application.ExecutablePath;
                string retired = baseExe + ".old";
                // 上次换下的旧文件此时已不被占用，先清掉
                if (File.Exists(retired)) File.Delete(retired);

                var pending = GetPending();
                if (pending == null) return;
                // 运行中的 exe 不能覆盖但可以改名：换下自己，再把新版复制进来
                File.Move(baseExe, retired);
                try
                {
                    File.Copy(pending.ExePath, baseExe);
                }
                catch
                {
                    File.Move(retired, baseExe);
                }
            }
            catch
            {
                // 非管理员权限运行或文件被占用时放弃，下次开机再试
            }
        }

        /// <summary>检查并静默下载解压新版本；已就绪时直接返回它。无新版本返回 null。</summary>
        public static async Task<PendingUpdate?> CheckAndDownloadAsync(Action<int>? onProgress = null)
        {
            if (!Enabled) return null;
            var existing = GetPending();
            if (existing != null) return existing;

            var info = await UpdateChecker.CheckAsync();
            if (info?.ZipUrl == null) return null;
            if (!Version.TryParse(info.Version.TrimStart('v', 'V'), out var version)) return null;

            Directory.CreateDirectory(AppDir);
            string zipPath = Path.Combine(AppDir, "download.zip");
            await DownloadAsync(info.ZipUrl, zipPath, onProgress);

            // 先解压到临时目录，成功后再改名，避免半成品目录被当作可用版本
            string stageDir = Path.Combine(AppDir, "staging");
            if (Directory.Exists(stageDir)) Directory.Delete(stageDir, recursive: true);
            ZipFile.ExtractToDirectory(zipPath, stageDir);
            File.Delete(zipPath);
            if (!File.Exists(Path.Combine(stageDir, ExeName))) return null;

            string versionDir = Path.Combine(AppDir, version.ToString());
            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, recursive: true);
            Directory.Move(stageDir, versionDir);
            return new PendingUpdate(version.ToString(), Path.Combine(versionDir, ExeName));
        }

        /// <summary>删除不比当前运行版本新的版本目录（自身所在的除外）；正被占用的留到下次清。</summary>
        private static void CleanupOldVersions()
        {
            try
            {
                if (!Directory.Exists(AppDir)) return;
                string self = Path.GetDirectoryName(Application.ExecutablePath)!;
                foreach (string dir in Directory.GetDirectories(AppDir))
                {
                    if (string.Equals(dir, self, StringComparison.OrdinalIgnoreCase)) continue;
                    if (Version.TryParse(Path.GetFileName(dir), out var version) && version > CurrentVersion) continue;
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static async Task DownloadAsync(string url, string path, Action<int>? onProgress)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("MiniTranslation");
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1;
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var target = File.Create(path);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (total > 0) onProgress?.Invoke((int)(done * 100 / total));
            }
        }
    }
}
