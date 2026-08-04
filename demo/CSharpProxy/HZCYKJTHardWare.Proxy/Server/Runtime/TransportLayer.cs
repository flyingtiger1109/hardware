using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Server.Runtime
{
    /// <summary>
    /// 管理 TcpListener 实例、连接接收循环、连接并发名额及带积压排空的正常关闭。
    ///
    /// 从 ProxyServer 拆分。AcceptTcpClientAsync 本身不响应 CancellationToken，
    /// 因此通过 CancellationToken.Register 调用 listener.Stop()，强制 AcceptTcpClientAsync
    /// 抛出 ObjectDisposedException，实现立即关闭而无需等待 2 秒读取超时。
    /// </summary>
    public sealed class TransportLayer : IDisposable
    {
        /// <summary>
        /// 具名监听绑定，组合 TcpListener、连接接收循环处理函数和并发名额。
        /// </summary>
        private sealed class ListenerBinding
        {
            public string Name;
            public TcpListener Listener;
            public Func<TcpClient, Task> Handler;
            public SemaphoreSlim Slots;
            public int Backlog;
            public Task AcceptLoopTask;
            public CancellationTokenRegistration CancellationRegistration;
            public readonly ConcurrentDictionary<long, Task> ActiveHandlers =
                new ConcurrentDictionary<long, Task>();
            public readonly ConcurrentDictionary<long, TcpClient> ActiveClients =
                new ConcurrentDictionary<long, TcpClient>();
            public long NextHandlerId;
        }

        private readonly ListenerBinding[] _bindings;
        private readonly Action<string> _log;
        private CancellationTokenSource _cts;
        private int _started;
        private int _stopped;
        private int _disposed;

        /// <summary>
        /// 创建最多可管理 2 个监听器的传输层，分别用于 DLL 通信和回调。
        /// </summary>
        public TransportLayer(Action<string> log)
        {
            _log = log ?? (msg => { });
            _bindings = new ListenerBinding[2];
        }

        /// <summary>
        /// 添加监听器，必须在 StartAllAsync 前调用。
        /// </summary>
        public void AddListener(string name, string listenHost, int port,
            Func<TcpClient, Task> handler, int maxConcurrent, int backlog)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i] == null)
                {
                    var ip = listenHost == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(listenHost);
                    _bindings[i] = new ListenerBinding
                    {
                        Name = name,
                        Listener = new TcpListener(ip, port),
                        Handler = handler,
                        Slots = new SemaphoreSlim(maxConcurrent, maxConcurrent),
                        Backlog = backlog
                    };
                    return;
                }
            }
            throw new InvalidOperationException("TransportLayer supports at most 2 listeners.");
        }

        /// <summary>
        /// 启动全部已注册监听器，在所有连接接收循环进入运行状态后返回。
        /// </summary>
        public void StartAll(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                throw new InvalidOperationException("TransportLayer has already been started.");
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(TransportLayer));

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            foreach (var b in _bindings)
            {
                if (b == null) continue;
                b.Listener.Start(b.Backlog);
                _log($"传输层 {b.Name} 已启动监听");

                // 注册取消回调以强制停止监听器。AcceptTcpClientAsync 不直接响应 CancellationToken，
                // 因此调用 listener.Stop() 使其抛出 ObjectDisposedException。
                b.CancellationRegistration = _cts.Token.Register(() =>
                {
                    try { b.Listener.Stop(); } catch { }
                });

                b.AcceptLoopTask = Task.Run(() => AcceptLoop(b, _cts.Token));
            }
        }

        /// <summary>
        /// 正常关闭：停止接收连接，以 503 响应排空等待连接，并在排空超时前等待活动处理函数结束。
        /// </summary>
        public async Task StopAsync(int drainTimeoutMs = 3000)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            _cts?.Cancel();

            foreach (var b in _bindings)
            {
                if (b == null) continue;

                try { b.Listener?.Stop(); } catch { }
                foreach (var client in b.ActiveClients.Values)
                {
                    try { client.Close(); } catch { }
                }
            }

            var drainDeadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, drainTimeoutMs));

            // 首先确保连接接收循环不会再添加新的活动处理函数
            foreach (var b in _bindings)
            {
                if (b == null) continue;
                await WaitUntilAsync(b.AcceptLoopTask, drainDeadline).ConfigureAwait(false);
            }

            foreach (var b in _bindings)
            {
                if (b == null || b.ActiveHandlers.IsEmpty) continue;
                // ConcurrentDictionary 在 Count 和枚举之间可能变化，直接使用
                // Values 快照避免预分配数组中留下 null Task。
                var handlers = b.ActiveHandlers.Values;
                await WaitUntilAsync(Task.WhenAll(handlers), drainDeadline).ConfigureAwait(false);

                if (!b.ActiveHandlers.IsEmpty)
                    _log($"[传输层] {b.Name} 停止超时，仍有 {b.ActiveHandlers.Count} 个连接处理任务");
            }

            _log("传输层已停止");
        }

        /// <summary>
        /// 连接接收循环：接收连接、检查并发名额并分派到处理函数。
        /// </summary>
        private async Task AcceptLoop(ListenerBinding binding, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await binding.Listener.AcceptTcpClientAsync();
                    if (ct.IsCancellationRequested)
                    {
                        client?.Dispose();
                        break;
                    }

                    if (!binding.Slots.Wait(0))
                    {
                        client.SendTimeout = 1000;
                        HttpProtocolHandler.Write503ServiceBusy(client);
                        continue;
                    }

                    var handlerId = Interlocked.Increment(ref binding.NextHandlerId);
                    binding.ActiveClients.TryAdd(handlerId, client);
                    var handlerTask = Task.Run(async () =>
                    {
                        try
                        {
                            await binding.Handler(client).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogException($"[传输层] {binding.Name} 处理异常", ex);
                        }
                        finally
                        {
                            binding.Slots.Release();
                        }
                    });
                    binding.ActiveHandlers.TryAdd(handlerId, handlerTask);
                    _ = handlerTask.ContinueWith(completedTask =>
                    {
                        binding.ActiveHandlers.TryRemove(handlerId, out _);
                        binding.ActiveClients.TryRemove(handlerId, out _);
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
                catch (ObjectDisposedException)
                {
                    break; // 监听器已停止，属于正常关闭流程
                }
                catch (SocketException)
                {
                    break; // Socket 已关闭，属于正常关闭流程
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogException($"[传输层] {binding.Name} 接收连接异常", ex);
                }
            }
        }

        private static async Task WaitUntilAsync(Task task, DateTime deadline)
        {
            if (task == null) return;
            if (task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0) return;
            var completed = await Task.WhenAny(task, Task.Delay(remaining)).ConfigureAwait(false);
            if (completed == task)
                await task.ConfigureAwait(false);
        }

        private void LogException(string context, Exception ex)
        {
            // UI 仍保留精简摘要；文件日志额外保留 ERROR 级别和完整堆栈。
            Logger.Error(context, ex);
            _log($"{context}: {ex.Message}");
        }

        private static async Task DisposeSlotsWhenIdleAsync(ListenerBinding binding)
        {
            try
            {
                if (binding.AcceptLoopTask != null)
                    await binding.AcceptLoopTask.ConfigureAwait(false);
            }
            catch
            {
                // AcceptLoop 异常已在接收边界记录，仍需继续等待 Handler。
            }

            try
            {
                while (!binding.ActiveHandlers.IsEmpty)
                {
                    var handlers = binding.ActiveHandlers.Values;
                    if (handlers.Count == 0)
                    {
                        await Task.Yield();
                        continue;
                    }
                    await Task.WhenAll(handlers).ConfigureAwait(false);
                }
            }
            catch
            {
                // Handler 的业务异常已在处理边界记录。
            }
            finally
            {
                binding.Slots?.Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            try { StopAsync(0).GetAwaiter().GetResult(); } catch { }
            foreach (var b in _bindings)
            {
                if (b == null) continue;
                try { b.Listener?.Stop(); } catch { }
                b.CancellationRegistration.Dispose();
                _ = DisposeSlotsWhenIdleAsync(b);
            }
            _cts?.Dispose();
        }
    }
}
