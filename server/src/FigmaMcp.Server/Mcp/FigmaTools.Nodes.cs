using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FigmaMcp.Server.Mcp;

public sealed partial class FigmaTools
{
    [McpServerTool(
        Name = "create_figma_nodes",
        Title = "Create Figma nodes",
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Create up to 100 Figma Design nodes with typed constructors and property patches.")]
    public Task<object> CreateFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with nodes. Each node has kind, optional parent_id, width, height, characters, "
            + "and properties. Supports dry_run and idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "create_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_nodes",
        Title = "Update Figma nodes",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Apply allowlisted geometry, appearance, layout, export, and prototype patches.")]
    public Task<object> UpdateFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with updates containing node_id and properties. "
            + "Supports dry_run and idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "clone_figma_nodes",
        Title = "Clone Figma nodes",
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Clone up to 100 explicit scene nodes and return source-to-clone mappings.")]
    public Task<object> CloneFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids, optional dry_run, and optional idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "clone_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "move_figma_nodes",
        Title = "Move Figma nodes",
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Reparent or reorder nodes with explicit parent IDs and optional child indices.")]
    public Task<object> MoveFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with moves containing node_id, parent_id, and optional index.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "move_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "delete_figma_nodes",
        Title = "Delete Figma nodes",
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Delete up to 100 explicit nodes. Use dry_run to preview the target summaries.")]
    public Task<object> DeleteFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_ids, optional dry_run, and optional idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "delete_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "resize_figma_nodes",
        Title = "Resize Figma nodes",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Resize, resize without constraints, or rescale explicit scene nodes.")]
    public Task<object> ResizeFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with items containing node_id, mode, dimensions or scale, and "
            + "optional lock_aspect_ratio.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "resize_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "combine_figma_nodes",
        Title = "Combine Figma nodes",
        Destructive = true,
        Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Group, transform-group, flatten, ungroup, combine as variants, or apply boolean operations.")]
    public Task<object> CombineFigmaNodes(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with operation, node_ids, optional parent_id/index, dry_run, "
            + "and idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "combine_figma_nodes", input, cancellationToken);

    [McpServerTool(
        Name = "set_figma_vector_network",
        Title = "Set Figma vector network",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Replace a vector node's vector network and/or vector paths.")]
    public Task<object> SetFigmaVectorNetwork(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with node_id and vector_network and/or vector_paths. "
            + "Supports dry_run and idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "set_figma_vector_network", input, cancellationToken);
}
