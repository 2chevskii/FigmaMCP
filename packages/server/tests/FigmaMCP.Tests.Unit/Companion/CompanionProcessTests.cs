using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using FigmaMCP.Bridge;
using FigmaMCP.Companion;
using FigmaMCP.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace FigmaMCP.Tests.Unit.Companion;

public sealed class CompanionProcessTests
{
    [Fact]
    public async Task ConcurrentClientsShareBackendAndSurviveFirstClientTreeExitAndBackendCrash()
    {
        var port = FreePort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var first = new Client(port);
        await using var second = new Client(port, idle: 2);
        await Task.WhenAll(first.Initialize(timeout.Token), second.Initialize(timeout.Token));
        var a = await first.Info(timeout.Token);
        var b = await second.Info(timeout.Token);
        Assert.Equal(
            a.GetProperty("server_instance_id").GetString(),
            b.GetProperty("server_instance_id").GetString()
        );
        var backend = Process.GetProcessById(a.GetProperty("process_id").GetInt32());
        using (backend)
        {
            using var plugin = await ConnectPlugin(a, timeout.Token);
            var one = await first.Tool("list_figma_connections", new { }, timeout.Token);
            var two = await second.Tool("list_figma_connections", new { }, timeout.Token);
            Assert.Equal(one.GetRawText(), two.GetRawText());
            Assert.Single(one.GetProperty("connections").EnumerateArray());
            var connection = one.GetProperty("connections")[0]
                .GetProperty("connection_id")
                .GetString();
            var call = second.Tool(
                "get_figma_document_metadata",
                new { connection_id = connection },
                timeout.Token
            );
            var buffer = new byte[4096];
            var received = await plugin.ReceiveAsync(buffer.AsMemory(), timeout.Token);
            var request = BridgeEnvelopeCodec.Decode(buffer.AsMemory(0, received.Count));
            await plugin.SendAsync(
                BridgeEnvelopeCodec.Encode(
                    new BridgeEnvelope(
                        "response",
                        2,
                        request.ConnectionId,
                        request.RequestId,
                        null,
                        BridgePayloadCodec.Encode(
                            JsonSerializer.SerializeToElement(new { name = "test" })
                        ),
                        null,
                        DateTimeOffset.UtcNow
                    )
                ),
                WebSocketMessageType.Binary,
                true,
                timeout.Token
            );
            Assert.Equal("test", (await call).GetProperty("name").GetString());
            first.KillTree();
            Assert.Equal(
                a.GetProperty("process_id").GetInt32(),
                (await second.Info(timeout.Token)).GetProperty("process_id").GetInt32()
            );

            // An outstanding document call fails without replay, but the same MCP session recovers.
            var interrupted = second.Tool(
                "get_figma_document_metadata",
                new { connection_id = connection },
                timeout.Token
            );
            await plugin.ReceiveAsync(buffer.AsMemory(), timeout.Token);
            backend.Kill();
            var failure = await interrupted;
            Assert.Equal(
                "backend_connection_lost",
                failure.GetProperty("error").GetProperty("code").GetString()
            );
            JsonElement recovered;
            while (true)
            {
                await Task.Delay(200, timeout.Token);
                try
                {
                    recovered = await second.Info(timeout.Token);
                    break;
                }
                catch (InvalidOperationException) { }
            }
            Assert.NotEqual(
                a.GetProperty("server_instance_id").GetString(),
                recovered.GetProperty("server_instance_id").GetString()
            );
            using var replacement = Process.GetProcessById(
                recovered.GetProperty("process_id").GetInt32()
            );
            using (var reconnected = await ConnectPlugin(recovered, timeout.Token))
                Assert.Equal(
                    1,
                    (await second.Info(timeout.Token))
                        .GetProperty("active_plugin_connections")
                        .GetInt32()
                );
            await second.DisposeAsync();
            await replacement.WaitForExitAsync(timeout.Token); // idle shutdown
        }
    }

    [Fact]
    public async Task VersionMismatchDoesNotReplaceBackend()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var port = FreePort();
        await using var client = new Client(port);
        await client.Initialize(timeout.Token);
        var initial = await client.Info(timeout.Token);
        await using var ipc = await IpcChannel.ConnectAsync(
            InstanceIdentity.Name(port),
            timeout.Token
        );
        await ipc.WriteAsync(
            new()
            {
                Type = "hello",
                Version = "different-build",
                Port = port,
            },
            timeout.Token
        );
        var error = await ipc.ReadAsync(timeout.Token);
        Assert.Equal("backend_version_mismatch", error.Code);
        Assert.Equal(
            initial.GetProperty("process_id").GetInt32(),
            (await client.Info(timeout.Token)).GetProperty("process_id").GetInt32()
        );
    }

    [Fact]
    public async Task CliOverridesEnvironmentAndExistingBackendKeepsItsTimeout()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var port = FreePort();
        await using var first = new Client(port, idle: 2, environmentIdle: "1");
        await first.Initialize(timeout.Token);
        await using var second = new Client(port, idle: 3);
        await second.Initialize(timeout.Token);
        await using var ipc = await IpcChannel.ConnectAsync(
            InstanceIdentity.Name(port),
            timeout.Token
        );
        await ipc.WriteAsync(
            new()
            {
                Type = "hello",
                Version = FigmaMCP.ProductInfo.Version,
                Port = port,
            },
            timeout.Token
        );
        Assert.Equal(2, (await ipc.ReadAsync(timeout.Token)).IdleSeconds);
        using var backend = Process.GetProcessById(
            (await first.Info(timeout.Token)).GetProperty("process_id").GetInt32()
        );
        await ipc.DisposeAsync();
        await first.DisposeAsync();
        await second.DisposeAsync();
        await backend.WaitForExitAsync(timeout.Token);
    }

    [Fact]
    public async Task OccupiedPortFailsWithoutFallback()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var client = new Client(((IPEndPoint)listener.LocalEndpoint).Port);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await client.Process.WaitForExitAsync(timeout.Token);
        Assert.Equal(3, client.Process.ExitCode);
        Assert.Contains("bridge_port_in_use", await client.Errors);
    }

    [Fact]
    public async Task SeparatePortsHaveSeparateBackends()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var port = FreePort();
        var other = FreePort();
        while (other == port)
            other = FreePort();
        await using var a = new Client(port);
        await using var b = new Client(other);
        await Task.WhenAll(a.Initialize(timeout.Token), b.Initialize(timeout.Token));
        Assert.NotEqual(
            (await a.Info(timeout.Token)).GetProperty("server_instance_id").GetString(),
            (await b.Info(timeout.Token)).GetProperty("server_instance_id").GetString()
        );
    }

    [Fact]
    public async Task InvalidFrameIsRejectedBeforeAllocatingItsPayload()
    {
        var header = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            header,
            IpcChannel.MaxFrameBytes + 1
        );
        await using var channel = new IpcChannel(new MemoryStream(header));
        await Assert.ThrowsAsync<IOException>(() =>
            channel.ReadAsync(TestContext.Current.CancellationToken)
        );
    }

    private static async Task<ClientWebSocket> ConnectPlugin(
        JsonElement info,
        CancellationToken token
    )
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(BridgeProtocol.Subprotocol);
        await socket.ConnectAsync(
            new(info.GetProperty("bridge").GetProperty("websocket_url").GetString()!),
            token
        );
        await socket.SendAsync(
            BridgeEnvelopeCodec.Encode(
                new BridgeEnvelope(
                    "hello",
                    2,
                    Guid.NewGuid(),
                    null,
                    null,
                    BridgePayloadCodec.Encode(
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                plugin_version = "test",
                                document_name = "Test",
                                current_page = new { id = "0:1", name = "Page" },
                                editor_type = "figma",
                                mode = "default",
                            }
                        )
                    ),
                    null,
                    DateTimeOffset.UtcNow
                )
            ),
            WebSocketMessageType.Binary,
            true,
            token
        );
        var buffer = new byte[4096];
        await socket.ReceiveAsync(buffer.AsMemory(), token);
        return socket;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class Client : IAsyncDisposable
    {
        public Process Process { get; }
        public Task<string> Errors { get; }
        private int _id;
        private bool _disposed;

        public Client(int port, int idle = 1, string? environmentIdle = null)
        {
            var path = FindServer();
            var start = new ProcessStartInfo(
                path.EndsWith(".dll", StringComparison.Ordinal) ? "dotnet" : path
            )
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (path.EndsWith(".dll", StringComparison.Ordinal))
                start.ArgumentList.Add(path);
            foreach (
                var arg in new[]
                {
                    "--port",
                    port.ToString(),
                    "--idle-timeout-seconds",
                    idle.ToString(),
                }
            )
                start.ArgumentList.Add(arg);
            start.Environment["Logging__LogLevel__Default"] = "None";
            if (environmentIdle is not null)
                start.Environment["FIGMA_MCP_IDLE_TIMEOUT_SECONDS"] = environmentIdle;
            Process = Process.Start(start)!;
            Errors = ReadErrors();
        }

        private async Task<string> ReadErrors()
        {
            var lines = new System.Text.StringBuilder();
            while (await Process.StandardError.ReadLineAsync() is { } line)
            {
                lines.AppendLine(line);
                Console.Error.WriteLine("frontend: " + line);
            }
            return lines.ToString();
        }

        public async Task Initialize(CancellationToken token)
        {
            await Request(
                "initialize",
                new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new { name = "test", version = "1" },
                },
                token
            );
            await Process.StandardInput.WriteLineAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"
            );
        }

        private async Task<JsonElement> Request(string method, object args, CancellationToken token)
        {
            var id = ++_id;
            await Process.StandardInput.WriteLineAsync(
                JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        id,
                        method,
                        @params = args,
                    }
                )
            );
            while (true)
            {
                var line = await Process.StandardOutput.ReadLineAsync(token);
                if (line is null)
                    throw new InvalidOperationException("MCP exited: " + await Errors);
                var value = JsonSerializer.Deserialize<JsonElement>(line);
                if (!value.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id)
                    continue;
                if (value.TryGetProperty("error", out var error))
                    throw new InvalidOperationException(error.ToString());
                return value.GetProperty("result");
            }
        }

        public async Task<JsonElement> Tool(string name, object arguments, CancellationToken token)
        {
            var result = await Request("tools/call", new { name, arguments }, token);
            if (result.TryGetProperty("isError", out var error) && error.GetBoolean())
                throw new InvalidOperationException(result.ToString());
            if (result.TryGetProperty("structuredContent", out var structured))
                return structured;
            return JsonSerializer.Deserialize<JsonElement>(
                result.GetProperty("content")[0].GetProperty("text").GetString()!
            );
        }

        public Task<JsonElement> Info(CancellationToken token) =>
            Tool("get_server_info", new { }, token);

        public void KillTree()
        {
            if (!Process.HasExited)
                Process.Kill(entireProcessTree: true);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!Process.HasExited)
            {
                Process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await Process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    KillTree();
                }
            }
            await Errors;
            Process.Dispose();
        }

        private static string FindServer()
        {
            var fixture = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath)!,
                "companion",
                OperatingSystem.IsWindows() ? "FigmaMCP.exe" : "FigmaMCP"
            );
            if (File.Exists(fixture))
                return fixture;
            for (
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                directory is not null;
                directory = directory.Parent
            )
                foreach (var configuration in new[] { "Release", "Debug" })
                {
                    var path = Path.Combine(
                        directory.FullName,
                        "src",
                        "FigmaMCP",
                        "bin",
                        configuration,
                        "net10.0",
                        "FigmaMCP.dll"
                    );
                    if (File.Exists(path))
                        return path;
                }
            throw new FileNotFoundException(
                "Build the server before running process tests; published tests require the companion fixture."
            );
        }
    }
}
