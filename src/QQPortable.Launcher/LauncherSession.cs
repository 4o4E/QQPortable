using System;
using System.Threading;
using System.Windows.Forms;

namespace QQPortable.Launcher
{
    /// <summary>
    /// 一次启动可以包含多个 QQ 会话：只在用户点击重启时重建会话。
    /// 监听线程只负责通知当前会话结束，不直接创建进程。
    /// </summary>
    internal sealed class LauncherSession
    {
        private readonly string executable;
        private readonly string arguments;
        private readonly string workingDirectory;
        private readonly EventWaitHandle restartEvent;
        private volatile BootstrapSession current;
        private int pendingRestart;

        public LauncherSession(string executable, string arguments, string workingDirectory, EventWaitHandle restartEvent)
        {
            this.executable = executable;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
            this.restartEvent = restartEvent;
        }

        public void Run()
        {
            Thread listener = new Thread(ListenRestart)
            {
                IsBackground = true,
                Name = "QQPortable restart listener",
            };
            listener.Start();

            while (true)
            {
                BootstrapSession session = new BootstrapSession(executable, arguments, workingDirectory);
                current = session;
                if (Interlocked.Exchange(ref pendingRestart, 0) == 1)
                {
                    session.RequestStop();
                }

                session.Run();
                current = null;

                bool restart = session.StopRequested;
                if (Interlocked.Exchange(ref pendingRestart, 0) == 1)
                {
                    restart = true;
                }
                if (!restart)
                {
                    // 覆盖 QQ 刚好退出时点击重启的竞争窗口。
                    Thread.Sleep(200);
                    restart = session.StopRequested || Interlocked.Exchange(ref pendingRestart, 0) == 1;
                }
                if (!restart)
                {
                    return;
                }
            }
        }

        private void ListenRestart()
        {
            while (true)
            {
                try
                {
                    restartEvent.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                BootstrapSession session = current;
                if (session == null)
                {
                    Interlocked.Exchange(ref pendingRestart, 1);
                    continue;
                }

                try
                {
                    session.RequestStop();
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message, "QQ Portable", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
