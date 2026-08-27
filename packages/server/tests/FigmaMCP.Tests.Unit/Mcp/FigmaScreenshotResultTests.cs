using System.Text.Json;
using FigmaMCP.Mcp;
using ModelContextProtocol.Protocol;

namespace FigmaMCP.Tests.Unit.Mcp;

public sealed class FigmaScreenshotResultTests
{
    [Fact]
    public void CreateScreenshotResultReturnsInlineImageContent()
    {
        var pngBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        using var response = JsonDocument.Parse(
            $$"""
            {
              "exports": [
                {
                  "node_id": "1:2",
                  "byte_length": {{pngBytes.Length}},
                  "data_base64": "{{Convert.ToBase64String(pngBytes)}}"
                }
              ]
            }
            """
        );

        var result = FigmaTools.CreateScreenshotResult(response.RootElement, "1:2");

        Assert.NotEqual(true, result.IsError);
        Assert.Collection(
            result.Content,
            content => Assert.IsType<TextContentBlock>(content),
            content =>
            {
                var image = Assert.IsType<ImageContentBlock>(content);
                Assert.Equal("image/png", image.MimeType);
                Assert.Equal(pngBytes, image.DecodedData.ToArray());
            }
        );
        Assert.Equal("1:2", result.StructuredContent!.Value.GetProperty("node_id").GetString());
        Assert.Equal(
            pngBytes.Length,
            result.StructuredContent.Value.GetProperty("byte_length").GetInt32()
        );
    }

    [Fact]
    public void CreateScreenshotResultRejectsNonBinaryExports()
    {
        using var response = JsonDocument.Parse(
            """
            {
              "exports": [
                {
                  "node_id": "1:2",
                  "data": {}
                }
              ]
            }
            """
        );

        var exception = Assert.Throws<FormatException>(() =>
            FigmaTools.CreateScreenshotResult(response.RootElement, "1:2")
        );

        Assert.Contains("PNG bytes", exception.Message, StringComparison.Ordinal);
    }
}
