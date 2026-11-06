using System.Collections.Concurrent;
using System.Text.Json;
using FigmaMCP.Bridge;
using FigmaMCP.Connections;
using FigmaMCP.Diagnostics;
using FigmaMCP.Options;

namespace FigmaMCP.Companion;

internal sealed class BackendServer(
    ServerOptions options,
    PluginConnectionRegistry registry,
    ServerInfoProvider info,
    BackendActivity activity,
    BackendLogProvider logs
)
{
    private readonly ConcurrentDictionary<Guid, Task> _sessions = new();
    private readonly object _budgetGate = new();
    private int _requests;
    private long _bytes;

    public async Task RunAsync(CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var pipe = IpcChannel.Listen(InstanceIdentity.Name(options.Port));
                try
                {
                    await pipe.WaitForConnectionAsync(stop);
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
                var id = Guid.NewGuid();
                var task = ServeAsync(new IpcChannel(pipe), stop);
                _sessions[id] = task;
                _ = task.ContinueWith(
                    completed => _sessions.TryRemove(id, out _),
                    TaskScheduler.Default
                );
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally
        {
            await Task.WhenAll(_sessions.Values);
        }
    }

    private async Task ServeAsync(IpcChannel channel, CancellationToken stop)
    {
        await using var owned = channel;
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var pending = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        var operations = new ConcurrentDictionary<Guid, Task>();
        Task diagnostics = Task.CompletedTask;
        try
        {
            using var lease = activity.Enter();
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(stop);
            handshake.CancelAfter(TimeSpan.FromSeconds(5));
            var hello = await channel.ReadAsync(handshake.Token, 16 * 1024);
            if (hello.Type != "hello" || hello.Protocol != 1 || hello.Port != options.Port)
                throw new IOException("Invalid IPC handshake.");
            if (!string.Equals(hello.Version, ProductInfo.Version, StringComparison.Ordinal))
            {
                await channel.WriteAsync(
                    new()
                    {
                        Type = "error",
                        Code = "backend_version_mismatch",
                        Message =
                            $"Frontend {hello.Version} cannot use backend {ProductInfo.Version}. Close existing clients and plugins, wait for idle shutdown, then restart.",
                    },
                    handshake.Token
                );
                return;
            }
            var serverInfo = await info.GetAsync(handshake.Token);
            await channel.WriteAsync(
                new()
                {
                    Type = "hello_ack",
                    Id = Guid.Parse(serverInfo.ServerInstanceId),
                    Version = ProductInfo.Version,
                    Port = options.Port,
                    IdleSeconds = options.IdleTimeoutSeconds,
                },
                handshake.Token
            );
            diagnostics = logs.PumpAsync(channel, session.Token);
            while (!session.IsCancellationRequested)
            {
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                    session.Token
                );
                readTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                var message = await channel.ReadAsync(readTimeout.Token);
                if (message.Type == "ping")
                {
                    await channel.WriteAsync(
                        new() { Type = "pong", Id = message.Id },
                        session.Token
                    );
                    continue;
                }
                if (message.Type == "cancel")
                {
                    lock (pending)
                        if (pending.TryGetValue(message.Id, out var cancellation))
                            cancellation.Cancel();
                    continue;
                }
                if (
                    message.Type != "request"
                    || message.Id == Guid.Empty
                    || pending.ContainsKey(message.Id)
                )
                    throw new IOException("Invalid IPC request.");
                var size = message.Payload?.Length ?? 0;
                bool admitted;
                lock (_budgetGate)
                {
                    admitted =
                        pending.Count < 32 && _requests < 64 && _bytes + size <= 64 * 1024 * 1024;
                    if (admitted)
                    {
                        _requests++;
                        _bytes += size;
                    }
                }
                if (!admitted)
                {
                    await channel.WriteAsync(
                        new()
                        {
                            Type = "error",
                            Id = message.Id,
                            Code = "backend_busy",
                            Message =
                                "Backend request capacity is exhausted; request was not sent to Figma.",
                        },
                        session.Token
                    );
                    continue;
                }
                var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    session.Token
                );
                pending[message.Id] = cancellationSource;
                var operation = ExecuteAsync(
                    message,
                    channel,
                    cancellationSource.Token,
                    session.Token
                );
                operations[message.Id] = operation;
                _ = operation.ContinueWith(
                    completed =>
                    {
                        lock (pending)
                            if (pending.TryRemove(message.Id, out var finished))
                                finished.Dispose();
                        lock (_budgetGate)
                        {
                            _requests--;
                            _bytes -= size;
                        }
                        operations.TryRemove(message.Id, out _);
                    },
                    TaskScheduler.Default
                );
            }
        }
        catch (Exception e)
            when (e
                    is IOException
                        or OperationCanceledException
                        or MessagePack.MessagePackSerializationException
                        or ObjectDisposedException
            ) { }
        finally
        {
            await session.CancelAsync();
            await channel.DisposeAsync();
            await Task.WhenAll(operations.Values);
            await diagnostics;
            foreach (var cancellation in pending.Values)
                cancellation.Dispose();
        }
    }

    private async Task ExecuteAsync(
        IpcMessage request,
        IpcChannel channel,
        CancellationToken cancel,
        CancellationToken session
    )
    {
        using var operation = activity.Enter();
        IpcMessage response;
        try
        {
            byte[] payload = request.Method switch
            {
                "list" => JsonSerializer.SerializeToUtf8Bytes(registry.Snapshot()),
                "info" => JsonSerializer.SerializeToUtf8Bytes(await info.GetAsync(cancel)),
                "invoke" when request.ConnectionId != Guid.Empty && request.Payload is not null => (
                    await registry.RequestAsync(
                        request.ConnectionId,
                        ReadMethod(request),
                        request.Payload,
                        cancel
                    )
                ).ToArray(),
                _ => throw new BridgeRpcException(
                    "backend_protocol_error",
                    "Unknown backend operation."
                ),
            };
            response = new()
            {
                Type = "response",
                Id = request.Id,
                Payload = payload,
            };
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (BridgeRpcException e)
        {
            response = new()
            {
                Type = "error",
                Id = request.Id,
                Code = e.Code,
                Message = e.Message,
            };
        }
        catch (Exception e)
        {
            response = new()
            {
                Type = "error",
                Id = request.Id,
                Code = "backend_protocol_error",
                Message = e.Message,
            };
        }
        try
        {
            await channel.WriteAsync(response, session);
        }
        catch (Exception e)
            when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    private static string ReadMethod(IpcMessage request)
    {
        var method = request.Message;
        if (
            string.IsNullOrEmpty(method)
            || method.Length > 128
            || method.Any(c => !(c is >= 'a' and <= 'z' or '_'))
        )
            throw new BridgeRpcException("backend_protocol_error", "Invalid Bridge method.");
        return method;
    }
}
