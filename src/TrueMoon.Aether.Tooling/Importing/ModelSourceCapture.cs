namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Caller-owned, single-use Windows source capture. Dispose waits for cancellation and actual IO cleanup.</summary>
public sealed class ModelSourceCapture : IDisposable, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ModelSourceCaptureArguments _arguments;
    private readonly ModelSourceCaptureLimits _limits;
    private readonly CancellationTokenSource _stop = new();
    private Task<CapturedModelSource>? _operation;
    private Task? _dispose;
    private int _handles;

    internal ModelSourceCapture(ModelSourceCaptureArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.SourceId == Guid.Empty) throw new ArgumentException("SourceId must be persistent and non-empty.", nameof(arguments));
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Directory);
        if (!Path.IsPathFullyQualified(arguments.Directory) || arguments.Directory.StartsWith(@"\\", StringComparison.Ordinal))
            throw new NotSupportedException("Capture profile v1 requires an absolute DOS-drive directory.");
        var root = ModelSourcePath.Normalize(arguments.RootName);
        if (Path.GetExtension(root).ToLowerInvariant() is not (".gltf" or ".glb" or ".obj"))
            throw new NotSupportedException("Capture profile v1 supports .gltf, .glb and .obj roots only.");
        _limits = (arguments.Limits ?? new()).Snapshot();
        _arguments = arguments with { Directory = Path.GetFullPath(arguments.Directory), RootName = root, Limits = _limits };
    }

    /// <summary>Tracked open physical file/directory handles; zero after a completed, cancelled or failed operation.</summary>
    public int OpenHandleCount => Volatile.Read(ref _handles);

    /// <summary>Captures a complete bounded dependency set once. No result escapes before revalidation and cleanup.</summary>
    /// <param name="cancellationToken">Cancellation is checked during reads, scanning and before returning the result.</param>
    /// <returns>Detached immutable input for a later parser; never a partial manifest.</returns>
    public Task<CapturedModelSource> CaptureAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_dispose != null, this);
            if (_operation != null) throw new InvalidOperationException("A source capture operation can run only once.");
            return _operation = RunAsync(cancellationToken);
        }
    }

    private async Task<CapturedModelSource> RunAsync(CancellationToken caller)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _stop.Token); var token = linked.Token;
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Handle-verified model capture currently requires Windows.");
        CapturedModelSource result;
        using (var files = new ModelSourceFiles(_arguments.Directory, delta => Interlocked.Add(ref _handles, delta)))
        {
            var captured = new Dictionary<string, CapturedModelFile>(StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var bytes = 0;
            async Task<CapturedModelFile> Read(string name, int minimum)
            {
                token.ThrowIfCancellationRequested();
                if (names.TryGetValue(name, out var exact) && exact != name) throw new InvalidDataException("Case-insensitive model dependency name collision.");
                if (captured.TryGetValue(name, out var existing))
                {
                    if (existing.Length < minimum) throw new InvalidDataException("Captured dependency is shorter than its declared length.");
                    return existing;
                }
                if (captured.Count >= _limits.Files) throw new InvalidDataException("Model source file-count budget exceeded before open.");
                if (minimum > Math.Min(_limits.FileBytes, _limits.TotalBytes - bytes)) throw new InvalidDataException("Declared dependency length exceeds capture budget.");
                var file = await files.ReadAsync(name, Math.Min(_limits.FileBytes, _limits.TotalBytes - bytes), token).ConfigureAwait(false);
                if (file.Length < minimum) throw new InvalidDataException("Model dependency is shorter than its declared length.");
                captured.Add(name, file); names.Add(name, name); bytes = checked(bytes + file.Length); return file;
            }
            var root = await Read(_arguments.RootName, 0).ConfigureAwait(false);
            foreach (var dependency in ModelSourceDependencies.Discover(root, token))
            {
                var file = await Read(dependency.Name, dependency.MinimumBytes).ConfigureAwait(false);
                if (dependency.Material) ModelSourceDependencies.DiscoverText(file, material: true, token);
            }
            await files.RevalidateAsync(token).ConfigureAwait(false); token.ThrowIfCancellationRequested();
            result = new CapturedModelSource(_arguments.SourceId, _arguments.RootName, captured.Values);
        }
        token.ThrowIfCancellationRequested(); return result;
    }

    /// <summary>Cancels and waits for active IO; callers observe operation errors through the capture task.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate) return new(_dispose ??= DisposeCoreAsync(_operation));
    }
    private async Task DisposeCoreAsync(Task<CapturedModelSource>? operation)
    {
        _stop.Cancel();
        try { if (operation != null) await operation.ConfigureAwait(false); }
        catch { /* The capture task retains its error; its finally blocks have completed. */ }
        finally { _stop.Dispose(); lock (_gate) _operation = null; }
    }
    /// <summary>Synchronously waits for cancellation and IO cleanup.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
