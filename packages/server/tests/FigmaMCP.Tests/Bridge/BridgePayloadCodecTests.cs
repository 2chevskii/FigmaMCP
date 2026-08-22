using System.Text.Json;
using FigmaMCP.Bridge;

namespace FigmaMCP.Tests.Bridge;

public sealed class BridgePayloadCodecTests
{
    [Fact]
    public void EncodeAndDecodePreserveStructuredJson()
    {
        using var input = JsonDocument.Parse(
            """
            {
              "node_ids": ["1:2", "1:3"],
              "limit": 25,
              "options": { "include_children": true }
            }
            """
        );

        var decoded = BridgePayloadCodec.Decode(BridgePayloadCodec.Encode(input.RootElement));

        Assert.Equal("1:2", decoded.GetProperty("node_ids")[0].GetString());
        Assert.Equal(25, decoded.GetProperty("limit").GetInt32());
        Assert.True(decoded.GetProperty("options").GetProperty("include_children").GetBoolean());
    }

    [Fact]
    public void EncodeNullProducesAnEmptyMap()
    {
        var decoded = BridgePayloadCodec.Decode(BridgePayloadCodec.Encode(null));

        Assert.Equal(JsonValueKind.Object, decoded.ValueKind);
        Assert.Empty(decoded.EnumerateObject());
    }
}
