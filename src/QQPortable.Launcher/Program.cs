using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace QQPortable.Launcher
{
    internal static class Program
    {
        private const string AppId = "QQ.Portable.LiteLoader";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppId);

                string launcherDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string portableRoot = Directory.GetParent(launcherDir)?.FullName;
                if (portableRoot == null)
                {
                    throw new InvalidOperationException("Launcher is not inside a portable directory.");
                }

                string qqDir = Path.Combine(portableRoot, "QQ");
                string qqExe = Path.Combine(qqDir, "QQ.exe");
                string liteLoaderDir = Path.Combine(portableRoot, "LiteLoader");
                string bootExe = Path.Combine(launcherDir, "NapCatWinBootMain.exe");
                string hookDll = Path.Combine(launcherDir, "NapCatWinBootHook.dll");
                string runtimeDir = Path.Combine(launcherDir, "runtime");
                string patchJson = Path.Combine(runtimeDir, "qqnt.json");
                string loadScript = Path.Combine(runtimeDir, "loadLiteLoader.js");

                RequireFile(qqExe);
                RequireFile(bootExe);
                RequireFile(hookDll);
                RequireDirectory(liteLoaderDir);

                bool prepareOnly = args.Any(argument => string.Equals(argument, "--prepare-only", StringComparison.OrdinalIgnoreCase));
                if (!prepareOnly)
                {
                    Process running = FindRunningQQ(qqExe);
                    if (running != null)
                    {
                        if (running.MainWindowHandle != IntPtr.Zero)
                        {
                            SetForegroundWindow(running.MainWindowHandle);
                        }
                        return;
                    }
                }

                PrepareRuntime(qqDir, liteLoaderDir, runtimeDir, patchJson, loadScript);
                if (prepareOnly)
                {
                    return;
                }

                Environment.SetEnvironmentVariable("NAPCAT_PATCH_PACKAGE", patchJson, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("NAPCAT_LOAD_PATH", loadScript, EnvironmentVariableTarget.Process);

                string bootArguments = "\"" + qqExe + "\" \"" + hookDll + "\"";
                StartHidden(bootExe, bootArguments, portableRoot);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "QQ Portable", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void PrepareRuntime(string qqDir, string liteLoaderDir, string runtimeDir, string patchJson, string loadScript)
        {
            Directory.CreateDirectory(runtimeDir);

            string packagePath = FindPackageJson(qqDir);
            string packageText = File.ReadAllText(packagePath, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> package = serializer.Deserialize<Dictionary<string, object>>(packageText);
            if (package == null)
            {
                throw new InvalidDataException("QQ package.json is empty: " + packagePath);
            }

            package["main"] = "./loadNapCat.js";
            string patchText = serializer.Serialize(package);
            File.WriteAllText(patchJson, patchText, new UTF8Encoding(false));

            string loader = "require(" + serializer.Serialize(liteLoaderDir) + ")";
            File.WriteAllText(loadScript, loader, new UTF8Encoding(false));
        }

        private static string FindPackageJson(string qqDir)
        {
            string versionsDir = Path.Combine(qqDir, "versions");
            if (Directory.Exists(versionsDir))
            {
                string[] candidates = Directory.GetDirectories(versionsDir)
                    .Where(directory => File.Exists(Path.Combine(directory, "resources", "app", "package.json")))
                    .OrderByDescending(directory => ParseVersion(Path.GetFileName(directory)))
                    .ThenByDescending(directory => Path.GetFileName(directory))
                    .ToArray();

                if (candidates.Length > 0)
                {
                    return Path.Combine(candidates[0], "resources", "app", "package.json");
                }
            }

            string basePackage = Path.Combine(qqDir, "resources", "app", "package.json");
            if (File.Exists(basePackage))
            {
                return basePackage;
            }

            throw new FileNotFoundException("QQ package.json was not found under " + qqDir, basePackage);
        }

        private static Version ParseVersion(string directoryName)
        {
            string value = directoryName.Split('-')[0];
            return Version.TryParse(value, out Version version) ? version : new Version(0, 0);
        }

        private static Process FindRunningQQ(string expectedPath)
        {
            string expected = Path.GetFullPath(expectedPath);
            foreach (Process process in Process.GetProcessesByName("QQ"))
            {
                try
                {
                    string actual = Path.GetFullPath(process.MainModule.FileName);
                    if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return process;
                    }
                }
                catch
                {
                    // 进程已退出或当前权限无法读取路径，继续检查下一个。
                }
            }
            return null;
        }

        private static void StartHidden(string fileName, string arguments, string workingDirectory)
        {
            ProcessStartInfo info = new ProcessStartInfo(fileName, arguments);
            info.WorkingDirectory = workingDirectory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Missing file: " + path, path);
            }
        }

        private static void RequireDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException("Missing directory: " + path);
            }
        }
    }
}
