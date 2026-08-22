using System.Net.WebSockets;
using System.Text.Json;
using FigmaMcp.Server.Connections;
using MessagePack;

namespace FigmaMcp.Server.Bridge;

public static class BridgeEndpoint
{
    public static async Task HandleAsync(
        HttpContext context,
        PluginConnectionRegistry registry,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime
    )
    {
        if (!IsValidRequest(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var logger = loggerFactory.CreateLogger("Bridge");
        using var socket = await AcceptSocketAsync(context);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted,
            lifetime.ApplicationStopping
        );

        cancellation.CancelAfter(BridgeProtocol.HelloTimeout);
        PluginConnection? connection = null;

        try
        {
            while (socket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
            {
                var envelope = await ReceiveAsync(socket, cancellation.Token);
                if (envelope is null)
                {
                    break;
                }

                ValidateProtocolVersion(envelope);

                if (connection is null)
                {
                    connection = await CompleteHandshakeAsync(
                        envelope,
                        socket,
                        registry,
                        cancellation.Token
                    );
                    cancellation.CancelAfter(Timeout.InfiniteTimeSpan);
                    continue;
                }

                ProcessMessage(envelope, connection, logger);
            }
        }
        catch (OperationCanceledException)
            when (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            logger.LogWarning("Bridge hello timed out");
        }
        catch (BridgeProtocolException exception)
        {
            logger.LogWarning("Invalid bridge message: {Message}", exception.Message);
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(exception, "Bridge socket closed");
        }
        finally
        {
            if (connection is not null)
            {
                registry.RemoveIfCurrent(connection);
                connection.Dispose();
            }

            await CloseOpenSocketAsync(socket);
        }
    }

    private static bool IsValidRequest(HttpContext context)
    {
        return context.WebSockets.IsWebSocketRequest
            && IsValidOrigin(context.Request.Headers.Origin)
            && context.WebSockets.WebSocketRequestedProtocols.Contains(
                BridgeProtocol.Subprotocol,
                StringComparer.Ordinal
            );
    }

    private static Task<WebSocket> AcceptSocketAsync(HttpContext context)
    {
        return context.WebSockets.AcceptWebSocketAsync(
            new WebSocketAcceptContext
            {
                SubProtocol = BridgeProtocol.Subprotocol,
                DangerousEnableCompression = true,
                DisableServerContextTakeover = true,
            }
        );
    }

    private static async Task<PluginConnection> CompleteHandshakeAsync(
        BridgeEnvelope envelope,
        WebSocket socket,
        PluginConnectionRegistry registry,
        CancellationToken cancellationToken
    )
    {
        if (envelope.Type != "hello" || envelope.ConnectionId is null || envelope.Payload is null)
        {
            throw new BridgeProtocolException("The first bridge message must be hello.");
        }

        var hello = ReadHello(envelope.Payload.Value);
        var now = DateTimeOffset.UtcNow;
        var summary = new ConnectionSummary(
            envelope.ConnectionId.Value,
            hello.PluginVersion,
            envelope.ProtocolVersion,
            hello.DocumentName,
            hello.CurrentPage.Id,
            hello.CurrentPage.Name,
            hello.EditorType,
            hello.Mode,
            now,
            now
        );
        var connection = new PluginConnection(summary, socket);

        await registry.RegisterAsync(connection);
        await connection.SendAsync(
            new BridgeEnvelope(
                "hello_ack",
                BridgeProtocol.Version,
                connection.Id,
                null,
                null,
                CreateHelloAcknowledgement(),
                null,
                DateTimeOffset.UtcNow
            ),
            cancellationToken
        );

        return connection;
    }

    private static void ProcessMessage(
        BridgeEnvelope envelope,
        PluginConnection connection,
        ILogger logger
    )
    {
        if (envelope.ConnectionId != connection.Id)
        {
            throw new BridgeProtocolException(
                "connection_id does not match the handshaken connection."
            );
        }

        connection.Touch();

        switch (envelope)
        {
            case { Type: "context_changed", Payload: { } payload }:
                var context = ReadContext(payload);
                connection.UpdateContext(
                    context.DocumentName,
                    context.CurrentPage.Id,
                    context.CurrentPage.Name,
                    context.EditorType,
                    context.Mode
                );
                break;
            case { Type: "response", RequestId: { } requestId, Payload: { } payload }:
                if (!connection.Complete(requestId, payload))
                {
                    logger.LogDebug("Late bridge response {RequestId}", requestId);
                }

                break;
            case { Type: "error", RequestId: { } requestId, Error: { } error }:
                if (!connection.Fail(requestId, error))
                {
                    logger.LogDebug("Late bridge error {RequestId}", requestId);
                }

                break;
            case { Type: "pong" }:
                break;
            default:
                throw new BridgeProtocolException("Unsupported bridge message type.");
        }
    }

    private static async Task<BridgeEnvelope?> ReceiveAsync(
        WebSocket socket,
        CancellationToken cancellationToken
    )
    {
        using var stream = new MemoryStream();
        var buffer = new byte[16 * 1024];

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                throw new BridgeProtocolException("Bridge messages must be binary.");
            }

            if (stream.Length + result.Count > BridgeProtocol.MaxMessageBytes)
            {
                throw new BridgeProtocolException("Bridge message exceeds the 16 MiB limit.");
            }

            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
        } while (!result.EndOfMessage);

        return BridgeEnvelopeCodec.Decode(stream.ToArray());
    }

    private static HelloPayload ReadHello(ReadOnlyMemory<byte> raw)
    {
        var payload = DeserializePayload<HelloPayload>(raw, "hello");
        ValidateContext(
            payload.PluginVersion,
            payload.EditorType,
            payload.Mode,
            payload.DocumentName,
            payload.CurrentPage,
            "hello"
        );
        return payload;
    }

    private static ContextPayload ReadContext(ReadOnlyMemory<byte> raw)
    {
        var payload = DeserializePayload<ContextPayload>(raw, "context_changed");
        ValidateContext(
            null,
            payload.EditorType,
            payload.Mode,
            payload.DocumentName,
            payload.CurrentPage,
            "context_changed"
        );
        return payload;
    }

    private static T DeserializePayload<T>(ReadOnlyMemory<byte> raw, string messageType)
    {
        var json = MessagePackSerializer.ConvertToJson(raw, BridgeProtocol.SerializerOptions);
        return JsonSerializer.Deserialize<T>(json, BridgeProtocol.PayloadJsonOptions)
            ?? throw new BridgeProtocolException($"Invalid {messageType} payload.");
    }

    private static void ValidateContext(
        string? pluginVersion,
        string? editorType,
        string? mode,
        string? documentName,
        PagePayload? currentPage,
        string messageType
    )
    {
        var invalid =
            string.IsNullOrWhiteSpace(editorType)
            || string.IsNullOrWhiteSpace(mode)
            || documentName is null
            || currentPage is null
            || string.IsNullOrWhiteSpace(currentPage.Id)
            || currentPage.Name is null
            || pluginVersion is not null && string.IsNullOrWhiteSpace(pluginVersion);

        if (invalid)
        {
            throw new BridgeProtocolException($"Invalid {messageType} payload.");
        }
    }

    private static ReadOnlyMemory<byte> CreateHelloAcknowledgement()
    {
        return MessagePackSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["server_version"] = "0.2.0",
                ["request_timeout_ms"] = (int)BridgeProtocol.RequestTimeout.TotalMilliseconds,
                ["max_message_bytes"] = BridgeProtocol.MaxMessageBytes,
            },
            BridgeProtocol.SerializerOptions
        );
    }

    private static void ValidateProtocolVersion(BridgeEnvelope envelope)
    {
        if (envelope.ProtocolVersion != BridgeProtocol.Version)
        {
            throw new BridgeProtocolException("Unsupported protocol version.");
        }
    }

    private static bool IsValidOrigin(string? origin)
    {
        return string.IsNullOrEmpty(origin)
            || string.Equals(origin, "null", StringComparison.Ordinal);
    }

    private static async Task CloseOpenSocketAsync(WebSocket socket)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.PolicyViolation,
                "bridge_closed",
                CancellationToken.None
            );
        }
    }
}
