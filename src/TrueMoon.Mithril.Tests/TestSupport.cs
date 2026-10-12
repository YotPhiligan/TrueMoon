using System.Diagnostics;
using TrueMoon.Diagnostics;

namespace TrueMoon.Mithril.Tests;

internal sealed class TestEvents<T> : IEventsSource<T>
{
    public string Name => typeof(T).Name;
    public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
    public void StopActivity(Activity activity) => activity.Stop();
    public void Write(Func<object>? func, string? category = null, string? caller = null) { }
}

internal sealed class TestLifetime : IAppLifetime, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    internal readonly TaskCompletionSource Notified = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Calls;
    internal Action? OnCancel;
    public CancellationToken AppCancellationToken => _cts.Token;
    public void Cancel() { Interlocked.Increment(ref Calls); _cts.Cancel(); OnCancel?.Invoke(); Notified.TrySetResult(); }
    public void OnStopped(Action action) { }
    public void OnStopping(Action action) { }
    public void Dispose() => _cts.Dispose();
}
