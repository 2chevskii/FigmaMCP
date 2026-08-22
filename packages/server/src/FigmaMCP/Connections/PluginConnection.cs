using System.Collections.Concurrent;
using System.Net.WebSockets;
using FigmaMCP.Bridge;

namespace FigmaMCP.Connections;

public sealed class PluginConnection : IDisposable
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly SemaphoreSlim _send = new(1, 1);
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ReadOnlyMemory<byte>>> _pending = new();

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
        string mode)
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
        CancellationToken cancellationToken)
    {
        await _operations.WaitAsync(cancellationToken);

        try
        {
            var requestId = Guid.NewGuid();
            var completion = new TaskCompletionSource<ReadOnlyMemory<byte>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pending.TryAdd(requestId, completion))
            {
                throw new BridgeRpcException(
                    "plugin_protocol_error",
                    "Unable to register the plugin request.");
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
                        DateTimeOffset.UtcNow),
                    cancellationToken);

                return await completion.Task.WaitAsync(
                    BridgeProtocol.RequestTimeout,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new BridgeRpcException(
                    "plugin_timeout",
                    "The Figma plugin did not respond within thirty seconds.");
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

    public async Task SendAsync(
        BridgeEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var bytes = BridgeEnvelopeCodec.Encode(envelope);
        await _send.WaitAsync(cancellationToken);

        try
        {
            await _socket.SendAsync(
                bytes,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            _send.Release();
        }
    }

    public async Task CloseAsync(
        WebSocketCloseStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _socket.CloseAsync(status, reason, cancellationToken);
        }
    }

    public void Dispose()
    {
        _operations.Dispose();
        _send.Dispose();
    }
}
