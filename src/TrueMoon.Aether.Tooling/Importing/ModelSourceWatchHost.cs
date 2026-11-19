namespace TrueMoon.Aether.Tooling.Importing;

internal sealed class ModelSourceWatchHost(IFactory<ModelSourceCapture, ModelSourceCaptureArguments> captures,
    TimeProvider time) : IAsyncDisposable
{
    private const int MaximumActiveWatches = 8;
    private readonly Lock _gate = new();
    private readonly HashSet<ModelSourceWatch> _active = [];
    private bool _closed;

    public ModelSourceWatch Create(ModelSourceWatchArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Source);
        ArgumentNullException.ThrowIfNull(arguments.Published);
        if (arguments.Published.Candidate.SourceId != arguments.Source.SourceId)
            throw new ArgumentException("The published import is for another model source.", nameof(arguments));
        if (!ModelImportCandidate.IsValidGeometryScale(arguments.GeometryScale) ||
            arguments.PollInterval < TimeSpan.FromMilliseconds(100) || arguments.PollInterval > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(arguments), "Invalid model watch settings.");

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_active.Count == MaximumActiveWatches)
                throw new InvalidOperationException("At most eight model source watches can be active per App.");
            using var validated = captures.Create(arguments.Source)
                ?? throw new InvalidOperationException("The model capture factory returned null.");
            var watch = new ModelSourceWatch(captures, arguments with { Source = validated.Arguments }, time, this);
            _active.Add(watch);
            return watch;
        }
    }

    internal void Release(ModelSourceWatch watch)
    {
        lock (_gate) _active.Remove(watch);
    }

    public ValueTask DisposeAsync()
    {
        ModelSourceWatch[] watches;
        lock (_gate)
        {
            _closed = true;
            watches = _active.ToArray();
        }
        return new(Task.WhenAll(watches.Select(watch => watch.DisposeAsync().AsTask())));
    }
}

internal sealed class ModelSourceWatchLifetime(ModelSourceWatchHost host) : IStoppable
{
    public Task StopAsync(CancellationToken cancellationToken = default) => host.DisposeAsync().AsTask();
}
