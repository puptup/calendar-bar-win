namespace CalendarBar;

public sealed class CoalescingTaskQueue<TRequest>
{
    private readonly Func<TRequest, TRequest, TRequest> _merge;
    private readonly Func<TRequest, Task> _run;
    private TRequest? _pending;
    private Task? _loopTask;
    private Task? _unwindingTask;
    private int _generation;
    private readonly object _gate = new();

    public CoalescingTaskQueue(Func<TRequest, TRequest, TRequest> merge, Func<TRequest, Task> run)
    {
        _merge = merge;
        _run = run;
    }

    public Task Submit(TRequest request)
    {
        lock (_gate)
        {
            _pending = _pending is null ? request : _merge(_pending, request);
            if (_loopTask is not null) return _loopTask;
            _generation++;
            var generation = _generation;
            var predecessor = _unwindingTask;
            _loopTask = Task.Run(async () =>
            {
                if (predecessor is not null) await predecessor;
                await Drain(generation);
            });
            return _loopTask;
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _pending = default;
            if (_loopTask is null) return;
            _unwindingTask = _loopTask;
            _loopTask = null;
            _generation++;
        }
    }

    private async Task Drain(int generation)
    {
        lock (_gate)
        {
            if (_generation != generation) return;
            _unwindingTask = null;
        }
        try
        {
            while (true)
            {
                TRequest request;
                lock (_gate)
                {
                    if (_generation != generation || _pending is null) return;
                    request = _pending;
                    _pending = default;
                }
                await _run(request);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_generation == generation) _loopTask = null;
            }
        }
    }
}
