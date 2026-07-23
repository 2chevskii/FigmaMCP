using ModelContextProtocol.Server;
using FigmaMcp.Server.Connections;
using System.ComponentModel;
using System.Text.Json;

namespace FigmaMcp.Server.Mcp;

[McpServerToolType]
public sealed class FigmaTools
{
    private readonly PluginConnectionRegistry _registry;
    public FigmaTools(PluginConnectionRegistry registry) => _registry = registry;

    [McpServerTool(Name = "list_figma_connections", Title = "List Figma connections")]
    [Description("Discover all currently connected Figma plugin invocations before document-specific work.")]
    public object ListFigmaConnections() => new
    {
        connections = _registry.Snapshot().Select(connection => new
        {
            connection_id = connection.ConnectionId.ToString("D"), plugin_version = connection.PluginVersion, protocol_version = connection.ProtocolVersion,
            document_name = connection.DocumentName, current_page = new { id = connection.CurrentPageId, name = connection.CurrentPageName },
            editor_type = connection.EditorType, mode = connection.Mode, connected_at = connection.ConnectedAt, last_seen_at = connection.LastSeenAt
        }).ToArray()
    };

    [McpServerTool(Name = "get_figma_document_metadata", Title = "Get Figma document metadata")]
    [Description("Read a fresh bounded metadata snapshot from one explicitly selected live Figma plugin.")]
    public async Task<object> GetFigmaDocumentMetadata([Description("The live Figma plugin connection UUID.")] string connection_id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(connection_id, "D", out var id)) return new { error = new { code = "connection_not_found", message = "No live Figma plugin connection exists for the supplied connection_id.", connection_id } };
        try
        {
            var payload = await _registry.RequestAsync(id, "get_document_metadata", cancellationToken);
            using var document = JsonDocument.Parse(MessagePack.MessagePackSerializer.ConvertToJson(payload, MessagePack.MessagePackSerializerOptions.Standard.WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)));
            return document.RootElement.Clone();
        }
        catch (BridgeRpcException exception)
        {
            return new { error = new { code = exception.Code, message = exception.Message, connection_id = id.ToString("D") } };
        }
    }
}
