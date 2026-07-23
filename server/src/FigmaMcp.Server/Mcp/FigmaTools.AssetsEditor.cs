using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FigmaMcp.Server.Mcp;

public sealed partial class FigmaTools
{
    [McpServerTool(Name = "create_figma_image", Title = "Create Figma image",
        Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Create a bounded image handle from base64 bytes or a public HTTP(S) URL.")]
    public Task<object> CreateFigmaImage(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with data_base64 or url and optional dry_run/idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "create_figma_image", input, cancellationToken);

    [McpServerTool(Name = "get_figma_image", Title = "Get Figma image",
        ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read image size, MIME type, and bounded base64 bytes by image hash.")]
    public Task<object> GetFigmaImage(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing hash.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "get_figma_image", input, cancellationToken);

    [McpServerTool(Name = "create_figma_media", Title = "Create Figma media",
        Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a bounded Figma Design video handle from base64 bytes.")]
    public Task<object> CreateFigmaMedia(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with kind=video and data_base64.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "create_figma_media", input, cancellationToken);

    [McpServerTool(Name = "list_figma_shaders", Title = "List or import Figma shaders",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List available shaders or materialize one by ID.")]
    public Task<object> ListFigmaShaders(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with import_id.")] JsonElement? input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "list_figma_shaders", input, cancellationToken);

    [McpServerTool(Name = "load_figma_brushes", Title = "Load Figma brushes",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Load the STRETCH or SCATTER brush family before applying brush strokes.")]
    public Task<object> LoadFigmaBrushes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with brush_type STRETCH or SCATTER.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "load_figma_brushes", input, cancellationToken);

    [McpServerTool(Name = "export_figma_nodes", Title = "Export Figma nodes",
        ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Export up to 20 nodes as bounded base64 binary or structured REST JSON.")]
    public Task<object> ExportFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids and optional Figma export settings.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "export_figma_nodes", input, cancellationToken);

    [McpServerTool(Name = "encode_figma_binary", Title = "Inspect Figma binary",
        ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Validate and optionally normalize a bounded base64 binary payload.")]
    public Task<object> EncodeFigmaBinary(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with data_base64 and optional operation.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "encode_figma_binary", input, cancellationToken);

    [McpServerTool(Name = "update_figma_prototype", Title = "Update Figma prototype",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Update reactions, flows, overflow, and overlay behavior on explicit nodes.")]
    public Task<object> UpdateFigmaPrototype(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with items containing node_id and prototype properties.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_prototype", input, cancellationToken);

    [McpServerTool(Name = "get_figma_viewport", Title = "Get Figma viewport",
        ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read viewport center, zoom, and bounds.")]
    public Task<object> GetFigmaViewport(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "get_figma_viewport", null, cancellationToken);

    [McpServerTool(Name = "set_figma_viewport", Title = "Set Figma viewport",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Set viewport center/zoom or scroll and zoom to explicit nodes.")]
    public Task<object> SetFigmaViewport(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with center/zoom or node_ids.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "set_figma_viewport", input, cancellationToken);

    [McpServerTool(Name = "notify_figma_user", Title = "Notify Figma user",
        Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Show a bounded notification in Figma for visible agent feedback.")]
    public Task<object> NotifyFigmaUser(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with message and optional timeout_ms and error.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "notify_figma_user", input, cancellationToken);

    [McpServerTool(Name = "commit_figma_undo", Title = "Control Figma undo",
        Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Commit an undo boundary or trigger one undo action.")]
    public Task<object> CommitFigmaUndo(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with operation commit or undo.")] JsonElement? input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "commit_figma_undo", input, cancellationToken);

    [McpServerTool(Name = "save_figma_version", Title = "Save Figma version",
        Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Save a named version-history checkpoint.")]
    public Task<object> SaveFigmaVersion(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with title and optional description/idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "save_figma_version", input, cancellationToken);

    [McpServerTool(Name = "get_figma_file_thumbnail_node",
        Title = "Get Figma file thumbnail node", ReadOnly = true, Destructive = false,
        OpenWorld = false)]
    [Description("Read the current file-thumbnail node.")]
    public Task<object> GetFigmaFileThumbnailNode(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "get_figma_file_thumbnail_node", null, cancellationToken);

    [McpServerTool(Name = "set_figma_file_thumbnail_node",
        Title = "Set Figma file thumbnail node", Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Set or clear the file-thumbnail frame/component/component-set/section.")]
    public Task<object> SetFigmaFileThumbnailNode(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with optional node_id; omit it to clear the thumbnail.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "set_figma_file_thumbnail_node", input, cancellationToken);
}
