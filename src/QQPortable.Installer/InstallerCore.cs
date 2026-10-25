using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QQPortable.Shared;

namespace QQPortable.Installer
{
    internal static class InstallerCore
    {
        public static InstallResult Install(InstallOptions options, Action<string> log)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            string target = Path.GetFullPath(options.TargetDirectory);
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && !options.Force)
            {
                throw new InvalidOperationException("目标目录不是空的。请换一个空目录，或明确选择覆盖。");
            }

            Directory.CreateDirectory(target);
            string temporary = Path.Combine(Path.GetTempPath(), "QQPortableInstaller", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);

            try
            {
                string qqVersion = options.QQVersion;
                if (string.IsNullOrWhiteSpace(options.QQInstallerPath))
                {
                    IReadOnlyList<QqAsset> assets = GitHubCatalog.GetQqAssetsAsync().GetAwaiter().GetResult();
                    QqAsset selected = string.IsNullOrWhiteSpace(options.QQVersion)
                        ? assets.FirstOrDefault()
                        : assets.FirstOrDefault(asset => string.Equals(asset.Version, options.QQVersion, StringComparison.OrdinalIgnoreCase));
                    if (selected == null)
                    {
                        throw new InvalidOperationException("没有找到 QQ 版本：" + options.QQVersion);
                    }

                    string installerPath = Path.Combine(temporary, "QQ-Installer.exe");
                    log?.Invoke("下载 QQ " + selected.Version);
                    GitHubCatalog.DownloadAsync(selected.Url, installerPath, log).GetAwaiter().GetResult();
                    options = CloneWithInstaller(options, installerPath);
                    qqVersion = selected.Version;
                }

                InstallQq(target, options.QQInstallerPath, temporary, log);

                string liteLoaderVersion = options.LiteLoaderVersion;
                if (!string.IsNullOrWhiteSpace(options.LiteLoaderDirectory))
                {
                    log?.Invoke("复制本地 LiteLoader");
                    CopyDirectory(options.LiteLoaderDirectory, Path.Combine(target, "LiteLoader"));
                }
                else
                {
                    IReadOnlyList<LiteLoaderRelease> releases = GitHubCatalog.GetLiteLoaderReleasesAsync().GetAwaiter().GetResult();
                    LiteLoaderRelease selected = string.IsNullOrWhiteSpace(options.LiteLoaderVersion)
                        ? releases.FirstOrDefault()
                        : releases.FirstOrDefault(release => string.Equals(release.Tag, options.LiteLoaderVersion, StringComparison.OrdinalIgnoreCase));
                    if (selected == null)
                    {
                        throw new InvalidOperationException("没有找到 LiteLoader 版本：" + options.LiteLoaderVersion);
                    }

                    string zipPath = Path.Combine(temporary, "LiteLoader.zip");
                    log?.Invoke("下载 LiteLoader " + selected.Tag);
                    GitHubCatalog.DownloadAsync(selected.Url, zipPath, log).GetAwaiter().GetResult();
                    string extractDir = Path.Combine(temporary, "LiteLoaderExtract");
                    ZipFile.ExtractToDirectory(zipPath, extractDir);
                    string root = FindLiteLoaderRoot(extractDir);
                    CopyDirectory(root, Path.Combine(target, "LiteLoader"));
                    liteLoaderVersion = selected.Tag;
                }

                LiteLoaderPatcher.Apply(Path.Combine(target, "LiteLoader"), log);

                EmbeddedAssets.ExtractLauncher(target);
                EmbeddedAssets.ExtractPlugin(target);
                PortableConfig.LoadOrCreate(target);

                string summary = "安装完成\nQQ: " + (string.IsNullOrWhiteSpace(qqVersion) ? "(已解压)" : qqVersion) +
                                 "\nLiteLoader: " + (string.IsNullOrWhiteSpace(liteLoaderVersion) ? "(本地目录)" : liteLoaderVersion) +
                                 "\n目标目录: " + target;
                log?.Invoke(summary);
                return new InstallResult { Summary = summary };
            }
            finally
            {
                TryDeleteDirectory(temporary);
            }
        }

        private static InstallOptions CloneWithInstaller(InstallOptions source, string installerPath)
        {
            return new InstallOptions
            {
                TargetDirectory = source.TargetDirectory,
                QQInstallerPath = installerPath,
                QQVersion = source.QQVersion,
                LiteLoaderVersion = source.LiteLoaderVersion,
                LiteLoaderDirectory = source.LiteLoaderDirectory,
                Force = source.Force,
            };
        }

        private static void InstallQq(string target, string qqInstallerPath, string temporary, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(qqInstallerPath))
            {
                throw new InvalidOperationException("QQ 安装包路径为空。");
            }

            string qqTarget = Path.Combine(target, "QQ");
            if (Directory.Exists(qqInstallerPath))
            {
                log?.Invoke("复制已解压的 QQ 目录");
                CopyDirectory(qqInstallerPath, qqTarget);
                EnsureQqRoot(qqTarget);
                return;
            }

            if (!File.Exists(qqInstallerPath))
            {
                throw new FileNotFoundException("QQ 安装包不存在：" + qqInstallerPath, qqInstallerPath);
            }

            string sevenZip = EmbeddedAssets.ExtractSevenZip(temporary);
            log?.Invoke("解压 QQ 安装包");
            string qqExe = ExtractQqExe(sevenZip, qqInstallerPath, temporary, log);
            if (qqExe == null)
            {
                throw new InvalidOperationException("QQ 安装包解压后没有找到 QQ.exe。可能是尚未支持的安装器格式。");
            }

            string qqRoot = Path.GetDirectoryName(qqExe);
            CopyDirectory(qqRoot, qqTarget);
            EnsureQqRoot(qqTarget);
        }

        private static void EnsureQqRoot(string qqTarget)
        {
            if (!File.Exists(Path.Combine(qqTarget, "QQ.exe")))
            {
                throw new InvalidOperationException("复制 QQ 后没有找到 QQ.exe：" + qqTarget);
            }
        }

        private static string FindLiteLoaderRoot(string extractDir)
        {
            string package = Directory.GetFiles(extractDir, "package.json", SearchOption.AllDirectories)
                .FirstOrDefault(path => File.Exists(Path.Combine(Path.GetDirectoryName(path), "src", "main", "store.js")));
            if (package == null)
            {
                throw new InvalidOperationException("LiteLoader 压缩包结构无法识别。");
            }
            return Path.GetDirectoryName(package);
        }

        private static string ExtractQqExe(string sevenZip, string installerPath, string temporary, Action<string> log)
        {
            string firstDir = Path.Combine(temporary, "QQExtractFirst");
            Directory.CreateDirectory(firstDir);
            if (TryRunProcess(sevenZip, "x -y -o\"" + firstDir + "\" \"" + installerPath + "\"", temporary))
            {
                string first = FindQqExe(firstDir);
                if (first != null)
                {
                    return first;
                }
            }

            log?.Invoke("安装包包含内层归档，正在提取 QQ");
            List<long> offsets = Find7zOffsets(installerPath);
            foreach (long offset in offsets)
            {
                string slice = Path.Combine(temporary, "qq-" + offset + ".7z");
                string extract = Path.Combine(temporary, "QQExtract-" + offset);
                try
                {
                    SliceFile(installerPath, offset, slice);
                    Directory.CreateDirectory(extract);
                    if (!TryRunProcess(sevenZip, "x -y -o\"" + extract + "\" \"" + slice + "\"", temporary))
                    {
                        continue;
                    }
                    string qq = FindQqExe(extract);
                    if (qq != null)
                    {
                        log?.Invoke("QQ 安装包已解压");
                        return qq;
                    }
                }
                catch
                {
                    // 内层归档候选可能不是有效安装包，继续尝试下一个。
                }
            }
            return null;
        }

        private static string FindQqExe(string directory)
        {
            return Directory.GetFiles(directory, "QQ.exe", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();
        }

        internal static List<long> Find7zOffsets(string path)
        {
            byte[] signature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
            List<long> offsets = new List<long>();
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] buffer = new byte[1024 * 1024 + signature.Length];
                long bufferOffset = 0;
                int carry = 0;
                int read;
                while ((read = stream.Read(buffer, carry, 1024 * 1024)) > 0)
                {
                    int total = carry + read;
                    for (int index = 0; index <= total - signature.Length; index++)
                    {
                        bool match = true;
                        for (int part = 0; part < signature.Length; part++)
                        {
                            if (buffer[index + part] != signature[part])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            offsets.Add(bufferOffset + index);
                            index += signature.Length - 1;
                        }
                    }
                    carry = Math.Min(signature.Length - 1, total);
                    Array.Copy(buffer, total - carry, buffer, 0, carry);
                    bufferOffset += total - carry;
                }
            }
            return offsets;
        }

        private static void SliceFile(string source, long offset, string destination)
        {
            using (FileStream input = File.OpenRead(source))
            using (FileStream output = File.Create(destination))
            {
                input.Position = offset;
                input.CopyTo(output);
            }
        }

        private static bool TryRunProcess(string fileName, string arguments, string workingDirectory)
        {
            ProcessStartInfo info = new ProcessStartInfo(fileName, arguments);
            info.WorkingDirectory = workingDirectory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string targetFile = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // 临时目录清不掉不影响安装结果。
            }
        }
    }
}
