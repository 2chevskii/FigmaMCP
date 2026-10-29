using System.ComponentModel;
using FigmaMCP.Diagnostics;
using ModelContextProtocol.Server;

namespace FigmaMCP.Mcp;

[McpServerToolType]
public sealed class ServerTools(FigmaMCP.Companion.IServerInfoSource serverInfo)
{
    [McpServerTool(
        Name = "get_server_info",
        Title = "Get server information",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(
        "Read this server instance's identity, version, startup time, active plugin connection "
            + "count, and actual Bridge host, port, and WebSocket URL. Available without a "
            + "connection_id, even when no plugin is connected. Use the returned port in Figma Bridge."
    )]
    public Task<ServerInfo> GetServerInfo(CancellationToken cancellationToken) =>
        serverInfo.GetAsync(cancellationToken);
}
