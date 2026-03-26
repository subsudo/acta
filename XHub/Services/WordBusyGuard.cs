using System.Threading;

namespace XHub.Services;

public static class WordBusyGuard
{
    private static int _busy;

    public static bool IsBusy => Volatile.Read(ref _busy) != 0;

    public static bool TryEnter()
    {
        return Interlocked.CompareExchange(ref _busy, 1, 0) == 0;
    }

    public static void Exit()
    {
        Interlocked.Exchange(ref _busy, 0);
    }
}
