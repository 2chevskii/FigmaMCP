using System.Collections.Concurrent;
using System.Net.WebSockets;
using FigmaMcp.Server.Bridge;

namespace FigmaMcp.Server.Connections;

public sealed record ConnectionSummary(Guid ConnectionId, string PluginVersion, int ProtocolVersion, string DocumentName, string CurrentPageId, string CurrentPageName, string EditorType, string Mode, DateTimeOffset ConnectedAt, DateTimeOffset LastSeenAt);
public sealed class BridgeRpcException(string code, string message) : Exception(message) { public string Code { get; } = code; }

public sealed class PluginConnectionRegistry(ILogger<PluginConnectionRegistry> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, PluginConnection> _connections = new();
    public int Count => _connections.Count;
    public IReadOnlyList<ConnectionSummary> Snapshot() => _connections.Values.Select(x => x.Summary).OrderBy(x => x.ConnectedAt).ThenBy(x => x.ConnectionId).ToArray();
    public async Task RegisterAsync(PluginConnection connection)
    {
        if (_connections.TryGetValue(connection.Id, out var previous)) { _connections[connection.Id] = connection; await previous.CloseAsync(WebSocketCloseStatus.NormalClosure, "connection_replaced", CancellationToken.None); logger.LogInformation("Plugin connection replaced {ConnectionId}", connection.Id); }
        else { _connections[connection.Id] = connection; logger.LogInformation("Plugin connected {ConnectionId}", connection.Id); }
    }
    public void RemoveIfCurrent(PluginConnection connection)
    {
        if (_connections.TryGetValue(connection.Id, out var current) && ReferenceEquals(current, connection) && _connections.TryRemove(connection.Id, out _)) { connection.FailPending(new BridgeRpcException("plugin_disconnected", "The Figma plugin disconnected before responding.")); logger.LogInformation("Plugin disconnected {ConnectionId}", connection.Id); }
    }
    public async Task<ReadOnlyMemory<byte>> RequestAsync(Guid id, string method, CancellationToken cancellationToken)
    {
        if (!_connections.TryGetValue(id, out var connection)) throw new BridgeRpcException("connection_not_found", "No live Figma plugin connection exists for the supplied connection_id.");
        return await connection.RequestAsync(method, cancellationToken);
    }
    public async ValueTask DisposeAsync()
    {
        var connections = _connections.Values.ToArray(); _connections.Clear();
        foreach (var connection in connections) { connection.FailPending(new BridgeRpcException("plugin_disconnected", "The server is shutting down.")); await connection.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "server_shutdown", CancellationToken.None); }
    }
}

public sealed class PluginConnection : IDisposable
{
    private readonly WebSocket _socket; private readonly SemaphoreSlim _operations = new(1, 1); private readonly SemaphoreSlim _send = new(1, 1); private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ReadOnlyMemory<byte>>> _pending = new();
    public PluginConnection(ConnectionSummary summary, WebSocket socket) { Summary = summary; _socket = socket; }
    public Guid Id => Summary.ConnectionId; public ConnectionSummary Summary { get; private set; }
    public void Touch() => Summary = Summary with { LastSeenAt = DateTimeOffset.UtcNow };
    public void UpdateContext(string documentName, string pageId, string pageName, string editorType, string mode) => Summary = Summary with { DocumentName = documentName, CurrentPageId = pageId, CurrentPageName = pageName, EditorType = editorType, Mode = mode, LastSeenAt = DateTimeOffset.UtcNow };
    public bool Complete(Guid requestId, ReadOnlyMemory<byte> payload) => _pending.TryRemove(requestId, out var completion) && completion.TrySetResult(payload);
    public bool Fail(Guid requestId, BridgeError error) => _pending.TryRemove(requestId, out var completion) && completion.TrySetException(new BridgeRpcException("plugin_protocol_error", error.Message));
    public void FailPending(Exception exception) { foreach (var pair in _pending) if (_pending.TryRemove(pair.Key, out var completion)) completion.TrySetException(exception); }
    public async Task<ReadOnlyMemory<byte>> RequestAsync(string method, CancellationToken cancellationToken)
    {
        await _operations.WaitAsync(cancellationToken);
        try
        {
            var requestId = Guid.NewGuid(); var completion = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously); if (!_pending.TryAdd(requestId, completion)) throw new BridgeRpcException("plugin_protocol_error", "Unable to register the plugin request.");
            try { await SendAsync(new BridgeEnvelope("request", 1, Id, requestId, method, BridgeEnvelopeCodec.EmptyMap(), null, DateTimeOffset.UtcNow), cancellationToken); return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
            catch (TimeoutException) { throw new BridgeRpcException("plugin_timeout", "The Figma plugin did not respond within ten seconds."); }
            finally { _pending.TryRemove(requestId, out _); }
        }
        finally { _operations.Release(); }
    }
    public async Task SendAsync(BridgeEnvelope envelope, CancellationToken cancellationToken)
    {
        var bytes = BridgeEnvelopeCodec.Encode(envelope); await _send.WaitAsync(cancellationToken); try { await _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken); } finally { _send.Release(); }
    }
    public async Task CloseAsync(WebSocketCloseStatus status, string reason, CancellationToken cancellationToken) { if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived) await _socket.CloseAsync(status, reason, cancellationToken); }
    public void Dispose() { _operations.Dispose(); _send.Dispose(); }
}
