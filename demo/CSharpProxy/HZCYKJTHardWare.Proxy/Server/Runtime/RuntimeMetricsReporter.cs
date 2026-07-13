using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HZCYKJTHardWare.Proxy.Core;
using HZCYKJTHardWare.Proxy.Infrastructure;
using HZCYKJTHardWare.Proxy.Preview;

namespace HZCYKJTHardWare.Proxy.Server.Runtime
{
    /// <summary>
    /// Low-frequency process and resource telemetry for multi-day stability tests.
    /// It intentionally avoids per-request timing and runs once every five minutes.
    /// </summary>
    internal sealed class RuntimeMetricsReporter : IDisposable
    {
        private const int InitialDelayMs = 60 * 1000;
        private const int IntervalMs = 5 * 60 * 1000;
        private readonly QueueManager _queueManager;
        private readonly ActiveTasksTracker _taskTracker;
        private readonly PreviewManager _previewManager;
        private readonly RequestRegistry _requestRegistry;
        private readonly TerminalProcessRegistry _processRegistry;
        private readonly Timer _timer;
        private int _running;
        private int _stopped = 1;

        internal RuntimeMetricsReporter(QueueManager queueManager,
            ActiveTasksTracker taskTracker,
            PreviewManager previewManager,
            RequestRegistry requestRegistry,
            TerminalProcessRegistry processRegistry)
        {
            _queueManager = queueManager;
            _taskTracker = taskTracker;
            _previewManager = previewManager;
            _requestRegistry = requestRegistry;
            _processRegistry = processRegistry;
            _timer = new Timer(Report, null, Timeout.Infinite, Timeout.Infinite);
        }

        internal void Start()
        {
            if (Interlocked.Exchange(ref _stopped, 0) == 0)
                return;
            _timer.Change(InitialDelayMs, IntervalMs);
        }

        internal void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;
            try { _timer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
        }

        private void Report(object state)
        {
            if (Volatile.Read(ref _stopped) != 0 ||
                Interlocked.Exchange(ref _running, 1) != 0)
                return;

            try
            {
                Logger.Info("[长稳指标] " + BuildSnapshot());
                var queueStats = (_queueManager?.GetAllStats() ?? "无")
                    .Replace("\r", "").Replace("\n", " | ");
                Logger.Info("[长稳指标] queues=" + queueStats);
            }
            catch (Exception ex)
            {
                Logger.Warn("[长稳指标] 采集失败: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        internal string BuildSnapshot()
        {
            using (var process = Process.GetCurrentProcess())
            {
                process.Refresh();
                var diskFreeMb = GetDiskFreeMb();
                var gdiHandles = GetGuiResourceCount(process.Handle, 0);
                var userHandles = GetGuiResourceCount(process.Handle, 1);
                return $"private_mb={ToMb(process.PrivateMemorySize64)}, " +
                       $"working_set_mb={ToMb(process.WorkingSet64)}, " +
                       $"managed_heap_mb={ToMb(GC.GetTotalMemory(false))}, " +
                       $"threads={process.Threads.Count}, handles={process.HandleCount}, " +
                       $"gdi_handles={gdiHandles}, user_handles={userHandles}, " +
                       $"gc0={GC.CollectionCount(0)}, gc1={GC.CollectionCount(1)}, gc2={GC.CollectionCount(2)}, " +
                       $"active_tasks={_taskTracker?.ActiveCount ?? 0}, " +
                       $"preview_sessions={_previewManager?.ActiveSessionCount ?? 0}, " +
                       $"preview_recoveries={_previewManager?.ActiveRecoveryCount ?? 0}, " +
                       $"requests={_requestRegistry?.ActiveCount ?? 0}, " +
                       $"process_sessions={_processRegistry?.ActiveCount ?? 0}, " +
                       $"log_pending={Logger.PendingCount}, log_dropped_total={Logger.TotalDroppedCount}, " +
                       $"disk_free_mb={diskFreeMb}";
            }
        }

        private static long ToMb(long bytes) => bytes / 1024L / 1024L;

        private static long GetDiskFreeMb()
        {
            try
            {
                var root = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                if (string.IsNullOrEmpty(root))
                    return -1;
                var drive = new DriveInfo(root);
                return drive.IsReady ? ToMb(drive.AvailableFreeSpace) : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static int GetGuiResourceCount(IntPtr processHandle, uint flag)
        {
            try { return GetGuiResources(processHandle, flag); }
            catch { return -1; }
        }

        [DllImport("user32.dll")]
        private static extern int GetGuiResources(IntPtr hProcess, uint uiFlags);

        public void Dispose()
        {
            Stop();
            using (var disposed = new ManualResetEvent(false))
            {
                try
                {
                    if (_timer.Dispose(disposed))
                        disposed.WaitOne(1000);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}
