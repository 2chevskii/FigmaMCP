using FigmaMcp.Server.Mcp;
using ModelContextProtocol.Server;

namespace FigmaMcp.Server.Tests.Mcp;

public sealed class FigmaToolsRegistrationTests
{
    [Fact]
    public void DesignReadToolsAreRegistered()
    {
        var registeredNames = typeof(FigmaTools)
            .GetMethods()
            .Select(method => method
                .GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
                .Cast<McpServerToolAttribute>()
                .SingleOrDefault()?.Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        var expectedNames = new[]
        {
            "list_figma_connections",
            "get_figma_document_metadata",
            "get_figma_capabilities",
            "get_figma_document",
            "list_figma_pages",
            "load_figma_page",
            "get_figma_selection",
            "set_figma_selection",
            "set_figma_current_page",
            "get_figma_document_changes",
            "get_figma_nodes",
            "query_figma_nodes",
            "get_figma_node_css",
            "get_figma_node_geometry",
            "get_figma_text",
            "get_figma_components",
            "get_figma_prototype",
            "get_figma_plugin_data",
            "get_figma_dev_metadata",
        };

        Assert.All(expectedNames, name => Assert.Contains(name, registeredNames));
    }
}
