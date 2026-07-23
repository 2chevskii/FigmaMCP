using System.Text.Json;
using MessagePack;

namespace FigmaMcp.Server.Bridge;

internal static class BridgeProtocol
{
    public const int Version = 1;
    public const int MaxMessageBytes = 1024 * 1024;
    public const string Subprotocol = "figma-mcp-bridge.v1";

    public static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static readonly MessagePackSerializerOptions SerializerOptions =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    public static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

internal sealed record PagePayload(string Id, string Name);

internal sealed record HelloPayload(
    string PluginVersion,
    string EditorType,
    string Mode,
    string DocumentName,
    PagePayload CurrentPage);

internal sealed record ContextPayload(
    string EditorType,
    string Mode,
    string DocumentName,
    PagePayload CurrentPage);
