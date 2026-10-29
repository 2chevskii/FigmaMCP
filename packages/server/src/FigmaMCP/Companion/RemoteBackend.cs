using System.Collections.Concurrent;
using System.Text.Json;
using FigmaMCP.Connections;
using FigmaMCP.Diagnostics;
using FigmaMCP.Options;

namespace FigmaMCP.Companion;

internal sealed class RemoteBackend(ServerOptions options) : ICompanionBackend, IAsyncDisposable
{
    private sealed class Session(IpcChannel channel)
    {
        public IpcChannel Channel { get; } = channel;
        public ConcurrentDictionary<Guid, TaskCompletionSource<IpcMessage>> Pending { get; } =
            new();
    }

    private readonly CancellationTokenSource _stop = new();
    private Session? _session;
    private Task? _runner;
    private int _effectiveIdle = options.IdleTimeoutSeconds;
    private string _unavailableCode = "backend_unavailable";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _session = new(await EnsureAsync(cancellationToken));
        _runner = RunAsync();
    }

    private async Task RunAsync()
    {
        var delay = 250;
        while (!_stop.IsCancellationRequested)
        {
            var current = Volatile.Read(ref _session);
            if (current is not null)
            {
                using var sessionStop = CancellationTokenSource.CreateLinkedTokenSource(
                    _stop.Token
                );
                var heartbeat = HeartbeatAsync(current, sessionStop.Token);
                try
                {
                    while (!sessionStop.IsCancellationRequested)
                    {
                        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                            sessionStop.Token
                        );
                        readTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                        var message = await current.Channel.ReadAsync(readTimeout.Token);
                        if (message.Type == "pong")
                            continue;
                        if (message.Type == "diagnostic")
                        {
                            Console.Error.WriteLine(message.Message);
                            continue;
                        }
                        if (message.Type is not ("response" or "error"))
                            throw new IOException("Unexpected backend response.");
                        if (current.Pending.TryRemove(message.Id, out var result))
                            result.TrySetResult(message);
                    }
                }
                catch (Exception e)
                    when (e
                            is IOException
                                or OperationCanceledException
                                or ObjectDisposedException
                                or MessagePack.MessagePackSerializationException
                    )
                {
                    Console.Error.WriteLine(
                        "Backend connection lost; pending calls will not be replayed."
                    );
                }
                finally
                {
                    Interlocked.CompareExchange(ref _session, null, current);
                    await current.Channel.DisposeAsync();
                    sessionStop.Cancel();
                    await heartbeat;
                    foreach (var pending in current.Pending.Values)
                        pending.TrySetException(
                            new BridgeRpcException(
                                "backend_connection_lost",
                                "Backend connection lost. An operation already sent to Figma may have completed; inspect the document before retrying."
                            )
                        );
                    current.Pending.Clear();
                }
            }
            try
            {
                await Task.Delay(delay + Random.Shared.Next(100), _stop.Token);
                Console.Error.WriteLine("Reconnecting to shared backend.");
                var channel = await EnsureAsync(_stop.Token);
                Volatile.Write(ref _session, new Session(channel));
                _unavailableCode = "backend_unavailable";
                delay = 250;
                Console.Error.WriteLine("Backend connection restored.");
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                _unavailableCode = e is BridgeRpcException rpc ? rpc.Code : "backend_unavailable";
                Console.Error.WriteLine($"{_unavailableCode}: {e}");
                delay = e is BridgeRpcException ? 30000 : Math.Min(5000, delay * 2);
            }
        }
    }

    private static async Task HeartbeatAsync(Session current, CancellationToken stop)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stop))
                await current.Channel.WriteAsync(
                    new() { Type = "ping", Id = Guid.NewGuid() },
                    stop
                );
        }
        catch (Exception e)
            when (e is IOException or OperationCanceledException or ObjectDisposedException)
        {
            await current.Channel.DisposeAsync();
        }
    }

    private async Task<IpcMessage> CallAsync(
        IpcMessage request,
        CancellationToken cancellationToken
    )
    {
        var session =
            Volatile.Read(ref _session)
            ?? throw new BridgeRpcException(
                _unavailableCode,
                "Shared backend is unavailable; recovery runs automatically."
            );
        var completion = new TaskCompletionSource<IpcMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        request = request with { Type = "request", Id = Guid.NewGuid() };
        if (session.Pending.Count >= 32)
            throw new BridgeRpcException("backend_busy", "Frontend request capacity is exhausted.");
        session.Pending[request.Id] = completion;
        try
        {
            await session.Channel.WriteAsync(request, cancellationToken);
            var response = await completion.Task.WaitAsync(cancellationToken);
            if (response.Type == "error")
                throw new BridgeRpcException(
                    response.Code ?? "backend_protocol_error",
                    response.Message ?? "Backend error."
                );
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await session.Channel.WriteAsync(
                    new() { Type = "cancel", Id = request.Id },
                    _stop.Token
                );
            }
            catch (Exception e)
                when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
            throw;
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            throw new BridgeRpcException(
                "backend_connection_lost",
                "Backend connection lost; operation outcome may be unknown."
            );
        }
        finally
        {
            session.Pending.TryRemove(request.Id, out _);
        }
    }

    public async Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            return JsonSerializer.Deserialize<ConnectionSummary[]>(
                (await CallAsync(new() { Method = "list" }, cancellationToken)).Payload!
            )!;
        }
        catch (BridgeRpcException e)
        {
            throw new ModelContextProtocol.McpException(e.Code + ": " + e.Message);
        }
    }

    public async Task<ServerInfo> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            return JsonSerializer.Deserialize<ServerInfo>(
                (await CallAsync(new() { Method = "info" }, cancellationToken)).Payload!
            )!;
        }
        catch (BridgeRpcException e)
        {
            throw new ModelContextProtocol.McpException(e.Code + ": " + e.Message);
        }
    }

    public async Task<ReadOnlyMemory<byte>> InvokeAsync(
        Guid connectionId,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    ) =>
        (
            await CallAsync(
                new()
                {
                    Method = "invoke",
                    Message = method,
                    ConnectionId = connectionId,
                    Payload = payload.ToArray(),
                },
                cancellationToken
            )
        ).Payload!;

    private Task<IpcChannel> EnsureAsync(CancellationToken stop)
    {
        // Blocking work stays on one dedicated thread for mutex ownership.
        var completion = new TaskCompletionSource<IpcChannel>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        new Thread(() =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var token = timeout.Token;
            using var registration = token.Register(() => completion.TrySetCanceled(token));
            try
            {
                var existing = TryConnectAsync(token).GetAwaiter().GetResult();
                if (existing is not null)
                {
                    if (!completion.TrySetResult(existing))
                        existing.DisposeAsync().GetAwaiter().GetResult();
                    return;
                }
                var name = InstanceIdentity.Name(options.Port);
                using var startup = InstanceIdentity.CreateMutex(name + "-startup");
                while (!InstanceIdentity.Acquire(startup, 100))
                    token.ThrowIfCancellationRequested();
                try
                {
                    existing = TryConnectAsync(token).GetAwaiter().GetResult();
                    if (existing is not null)
                    {
                        if (!completion.TrySetResult(existing))
                            existing.DisposeAsync().GetAwaiter().GetResult();
                        return;
                    }
                    using var lifetime = InstanceIdentity.CreateMutex(name + "-lifetime");
                    if (InstanceIdentity.Acquire(lifetime, 0))
                    {
                        lifetime.ReleaseMutex();
                        var bootstrap = name + "-boot-" + Guid.NewGuid().ToString("N");
                        using var pipe = IpcChannel.Listen(bootstrap);
                        BackendLauncher.Launch(
                            options with
                            {
                                IdleTimeoutSeconds = _effectiveIdle,
                            },
                            bootstrap
                        );
                        pipe.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                        var report = new IpcChannel(pipe)
                            .ReadAsync(token, 16 * 1024)
                            .GetAwaiter()
                            .GetResult();
                        if (report.Type != "ready")
                            throw new BridgeRpcException(
                                report.Code ?? "backend_unavailable",
                                report.Message ?? "Backend startup failed."
                            );
                    }
                    while ((existing = TryConnectAsync(token).GetAwaiter().GetResult()) is null)
                        token.ThrowIfCancellationRequested();
                    if (!completion.TrySetResult(existing))
                        existing.DisposeAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    startup.ReleaseMutex();
                }
            }
            catch (Exception e)
            {
                completion.TrySetException(e);
            }
        })
        {
            IsBackground = true,
            Name = "Companion startup",
        }.Start();
        return completion.Task;
    }

    private async Task<IpcChannel?> TryConnectAsync(CancellationToken stop)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(500));
        IpcChannel channel;
        try
        {
            channel = await IpcChannel.ConnectAsync(
                InstanceIdentity.Name(options.Port),
                timeout.Token
            );
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(stop);
            handshake.CancelAfter(TimeSpan.FromSeconds(5));
            await channel.WriteAsync(
                new()
                {
                    Type = "hello",
                    Version = ProductInfo.Version,
                    Port = options.Port,
                },
                handshake.Token
            );
            var reply = await channel.ReadAsync(handshake.Token, 16 * 1024);
            if (reply.Type == "error")
                throw new BridgeRpcException(
                    reply.Code ?? "backend_protocol_error",
                    reply.Message ?? "Handshake failed."
                );
            if (
                reply.Type != "hello_ack"
                || reply.Version != ProductInfo.Version
                || reply.Protocol != 1
                || reply.Port != options.Port
                || reply.IdleSeconds is < 1 or > 86400
            )
                throw new BridgeRpcException(
                    "backend_protocol_error",
                    "Invalid backend handshake."
                );
            _effectiveIdle = reply.IdleSeconds;
            if (_effectiveIdle != options.IdleTimeoutSeconds)
                Console.Error.WriteLine(
                    $"Using existing backend idle timeout: {_effectiveIdle} seconds (requested {options.IdleTimeoutSeconds})."
                );
            return channel;
        }
        catch
        {
            await channel.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        var current = Volatile.Read(ref _session);
        if (current is not null)
            await current.Channel.DisposeAsync();
        if (_runner is not null)
            await _runner;
    }
}
