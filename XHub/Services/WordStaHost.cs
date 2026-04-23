using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace XHub.Services;

public sealed class WordStaHost : IDisposable
{
    private readonly BlockingCollection<IWordStaWorkItem> _queue = new();
    private readonly Thread _workerThread;
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeRequested;

    public WordStaHost()
    {
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "Acta.WordStaHost"
        };
        _workerThread.SetApartmentState(ApartmentState.STA);
        _workerThread.Start();
        _readyTcs.Task.GetAwaiter().GetResult();
    }

    public Task RunAsync(string operationName, Action<WordService> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync<object?>(operationName, service =>
        {
            action(service);
            return null;
        });
    }

    public Task<T> RunAsync<T>(string operationName, Func<WordService, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);

        var workItem = new WordStaWorkItem<T>(operationName, action);
        try
        {
            _queue.Add(workItem);
        }
        catch (InvalidOperationException ex)
        {
            throw new ObjectDisposedException(nameof(WordStaHost), ex.Message);
        }

        return workItem.Task;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            return;
        }

        _queue.CompleteAdding();
        if (Thread.CurrentThread != _workerThread && _workerThread.IsAlive)
        {
            _workerThread.Join(TimeSpan.FromSeconds(5));
        }

        _queue.Dispose();
    }

    private void WorkerLoop()
    {
        WordService? service = null;

        try
        {
            service = new WordService();
            _readyTcs.TrySetResult();

            foreach (var workItem in _queue.GetConsumingEnumerable())
            {
                workItem.Execute(service);
            }
        }
        catch (Exception ex)
        {
            _readyTcs.TrySetException(ex);
            while (_queue.TryTake(out var pending))
            {
                pending.Fail(ex);
            }
        }
    }

    private interface IWordStaWorkItem
    {
        void Execute(WordService service);
        void Fail(Exception ex);
    }

    private sealed class WordStaWorkItem<T> : IWordStaWorkItem
    {
        private readonly Func<WordService, T> _action;
        private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WordStaWorkItem(string operationName, Func<WordService, T> action)
        {
            _ = operationName;
            _action = action;
        }

        public Task<T> Task => _tcs.Task;

        public void Execute(WordService service)
        {
            try
            {
                _tcs.TrySetResult(_action(service));
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }

        public void Fail(Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }
}
