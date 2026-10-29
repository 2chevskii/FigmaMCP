using System.Diagnostics;

namespace FigmaMCP.Companion;

internal sealed class BackendActivity(int idleSeconds)
{
    private readonly object _gate = new();
    private int _count;
    private long _idleSince = Stopwatch.GetTimestamp();
    private bool _stopping;

    public IDisposable Enter()
    {
        lock (_gate)
        {
            if (_stopping)
                throw new IOException("Backend is stopping.");
            _count++;
            return new Lease(this);
        }
    }

    public bool TryStop()
    {
        lock (_gate)
        {
            if (_count != 0 || Stopwatch.GetElapsedTime(_idleSince).TotalSeconds < idleSeconds)
                return false;
            return _stopping = true;
        }
    }

    private sealed class Lease(BackendActivity owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            lock (owner._gate)
                if (--owner._count == 0)
                    owner._idleSince = Stopwatch.GetTimestamp();
        }
    }
}
