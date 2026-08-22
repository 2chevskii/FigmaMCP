using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FigmaMCP.Mcp;

public sealed partial class FigmaTools
{
    [McpServerTool(
        Name = "get_figma_capabilities",
        Title = "Get Figma capabilities",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Report the Figma Design capabilities and limits available on one live connector."
    )]
    public Task<object> GetFigmaCapabilities(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_capabilities", null, cancellationToken);

    [McpServerTool(
        Name = "get_figma_document",
        Title = "Get Figma document",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Read document, page, selection, editor, and file-thumbnail context.")]
    public Task<object> GetFigmaDocument(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_document", null, cancellationToken);

    [McpServerTool(
        Name = "list_figma_pages",
        Title = "List Figma pages",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("List bounded page summaries. Input supports cursor and limit.")]
    public Task<object> ListFigmaPages(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with cursor and limit.")] JsonElement? input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "list_figma_pages", input, cancellationToken);

    [McpServerTool(
        Name = "load_figma_page",
        Title = "Load Figma page",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Explicitly load one page for dynamic-page document access.")]
    public Task<object> LoadFigmaPage(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing page_id.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "load_figma_page", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_selection",
        Title = "Get Figma selection",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Read the current selection and selected text range.")]
    public Task<object> GetFigmaSelection(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_selection", null, cancellationToken);

    [McpServerTool(
        Name = "set_figma_selection",
        Title = "Set Figma selection",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Select up to 100 explicit scene-node IDs and optionally focus the viewport.")]
    public Task<object> SetFigmaSelection(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids and optional focus.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "set_figma_selection", input, cancellationToken);

    [McpServerTool(
        Name = "set_figma_current_page",
        Title = "Set current Figma page",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Switch the editor to one explicit page ID.")]
    public Task<object> SetFigmaCurrentPage(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing page_id.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "set_figma_current_page", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_document_changes",
        Title = "Get Figma document changes",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Poll the bounded connector change journal by cursor.")]
    public Task<object> GetFigmaDocumentChanges(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with cursor and limit.")] JsonElement? input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_document_changes", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_nodes",
        Title = "Get Figma nodes",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Fetch up to 100 nodes by ID with an explicit field projection and bounded children."
    )]
    public Task<object> GetFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids, optional fields, and optional child_depth (0-4).")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "query_figma_nodes",
        Title = "Query Figma nodes",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Search a loaded page or subtree by type, name, visibility, or plugin-data key.")]
    public Task<object> QueryFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with optional root_id, node_types, name, name_contains, visible, "
                + "plugin_data_key, fields, and limit."
        )]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "query_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_node_css",
        Title = "Get Figma node CSS",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Return generated CSS for up to 100 explicit scene nodes.")]
    public Task<object> GetFigmaNodeCss(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing node_ids.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_node_css", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_node_geometry",
        Title = "Get Figma node geometry",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Read transforms, bounds, paints, vectors, corners, constraints, and layout geometry."
    )]
    public Task<object> GetFigmaNodeGeometry(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing node_ids.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_node_geometry", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_text",
        Title = "Get Figma text",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Read bounded text content and requested styled-segment fields.")]
    public Task<object> GetFigmaText(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids and optional start, end, and segment_fields.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_text", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_components",
        Title = "Get Figma components",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Read component, component-set, instance, slot, variant, and override metadata.")]
    public Task<object> GetFigmaComponents(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing node_ids.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_components", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_prototype",
        Title = "Get Figma prototype",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Read reactions, flows, overlays, and prototype settings for explicit nodes.")]
    public Task<object> GetFigmaPrototype(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing node_ids.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_prototype", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_plugin_data",
        Title = "Get Figma plugin data",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("Read this connector's private/shared plugin data for explicit nodes.")]
    public Task<object> GetFigmaPluginData(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids and optional keys and shared_namespaces.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_plugin_data", input, cancellationToken);

    [McpServerTool(
        Name = "get_figma_dev_metadata",
        Title = "Get Figma development metadata",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Read public annotations, measurements, dev status, and dev resources when available."
    )]
    public Task<object> GetFigmaDevMetadata(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing node_ids.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "get_figma_dev_metadata", input, cancellationToken);
}
