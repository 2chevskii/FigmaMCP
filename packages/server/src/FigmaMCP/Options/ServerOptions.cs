namespace FigmaMCP.Options;

public sealed record ServerOptions(int Port)
{
    public const int DefaultPort = 3846;

    public static bool TryParse(string[] args, out ServerOptions? options, out string? error)
    {
        options = null;
        error = null;

        if (args.Length == 0)
        {
            options = new ServerOptions(DefaultPort);
            return true;
        }

        if (args.Length != 2 || !string.Equals(args[0], "--port", StringComparison.Ordinal))
        {
            error = "Usage: figma-mcp-server.exe [--port <1-65535>]";
            return false;
        }

        if (
            !int.TryParse(args[1], System.Globalization.CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535
        )
        {
            error = "The port must be an integer from 1 through 65535.";
            return false;
        }

        options = new ServerOptions(port);
        return true;
    }
}
