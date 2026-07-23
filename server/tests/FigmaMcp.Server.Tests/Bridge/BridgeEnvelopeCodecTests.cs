using FigmaMcp.Server.Bridge;

namespace FigmaMcp.Server.Tests.Bridge;

public sealed class BridgeEnvelopeCodecTests
{
    [Fact]
    public void EncodeAndDecodePreserveAllEnvelopeFields()
    {
        var connectionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var requestId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var sentAt = new DateTimeOffset(2026, 7, 23, 12, 30, 0, TimeSpan.Zero);
        var envelope = new BridgeEnvelope(
            "request",
            1,
            connectionId,
            requestId,
            "get_document_metadata",
            BridgeEnvelopeCodec.EmptyMap(),
            null,
            sentAt);

        var decoded = BridgeEnvelopeCodec.Decode(BridgeEnvelopeCodec.Encode(envelope));

        Assert.Equal(envelope.Type, decoded.Type);
        Assert.Equal(envelope.ProtocolVersion, decoded.ProtocolVersion);
        Assert.Equal(envelope.ConnectionId, decoded.ConnectionId);
        Assert.Equal(envelope.RequestId, decoded.RequestId);
        Assert.Equal(envelope.Method, decoded.Method);
        Assert.Equal(envelope.SentAt, decoded.SentAt);
        Assert.NotNull(decoded.Payload);
    }

    [Fact]
    public void EncodeAndDecodeSupportAnErrorEnvelopeWithoutPayload()
    {
        var envelope = new BridgeEnvelope(
            "error",
            1,
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            null,
            null,
            new BridgeError("figma_api_error", "Unable to read document metadata."),
            DateTimeOffset.UtcNow);

        var decoded = BridgeEnvelopeCodec.Decode(BridgeEnvelopeCodec.Encode(envelope));

        Assert.Equal(envelope.Error, decoded.Error);
        Assert.Null(decoded.Payload);
    }
}
