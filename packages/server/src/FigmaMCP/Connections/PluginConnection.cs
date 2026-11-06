using System.Collections.Concurrent;
using System.Net.WebSockets;
using FigmaMCP.Bridge;

namespace FigmaMCP.Connections;

public sealed class PluginConnection : IDisposable
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly SemaphoreSlim _send = new(1, 1);
    private readonly ConcurrentDictionary<
        Guid,
        TaskCompletionSource<ReadOnlyMemory<byte>>
    > _pending = new();

    public PluginConnection(ConnectionSummary summary, WebSocket socket)
    {
        Summary = summary;
        _socket = socket;
    }

    public Guid Id => Summary.ConnectionId;

    public ConnectionSummary Summary { get; private set; }

    public void Touch()
    {
        Summary = Summary with { LastSeenAt = DateTimeOffset.UtcNow };
    }

    public void UpdateContext(
        string documentName,
        string pageId,
        string pageName,
        string editorType,
        string mode
    )
    {
        Summary = Summary with
        {
            DocumentName = documentName,
            CurrentPageId = pageId,
            CurrentPageName = pageName,
            EditorType = editorType,
            Mode = mode,
            LastSeenAt = DateTimeOffset.UtcNow,
        };
    }

    public bool Complete(Guid requestId, ReadOnlyMemory<byte> payload)
    {
        return _pending.TryRemove(requestId, out var completion)
            && completion.TrySetResult(payload);
    }

    public bool Fail(Guid requestId, BridgeError error)
    {
        return _pending.TryRemove(requestId, out var completion)
            && completion.TrySetException(new BridgeRpcException(error.Code, error.Message));
    }

    public void FailPending(Exception exception)
    {
        foreach (var requestId in _pending.Keys)
        {
            if (_pending.TryRemove(requestId, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }

    public async Task<ReadOnlyMemory<byte>> RequestAsync(
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    )
    {
        await _operations.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var deadline = new CancellationTokenSource(BridgeProtocol.RequestTimeout);
            var requestId = Guid.NewGuid();
            var completion = new TaskCompletionSource<ReadOnlyMemory<byte>>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            if (!_pending.TryAdd(requestId, completion))
            {
                throw new BridgeRpcException(
                    "plugin_protocol_error",
                    "Unable to register the plugin request."
                );
            }

            try
            {
                await SendAsync(
                    new BridgeEnvelope(
                        "request",
                        BridgeProtocol.Version,
                        Id,
                        requestId,
                        method,
                        payload,
                        null,
                        DateTimeOffset.UtcNow
                    ),
                    deadline.Token
                );

                return await completion.Task.WaitAsync(
                    BridgeProtocol.RequestTimeout,
                    deadline.Token
                );
            }
            catch (Exception exception)
                when (exception is TimeoutException
                    || exception is OperationCanceledException && deadline.IsCancellationRequested
                )
            {
                throw new BridgeRpcException(
                    "plugin_timeout",
                    "The Figma plugin did not respond within thirty seconds."
                );
            }
            finally
            {
                _pending.TryRemove(requestId, out _);
            }
        }
        finally
        {
            _operations.Release();
        }
    }

    public async Task SendAsync(BridgeEnvelope envelope, CancellationToken cancellationToken)
    {
        var bytes = BridgeEnvelopeCodec.Encode(envelope);
        await _send.WaitAsync(cancellationToken);

        try
        {
            await _socket.SendAsync(
                bytes,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken
            );
        }
        finally
        {
            _send.Release();
        }
    }

    public async Task CloseAsync(
        WebSocketCloseStatus status,
        string reason,
        CancellationToken cancellationToken
    )
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await _socket.CloseAsync(status, reason, timeout.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException)
            {
                _socket.Abort();
            }
        }
    }

    public void Dispose()
    {
        // Pending and queued asynchronous operations may still release these gates.
        // They own no native handles; let them be collected after those operations complete.
    }
}
