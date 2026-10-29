using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace FigmaMCP.Companion;

internal static partial class InstanceIdentity
{
    public static string Name(int port)
    {
        var user = OperatingSystem.IsWindows()
            ? WindowsIdentity.GetCurrent().User!.Value
            : GetUid().ToString();
        return "figma-mcp-"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user)))[..24]
            + "-"
            + port;
    }

    public static Mutex CreateMutex(string name) =>
        new(
            false,
            name,
            new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = false }
        );

    public static bool Acquire(Mutex mutex, int milliseconds)
    {
        try
        {
            return mutex.WaitOne(milliseconds);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    [LibraryImport("libc", EntryPoint = "getuid")]
    private static partial uint GetUid();
}

// Ownership is thread-affine; acquisition and release always use the same thread.
internal sealed class LifetimeLock : IDisposable
{
    private readonly ManualResetEventSlim _stop = new();
    private readonly Thread _thread;

    private LifetimeLock(string name, TaskCompletionSource<bool> ready)
    {
        _thread = new Thread(() =>
        {
            try
            {
                using var mutex = InstanceIdentity.CreateMutex(name);
                var owned = InstanceIdentity.Acquire(mutex, 0);
                ready.TrySetResult(owned);
                if (!owned)
                    return;
                try
                {
                    _stop.Wait();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            catch (Exception e)
            {
                ready.TrySetException(e);
            }
        })
        {
            IsBackground = true,
            Name = "Companion lifetime lock",
        };
        _thread.Start();
    }

    public static async Task<LifetimeLock> AcquireAsync(string name)
    {
        var ready = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var result = new LifetimeLock(name, ready);
        if (await ready.Task)
            return result;
        result.Dispose();
        throw new IOException("A backend already owns this port's lifetime lock.");
    }

    public void Dispose()
    {
        _stop.Set();
        _thread.Join();
        _stop.Dispose();
    }
}
