using FigmaMcp.Server.Options;

if (!ServerOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls($"http://127.0.0.1:{options!.Port}");
builder.Services.AddSingleton(options);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapGet("/health", (ServerOptions settings, IHostApplicationLifetime lifetime) => Results.Json(new
{
    service = "figma-mcp-server",
    version = "0.1.0",
    bridge_protocol_version = 1,
    mcp_endpoint = "/mcp",
    bridge_endpoint = "/bridge",
    port = settings.Port,
    uptime_seconds = (long)(DateTimeOffset.UtcNow - ProcessStart.UtcNow).TotalSeconds,
    connected_plugins = 0
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
