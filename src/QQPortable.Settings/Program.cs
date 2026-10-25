using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QQPortable.Shared;

namespace QQPortable.Settings
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool applyOnly = args.Any(argument => string.Equals(argument, "--apply", StringComparison.OrdinalIgnoreCase));
            try
            {
                if (applyOnly)
                {
                    PortableConfig config = PortableConfig.LoadOrCreate(root);
                    AutoStartRegistration.Sync(root, config.AutoStart);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SettingsForm(root));
            }
            catch (Exception error)
            {
                if (applyOnly)
                {
                    Console.Error.WriteLine(error.Message);
                }
                else
                {
                    MessageBox.Show(error.ToString(), "QQ Portable 设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Environment.ExitCode = 1;
            }
        }
    }
}
