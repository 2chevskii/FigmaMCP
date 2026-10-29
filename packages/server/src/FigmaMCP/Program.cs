using FigmaMCP;
using FigmaMCP.Bridge;
using FigmaMCP.Companion;
using FigmaMCP.Connections;
using FigmaMCP.Diagnostics;
using FigmaMCP.Mcp;
using FigmaMCP.Options;

if (!ServerOptions.TryParse(args, out var parsedOptions, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}
var options = parsedOptions!;
try
{
    if (options.Backend)
        await RunBackendAsync(options);
    else
    {
        await using var backend = new RemoteBackend(options);
        await backend.StartAsync(CancellationToken.None);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton<ICompanionBackend>(backend);
        builder.Services.AddSingleton<IServerInfoSource>(backend);
        builder
            .Services.AddMcpServer(server =>
            {
                server.ServerInfo = new()
                {
                    Name = ProductInfo.Name,
                    Version = ProductInfo.Version,
                };
                server.ServerInstructions =
                    "Call get_server_info for the shared backend's Bridge address. Call list_figma_connections before document work and always pass the chosen live connection_id. Backend connection loss never automatically replays a document operation; inspect state before retrying uncertain mutations.";
            })
            .WithStdioServerTransport()
            .WithTools<FigmaTools>()
            .WithTools<ServerTools>();
        using var app = builder.Build();
        await app.RunAsync();
    }
    return 0;
}
catch (Exception exception)
{
    var code = exception is BridgeRpcException rpc ? rpc.Code : "backend_unavailable";
    Console.Error.WriteLine($"{code}: {exception.Message}");
    if (options.Backend)
        await ReportAsync(
            options,
            new()
            {
                Type = "error",
                Code = code,
                Message = exception.Message,
            }
        );
    return code == "bridge_port_in_use" ? 3 : 1;
}
static async Task ReportAsync(ServerOptions options, IpcMessage message)
{
    if (options.BootstrapPipe is null)
        return;
    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var channel = await IpcChannel.ConnectAsync(
            options.BootstrapPipe,
            timeout.Token
        );
        await channel.WriteAsync(message, timeout.Token);
    }
    catch (Exception e) when (e is IOException or OperationCanceledException) { }
}
static async Task RunBackendAsync(ServerOptions options)
{
    BackendLauncher.DetachUnix();
    using var ownership = await LifetimeLock.AcquireAsync(
        InstanceIdentity.Name(options.Port) + "-lifetime"
    );
    var activity = new BackendActivity(options.IdleTimeoutSeconds);
    using var startupActivity = activity.Enter();
    using var logs = new BackendLogProvider();
    var builder = WebApplication.CreateSlimBuilder();
    builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(logs);
    builder.Services.AddSingleton<PluginConnectionRegistry>();
    builder.Services.AddSingleton<ServerInfoProvider>();
    await using var app = builder.Build();
    app.UseWebSockets();
    app.Use(
        async (context, next) =>
        {
            if (
                context.Request.Host.Value != $"127.0.0.1:{options.Port}"
                && !string.Equals(
                    context.Request.Host.Value,
                    $"localhost:{options.Port}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                context.Response.StatusCode = 400;
                return;
            }
            IDisposable lease;
            try
            {
                lease = activity.Enter();
            }
            catch (IOException)
            {
                context.Response.StatusCode = 503;
                return;
            }
            using (lease)
                await next(context);
        }
    );
    app.Map("/bridge", BridgeEndpoint.HandleAsync);
    try
    {
        await app.StartAsync();
    }
    catch (IOException e)
    {
        throw new BridgeRpcException(
            "bridge_port_in_use",
            $"Unable to bind fixed Bridge port {options.Port}: {e.Message}"
        );
    }
    using var stop = new CancellationTokenSource();
    var server = new BackendServer(
        options,
        app.Services.GetRequiredService<PluginConnectionRegistry>(),
        app.Services.GetRequiredService<ServerInfoProvider>(),
        activity,
        logs
    );
    var serving = server.RunAsync(stop.Token);
    if (serving.IsCompleted)
        await serving;
    await ReportAsync(options, new() { Type = "ready" });
    startupActivity.Dispose();
    try
    {
        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested && !activity.TryStop())
        {
            if (serving.IsCompleted)
            {
                await serving;
                break;
            }
            await Task.Delay(100);
        }
    }
    finally
    {
        await stop.CancelAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await app.StopAsync(timeout.Token);
        await serving;
    }
}
