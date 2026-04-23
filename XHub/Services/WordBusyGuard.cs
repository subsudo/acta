using System.Threading;

namespace XHub.Services;

public static class WordBusyGuard
{
    private static int _busy;
    public static event EventHandler? BusyStateChanged;

    public static bool IsBusy => Volatile.Read(ref _busy) != 0;

    public static bool TryEnter()
    {
        var entered = Interlocked.CompareExchange(ref _busy, 1, 0) == 0;
        if (entered)
        {
            BusyStateChanged?.Invoke(null, EventArgs.Empty);
        }

        return entered;
    }

    public static void Exit()
    {
        if (Interlocked.Exchange(ref _busy, 0) != 0)
        {
            BusyStateChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
