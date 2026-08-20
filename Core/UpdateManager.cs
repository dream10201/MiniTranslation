using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Win32;

namespace MiniTranslation.Core
{
    public sealed record PendingUpdate(string Version, string File, bool IsInstaller);

    /// <summary>
    /// 自动更新：后台检查并静默下载到本地，默认在下次启动时安装，
    /// 也可立即重启安装。调试状态下整体禁用。
    /// </summary>
    public static class UpdateManager
    {
        private const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8A785476-1AF1-47C8-95BB-7153DBAB8CB3}_is1";

        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniTranslation", "update");
        private static readonly string MarkerPath = Path.Combine(Dir, "pending.json");

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

        /// <summary>已下载待安装的更新；过期（已装上）或文件缺失时自动清理。</summary>
        public static PendingUpdate? GetPending()
        {
            try
            {
                if (!File.Exists(MarkerPath)) return null;
                var pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(MarkerPath));
                if (pending == null || !File.Exists(pending.File) || !IsNewer(pending.Version))
                {
                    Cleanup();
                    return null;
                }
                return pending;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>启动时安装待装更新；返回 true 表示已发起安装，调用方应直接退出。</summary>
        public static bool TryApplyPendingAtStartup()
        {
            if (!Enabled) return false;
            var pending = GetPending();
            if (pending == null) return false;
            try
            {
                File.Delete(MarkerPath); // 先删标记，安装失败也不会陷入重试循环
            }
            catch
            {
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

        /// <summary>发起安装；调用方随后应退出应用。</summary>
        public static void Apply(PendingUpdate pending)
        {
            if (pending.IsInstaller)
            {
                // 跟随原安装模式，按机器安装的会触发 UAC 提权
                string scopeArg = GetInstallScope() == InstallScope.Machine ? "/ALLUSERS" : "/CURRENTUSER";
                Process.Start(new ProcessStartInfo(pending.File, $"/VERYSILENT /NORESTART {scopeArg}")
                {
                    UseShellExecute = true,
                });
                return;
            }

            // 绿色版：脚本等旧进程退出解锁后替换 exe 并重启
            string currentExe = Application.ExecutablePath;
            string script = Path.Combine(Dir, "apply.cmd");
            File.WriteAllText(script,
                "@echo off\r\n" +
                ":retry\r\n" +
                $"copy /y \"{pending.File}\" \"{currentExe}\" >nul 2>&1 || (timeout /t 1 /nobreak >nul & goto retry)\r\n" +
                $"start \"\" \"{currentExe}\"\r\n");
            Process.Start(new ProcessStartInfo(script)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }

        /// <summary>检查并静默下载新版本；已有待装更新时直接返回它。无新版本返回 null。</summary>
        public static async Task<PendingUpdate?> CheckAndDownloadAsync(Action<int>? onProgress = null)
        {
            if (!Enabled) return null;
            var existing = GetPending();
            if (existing != null) return existing;

            var info = await UpdateChecker.CheckAsync();
            if (info == null) return null;
            Directory.CreateDirectory(Dir);

            PendingUpdate pending;
            if (IsInstalledCopy() && info.SetupUrl != null)
            {
                string path = Path.Combine(Dir, "MiniTranslation-Setup.exe");
                await DownloadAsync(info.SetupUrl, path, onProgress);
                pending = new PendingUpdate(info.Version, path, IsInstaller: true);
            }
            else if (info.ZipUrl != null)
            {
                string zipPath = Path.Combine(Dir, "update.zip");
                await DownloadAsync(info.ZipUrl, zipPath, onProgress);
                string extractDir = Path.Combine(Dir, "extracted");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                File.Delete(zipPath);
                string newExe = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories).First();
                pending = new PendingUpdate(info.Version, newExe, IsInstaller: false);
            }
            else
            {
                return null;
            }

            File.WriteAllText(MarkerPath, JsonSerializer.Serialize(pending));
            return pending;
        }

        private static bool IsNewer(string tag)
        {
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var candidate)) return false;
            return Version.TryParse(Application.ProductVersion.Split('+', '-')[0], out var current) &&
                   candidate > current;
        }

        private enum InstallScope { None, User, Machine }

        /// <summary>是否为安装版（存在 Inno 卸载注册表键且路径匹配）。</summary>
        private static bool IsInstalledCopy() => GetInstallScope() != InstallScope.None;

        private static InstallScope GetInstallScope()
        {
            if (MatchesInstallLocation(Registry.CurrentUser)) return InstallScope.User;
            if (MatchesInstallLocation(Registry.LocalMachine)) return InstallScope.Machine;
            return InstallScope.None;
        }

        private static bool MatchesInstallLocation(RegistryKey root)
        {
            try
            {
                using var key = root.OpenSubKey(UninstallKey);
                return key?.GetValue("InstallLocation") is string location &&
                       location.Length > 0 &&
                       Application.ExecutablePath.StartsWith(location.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
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
