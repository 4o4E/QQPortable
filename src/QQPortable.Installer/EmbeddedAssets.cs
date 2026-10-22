using System;
using System.IO;
using System.Reflection;

namespace QQPortable.Installer
{
    internal static class EmbeddedAssets
    {
        public static void ExtractLauncher(string launcherDirectory)
        {
            Directory.CreateDirectory(launcherDirectory);
            Extract("QQPortable.Launcher.exe", Path.Combine(launcherDirectory, "QQPortable.Launcher.exe"));
            Extract("NapCatWinBootMain.exe", Path.Combine(launcherDirectory, "NapCatWinBootMain.exe"));
            Extract("NapCatWinBootHook.dll", Path.Combine(launcherDirectory, "NapCatWinBootHook.dll"));
            Directory.CreateDirectory(Path.Combine(launcherDirectory, "runtime"));
        }

        public static string ExtractSevenZip(string temporaryDirectory)
        {
            string path = Path.Combine(temporaryDirectory, "7za.exe");
            Extract("7za.exe", path);
            return path;
        }

        public static void WriteSevenZipLicense(string path)
        {
            Extract("7za-License.txt", path);
        }

        private static void Extract(string logicalName, string destination)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(logicalName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("安装器资源缺失：" + logicalName);
                }
                using (FileStream output = File.Create(destination))
                {
                    input.CopyTo(output);
                }
            }
        }
    }
}
