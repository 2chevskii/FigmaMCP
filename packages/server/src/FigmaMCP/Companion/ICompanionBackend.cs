using FigmaMCP.Connections;
using FigmaMCP.Diagnostics;

namespace FigmaMCP.Companion;

public interface IServerInfoSource
{
    Task<ServerInfo> GetAsync(CancellationToken cancellationToken);
}

public interface ICompanionBackend : IServerInfoSource
{
    Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(
        CancellationToken cancellationToken
    );
    Task<ReadOnlyMemory<byte>> InvokeAsync(
        Guid connectionId,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    );
}
