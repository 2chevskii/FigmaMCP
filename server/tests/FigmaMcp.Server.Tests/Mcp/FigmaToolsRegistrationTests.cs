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
            "create_figma_nodes",
            "update_figma_nodes",
            "clone_figma_nodes",
            "move_figma_nodes",
            "delete_figma_nodes",
            "resize_figma_nodes",
            "combine_figma_nodes",
            "set_figma_vector_network",
            "list_figma_fonts",
            "update_figma_text",
            "update_figma_text_path",
            "create_figma_component_instance",
            "update_figma_component",
            "update_figma_instance",
            "update_figma_slot",
            "list_figma_component_instances",
            "list_figma_styles",
            "create_figma_style",
            "update_figma_style",
            "delete_figma_style",
            "reorder_figma_styles",
            "list_figma_style_consumers",
            "list_figma_variables",
            "create_figma_variable_collection",
            "create_figma_variable",
            "update_figma_variable",
            "delete_figma_variable",
            "bind_figma_variable",
            "list_figma_team_library_assets",
            "create_figma_image",
            "get_figma_image",
            "create_figma_media",
            "list_figma_shaders",
            "load_figma_brushes",
            "export_figma_nodes",
            "get_figma_screenshot",
            "encode_figma_binary",
            "update_figma_prototype",
            "get_figma_viewport",
            "set_figma_viewport",
            "notify_figma_user",
            "commit_figma_undo",
            "save_figma_version",
            "get_figma_file_thumbnail_node",
            "set_figma_file_thumbnail_node",
            "set_figma_plugin_data",
            "list_figma_annotation_categories",
            "create_figma_annotation_category",
            "set_figma_annotations",
            "manage_figma_measurements",
            "manage_figma_dev_resources",
            "set_figma_dev_status",
            "list_figma_animation_styles",
            "get_figma_motion",
            "update_figma_motion",
        };

        Assert.All(expectedNames, name => Assert.Contains(name, registeredNames));
        Assert.Equal(expectedNames.Length, registeredNames.Count);
    }
}
