using System.Buffers.Binary;
using System.IO.Pipes;
using FigmaMCP.Bridge;
using MessagePack;

namespace FigmaMCP.Companion;

[MessagePackObject]
public sealed record IpcMessage
{
    [Key(0)]
    public string Type { get; set; } = "";

    [Key(1)]
    public Guid Id { get; init; }

    [Key(2)]
    public string? Method { get; init; }

    [Key(3)]
    public Guid ConnectionId { get; init; }

    [Key(4)]
    public byte[]? Payload { get; init; }

    [Key(5)]
    public string? Code { get; init; }

    [Key(6)]
    public string? Message { get; init; }

    [Key(7)]
    public int Protocol { get; set; } = 1;

    [Key(8)]
    public string? Version { get; init; }

    [Key(9)]
    public int Port { get; init; }

    [Key(10)]
    public int IdleSeconds { get; init; }
}

public sealed class IpcChannel(Stream stream) : IAsyncDisposable
{
    public const int MaxFrameBytes = BridgeProtocol.MaxMessageBytes + 64 * 1024;
    private readonly SemaphoreSlim _writer = new(1);
    private readonly CancellationTokenSource _closed = new();

    public async Task WriteAsync(IpcMessage message, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _closed.Token
        );
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await _writer.WaitAsync(timeout.Token);
        try
        {
            var bytes = MessagePackSerializer.Serialize(message, BridgeProtocol.SerializerOptions);
            if (bytes.Length > MaxFrameBytes)
                throw new IOException("IPC message exceeds the size limit.");
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
            await stream.WriteAsync(header, timeout.Token);
            await stream.WriteAsync(bytes, timeout.Token);
            await stream.FlushAsync(timeout.Token);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task<IpcMessage> ReadAsync(
        CancellationToken cancellationToken,
        int maximum = MaxFrameBytes
    )
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > maximum)
            throw new IOException("Invalid IPC frame length.");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return MessagePackSerializer.Deserialize<IpcMessage>(
                bytes,
                BridgeProtocol.SerializerOptions
            ) ?? throw new IOException("Null IPC message.");
    }

    public static NamedPipeServerStream Listen(string name) =>
        new(
            name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );

    public static async Task<IpcChannel> ConnectAsync(
        string name,
        CancellationToken cancellationToken
    )
    {
        var pipe = new NamedPipeClientStream(
            ".",
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );
        try
        {
            await pipe.ConnectAsync(cancellationToken);
            return new(pipe);
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _closed.Cancel();
        stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
