namespace TrueMoon.Alloy.Hosting;

// Native-free frame policy used by the real window; requests can arrive on any thread.
internal sealed class UiFramePump : IDisposable
{
    internal const int NativePollMilliseconds = 16;
    private readonly UiFrameMode _mode;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _interval;
    private readonly Action _wake;
    private UiSession? _session;
    private int _work = 1, _presentation = 1, _render, _disposed;
    private long? _lastAttempt;
    internal bool UpdatedLastTick { get; private set; }

    internal UiFramePump(UiFrameMode mode, int framesPerSecond, Action wake, TimeProvider? clock = null)
    {
        Validate(mode, framesPerSecond); ArgumentNullException.ThrowIfNull(wake);
        _mode = mode; _interval = TimeSpan.FromSeconds(1d / framesPerSecond);
        _clock = clock ?? TimeProvider.System; _wake = wake;
    }

    internal static void Validate(UiFrameMode mode, int framesPerSecond)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (framesPerSecond is < 1 or > 240) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
    }

    internal void Attach(UiSession session)
    {
        session.VerifyRendering();
        if (_session != null || Volatile.Read(ref _disposed) != 0) throw new InvalidOperationException("Frame pump is already attached or disposed.");
        _session = session; session.SetUpdateSignal(RequestWork);
    }

    internal void RequestWork() => Request(ref _work);
    internal void RequestPresentation() => Request(ref _presentation);
    internal void RequestFrame() { Request(ref _render); RequestWork(); }
    private void Request(ref int flag)
    {
        if (Volatile.Read(ref _disposed) == 0 && Interlocked.Exchange(ref flag, 1) == 0) _wake();
    }
    private bool Continuous(bool observers) => _mode == UiFrameMode.Continuous || _mode == UiFrameMode.Automatic && observers;
    private bool Demand(bool needsUpdate, bool suspended, bool observers) => Volatile.Read(ref _work) != 0
        || !suspended && (needsUpdate || Continuous(observers) || Volatile.Read(ref _presentation) != 0 || Volatile.Read(ref _render) != 0);

    internal int WaitMilliseconds(bool needsUpdate, bool suspended, bool observers)
    {
        if (!Demand(needsUpdate, suspended, observers)) return NativePollMilliseconds;
        if (_lastAttempt is not { } last) return 0;
        var remaining = _interval - _clock.GetElapsedTime(last);
        return remaining <= TimeSpan.Zero ? 0 : Math.Min(NativePollMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds));
    }

    internal bool Tick(bool observers, Func<bool> output, bool immediate = false)
    {
        UpdatedLastTick = false;
        var session = _session ?? throw new InvalidOperationException("Frame pump is not attached.");
        if (!Demand(session.NeedsUpdate, session.Viewport.IsEmpty, observers)) return false;
        if (!immediate && _lastAttempt is { } last && _clock.GetElapsedTime(last) < _interval) return false;
        _lastAttempt = _clock.GetTimestamp();
        Interlocked.Exchange(ref _work, 0);
        var present = !session.Viewport.IsEmpty && Interlocked.Exchange(ref _presentation, 0) != 0;
        try
        {
            if (Interlocked.Exchange(ref _render, 0) != 0) session.RequestFrame();
            var updated = (session.NeedsUpdate || !session.Viewport.IsEmpty && Continuous(observers)) && session.Update();
            UpdatedLastTick = updated;
            if (session.Viewport.IsEmpty) { if (present) RequestPresentation(); return false; }
            if (!present && !updated && !Continuous(observers)) return false;
            if (UiPresentationBoundary.Run(session, output)) return true;
            RequestPresentation(); return false;
        }
        catch { RequestPresentation(); RequestWork(); throw; }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        _session?.SetUpdateSignal(null); _session = null;
    }
}
