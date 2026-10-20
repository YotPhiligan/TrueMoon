using System.Diagnostics;
using TrueMoon.Aether.Contracts;

namespace TrueMoon.Aether.Builds;

/// <summary>Owns one trusted-local SDK build at a time, independently of the active game session.</summary>
/// <remarks>Results are explicit candidates. Neither completion nor source revision starts/replaces a runtime.</remarks>
public sealed class GameBuildCompiler : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly string _hostId, _root;
    private readonly TimeSpan _timeout;
    private readonly CancellationTokenSource _closing = new();
    private Task<GameBuildArtifact>? _job;
    private Task? _dispose;
    private OwnedJob? _owned;
    private bool _closed;
    /// <summary>Creates a compiler with a dedicated artifact root and a maximum 120-second build budget.</summary>
    public GameBuildCompiler(string hostContractId, string artifactRoot, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        new SessionIdentity(Guid.NewGuid(), 1, hostContractId, hostContractId, hostContractId).Validate();
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromSeconds(120)) throw new ArgumentOutOfRangeException(nameof(timeout));
        _root = Path.GetFullPath(artifactRoot); _hostId = hostContractId;
    }
    /// <summary>Gets the current owned compiler PID, or null after reap.</summary>
    public int? ProcessId { get { lock (_gate) return _owned?.Process?.Id; } }
    /// <summary>Gets the last job directory containing persistent compiler diagnostics.</summary>
    public string? DiagnosticsDirectory { get; private set; }
    /// <summary>Builds a candidate into owned staging, using the currently loaded Aether DLL as the frozen contract.</summary>
    /// <remarks>The project must consume AetherContractPath. ProjectReference builds and shared build servers are disabled.</remarks>
    public Task<GameBuildArtifact> BuildAsync(GameBuildRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectPath);
        var project = Path.GetFullPath(request.ProjectPath);
        if (!File.Exists(project) || !project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected an existing game csproj.", nameof(request));
        if (request.SourceRevision < 0 || string.IsNullOrWhiteSpace(request.EntryType)) throw new ArgumentException("Invalid game revision/entry type.", nameof(request));
        var dependencies = request.Dependencies?.ToArray() ?? [];
        GameBuildArtifact.ValidateNames(new[] { request.EntryAssembly }.Concat(dependencies).ToArray());
        var snapshot = request with { ProjectPath = project, Dependencies = dependencies };
        TaskCompletionSource<GameBuildArtifact> completion;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this); cancellationToken.ThrowIfCancellationRequested();
            if (_job is { IsCompleted: false } || _owned != null) throw new InvalidOperationException("A compiler job/cleanup is already active.");
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _job = completion.Task;
        }
        _ = CompleteAsync(snapshot, cancellationToken, completion);
        return completion.Task;
    }
    private async Task CompleteAsync(GameBuildRequest request, CancellationToken caller, TaskCompletionSource<GameBuildArtifact> completion)
    {
        try { completion.TrySetResult(await RunAsync(request, caller).ConfigureAwait(false)); }
        catch (OperationCanceledException) { completion.TrySetCanceled(caller.IsCancellationRequested ? caller : _closing.Token); }
        catch (Exception error) { completion.TrySetException(error); }
    }
    private async Task<GameBuildArtifact> RunAsync(GameBuildRequest request, CancellationToken caller)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(caller, _closing.Token, timeout.Token);
        var job = Path.Combine(_root, "jobs", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
        DiagnosticsDirectory = job;
        var output = Path.Combine(job, "output"); var work = Path.Combine(job, "work");
        var owned = new OwnedJob(job); lock (_gate) _owned = owned;
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(request.ProjectPath)!,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "build", request.ProjectPath, "--configuration", "Release", "--framework", "net10.0",
            "--output", output, "--artifacts-path", work, "--verbosity", "quiet", "--disable-build-servers",
            "-property:BuildProjectReferences=false", "-property:UseSharedCompilation=false", "-nodeReuse:false",
            "-property:AetherContractPath=" + typeof(IGameSession).Assembly.Location }) info.ArgumentList.Add(argument);
        try
        {
            end.Token.ThrowIfCancellationRequested();
            var process = Process.Start(info) ?? throw new IOException("Compiler process did not start.");
            lock (_gate) owned.Process = process;
            owned.Stdout = DrainAsync(process.StandardOutput, Path.Combine(job, "build.stdout.log"));
            owned.Stderr = DrainAsync(process.StandardError, Path.Combine(job, "build.stderr.log"));
            await process.WaitForExitAsync(end.Token).ConfigureAwait(false);
            end.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new GameBuildException("Game compilation failed.", process.ExitCode, job);
            var allowed = new[] { request.EntryAssembly }.Concat(request.Dependencies!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (Directory.EnumerateFiles(output, "*.dll").Any(p => !allowed.Contains(Path.GetFileName(p))))
                throw new InvalidDataException("Compiler output contains undeclared DLLs; use a non-copying frozen Aether reference.");
            return GameBuildArtifact.Publish(output, _root, _hostId, request.EntryAssembly, request.EntryType,
                request.SourceRevision, request.Dependencies, end.Token);
        }
        catch (OperationCanceledException error) when (timeout.IsCancellationRequested && !caller.IsCancellationRequested && !_closing.IsCancellationRequested)
        { throw new TimeoutException("Game build exceeded its compiler deadline.", error); }
        finally { await CleanupAsync(owned).ConfigureAwait(false); }
    }
    private static async Task DrainAsync(StreamReader reader, string path)
    {
        await using var writer = new StreamWriter(path); var buffer = new char[4096]; var remaining = 1024 * 1024;
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
        {
            var keep = Math.Min(count, remaining);
            if (keep != 0) { await writer.WriteAsync(buffer.AsMemory(0, keep)).ConfigureAwait(false); remaining -= keep; }
        }
    }
    private async Task CleanupAsync(OwnedJob owned)
    {
        if (owned.Process is { } process)
        {
            await ReapAsync(process).ConfigureAwait(false);
            await Task.WhenAll(owned.Stdout, owned.Stderr).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            lock (_gate) owned.Process = null;
            process.Dispose();
        }
        GameBuildArtifact.DeleteOwned(Path.Combine(owned.Directory, "output"), owned.Directory);
        GameBuildArtifact.DeleteOwned(Path.Combine(owned.Directory, "work"), owned.Directory);
        lock (_gate) { if (_owned == owned) _owned = null; }
    }
    private static async Task ReapAsync(Process process)
    {
        if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) when (process.HasExited) { } }
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        catch (TimeoutException error) { throw new IOException("Owned compiler kill/reap failed; cleanup must be retried.", error); }
    }
    internal void CloseAdmission() { lock (_gate) { _closed = true; _closing.Cancel(); } }
    /// <summary>Fences new jobs, cancels and reaps the owned compiler, and waits for diagnostics/staging cleanup.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            CloseAdmission();
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            return new(_dispose = DisposeCoreAsync(_job));
        }
    }
    private async Task DisposeCoreAsync(Task<GameBuildArtifact>? job)
    {
        if (job != null) { try { await job.ConfigureAwait(false); } catch { /* Build errors belong to their caller; process ownership is checked below. */ } }
        OwnedJob? owned; lock (_gate) owned = _owned;
        if (owned != null) await CleanupAsync(owned).ConfigureAwait(false);
    }
    /// <summary>Synchronously completes the same owned cleanup.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    private sealed class OwnedJob(string directory)
    {
        internal readonly string Directory = directory;
        internal Process? Process;
        internal Task Stdout = Task.CompletedTask, Stderr = Task.CompletedTask;
    }
}
