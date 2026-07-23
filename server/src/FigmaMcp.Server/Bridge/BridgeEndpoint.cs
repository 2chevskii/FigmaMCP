using System.Net.WebSockets;
using System.Text.Json;
using FigmaMcp.Server.Connections;

namespace FigmaMcp.Server.Bridge;

public static class BridgeEndpoint
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public static async Task HandleAsync(HttpContext context, PluginConnectionRegistry registry, ILoggerFactory loggerFactory, IHostApplicationLifetime lifetime)
    {
        var logger = loggerFactory.CreateLogger("Bridge");
        if (!context.WebSockets.IsWebSocketRequest || !IsValidOrigin(context.Request.Headers.Origin) || !context.WebSockets.WebSocketRequestedProtocols.Contains("figma-mcp-bridge.v1", StringComparer.Ordinal)) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext { SubProtocol = "figma-mcp-bridge.v1", DangerousEnableCompression = true, DisableServerContextTakeover = true });
        PluginConnection? connection = null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping); linked.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            while (socket.State == WebSocketState.Open && !linked.IsCancellationRequested)
            {
                var envelope = await ReceiveAsync(socket, linked.Token);
                if (envelope.ProtocolVersion != 1) throw new BridgeProtocolException("Unsupported protocol version.");
                if (connection is null)
                {
                    if (envelope.Type != "hello" || envelope.ConnectionId is null || envelope.Payload is null) throw new BridgeProtocolException("The first bridge message must be hello.");
                    var hello = ReadHello(envelope.Payload.Value); var now = DateTimeOffset.UtcNow;
                    connection = new PluginConnection(new ConnectionSummary(envelope.ConnectionId.Value, hello.PluginVersion, envelope.ProtocolVersion, hello.DocumentName, hello.CurrentPage.Id, hello.CurrentPage.Name, hello.EditorType, hello.Mode, now, now), socket);
                    await registry.RegisterAsync(connection); linked.CancelAfter(Timeout.InfiniteTimeSpan);
                    await connection.SendAsync(new BridgeEnvelope("hello_ack", 1, connection.Id, null, null, AckPayload(), null, DateTimeOffset.UtcNow), linked.Token); continue;
                }
                if (envelope.ConnectionId != connection.Id) throw new BridgeProtocolException("connection_id does not match the handshaken connection.");
                connection.Touch();
                switch (envelope.Type)
                {
                    case "context_changed" when envelope.Payload is { } payload: var changed = ReadContext(payload); connection.UpdateContext(changed.DocumentName, changed.CurrentPage.Id, changed.CurrentPage.Name, changed.EditorType, changed.Mode); break;
                    case "response" when envelope.RequestId is { } responseId && envelope.Payload is { } responsePayload: if (!connection.Complete(responseId, responsePayload)) logger.LogDebug("Late bridge response {RequestId}", responseId); break;
                    case "error" when envelope.RequestId is { } errorId && envelope.Error is { } error: if (!connection.Fail(errorId, error)) logger.LogDebug("Late bridge error {RequestId}", errorId); break;
                    case "pong": break;
                    default: throw new BridgeProtocolException("Unsupported bridge message type.");
                }
            }
        }
        catch (OperationCanceledException) when (!lifetime.ApplicationStopping.IsCancellationRequested) { logger.LogWarning("Bridge hello timed out"); }
        catch (BridgeProtocolException exception) { logger.LogWarning("Invalid bridge message: {Message}", exception.Message); }
        catch (WebSocketException exception) { logger.LogDebug(exception, "Bridge socket closed"); }
        finally { if (connection is not null) registry.RemoveIfCurrent(connection); if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bridge_closed", CancellationToken.None); }
    }
    private static async Task<BridgeEnvelope> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(); var buffer = new byte[16 * 1024]; WebSocketReceiveResult result;
        do { result = await socket.ReceiveAsync(buffer, cancellationToken); if (result.MessageType != WebSocketMessageType.Binary) throw new BridgeProtocolException("Bridge messages must be binary."); if (stream.Length + result.Count > BridgeEnvelopeCodec.MaxMessageBytes) throw new BridgeProtocolException("Bridge message exceeds the 1 MiB limit."); await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken); } while (!result.EndOfMessage);
        return BridgeEnvelopeCodec.Decode(stream.ToArray());
    }
    private static bool IsValidOrigin(string? origin) => string.IsNullOrEmpty(origin) || string.Equals(origin, "null", StringComparison.Ordinal);
    private static HelloPayload ReadHello(ReadOnlyMemory<byte> raw)
    {
        var payload = DeserializePayload<HelloPayload>(raw, "hello");
        ValidateContext(payload.PluginVersion, payload.EditorType, payload.Mode, payload.DocumentName, payload.CurrentPage, "hello");
        return payload;
    }
    private static ContextPayload ReadContext(ReadOnlyMemory<byte> raw)
    {
        var payload = DeserializePayload<ContextPayload>(raw, "context_changed");
        ValidateContext(null, payload.EditorType, payload.Mode, payload.DocumentName, payload.CurrentPage, "context_changed");
        return payload;
    }
    private static T DeserializePayload<T>(ReadOnlyMemory<byte> raw, string type) => JsonSerializer.Deserialize<T>(MessagePack.MessagePackSerializer.ConvertToJson(raw, MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)), PayloadJsonOptions) ?? throw new BridgeProtocolException($"Invalid {type} payload.");
    private static void ValidateContext(string? pluginVersion, string? editorType, string? mode, string? documentName, PagePayload? currentPage, string type)
    {
        if (string.IsNullOrWhiteSpace(editorType) || string.IsNullOrWhiteSpace(mode) || documentName is null || currentPage is null || string.IsNullOrWhiteSpace(currentPage.Id) || currentPage.Name is null || (pluginVersion is not null && string.IsNullOrWhiteSpace(pluginVersion))) throw new BridgeProtocolException($"Invalid {type} payload.");
    }
    private static ReadOnlyMemory<byte> AckPayload() => MessagePack.MessagePackSerializer.Serialize(new Dictionary<string, object> { ["server_version"] = "0.1.0", ["request_timeout_ms"] = 10000, ["max_message_bytes"] = BridgeEnvelopeCodec.MaxMessageBytes }, MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData));
    private sealed record PagePayload(string Id, string Name);
    private sealed record HelloPayload(string PluginVersion, string EditorType, string Mode, string DocumentName, PagePayload CurrentPage);
    private sealed record ContextPayload(string EditorType, string Mode, string DocumentName, PagePayload CurrentPage);
}
