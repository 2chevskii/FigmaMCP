using System.ComponentModel;
using System.Text.Json;
using FigmaMcp.Server.Bridge;
using FigmaMcp.Server.Connections;
using MessagePack;
using ModelContextProtocol.Server;

namespace FigmaMcp.Server.Mcp;

[McpServerToolType]
public sealed partial class FigmaTools
{
    private const string ConnectionNotFoundMessage =
        "No live Figma plugin connection exists for the supplied connection_id.";

    private readonly PluginConnectionRegistry _registry;

    public FigmaTools(PluginConnectionRegistry registry) => _registry = registry;

    [McpServerTool(Name = "list_figma_connections", Title = "List Figma connections")]
    [Description("Discover all currently connected Figma plugin invocations before document-specific work.")]
    public object ListFigmaConnections() => new
    {
        connections = _registry.Snapshot().Select(connection => new
        {
            connection_id = connection.ConnectionId.ToString("D"),
            plugin_version = connection.PluginVersion,
            protocol_version = connection.ProtocolVersion,
            document_name = connection.DocumentName,
            current_page = new
            {
                id = connection.CurrentPageId,
                name = connection.CurrentPageName,
            },
            editor_type = connection.EditorType,
            mode = connection.Mode,
            connected_at = connection.ConnectedAt,
            last_seen_at = connection.LastSeenAt,
        }).ToArray(),
    };

    [McpServerTool(Name = "get_figma_document_metadata", Title = "Get Figma document metadata")]
    [Description("Read a fresh bounded metadata snapshot from one explicitly selected live Figma plugin.")]
    public async Task<object> GetFigmaDocumentMetadata(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        CancellationToken cancellationToken)
    {
        return await InvokeAsync(
            connection_id,
            "get_document_metadata",
            null,
            cancellationToken);
    }

    private async Task<object> InvokeAsync(
        string connectionId,
        string method,
        JsonElement? input,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(connectionId, "D", out var id))
        {
            return Error("connection_not_found", ConnectionNotFoundMessage, connectionId);
        }

        try
        {
            var payload = await _registry.RequestAsync(
                id,
                method,
                BridgePayloadCodec.Encode(input),
                cancellationToken);
            return BridgePayloadCodec.Decode(payload);
        }
        catch (BridgeRpcException exception)
        {
            return Error(exception.Code, exception.Message, id.ToString("D"));
        }
    }

    private static object Error(string code, string message, string connectionId)
    {
        return new
        {
            error = new
            {
                code,
                message,
                connection_id = connectionId,
            },
        };
    }
}
