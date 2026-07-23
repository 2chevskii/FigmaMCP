using FigmaMcp.Server.Options;
using FigmaMcp.Server.Mcp;
using FigmaMcp.Server.Bridge;
using FigmaMcp.Server.Connections;

if (!ServerOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls($"http://127.0.0.1:{options!.Port}");
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<PluginConnectionRegistry>();
builder.Services.AddMcpServer(server =>
    {
        server.ServerInfo = new() { Name = "figma-mcp-server", Version = "0.1.0" };
        server.ServerInstructions = "Call list_figma_connections before document-specific work. Always pass the chosen connection_id. Connection IDs identify live plugin invocations, not permanent Figma files. A missing connection means the plugin must be opened or reconnected.";
    })
    .WithHttpTransport(transport => transport.Stateless = true)
    .WithTools<FigmaTools>();

var app = builder.Build();
app.UseWebSockets();
app.Use(async (context, next) =>
{
    var validHosts = new[] { $"127.0.0.1:{options.Port}", $"localhost:{options.Port}" };
    if (!validHosts.Contains(context.Request.Host.Value, StringComparer.OrdinalIgnoreCase) || (context.Request.Path.StartsWithSegments("/mcp") && context.Request.Headers.ContainsKey("Origin"))) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
    await next(context);
});
app.MapMcp("/mcp");
app.Map("/bridge", BridgeEndpoint.HandleAsync);
app.MapGet("/health", (PluginConnectionRegistry registry) => Results.Json(new
{
    service = "figma-mcp-server",
    version = "0.1.0",
    bridge_protocol_version = 1,
    mcp_endpoint = "/mcp",
    bridge_endpoint = "/bridge",
    port = options.Port,
    uptime_seconds = (long)(DateTimeOffset.UtcNow - ProcessStart.UtcNow).TotalSeconds,
    connected_plugins = registry.Count
}));

Console.WriteLine("figma-mcp-server 0.1.0");
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

file static class ProcessStart
{
    public static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;
}
