using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace FigmaMcp.Server.Connections;

public sealed class PluginConnectionRegistry(
    ILogger<PluginConnectionRegistry> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, PluginConnection> _connections = new();

    public int Count => _connections.Count;

    public IReadOnlyList<ConnectionSummary> Snapshot()
    {
        return _connections.Values
            .Select(connection => connection.Summary)
            .OrderBy(summary => summary.ConnectedAt)
            .ThenBy(
                summary => summary.ConnectionId.ToString("D"),
                StringComparer.Ordinal)
            .ToArray();
    }

    public async Task RegisterAsync(PluginConnection connection)
    {
        while (true)
        {
            if (_connections.TryAdd(connection.Id, connection))
            {
                logger.LogInformation("Plugin connected {ConnectionId}", connection.Id);
                return;
            }

            if (!_connections.TryGetValue(connection.Id, out var previous)
                || !_connections.TryUpdate(connection.Id, connection, previous))
            {
                continue;
            }

            previous.FailPending(
                new BridgeRpcException(
                    "plugin_disconnected",
                    "The Figma plugin connection was replaced before responding."));
            await previous.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "connection_replaced",
                CancellationToken.None);
            logger.LogInformation("Plugin connection replaced {ConnectionId}", connection.Id);
            return;
        }
    }

    public void RemoveIfCurrent(PluginConnection connection)
    {
        var pair = new KeyValuePair<Guid, PluginConnection>(connection.Id, connection);
        var removed = ((ICollection<KeyValuePair<Guid, PluginConnection>>)_connections).Remove(pair);

        if (!removed)
        {
            return;
        }

        connection.FailPending(
            new BridgeRpcException(
                "plugin_disconnected",
                "The Figma plugin disconnected before responding."));
        logger.LogInformation("Plugin disconnected {ConnectionId}", connection.Id);
    }

    public Task<ReadOnlyMemory<byte>> RequestAsync(
        Guid id,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!_connections.TryGetValue(id, out var connection))
        {
            throw new BridgeRpcException(
                "connection_not_found",
                "No live Figma plugin connection exists for the supplied connection_id.");
        }

        return connection.RequestAsync(method, payload, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var connections = _connections.Values.ToArray();
        _connections.Clear();

        foreach (var connection in connections)
        {
            connection.FailPending(
                new BridgeRpcException(
                    "plugin_disconnected",
                    "The server is shutting down."));
            await connection.CloseAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "server_shutdown",
                CancellationToken.None);
        }
    }
}
