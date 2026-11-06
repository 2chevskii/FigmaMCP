using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FigmaMCP.Companion;

internal sealed class BackendLogProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryComplete();
    }

    public async Task PumpAsync(IpcChannel connection, CancellationToken stop)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        _subscribers[id] = channel;
        try
        {
            await foreach (var line in channel.Reader.ReadAllAsync(stop))
                await connection.WriteAsync(new() { Type = "diagnostic", Message = line }, stop);
        }
        catch (Exception e)
            when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }

    private sealed class Logger(BackendLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(
            LogLevel level,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(level))
                return;
            var message = $"{level} {category}: {formatter(state, exception)}";
            if (message.Length > 2048)
                message = message[..2048];
            foreach (var channel in owner._subscribers.Values)
                channel.Writer.TryWrite(message);
        }
    }
}
