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
    /// Manages TcpListener instances, accept loops, connection slot limiting,
    /// and graceful shutdown with backlog drain.
    ///
    /// Extracted from ProxyServer. The key fix: AcceptTcpClientAsync does NOT
    /// observe CancellationToken natively. We use CancellationToken.Register
    /// to call listener.Stop(), which forces AcceptTcpClientAsync to throw
    /// ObjectDisposedException, enabling immediate (non-2s-read-timeout) shutdown.
    /// </summary>
    public sealed class TransportLayer : IDisposable
    {
        /// <summary>
        /// A named listener binding together the TcpListener, its accept-loop handler,
        /// and its concurrency slot.
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
        /// Create a transport layer that can manage up to 2 listeners (DLL + callback).
        /// </summary>
        public TransportLayer(Action<string> log)
        {
            _log = log ?? (msg => { });
            _bindings = new ListenerBinding[2];
        }

        /// <summary>
        /// Add a listener. Must be called before StartAllAsync.
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
        /// Start all registered listeners. Returns when all accept loops are running.
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

                // CRITICAL FIX: Register cancellation to force-stop the listener.
                // AcceptTcpClientAsync does not observe CancellationToken natively,
                // so we force listener.Stop() which throws ObjectDisposedException.
                b.CancellationRegistration = _cts.Token.Register(() =>
                {
                    try { b.Listener.Stop(); } catch { }
                });

                b.AcceptLoopTask = Task.Run(() => AcceptLoop(b, _cts.Token));
            }
        }

        /// <summary>
        /// Graceful shutdown: stop accepting, drain pending connections with 503,
        /// wait for active handlers up to the drain timeout.
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

            // First ensure no accept loop can add another active handler.
            foreach (var b in _bindings)
            {
                if (b == null) continue;
                await WaitUntilAsync(b.AcceptLoopTask, drainDeadline).ConfigureAwait(false);
            }

            foreach (var b in _bindings)
            {
                if (b == null || b.ActiveHandlers.IsEmpty) continue;
                var handlers = new Task[b.ActiveHandlers.Count];
                var index = 0;
                foreach (var task in b.ActiveHandlers.Values)
                    handlers[index++] = task;
                await WaitUntilAsync(Task.WhenAll(handlers), drainDeadline).ConfigureAwait(false);

                if (!b.ActiveHandlers.IsEmpty)
                    _log($"[传输层] {b.Name} 停止超时，仍有 {b.ActiveHandlers.Count} 个连接处理任务");
            }

            _log("传输层已停止");
        }

        /// <summary>
        /// Accept loop: accept connections, check slot availability, dispatch to handler.
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
                            _log($"[传输层] {binding.Name} 处理异常: {ex.Message}");
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
                    break; // Listener stopped — normal shutdown
                }
                catch (SocketException)
                {
                    break; // Socket closed — normal shutdown
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log($"[传输层] {binding.Name} 接收连接异常: {ex.Message}");
                }
            }
        }

        private static async Task WaitUntilAsync(Task task, DateTime deadline)
        {
            if (task == null || task.IsCompleted) return;
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0) return;
            await Task.WhenAny(task, Task.Delay(remaining)).ConfigureAwait(false);
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
                if (b.ActiveHandlers.IsEmpty)
                    b.Slots?.Dispose();
            }
            _cts?.Dispose();
        }
    }
}
