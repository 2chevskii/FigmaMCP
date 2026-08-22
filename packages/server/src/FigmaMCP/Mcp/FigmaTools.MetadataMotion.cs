using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FigmaMCP.Mcp;

public sealed partial class FigmaTools
{
    [McpServerTool(Name = "set_figma_plugin_data", Title = "Set Figma plugin data",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Set/delete private/shared plugin data and relaunch data on explicit nodes.")]
    public Task<object> SetFigmaPluginData(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with items containing node_id and private/shared entries.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "set_figma_plugin_data", input, cancellationToken);

    [McpServerTool(Name = "list_figma_annotation_categories",
        Title = "List Figma annotation categories", ReadOnly = true, Destructive = false,
        OpenWorld = false)]
    [Description("List annotation categories or fetch one category by ID.")]
    public Task<object> ListFigmaAnnotationCategories(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with category_id.")] JsonElement? input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "list_figma_annotation_categories", input, cancellationToken);

    [McpServerTool(Name = "create_figma_annotation_category",
        Title = "Create Figma annotation category", Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Create an annotation category with a label and supported color.")]
    public Task<object> CreateFigmaAnnotationCategory(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with label, color, and optional dry_run/idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "create_figma_annotation_category", input, cancellationToken);

    [McpServerTool(Name = "set_figma_annotations", Title = "Set Figma annotations",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Replace annotations on explicit supported scene nodes.")]
    public Task<object> SetFigmaAnnotations(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with items containing node_id and annotations.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "set_figma_annotations", input, cancellationToken);

    [McpServerTool(Name = "manage_figma_measurements", Title = "Manage Figma measurements",
        Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "List Design measurements; add/edit/delete operations report that Dev Mode is required.")]
    public Task<object> ManageFigmaMeasurements(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with list, add, edit, or delete operation and required fields.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "manage_figma_measurements", input, cancellationToken);

    [McpServerTool(Name = "manage_figma_dev_resources", Title = "Manage Figma dev resources",
        Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("List, add, edit, or delete public development-resource links on a node.")]
    public Task<object> ManageFigmaDevResources(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_id, operation, and resource URL/name fields.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "manage_figma_dev_resources", input, cancellationToken);

    [McpServerTool(Name = "set_figma_dev_status", Title = "Set Figma dev status",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Read or set supported development status metadata on a scene node.")]
    public Task<object> SetFigmaDevStatus(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with node_id and optional status.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "set_figma_dev_status", input, cancellationToken);

    [McpServerTool(Name = "list_figma_animation_styles",
        Title = "List Figma Motion styles", ReadOnly = true, Destructive = false,
        OpenWorld = false)]
    [Description("List beta Motion animation styles and optionally normalize spring parameters.")]
    public Task<object> ListFigmaAnimationStyles(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with physical_spring.")] JsonElement? input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "list_figma_animation_styles", input, cancellationToken);

    [McpServerTool(Name = "get_figma_motion", Title = "Get Figma Motion",
        ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read beta animation styles, keyframes, manual tracks, and timelines.")]
    public Task<object> GetFigmaMotion(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object containing node_ids.")] JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "get_figma_motion", input, cancellationToken);

    [McpServerTool(Name = "update_figma_motion", Title = "Update Figma Motion",
        Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Apply/remove beta Motion styles or tracks and set timeline duration.")]
    public Task<object> UpdateFigmaMotion(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with bounded items and explicit Motion operations.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_motion", input, cancellationToken);
}
