using System.Net.WebSockets;
using System.Threading.Channels;
using FigmaMCP.Bridge;
using FigmaMCP.Connections;

namespace FigmaMCP.Tests.Unit.Companion;

public sealed class OperationCancellationTests
{
    [Fact]
    public async Task CancellationDoesNotReleaseDispatchedOperationButRemovesQueuedRequest()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var socket = new RecordingSocket();
        using var connection = new PluginConnection(
            new ConnectionSummary(
                Guid.NewGuid(),
                "test",
                2,
                "doc",
                "0:1",
                "page",
                "figma",
                "default",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            ),
            socket
        );
        using var firstCancellation = new CancellationTokenSource();
        var first = connection.RequestAsync(
            "get_document_metadata",
            BridgeEnvelopeCodec.EmptyMap(),
            firstCancellation.Token
        );
        var firstEnvelope = await socket.Sent.Reader.ReadAsync(timeout.Token);
        firstCancellation.Cancel();
        using var queuedCancellation = new CancellationTokenSource();
        var queued = connection.RequestAsync(
            "get_document_metadata",
            BridgeEnvelopeCodec.EmptyMap(),
            queuedCancellation.Token
        );
        queuedCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        var next = connection.RequestAsync(
            "get_document_metadata",
            BridgeEnvelopeCodec.EmptyMap(),
            timeout.Token
        );
        Assert.False(socket.Sent.Reader.TryRead(out _));
        Assert.False(first.IsCompleted);
        connection.Complete(firstEnvelope.RequestId!.Value, BridgeEnvelopeCodec.EmptyMap());
        await first;
        var nextEnvelope = await socket.Sent.Reader.ReadAsync(timeout.Token);
        connection.Complete(nextEnvelope.RequestId!.Value, BridgeEnvelopeCodec.EmptyMap());
        await next;
        Assert.False(socket.Sent.Reader.TryRead(out _));
    }

    private sealed class RecordingSocket : WebSocket
    {
        public Channel<BridgeEnvelope> Sent { get; } = Channel.CreateUnbounded<BridgeEnvelope>();
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort() { }

        public override void Dispose() { }

        public override Task CloseAsync(
            WebSocketCloseStatus status,
            string? description,
            CancellationToken token
        ) => Task.CompletedTask;

        public override Task CloseOutputAsync(
            WebSocketCloseStatus status,
            string? description,
            CancellationToken token
        ) => Task.CompletedTask;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken token
        ) => throw new NotSupportedException();

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType type,
            bool end,
            CancellationToken token
        )
        {
            Sent.Writer.TryWrite(BridgeEnvelopeCodec.Decode(buffer.AsMemory()));
            return Task.CompletedTask;
        }
    }
}
