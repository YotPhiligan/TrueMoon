using TrueMoon.Threading;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class SignalsTaskScheduler : IDisposable
{
    private readonly CancellationTokenSource _cts;

    public SignalsTaskScheduler(SignalsMemoryConfiguration? configuration = default)
    {
        _cts = new CancellationTokenSource();
        TaskScheduler = new TmTaskScheduler(nameof(SignalsTaskScheduler), configuration?.Threads ?? 8, _cts.Token);
    }

    public TaskScheduler TaskScheduler { get; }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (TaskScheduler is IDisposable d)
        {
            d.Dispose();
        }
    }
}