using System.Net.WebSockets;
using System.Text.Json;
using FigmaMCP.Bridge;
using FigmaMCP.Connections;
using FigmaMCP.Diagnostics;
using FigmaMCP.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace FigmaMCP.Tests.Unit.Mcp;

public sealed class ServerInfoTests
{
    [Fact]
    public async Task ServerInfoWaitsForBindingAndReturnsStableBoundedMetadata()
    {
        using var timeout = CreateTimeout();
        await using var app = CreateApplication();
        var provider = app.Services.GetRequiredService<ServerInfoProvider>();
        var pending = new ServerTools(provider).GetServerInfo(timeout.Token);
        Assert.False(pending.IsCompleted);

        await app.StartAsync(timeout.Token);
        var info = await pending;
        var repeated = await new ServerTools(provider).GetServerInfo(timeout.Token);

        Assert.Equal(info, repeated);
        Assert.True(Guid.TryParseExact(info.ServerInstanceId, "D", out _));
        Assert.Equal(Environment.ProcessId, info.ProcessId);
        Assert.Equal(ProductInfo.Version, info.ProductVersion);
        Assert.InRange(info.StartedAt, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, info.StartedAt.Offset);
        Assert.Equal(0, info.ActivePluginConnections);
        Assert.Equal("127.0.0.1", info.Bridge.Host);
        Assert.InRange(info.Bridge.Port, 1, 65535);
        Assert.Equal($"http://127.0.0.1:{info.Bridge.Port}", app.Urls.Single());
        Assert.Equal($"ws://127.0.0.1:{info.Bridge.Port}/bridge", info.Bridge.WebSocketUrl);

        var json = JsonSerializer.SerializeToElement(info);
        Assert.Equal(
            [
                "active_plugin_connections",
                "bridge",
                "process_id",
                "product_version",
                "server_instance_id",
                "started_at",
            ],
            json.EnumerateObject().Select(property => property.Name).Order().ToArray()
        );
        Assert.Equal(
            ["host", "port", "websocket_url"],
            json.GetProperty("bridge")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order()
                .ToArray()
        );
        await app.StopAsync(timeout.Token);
    }

    [Fact]
    public async Task ServerInfoTracksLiveBridgeConnections()
    {
        using var timeout = CreateTimeout();
        await using var app = CreateApplication();
        await app.StartAsync(timeout.Token);
        var provider = app.Services.GetRequiredService<ServerInfoProvider>();
        var initial = await provider.GetAsync(timeout.Token);

        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(BridgeProtocol.Subprotocol);
        await socket.ConnectAsync(new Uri(initial.Bridge.WebSocketUrl), timeout.Token);
        var hello = new BridgeEnvelope(
            "hello",
            BridgeProtocol.Version,
            Guid.NewGuid(),
            null,
            null,
            BridgePayloadCodec.Encode(
                JsonSerializer.SerializeToElement(
                    new
                    {
                        plugin_version = "1.0.0-test",
                        document_name = "Test document",
                        current_page = new { id = "0:1", name = "Page 1" },
                        editor_type = "figma",
                        mode = "default",
                    }
                )
            ),
            null,
            DateTimeOffset.UtcNow
        );
        await socket.SendAsync(
            BridgeEnvelopeCodec.Encode(hello),
            WebSocketMessageType.Binary,
            true,
            timeout.Token
        );
        var buffer = new byte[4096];
        var received = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.True(received.EndOfMessage);
        Assert.Equal(
            "hello_ack",
            BridgeEnvelopeCodec.Decode(buffer.AsMemory(0, received.Count)).Type
        );

        var connected = await provider.GetAsync(timeout.Token);
        Assert.Equal(initial with { ActivePluginConnections = 1 }, connected);
        await socket.CloseOutputAsync(
            WebSocketCloseStatus.NormalClosure,
            "test_complete",
            timeout.Token
        );
        while ((await provider.GetAsync(timeout.Token)).ActivePluginConnections != 0)
        {
            await Task.Delay(10, timeout.Token);
        }
        Assert.Equal(initial, await provider.GetAsync(timeout.Token));
        await app.StopAsync(timeout.Token);
    }

    [Fact]
    public async Task WaitingForStartupRespectsCancellation()
    {
        await using var app = CreateApplication();
        var provider = app.Services.GetRequiredService<ServerInfoProvider>();
        using var cancellation = new CancellationTokenSource();
        var pending = provider.GetAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task ToolAdvertisesReadOnlyStructuredContractWithoutInputs()
    {
        await using var app = CreateApplication();
        var tools = new ServerTools(app.Services.GetRequiredService<ServerInfoProvider>());
        var tool = McpServerTool.Create(tools.GetServerInfo).ProtocolTool;

        Assert.Equal("get_server_info", tool.Name);
        Assert.True(tool.Annotations!.ReadOnlyHint);
        Assert.False(tool.Annotations.DestructiveHint);
        Assert.False(tool.Annotations.OpenWorldHint);
        Assert.Empty(tool.InputSchema.GetProperty("properties").EnumerateObject());
        Assert.NotNull(tool.OutputSchema);
        Assert.True(
            tool.OutputSchema.Value.GetProperty("properties").TryGetProperty("bridge", out _)
        );
    }

    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<PluginConnectionRegistry>();
        builder.Services.AddSingleton<ServerInfoProvider>();
        var app = builder.Build();
        app.UseWebSockets();
        app.Run(context =>
            BridgeEndpoint.HandleAsync(
                context,
                app.Services.GetRequiredService<PluginConnectionRegistry>(),
                app.Services.GetRequiredService<ILoggerFactory>(),
                app.Lifetime
            )
        );
        return app;
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        return timeout;
    }
}
