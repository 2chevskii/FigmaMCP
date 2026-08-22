using FigmaMCP.Bridge;
using FigmaMCP.Connections;
using FigmaMCP.Mcp;
using FigmaMCP.Options;

const string ProductName = "figma-mcp-server";
const string ProductVersion = "0.2.0";

if (!ServerOptions.TryParse(args, out var parsedOptions, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

var options = parsedOptions!;
var validHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    $"127.0.0.1:{options.Port}",
    $"localhost:{options.Port}",
};

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
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
    .WithStdioServerTransport()
    .WithTools<FigmaTools>();

var app = builder.Build();
app.UseWebSockets();
app.Use(async (context, next) =>
{
    var invalidHost = !validHosts.Contains(context.Request.Host.Value ?? string.Empty);
    if (invalidHost)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    await next(context);
});

app.Map("/bridge", BridgeEndpoint.HandleAsync);

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
