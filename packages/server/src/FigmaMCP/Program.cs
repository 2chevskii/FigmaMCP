using FigmaMCP;
using FigmaMCP.Bridge;
using FigmaMCP.Connections;
using FigmaMCP.Mcp;
using FigmaMCP.Options;

if (!ServerOptions.TryParse(args, out var parsedOptions, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}

var options = parsedOptions!;
var port = options.IsPortExplicit ? options.Port : FindAvailablePort(options.Port);
await using var app = BuildApplication(port);

try
{
    await app.RunAsync();
    return 0;
}
catch (IOException exception) when (IsAddressAlreadyInUse(exception))
{
    Console.Error.WriteLine($"Listener startup failure: {exception.Message}");
    return 3;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Fatal startup failure: {exception.Message}");
    return 1;
}

static int FindAvailablePort(int startPort)
{
    for (var port = startPort; port <= ushort.MaxValue; port++)
    {
        if (!IsPortAvailable(port))
        {
            continue;
        }

        if (port != startPort)
        {
            Console.Error.WriteLine($"Bridge port {startPort} is unavailable; using {port}.");
        }

        return port;
    }

    throw new IOException(
        $"No loopback bridge ports are available from {startPort} through 65535."
    );
}

static bool IsPortAvailable(int port)
{
    using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);

    try
    {
        listener.Start();
        return true;
    }
    catch (System.Net.Sockets.SocketException)
    {
        return false;
    }
}

static bool IsAddressAlreadyInUse(IOException exception)
{
    for (Exception? current = exception; current is not null; current = current.InnerException)
    {
        if (
            current is System.Net.Sockets.SocketException
            {
                SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse
            }
        )
        {
            return true;
        }
    }

    return false;
}

WebApplication BuildApplication(int bridgePort)
{
    var validHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        $"127.0.0.1:{bridgePort}",
        $"localhost:{bridgePort}",
    };

    var builder = WebApplication.CreateSlimBuilder();
    builder.WebHost.UseUrls($"http://127.0.0.1:{bridgePort}");
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddSingleton<PluginConnectionRegistry>();
    builder
        .Services.AddMcpServer(server =>
        {
            server.ServerInfo = new() { Name = ProductInfo.Name, Version = ProductInfo.Version };
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
    app.Use(
        async (context, next) =>
        {
            var invalidHost = !validHosts.Contains(context.Request.Host.Value ?? string.Empty);
            if (invalidHost)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await next(context);
        }
    );

    app.Map("/bridge", BridgeEndpoint.HandleAsync);
    return app;
}
