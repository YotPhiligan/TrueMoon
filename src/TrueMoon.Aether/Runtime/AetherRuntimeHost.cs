using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Aether.Runtime;

/// <summary>Owns one runtime's game, control server, frame producer and parent listener.</summary>
/// <remarks>Application/GPU services are borrowed. Stop drains workers before disposing the game.</remarks>
public sealed class AetherRuntimeHost : IAsyncDisposable
{
    private readonly AetherHost _engine;
    private readonly UnitParentProcessEventsHandler _parent;
    private readonly GameBuildLoader _loaded;
    private readonly FrameProducer _frames;
    private readonly RuntimeProtocol _protocol;
    private readonly PipesInvocationServer<IEngineControl> _server;
    private readonly SessionLaunch _launch;
    private readonly int _parentId;
    private readonly CancellationTokenSource _end;
    private readonly Lock _gate = new();
    private Task[] _workers = [];
    private Task? _dispose;
    private bool _running;
    private bool _serverReleased, _parentReleased, _framesReleased, _gameReleased;

    internal AetherRuntimeHost(AetherHost engine, IAppLifetime lifetime, UnitParentProcessEventsHandler parent,
        GameBuildLoader loaded, FrameProducer frames, RuntimeProtocol protocol, PipesInvocationServer<IEngineControl> server,
        SessionLaunch launch, int parentId)
    {
        _engine = engine; _parent = parent; _loaded = loaded; _frames = frames; _protocol = protocol;
        _server = server; _launch = launch; _parentId = parentId;
        _end = CancellationTokenSource.CreateLinkedTokenSource(lifetime.AppCancellationToken);
    }

    /// <summary>Starts listening to the owned parent and runs until shutdown or worker failure.</summary>
    /// <param name="token">Cancellation requests orderly runtime shutdown.</param>
    /// <returns>Completes after all owned work and resources have drained.</returns>
    public async Task RunAsync(CancellationToken token = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_dispose != null, this);
            if (_running) throw new InvalidOperationException("The runtime is already running.");
            _running = true;
        }
        using var cancellation = token.Register(Cancel);
        Exception? failure = null;
        try
        {
            _parent.StartListen(_parentId);
            var end = _end.Token;
            var gpu = _engine.Gpu.RunSurfaceProducerAsync(_launch, end,
                validation: Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1",
                frameProvider: (stamp, width, height) => { var frame = _protocol.CaptureRenderFrame(); return GpuFrameData.FromScene(stamp, frame.Tick, frame.Scene); });
            _workers = [TickAsync(end), ReadStopAsync(end), _frames.RunAsync(_protocol, end), gpu];
            foreach (var worker in _workers)
                _ = worker.ContinueWith(_ => Cancel(), CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, end).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_end.IsCancellationRequested) { }
        }
        catch (Exception error) { failure = error; }
        try { await DisposeAsync().ConfigureAwait(false); }
        catch (Exception cleanup) { if (failure != null) throw new AggregateException(failure, cleanup); throw; }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private async Task TickAsync(CancellationToken token)
    { using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / 60)); while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)) _protocol.FixedTick(); }
    private async Task ReadStopAsync(CancellationToken token)
    {
        while (await Console.In.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            if (line == UnitCodes.Exit) { Cancel(); return; }
        Cancel();
    }

    /// <summary>Stops admission, cancels workers and releases owned services exactly once.</summary>
    /// <returns>The shared cleanup completion.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispose = completion.Task;
            _ = CleanupAsync(completion);
            return new(_dispose);
        }
    }
    private async Task CleanupAsync(TaskCompletionSource completion)
    {
        var errors = new List<Exception>();
        Cancel();
        if (!_serverReleased) Try(() => { _server.Dispose(); _serverReleased = true; });
        try { await Task.WhenAll(_workers).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_end.IsCancellationRequested) { }
        catch (Exception error) { errors.Add(error); }
        finally { _workers = []; }
        if (!_parentReleased)
        {
            try { await _parent.StopAsync().ConfigureAwait(false); await _parent.Completion.ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
            try { await _parent.DisposeAsync().ConfigureAwait(false); _parentReleased = true; }
            catch (Exception error) { errors.Add(error); }
        }
        if (!_framesReleased) Try(() => { _frames.Dispose(); _framesReleased = true; });
        if (!_gameReleased) Try(() => { _loaded.Dispose(); _gameReleased = true; });
        if (errors.Count == 0) _end.Dispose();
        if (errors.Count == 0) completion.TrySetResult();
        else completion.TrySetException(errors.Count == 1 ? errors[0] : new AggregateException(errors));
        void Try(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
    }
    private void Cancel() { try { _end.Cancel(); } catch (ObjectDisposedException) { } }
}
