using FigmaMcp.Server.Bridge;
using FigmaMcp.Server.Connections;
using FigmaMcp.Server.Mcp;
using FigmaMcp.Server.Options;

const string ProductName = "figma-mcp-server";
const string ProductVersion = "0.2.0";

if (!ServerOptions.TryParse(args, out var parsedOptions, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

var options = parsedOptions!;
var startedAt = DateTimeOffset.UtcNow;
var validHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    $"127.0.0.1:{options.Port}",
    $"localhost:{options.Port}",
};

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
builder.Services.AddSingleton<PluginConnectionRegistry>();
builder.Services
    .AddMcpServer(server =>
    {
        server.ServerInfo = new() { Name = ProductName, Version = ProductVersion };
        server.ServerInstructions =
            "Call list_figma_connections before document-specific work. "
            + "Always pass the chosen connection_id. Connection IDs identify live plugin "
            + "invocations, not permanent Figma files. A missing connection means the plugin "
            + "must be opened or reconnected.";
    })
    .WithHttpTransport(transport => transport.Stateless = true)
    .WithTools<FigmaTools>();

var app = builder.Build();
app.UseWebSockets();
app.Use(async (context, next) =>
{
    var invalidHost = !validHosts.Contains(context.Request.Host.Value ?? string.Empty);
    var browserRequestToMcp = context.Request.Path.StartsWithSegments("/mcp")
        && context.Request.Headers.ContainsKey("Origin");

    if (invalidHost || browserRequestToMcp)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    await next(context);
});

app.MapMcp("/mcp");
app.Map("/bridge", BridgeEndpoint.HandleAsync);
app.MapGet("/health", (PluginConnectionRegistry registry) => Results.Json(new
{
    service = ProductName,
    version = ProductVersion,
    bridge_protocol_version = BridgeProtocol.Version,
    mcp_endpoint = "/mcp",
    bridge_endpoint = "/bridge",
    port = options.Port,
    uptime_seconds = (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    connected_plugins = registry.Count,
}));

Console.WriteLine($"{ProductName} {ProductVersion}");
Console.WriteLine($"MCP URL: http://127.0.0.1:{options.Port}/mcp");
Console.WriteLine($"Plugin bridge URL: ws://127.0.0.1:{options.Port}/bridge");
Console.WriteLine($"Health URL: http://127.0.0.1:{options.Port}/health");
Console.WriteLine("Keep this process running while using Figma MCP.");

try
{
    await app.RunAsync();
    return 0;
}
catch (IOException exception) when (exception.InnerException is System.Net.Sockets.SocketException)
{
    Console.Error.WriteLine($"Listener startup failure: {exception.Message}");
    return 3;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Fatal startup failure: {exception.Message}");
    return 1;
}
