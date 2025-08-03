namespace EasyVoice.Core.Services;

/// <summary>
/// 并发控制器
/// </summary>
public class ConcurrencyController
{
    private bool _cancelled = false;
    private readonly HashSet<Task<dynamic>> _runningTasks = new();
    private readonly List<Func<Task<dynamic>>> _tasks;
    private readonly int _concurrency;
    private readonly Action _callback;

    public ConcurrencyController(List<Func<Task<dynamic>>> tasks, int concurrency = 3, Action? callback = null)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _concurrency = Math.Max(1, concurrency);
        _callback = callback ?? (() => { });
    }

    public void Cancel()
    {
        _cancelled = true;
    }

    public async Task<(List<dynamic> results, bool cancelled)> RunAsync()
    {
        if (_tasks.Count == 0)
        {
            _callback();
            return (new List<dynamic>(), false);
        }

        var results = new dynamic[_tasks.Count];
        var running = 0;
        var completed = 0;
        var index = 0;
        var originalLength = _tasks.Count;

        var tcs = new TaskCompletionSource<(List<dynamic>, bool)>();

        void Complete()
        {
            _callback();
            tcs.SetResult((results.ToList(), _cancelled));
        }

        void RunNext()
        {
            while (!_cancelled && running < _concurrency && index < _tasks.Count)
            {
                var currentIndex = index++;
                running++;

                var taskPromise = _tasks[currentIndex]();
                _runningTasks.Add(taskPromise);

                taskPromise.ContinueWith(task =>
                {
                    _runningTasks.Remove(taskPromise);
                    running--;
                    completed++;

                    if (!_cancelled)
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            results[currentIndex] = new { success = true, value = task.Result };
                        }
                        else if (task.IsFaulted)
                        {
                            results[currentIndex] = new 
                            { 
                                success = false, 
                                index = currentIndex, 
                                error = task.Exception?.GetBaseException().Message ?? "Unknown error" 
                            };
                        }
                    }

                    if (completed == originalLength)
                    {
                        Complete();
                    }
                    else if (!_cancelled)
                    {
                        RunNext();
                    }
                    else if (running == 0)
                    {
                        Complete();
                    }
                }, TaskScheduler.Default);
            }
        }

        RunNext();
        return await tcs.Task;
    }
}