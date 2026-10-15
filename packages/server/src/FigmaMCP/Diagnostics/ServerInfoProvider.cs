using System.Diagnostics;
using System.Text.Json.Serialization;
using FigmaMCP.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace FigmaMCP.Diagnostics;

public sealed class ServerInfoProvider(
    IServer server,
    IHostApplicationLifetime lifetime,
    PluginConnectionRegistry registry
)
{
    private readonly string _instanceId = Guid.NewGuid().ToString("D");
    private readonly DateTimeOffset _startedAt = GetProcessStartTime();

    public async Task<ServerInfo> GetAsync(CancellationToken cancellationToken)
    {
        // STDIO can receive calls before Kestrel has finished binding its listener.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ready = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using var stopping = lifetime.ApplicationStopping.Register(() =>
            started.TrySetCanceled(lifetime.ApplicationStopping)
        );
        await started.Task.WaitAsync(cancellationToken);

        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var listener = new Uri(address);
        var websocket = new UriBuilder(listener) { Scheme = "ws", Path = "/bridge" };

        return new ServerInfo(
            _instanceId,
            Environment.ProcessId,
            ProductInfo.Version,
            _startedAt,
            registry.Count,
            new BridgeListenerInfo(listener.Host, listener.Port, websocket.Uri.AbsoluteUri)
        );
    }

    private static DateTimeOffset GetProcessStartTime()
    {
        using var process = Process.GetCurrentProcess();
        return new DateTimeOffset(process.StartTime.ToUniversalTime());
    }
}

public sealed record ServerInfo(
    [property: JsonPropertyName("server_instance_id")] string ServerInstanceId,
    [property: JsonPropertyName("process_id")] int ProcessId,
    [property: JsonPropertyName("product_version")] string ProductVersion,
    [property: JsonPropertyName("started_at")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("active_plugin_connections")] int ActivePluginConnections,
    [property: JsonPropertyName("bridge")] BridgeListenerInfo Bridge
);

public sealed record BridgeListenerInfo(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("websocket_url")] string WebSocketUrl
);
