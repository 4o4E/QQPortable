using System;
using System.IO;
using Microsoft.Win32;

namespace QQPortable.Shared
{
    /// <summary>
    /// 开机自启只写当前用户 Run 项，不申请管理员权限。
    /// 固定值名，同一用户只保留一个 QQPortable 启动项。
    /// </summary>
    internal static class AutoStartRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ValueName = "QQPortable";

        public static string BuildCommand(string root)
        {
            string executable = Path.Combine(Path.GetFullPath(root), "QQPortable.exe");
            return "\"" + executable + "\"";
        }

        public static void Sync(string root, bool enabled)
        {
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (run == null)
                {
                    throw new InvalidOperationException("无法打开当前用户启动项注册表：" + RunKeyPath);
                }
                if (enabled)
                {
                    run.SetValue(ValueName, BuildCommand(root), RegistryValueKind.String);
                }
                else
                {
                    run.DeleteValue(ValueName, false);
                }
            }

            // 任务管理器的“禁用”写在 StartupApproved 中，只在 Run 值存在时才有意义。
            // 开启时清除旧禁用标记，关闭时一并清理。
            using (RegistryKey approved = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, true))
            {
                approved?.DeleteValue(ValueName, false);
            }
        }
    }
}
