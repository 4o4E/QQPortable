using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QQPortable.Installer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--install", StringComparison.OrdinalIgnoreCase))
            {
                int exitCode = RunCommandLine(args.Skip(1).ToArray());
                Environment.ExitCode = exitCode;
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static int RunCommandLine(string[] args)
        {
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                {
                    continue;
                }
                options[argument.Substring(2)] = args[++index];
            }

            if (!options.TryGetValue("target", out string target) || string.IsNullOrWhiteSpace(target))
            {
                Console.Error.WriteLine("usage: QQPortable.Installer.exe --install --target <dir> [--qq-installer <file>] [--qq-version <version>] [--liteloader-version <tag>]");
                return 2;
            }

            string logPath = Path.Combine(Path.GetTempPath(), "QQPortableInstaller-cli.log");
            File.WriteAllText(logPath, string.Empty);
            Action<string> log = message => File.AppendAllText(logPath, message + Environment.NewLine);
            try
            {
                InstallOptions installOptions = new InstallOptions
                {
                    TargetDirectory = target,
                    QQInstallerPath = options.TryGetValue("qq-installer", out string qqInstaller) ? qqInstaller : null,
                    QQVersion = options.TryGetValue("qq-version", out string qqVersion) ? qqVersion : null,
                    LiteLoaderVersion = options.TryGetValue("liteloader-version", out string liteloaderVersion) ? liteloaderVersion : null,
                    LiteLoaderDirectory = options.TryGetValue("liteloader-directory", out string liteloaderDirectory) ? liteloaderDirectory : null,
                    Force = options.ContainsKey("force"),
                };
                InstallResult result = InstallerCore.Install(installOptions, log);
                log(result.Summary);
                return 0;
            }
            catch (Exception error)
            {
                log(error.ToString());
                return 1;
            }
        }
    }
}
