namespace FigmaMcp.Server.Connections;

public sealed record ConnectionSummary(
    Guid ConnectionId,
    string PluginVersion,
    int ProtocolVersion,
    string DocumentName,
    string CurrentPageId,
    string CurrentPageName,
    string EditorType,
    string Mode,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastSeenAt);
