using System.Buffers;
using System.Globalization;
using MessagePack;

namespace FigmaMcp.Server.Bridge;

public sealed record BridgeEnvelope(
    string Type,
    int ProtocolVersion,
    Guid? ConnectionId,
    Guid? RequestId,
    string? Method,
    ReadOnlyMemory<byte>? Payload,
    BridgeError? Error,
    DateTimeOffset SentAt);

public sealed record BridgeError(string Code, string Message);

public sealed class BridgeProtocolException(string message) : Exception(message);

public static class BridgeEnvelopeCodec
{
    public const int MaxMessageBytes = BridgeProtocol.MaxMessageBytes;

    public static BridgeEnvelope Decode(ReadOnlyMemory<byte> source)
    {
        if (source.Length is 0 or > MaxMessageBytes)
        {
            throw new BridgeProtocolException("Bridge message exceeds the 1 MiB limit.");
        }

        var reader = new MessagePackReader(source);
        if (reader.NextMessagePackType != MessagePackType.Map)
        {
            throw new BridgeProtocolException("Bridge envelope must be a map.");
        }

        var count = reader.ReadMapHeader();
        if (count > 10_000)
        {
            throw new BridgeProtocolException("Bridge map has too many entries.");
        }

        string? type = null;
        int? protocol = null;
        Guid? connection = null;
        Guid? request = null;
        string? method = null;
        ReadOnlyMemory<byte>? payload = null;
        BridgeError? error = null;
        DateTimeOffset? sentAt = null;

        for (var i = 0; i < count; i++)
        {
            var key = ReadString(ref reader, "map key");
            switch (key)
            {
                case "type":
                    type = ReadString(ref reader, "type");
                    break;
                case "protocol_version":
                    protocol = reader.ReadInt32();
                    break;
                case "connection_id":
                    connection = ReadGuid(ref reader, "connection_id");
                    break;
                case "request_id":
                    request = ReadGuid(ref reader, "request_id");
                    break;
                case "method":
                    method = reader.TryReadNil() ? null : ReadString(ref reader, "method");
                    break;
                case "payload":
                    payload = reader.TryReadNil() ? null : ReadRaw(ref reader);
                    break;
                case "error":
                    error = reader.TryReadNil() ? null : ReadError(ref reader);
                    break;
                case "sent_at":
                    sentAt = ReadTimestamp(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(type) || protocol is null || sentAt is null)
        {
            throw new BridgeProtocolException("Bridge envelope is missing required fields.");
        }

        return new BridgeEnvelope(type, protocol.Value, connection, request, method, payload, error, sentAt.Value);
    }

    public static byte[] Encode(BridgeEnvelope envelope)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        var fields = 3
            + Count(envelope.ConnectionId)
            + Count(envelope.RequestId)
            + Count(envelope.Method)
            + Count(envelope.Payload)
            + Count(envelope.Error);

        writer.WriteMapHeader(fields);
        Write(ref writer, "type", envelope.Type);
        Write(ref writer, "protocol_version", envelope.ProtocolVersion);
        Write(
            ref writer,
            "sent_at",
            envelope.SentAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        if (envelope.ConnectionId is { } connection)
        {
            Write(ref writer, "connection_id", connection.ToString("D"));
        }

        if (envelope.RequestId is { } request)
        {
            Write(ref writer, "request_id", request.ToString("D"));
        }

        if (envelope.Method is { } method)
        {
            Write(ref writer, "method", method);
        }

        if (envelope.Payload is { } payload)
        {
            writer.Write("payload");
            writer.WriteRaw(payload.Span);
        }

        if (envelope.Error is { } error)
        {
            writer.Write("error");
            writer.WriteMapHeader(2);
            Write(ref writer, "code", error.Code);
            Write(ref writer, "message", error.Message);
        }

        writer.Flush();
        return buffer.WrittenMemory.ToArray();
    }

    public static ReadOnlyMemory<byte> EmptyMap()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteMapHeader(0);
        writer.Flush();
        return buffer.WrittenMemory;
    }

    private static ReadOnlyMemory<byte> ReadRaw(ref MessagePackReader reader)
    {
        var start = reader.Position;
        reader.Skip();
        return reader.Sequence.Slice(start, reader.Position).ToArray();
    }

    private static BridgeError ReadError(ref MessagePackReader reader)
    {
        if (reader.NextMessagePackType != MessagePackType.Map)
        {
            throw new BridgeProtocolException("error must be a map.");
        }

        var count = reader.ReadMapHeader();
        string? code = null;
        string? message = null;

        for (var i = 0; i < count; i++)
        {
            var key = ReadString(ref reader, "error key");
            switch (key)
            {
                case "code":
                    code = ReadString(ref reader, "error.code");
                    break;
                case "message":
                    message = ReadString(ref reader, "error.message");
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message)
            ? new BridgeError(code, message)
            : throw new BridgeProtocolException("error is missing code or message.");
    }

    private static Guid ReadGuid(ref MessagePackReader reader, string field)
    {
        var raw = ReadString(ref reader, field);
        return Guid.TryParseExact(raw, "D", out var guid)
            && string.Equals(raw, guid.ToString("D"), StringComparison.Ordinal)
            ? guid
            : throw new BridgeProtocolException($"{field} must be a lowercase UUID.");
    }

    private static string ReadString(ref MessagePackReader reader, string field)
    {
        var value = reader.ReadString();
        return !string.IsNullOrEmpty(value) && value.Length <= 256 * 1024
            ? value
            : throw new BridgeProtocolException($"{field} must be a non-empty string within the limit.");
    }

    private static DateTimeOffset ReadTimestamp(ref MessagePackReader reader)
    {
        var raw = ReadString(ref reader, "sent_at");
        if (!DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            throw new BridgeProtocolException("sent_at must be an ISO-8601 timestamp.");
        }

        return timestamp.ToUniversalTime();
    }

    private static int Count<T>(T? value) => value is null ? 0 : 1;

    private static void Write(ref MessagePackWriter writer, string key, string value)
    {
        writer.Write(key);
        writer.Write(value);
    }

    private static void Write(ref MessagePackWriter writer, string key, int value)
    {
        writer.Write(key);
        writer.Write(value);
    }
}
