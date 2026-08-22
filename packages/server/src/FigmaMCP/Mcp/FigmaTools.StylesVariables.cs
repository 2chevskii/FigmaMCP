using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FigmaMCP.Mcp;

public sealed partial class FigmaTools
{
    [McpServerTool(
        Name = "list_figma_styles",
        Title = "List Figma styles",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("List local paint, text, effect, and grid styles with pagination.")]
    public Task<object> ListFigmaStyles(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with kinds, cursor, and limit.")] JsonElement? input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "list_figma_styles", input, cancellationToken);

    [McpServerTool(
        Name = "create_figma_style",
        Title = "Create Figma style",
        Destructive = false,
        Idempotent = false,
        OpenWorld = false
    )]
    [Description("Create a local paint, text, effect, or grid style.")]
    public Task<object> CreateFigmaStyle(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with kind, name, style value, and optional dry_run/idempotency_key.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "create_figma_style", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_style",
        Title = "Update Figma style",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Rename or patch a local style's type-specific value.")]
    public Task<object> UpdateFigmaStyle(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with style_id and fields to patch.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "update_figma_style", input, cancellationToken);

    [McpServerTool(
        Name = "delete_figma_style",
        Title = "Delete Figma style",
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Remove up to 100 local styles.")]
    public Task<object> DeleteFigmaStyle(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with style_ids and optional dry_run/idempotency_key.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "delete_figma_style", input, cancellationToken);

    [McpServerTool(
        Name = "reorder_figma_styles",
        Title = "Reorder Figma styles",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Move a style or style folder after an explicit reference.")]
    public Task<object> ReorderFigmaStyles(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with kind, operation, and target/reference IDs or folder paths.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "reorder_figma_styles", input, cancellationToken);

    [McpServerTool(
        Name = "list_figma_style_consumers",
        Title = "List Figma style consumers",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("List bounded consumers for one style.")]
    public Task<object> ListFigmaStyleConsumers(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with style_id and optional cursor and limit.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "list_figma_style_consumers", input, cancellationToken);

    [McpServerTool(
        Name = "list_figma_variables",
        Title = "List Figma variables",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description("List local variables and collections with optional resolved-type filtering.")]
    public Task<object> ListFigmaVariables(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Optional object with resolved_type, cursor, and limit.")] JsonElement? input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "list_figma_variables", input, cancellationToken);

    [McpServerTool(
        Name = "create_figma_variable_collection",
        Title = "Create Figma variable collection",
        Destructive = false,
        Idempotent = false,
        OpenWorld = false
    )]
    [Description("Create or extend a variable collection and manage its modes.")]
    public Task<object> CreateFigmaVariableCollection(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with name, optional extend_collection_key, and mode_actions.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "create_figma_variable_collection", input, cancellationToken);

    [McpServerTool(
        Name = "create_figma_variable",
        Title = "Create Figma variable",
        Destructive = false,
        Idempotent = false,
        OpenWorld = false
    )]
    [Description("Create a variable with values, aliases, scopes, and code syntax.")]
    public Task<object> CreateFigmaVariable(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description(
            "Object with collection_id, name, resolved_type, and optional variable fields."
        )]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "create_figma_variable", input, cancellationToken);

    [McpServerTool(
        Name = "update_figma_variable",
        Title = "Update Figma variable",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Update variable values, aliases, scopes, publishing state, and code syntax.")]
    public Task<object> UpdateFigmaVariable(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with variable_id and fields to patch.")] JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "update_figma_variable", input, cancellationToken);

    [McpServerTool(
        Name = "delete_figma_variable",
        Title = "Delete Figma variable",
        Destructive = true,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description("Delete explicit variables and/or collections.")]
    public Task<object> DeleteFigmaVariable(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with variable_ids and/or collection_ids plus optional dry_run.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "delete_figma_variable", input, cancellationToken);

    [McpServerTool(
        Name = "bind_figma_variable",
        Title = "Bind Figma variable",
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "Bind or unbind variables on nodes, text ranges, paints, effects, and layout grids."
    )]
    public Task<object> BindFigmaVariable(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with bounded bindings and optional dry_run/idempotency_key.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "bind_figma_variable", input, cancellationToken);

    [McpServerTool(
        Name = "list_figma_team_library_assets",
        Title = "List or import Figma library assets",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    )]
    [Description(
        "List enabled library variables or import a component, set, style, or variable by key."
    )]
    public Task<object> ListFigmaTeamLibraryAssets(
        [Description("The live Figma plugin connection UUID.")] string connection_id,
        [Description("Object with a list/import operation and its collection_key or asset key.")]
            JsonElement input,
        CancellationToken cancellationToken
    ) => InvokeAsync(connection_id, "list_figma_team_library_assets", input, cancellationToken);
}
