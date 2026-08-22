namespace FigmaMCP.Connections;

public sealed class BridgeRpcException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
