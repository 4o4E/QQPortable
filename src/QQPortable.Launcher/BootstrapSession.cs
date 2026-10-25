using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace QQPortable.Launcher
{
    /// <summary>
    /// QQ 可能创建多个客户端进程，关闭窗口到托盘不代表退出。
    /// Windows Job 限定本次进程树，QQ 进程句柄全部发出退出信号后，
    /// 结束本次 NapCat 与控制台宿主，不影响其他 QQ 会话。
    /// 重启请求通过 RequestStop 结束当前 Job，由外层会话循环创建新会话。
    /// </summary>
    internal sealed class BootstrapSession
    {
        private const uint Infinite = uint.MaxValue;
        private const uint NewProcess = 6;
        private const uint ActiveProcessZero = 4;
        private const uint ClientExited = 0x100;

        private readonly string executable;
        private readonly string arguments;
        private readonly string workingDirectory;
        private readonly object gate = new object();
        private SafeFileHandle job;
        private bool stopRequested;

        public BootstrapSession(string executable, string arguments, string workingDirectory)
        {
            this.executable = executable;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
        }

        public bool StopRequested
        {
            get
            {
                lock (gate)
                {
                    return stopRequested;
                }
            }
        }

        public void RequestStop()
        {
            lock (gate)
            {
                stopRequested = true;
                if (job != null && !job.IsInvalid)
                {
                    if (!TerminateJobObject(job, 0))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }
        }

        public void Run()
        {
            using (SafeFileHandle jobHandle = CreateJobObject(IntPtr.Zero, null))
            using (SafeFileHandle port = CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, UIntPtr.Zero, 1))
            {
                if (jobHandle.IsInvalid || port.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                lock (gate)
                {
                    job = jobHandle;
                }

                try
                {
                    if (IsStopRequested()) return;
                    CompletionPort association = new CompletionPort { Port = port.DangerousGetHandle() };
                    if (!SetInformationJobObject(jobHandle, 7, ref association, (uint)Marshal.SizeOf(association)))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    StartupInfo startup = new StartupInfo { Size = (uint)Marshal.SizeOf(typeof(StartupInfo)) };
                    // 挂起创建，先加入 Job 再恢复，避免 QQ 在加入监管前创建子进程。
                    StringBuilder command = new StringBuilder("\"" + executable + "\" " + arguments);
                    if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                        0x00000004 | 0x08000000, IntPtr.Zero, workingDirectory, ref startup, out ProcessInformation process))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    bool assigned = false;
                    try
                    {
                        if (IsStopRequested())
                        {
                            TerminateProcess(process.Process, 0);
                            return;
                        }
                        if (!AssignProcessToJobObject(jobHandle, process.Process)) throw new Win32Exception(Marshal.GetLastWin32Error());
                        assigned = true;
                        if (IsStopRequested())
                        {
                            TerminateJobObject(jobHandle, 0);
                            return;
                        }
                        if (ResumeThread(process.Thread) == uint.MaxValue)
                        {
                            if (IsStopRequested()) return;
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        HashSet<uint> clients = new HashSet<uint>();
                        bool jobEmpty = false;
                        while (true)
                        {
                            if (!GetQueuedCompletionStatus(port, out uint message, out UIntPtr key, out IntPtr processId, Infinite))
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            if (message == ActiveProcessZero)
                            {
                                jobEmpty = true;
                                if (clients.Count == 0) return;
                            }
                            uint id = unchecked((uint)processId.ToInt64());
                            if (message == NewProcess && id != process.ProcessId && WatchClient(id, port)) clients.Add(id);
                            if (message == ClientExited && clients.Remove(id) && clients.Count == 0)
                            {
                                if (jobEmpty) return;
                                // conhost 与 crashpad 等辅助进程不能阻止 QQ 退出后的清理。
                                if (!TerminateJobObject(jobHandle, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                        }
                    }
                    catch
                    {
                        // 启动或监管失败不能遗留挂起进程；清理后继续报告原始错误。
                        if (assigned) TerminateJobObject(jobHandle, 1);
                        else TerminateProcess(process.Process, 1);
                        throw;
                    }
                    finally
                    {
                        CloseHandle(process.Thread);
                        CloseHandle(process.Process);
                    }
                }
                finally
                {
                    lock (gate)
                    {
                        job = null;
                    }
                }
            }
        }

        private bool IsStopRequested()
        {
            lock (gate)
            {
                return stopRequested;
            }
        }

        private static bool WatchClient(uint processId, SafeFileHandle port)
        {
            SafeWaitHandle process = OpenProcess(0x00101000, false, processId);
            ProcessWaitHandle wait = null;
            bool watching = false;
            bool portReferenced = false;
            try
            {
                if (process.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 87) return false; // 进程在处理创建通知前已经退出。
                    throw new Win32Exception(error);
                }
                StringBuilder image = new StringBuilder(260);
                uint length = (uint)image.Capacity;
                if (!QueryFullProcessImageName(process, 0, image, ref length))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (WaitForSingleObject(process.DangerousGetHandle(), 0) == 0) return false;
                    throw new Win32Exception(error);
                }
                if (!image.ToString().EndsWith("\\QQ.exe", StringComparison.OrdinalIgnoreCase)) return false;

                // Job 的 EXIT_PROCESS 通知可能被外部持有的进程句柄延迟。
                // 直接等待进程句柄的终止信号，不等待句柄持有者释放引用。
                wait = new ProcessWaitHandle(process);
                port.DangerousAddRef(ref portReferenced);
                IntPtr nativePort = port.DangerousGetHandle();
                ThreadPool.RegisterWaitForSingleObject(wait, (state, timedOut) =>
                {
                    try
                    {
                        if (!PostQueuedCompletionStatus(nativePort, ClientExited, UIntPtr.Zero, new IntPtr(processId)))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    finally
                    {
                        wait.Dispose();
                        port.DangerousRelease();
                    }
                }, null, Timeout.Infinite, true);
                watching = true;
                return true;
            }
            finally
            {
                if (!watching)
                {
                    wait?.Dispose();
                    process.Dispose();
                    if (portReferenced) port.DangerousRelease();
                }
            }
        }

        private sealed class ProcessWaitHandle : WaitHandle
        {
            public ProcessWaitHandle(SafeWaitHandle process) { SafeWaitHandle = process; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CompletionPort
        {
            public IntPtr Key;
            public IntPtr Port;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public uint Size;
            public string Reserved;
            public string Desktop;
            public string Title;
            public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
            public ushort ShowWindow, ReservedSize;
            public IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle CreateIoCompletionPort(IntPtr file, IntPtr existingPort, UIntPtr key, uint threads);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref CompletionPort information, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW", SetLastError = true)]
        private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
            IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory,
            ref StartupInfo startup, out ProcessInformation process);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetQueuedCompletionStatus(SafeFileHandle port, out uint message, out UIntPtr key, out IntPtr overlapped, uint timeout);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PostQueuedCompletionStatus(IntPtr port, uint message, UIntPtr key, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeWaitHandle OpenProcess(uint access, bool inheritHandle, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeWaitHandle process, uint flags, StringBuilder image, ref uint length);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
