using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using QQPortable.Shared;

namespace QQPortable.Launcher
{
    internal static class Program
    {
        private const string AppId = "QQ.Portable.LiteLoader";
        private const int RestoreWindow = 9;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppId);

                string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string launcherDir = Path.Combine(root, "Launcher");
                string qqDir = Path.Combine(root, "QQ");
                string qqExe = Path.Combine(qqDir, "QQ.exe");
                string liteLoaderDir = Path.Combine(root, "LiteLoader");
                string bootExe = Path.Combine(launcherDir, "NapCatWinBootMain.exe");
                string hookDll = Path.Combine(launcherDir, "NapCatWinBootHook.dll");
                string runtimeDir = Path.Combine(launcherDir, "runtime");
                string patchJson = Path.Combine(runtimeDir, "qqnt.json");
                string loadScript = Path.Combine(runtimeDir, "loadLiteLoader.js");

                if (HasArgument(args, "--restart"))
                {
                    RestartSignal.Signal(root);
                    return;
                }

                RequireFile(qqExe);
                RequireFile(bootExe);
                RequireFile(hookDll);
                RequireDirectory(liteLoaderDir);

                if (HasArgument(args, "--prepare-only"))
                {
                    PrepareRuntime(qqDir, liteLoaderDir, runtimeDir, patchJson, loadScript);
                    return;
                }

                using (Mutex mutex = new Mutex(false, RestartSignal.MutexName(root)))
                {
                    bool acquired;
                    try
                    {
                        acquired = mutex.WaitOne(0);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }
                    if (!acquired)
                    {
                        ActivateRunningQQ(qqExe);
                        return;
                    }

                    try
                    {
                        PortableConfig config = PortableConfig.LoadOrCreate(root);
                        AutoStartRegistration.Sync(root, config.AutoStart);

                        using (Process running = FindRunningQQ(qqExe))
                        {
                            if (running != null)
                            {
                                Activate(running);
                                return;
                            }
                        }

                        PrepareRuntime(qqDir, liteLoaderDir, runtimeDir, patchJson, loadScript);

                        Environment.SetEnvironmentVariable("NAPCAT_PATCH_PACKAGE", patchJson, EnvironmentVariableTarget.Process);
                        Environment.SetEnvironmentVariable("NAPCAT_LOAD_PATH", loadScript, EnvironmentVariableTarget.Process);

                        string bootArguments = "\"" + qqExe + "\" \"" + hookDll + "\"";
                        using (EventWaitHandle restartEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestartSignal.EventName(root)))
                        {
                            new LauncherSession(bootExe, bootArguments, root, restartEvent).Run();
                        }
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "QQ Portable", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
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
            Process matched = null;
            foreach (Process process in Process.GetProcessesByName("QQ"))
            {
                bool keep = false;
                try
                {
                    string actual = Path.GetFullPath(process.MainModule.FileName);
                    if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        // 优先保留有主窗口的客户端进程，其余匹配进程只释放句柄。
                        if (matched == null || (matched.MainWindowHandle == IntPtr.Zero && process.MainWindowHandle != IntPtr.Zero))
                        {
                            matched?.Dispose();
                            matched = process;
                            keep = true;
                        }
                    }
                }
                catch
                {
                    // 进程已退出或当前权限无法读取路径，继续检查下一个。
                }
                finally
                {
                    if (!keep)
                    {
                        process.Dispose();
                    }
                }
            }
            return matched;
        }

        private static void ActivateRunningQQ(string qqExe)
        {
            using (Process running = FindRunningQQ(qqExe))
            {
                if (running != null)
                {
                    Activate(running);
                }
            }
        }

        private static void Activate(Process running)
        {
            IntPtr window = running.MainWindowHandle;
            if (window == IntPtr.Zero)
            {
                return;
            }
            ShowWindow(window, RestoreWindow);
            SetForegroundWindow(window);
        }

        private static bool HasArgument(string[] args, string expected)
        {
            return args.Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));
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
