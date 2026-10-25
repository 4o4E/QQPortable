using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace QQPortable.Launcher
{
    /// <summary>
    /// 命名事件用于把插件点击转成启动器动作。
    /// 名称由便携根目录决定，避免影响其他便携安装。
    /// </summary>
    internal static class RestartSignal
    {
        public static string Hash(string root)
        {
            string normalized = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                StringBuilder builder = new StringBuilder(16);
                for (int index = 0; index < 8; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string EventName(string root)
        {
            return @"Local\QQPortable.Restart." + Hash(root);
        }

        public static string MutexName(string root)
        {
            return @"Local\QQPortable.Instance." + Hash(root);
        }

        public static void Signal(string root)
        {
            try
            {
                using (EventWaitHandle handle = EventWaitHandle.OpenExisting(EventName(root)))
                {
                    handle.Set();
                }
            }
            catch (WaitHandleCannotBeOpenedException error)
            {
                throw new InvalidOperationException("QQPortable 启动器没有在运行，无法重启 QQ。", error);
            }
        }
    }
}
