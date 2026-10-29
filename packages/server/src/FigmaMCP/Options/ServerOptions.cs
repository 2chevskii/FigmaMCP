namespace FigmaMCP.Options;

public sealed record ServerOptions(
    int Port,
    bool IsPortExplicit,
    int IdleTimeoutSeconds = 60,
    bool Backend = false,
    string? BootstrapPipe = null
)
{
    public const int DefaultPort = 3846;
    public const string IdleEnvironmentVariable = "FIGMA_MCP_IDLE_TIMEOUT_SECONDS";

    public static bool TryParse(string[] args, out ServerOptions? options, out string? error)
    {
        options = null;
        error = null;
        var port = DefaultPort;
        var explicitPort = false;
        string? idle = null;
        string? bootstrap = null;
        var backend = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!seen.Add(arg))
                return Fail("Duplicate option: " + arg, out error);
            if (arg == "--backend")
            {
                backend = true;
                continue;
            }
            if (++i >= args.Length)
                return Fail("Missing value for " + arg, out error);
            switch (arg)
            {
                case "--port":
                    if (!int.TryParse(args[i], out port) || port is < 1 or > 65535)
                        return Fail("The port must be an integer from 1 through 65535.", out error);
                    explicitPort = true;
                    break;
                case "--idle-timeout-seconds":
                    idle = args[i];
                    break;
                case "--bootstrap-pipe":
                    bootstrap = args[i];
                    break;
                default:
                    return Fail(
                        "Usage: figma-mcp-server [--port <1-65535>] [--idle-timeout-seconds <1-86400>]",
                        out error
                    );
            }
        }
        idle ??= Environment.GetEnvironmentVariable(IdleEnvironmentVariable) ?? "60";
        if (!int.TryParse(idle, out var seconds) || seconds is < 1 or > 86400)
            return Fail("Idle timeout must be an integer from 1 through 86400 seconds.", out error);
        if (bootstrap is not null && !backend)
            return Fail("Bootstrap pipe requires backend mode.", out error);
        options = new(port, explicitPort, seconds, backend, bootstrap);
        return true;
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}
