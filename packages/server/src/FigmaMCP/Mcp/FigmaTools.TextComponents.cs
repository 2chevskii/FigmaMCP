using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FigmaMCP.Mcp;

public sealed partial class FigmaTools
{
    [McpServerTool(
        Name = "list_figma_fonts",
        Title = "List Figma fonts",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("List available fonts with optional family filtering and pagination.")]
    public Task<object> ListFigmaFonts(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with family, cursor, and limit.")] JsonElement? input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "list_figma_fonts", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_text",
        Title = "Update Figma text",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Replace, insert, delete, or format text after automatically loading required fonts.")]
    public Task<object> UpdateFigmaText(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with items containing node_id, operation, ranges, characters, font_names, "
            + "and range properties. Supports dry_run and idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_text", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_text_path",
        Title = "Update Figma text path",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Update text-path content, typography, and path alignment.")]
    public Task<object> UpdateFigmaTextPath(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object using the update_figma_text item schema for TEXT_PATH nodes.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_text_path", input, cancellationToken);

    [McpServerTool(
        Name = "create_figma_component_instance",
        Title = "Create Figma component or instance",
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Create an instance from a component or convert an existing scene node to a component.")]
    public Task<object> CreateFigmaComponentInstance(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with operation create_instance or component_from_node and its target ID. "
            + "Supports dry_run and idempotency_key.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "create_figma_component_instance", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_component",
        Title = "Update Figma component",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Update component metadata and add, edit, or delete component properties.")]
    public Task<object> UpdateFigmaComponent(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with component items and optional property_actions.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_component", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_instance",
        Title = "Update Figma instance",
        Destructive = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Swap components, set properties, clear overrides, expose, scale, or detach instances.")]
    public Task<object> UpdateFigmaInstance(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with instance items and explicit operations.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_instance", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_slot",
        Title = "Update Figma slot",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Create a component slot, reset a slot, or inspect slot limit violations.")]
    public Task<object> UpdateFigmaSlot(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with operation and component_id or slot_id.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "update_figma_slot", input, cancellationToken);

    [McpServerTool(
        Name = "list_figma_component_instances",
        Title = "List Figma component instances",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false)]
    [Description("List bounded local instance references for one component.")]
    public Task<object> ListFigmaComponentInstances(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with component_id and optional cursor and limit.")]
        JsonElement input,
        CancellationToken cancellationToken) =>
        InvokeAsync(connection_id, "list_figma_component_instances", input, cancellationToken);
}
