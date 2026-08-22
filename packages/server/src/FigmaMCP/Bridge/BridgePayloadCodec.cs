using System.Text.Json;
using MessagePack;

namespace FigmaMCP.Bridge;

public static class BridgePayloadCodec
{
    public static ReadOnlyMemory<byte> Encode(JsonElement? payload)
    {
        if (
            payload is null
            || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        )
        {
            return BridgeEnvelopeCodec.EmptyMap();
        }

        return MessagePackSerializer.ConvertFromJson(
            payload.Value.GetRawText(),
            BridgeProtocol.SerializerOptions
        );
    }

    public static JsonElement Decode(ReadOnlyMemory<byte> payload)
    {
        var json = MessagePackSerializer.ConvertToJson(payload, BridgeProtocol.SerializerOptions);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
