using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Win32;

namespace MiniTranslation.Core
{
    /// <summary>
    /// 自动更新：安装版静默运行新版安装包；绿色版下载 zip 后
    /// 由临时脚本等待进程退出、替换 exe 并重启。
    /// 本方法返回后由调用方负责退出应用。
    /// </summary>
    public static class Updater
    {
        private const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8A785476-1AF1-47C8-95BB-7153DBAB8CB3}_is1";

        private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

        public static async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? onProgress = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "MiniTranslation.Update");
            Directory.CreateDirectory(tempDir);

            if (IsInstalledCopy() && info.SetupUrl != null)
            {
                string setupPath = Path.Combine(tempDir, "MiniTranslation-Setup.exe");
                await DownloadAsync(info.SetupUrl, setupPath, onProgress);
                Process.Start(new ProcessStartInfo(setupPath, "/VERYSILENT /NORESTART")
                {
                    UseShellExecute = true,
                });
                return;
            }

            if (info.ZipUrl == null)
            {
                throw new InvalidOperationException("发布中没有可用的更新文件。");
            }

            string zipPath = Path.Combine(tempDir, "update.zip");
            await DownloadAsync(info.ZipUrl, zipPath, onProgress);

            string extractDir = Path.Combine(tempDir, "extracted");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            string newExe = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories).First();

            string currentExe = Application.ExecutablePath;
            string script = Path.Combine(tempDir, "apply.cmd");
            File.WriteAllText(script,
                "@echo off\r\n" +
                ":retry\r\n" +
                $"copy /y \"{newExe}\" \"{currentExe}\" >nul 2>&1 || (timeout /t 1 /nobreak >nul & goto retry)\r\n" +
                $"start \"\" \"{currentExe}\"\r\n");
            Process.Start(new ProcessStartInfo(script)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }

        /// <summary>是否为安装版（存在 Inno 卸载注册表键且路径匹配）。</summary>
        private static bool IsInstalledCopy()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                return key?.GetValue("InstallLocation") is string location &&
                       location.Length > 0 &&
                       Application.ExecutablePath.StartsWith(location.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
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
