using System.Diagnostics;
using TrueMoon.Aether.Sessions;
using TrueMoon.Diagnostics;
using System.Text.Json;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Diagnostics;

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
    private readonly IEventsSource? _events;
    private readonly IDiagnosticTelemetry? _telemetry;
    private readonly BuildDiagnosticStore _diagnostics;
    private ProcessOutputCapture? _stdout, _stderr;
    private readonly Queue<ProcessDiagnosticSnapshot> _completedDiagnostics = new();

    /// <summary>Returns at most16 completed jobs plus the active job, bounded by the requested count/tail window; no files are read.</summary>
    public IReadOnlyList<ProcessDiagnosticSnapshot> ReadDiagnostics(int maximumCount, int maximumOutputCharacters)
    {
        ProcessDiagnosticSnapshot.ValidateLimits(maximumCount, maximumOutputCharacters);
        lock (_gate)
        {
            var records = _completedDiagnostics.Select(item => item.Limit(maximumOutputCharacters));
            if (_owned is { } current) records = records.Append(current.Snapshot(false, maximumOutputCharacters));
            return Array.AsReadOnly(records.TakeLast(maximumCount).Reverse().ToArray());
        }
    }
    /// <summary>Last best-effort retention pass. Active, unrecognized and incomplete directories are preserved.</summary>
    public GameBuildRetentionStatus? RetentionStatus { get; private set; }
    /// <summary>Live bounded compiler stdout tail, file loss and error counters.</summary>
    public ProcessOutputSnapshot? StandardOutputStatus => _stdout?.Read();
    /// <summary>Live bounded compiler stderr tail, file loss and error counters.</summary>
    public ProcessOutputSnapshot? StandardErrorStatus => _stderr?.Read();
    /// <summary>Creates a compiler with a dedicated artifact root and a maximum 120-second build budget.</summary>
    public GameBuildCompiler(string hostContractId, string artifactRoot, TimeSpan? timeout = null)
        : this(hostContractId, artifactRoot, timeout, null) { }

    /// <summary>Creates a compiler borrowing an injected diagnostic source.</summary>
    /// <param name="hostContractId">Frozen host identity.</param><param name="artifactRoot">Owned artifact root.</param>
    /// <param name="timeout">Compiler deadline.</param><param name="events">Borrowed diagnostics.</param>
    public GameBuildCompiler(string hostContractId, string artifactRoot, TimeSpan? timeout, IEventsSource? events)
        : this(hostContractId, artifactRoot, timeout, events, null) { }

    /// <summary>Creates a compiler with snapshotted completed-job retention limits.</summary>
    /// <param name="hostContractId">Frozen host identity.</param><param name="artifactRoot">Owned artifact root.</param>
    /// <param name="timeout">Compiler deadline.</param><param name="events">Borrowed diagnostics.</param>
    /// <param name="retention">Limits copied before resource creation; null selects the defaults.</param>
    public GameBuildCompiler(string hostContractId, string artifactRoot, TimeSpan? timeout, IEventsSource? events,
        GameBuildRetentionOptions? retention)
        : this(hostContractId, artifactRoot, timeout, events, retention, null) { }

    /// <summary>Creates a compiler borrowing App-owned events and operation telemetry.</summary>
    public GameBuildCompiler(string hostContractId, string artifactRoot, TimeSpan? timeout, IEventsSource? events,
        GameBuildRetentionOptions? retention, IDiagnosticTelemetry? telemetry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        new SessionIdentity(Guid.NewGuid(), 1, hostContractId, hostContractId, hostContractId).Validate();
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromSeconds(120)) throw new ArgumentOutOfRangeException(nameof(timeout));
        _root = Path.GetFullPath(artifactRoot); _hostId = hostContractId; _events = events; _telemetry = telemetry;
        _diagnostics = new(_root, retention ?? new());
    }
    /// <summary>Gets the current owned compiler PID, or null after reap.</summary>
    public int? ProcessId { get { lock (_gate) return _owned?.Process?.Id; } }
    /// <summary>Gets the last job directory; completed diagnostics remain subject to the root's retention policy.</summary>
    public string? DiagnosticsDirectory { get; private set; }
    /// <summary>Builds a candidate into owned staging, using the currently loaded Aether DLL as the frozen contract.</summary>
    /// <remarks>The project must consume AetherContractPath. ProjectReference builds and shared build servers are disabled.
    /// Each single-project job owns its artifact paths, so intermediate paths omit the redundant project-name segment.</remarks>
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
        using var operation = AetherDiagnosticEvents.Start(_telemetry, "Aether.Build", () =>
            new Dictionary<string, string?> { ["SourceRevision"] = request.SourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        try
        {
            var result = await RunAsync(request, caller).ConfigureAwait(false);
            Emit("Build.Completed", DiagnosticLevel.Information);
            operation?.Complete(); operation?.Dispose();
            completion.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            Emit("Build.Cancelled", DiagnosticLevel.Warning);
            operation?.Complete(DiagnosticOperationOutcome.Cancelled); operation?.Dispose();
            completion.TrySetCanceled(caller.IsCancellationRequested ? caller : _closing.Token);
        }
        catch (Exception error)
        {
            Emit("Build.Failed", DiagnosticLevel.Error, error);
            operation?.Complete(DiagnosticOperationOutcome.Failed, error); operation?.Dispose(); completion.TrySetException(error);
        }
        void Emit(string name, DiagnosticLevel level, Exception? error = null)
            => AetherDiagnosticEvents.Emit(_events, level, name switch { "Build.Completed" => 1201, "Build.Cancelled" => 1202, _ => 1203 }, name, "Game build operation", () =>
                new Dictionary<string, string?> { ["SourceRevision"] = request.SourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["JobId"] = DiagnosticsDirectory is null ? null : Path.GetFileName(DiagnosticsDirectory) }, error);
    }
    private async Task<GameBuildArtifact> RunAsync(GameBuildRequest request, CancellationToken caller)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(caller, _closing.Token, timeout.Token);
        DiagnosticsDirectory = null; _stdout = null; _stderr = null;
        // Directory maintenance can inspect previous jobs; keep it off the caller/UI thread.
        var lease = await Task.Run(_diagnostics.Begin).ConfigureAwait(false);
        var job = lease.Directory;
        DiagnosticsDirectory = job;
        var owned = new OwnedJob(lease) { SourceRevision = request.SourceRevision }; lock (_gate) _owned = owned;
        AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Information, 1200, "Build.Starting", "Game build operation", () =>
            new Dictionary<string, string?> { ["SourceRevision"] = request.SourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["JobId"] = Path.GetFileName(job) });
        var output = Path.Combine(job, "output"); var work = Path.Combine(job, "work");
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(request.ProjectPath)!,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "build", request.ProjectPath, "--configuration", "Release", "--framework", "net10.0",
            "--output", output, "--artifacts-path", work, "--verbosity", "quiet", "--disable-build-servers",
            "-property:IncludeProjectNameInArtifactsPaths=false",
            "-property:BuildProjectReferences=false", "-property:UseSharedCompilation=false", "-nodeReuse:false",
            "-property:AetherContractPath=" + typeof(IGameSession).Assembly.Location,
            "-property:TrueMoonContractsPath=" + typeof(IFactory<>).Assembly.Location }) info.ArgumentList.Add(argument);
        try
        {
            UpdateRetention(job);
            end.Token.ThrowIfCancellationRequested();
            var process = Process.Start(info) ?? throw new IOException("Compiler process did not start.");
            lock (_gate) { owned.Process = process; owned.ProcessId = process.Id; }
            owned.Output = new(process.StandardOutput, process.StandardError, null, job, "build");
            _stdout = owned.Output.Stdout; _stderr = owned.Output.Stderr;
            await process.WaitForExitAsync(end.Token).ConfigureAwait(false);
            end.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new GameBuildException("Game compilation failed.", process.ExitCode, job);
            var allowed = new[] { request.EntryAssembly }.Concat(request.Dependencies!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (Directory.EnumerateFiles(output, "*.dll").Any(p => !allowed.Contains(Path.GetFileName(p))))
                throw new InvalidDataException("Compiler output contains undeclared DLLs; use a non-copying frozen Aether reference.");
            var artifact = GameBuildArtifact.Publish(output, _root, _hostId, request.EntryAssembly, request.EntryType,
                request.SourceRevision, request.Dependencies, end.Token);
            owned.GameBuildId = artifact.BuildId;
            return artifact;
        }
        catch (OperationCanceledException error) when (timeout.IsCancellationRequested && !caller.IsCancellationRequested && !_closing.IsCancellationRequested)
        { throw new TimeoutException("Game build exceeded its compiler deadline.", error); }
        finally { await CleanupAsync(owned).ConfigureAwait(false); }
    }
    private async Task CleanupAsync(OwnedJob owned)
    {
        if (owned.Process is { } process)
        {
            await ReapAsync(process).ConfigureAwait(false);
            if (owned.Output != null) await owned.Output.DrainAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            // Capture failures/truncation remain observable independently of the compiler's exit outcome.
            try
            {
                File.WriteAllText(Path.Combine(owned.Directory, "build.output.json"), JsonSerializer.Serialize(new
                    { StandardOutput = StandardOutputStatus, StandardError = StandardErrorStatus }));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Warning, 1204, "Build.DiagnosticsWriteFailed",
                    "Could not persist build output status", () => new Dictionary<string, string?>(), error);
            }
            lock (_gate) owned.Process = null;
            process.Dispose();
        }
        GameBuildArtifact.DeleteOwned(Path.Combine(owned.Directory, "output"), owned.Directory);
        GameBuildArtifact.DeleteOwned(Path.Combine(owned.Directory, "work"), owned.Directory);
        Exception? completionError = null;
        try { owned.Diagnostics.Complete(DateTimeOffset.UtcNow); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            completionError = error;
            AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Warning, 1204, "Build.DiagnosticsWriteFailed",
                "Could not complete build diagnostics ownership metadata", () => new Dictionary<string, string?>(), error);
        }
        UpdateRetention(owned.Directory, completionError);
        lock (_gate)
        {
            if (_owned == owned)
            {
                _completedDiagnostics.Enqueue(owned.Snapshot(true, 32768));
                while (_completedDiagnostics.Count > 16) _completedDiagnostics.Dequeue();
                _owned = null;
            }
        }
    }
    private void UpdateRetention(string currentJob, Exception? completionError = null)
    {
        var status = _diagnostics.Sweep(DateTimeOffset.UtcNow, currentJob);
        if (completionError != null)
        {
            var summary = completionError.GetType().Name + ": " + completionError.Message;
            status = status with { Failures = status.Failures + 1, LastError = summary.Length <= 1024 ? summary : summary[..1024] };
        }
        RetentionStatus = status;
        if (status.Failures == 0 && !status.IsOverBudget && !status.MaintenanceBusy) return;
        AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Warning, 1205, "Build.RetentionLimited",
            "Build diagnostics retention could not satisfy all limits", () => new Dictionary<string, string?>
            {
                ["JobId"] = Path.GetFileName(currentJob),
                ["RetainedJobs"] = status.RetainedJobs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RetainedBytes"] = status.RetainedBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["ActiveJobs"] = status.ActiveJobs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["SkippedDirectories"] = status.SkippedDirectories.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Failures"] = status.Failures.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["IsOverBudget"] = status.IsOverBudget.ToString(), ["MaintenanceBusy"] = status.MaintenanceBusy.ToString(),
                ["Error"] = status.LastError
            });
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
    private sealed class OwnedJob(BuildDiagnosticStore.Job diagnostics)
    {
        internal readonly DateTimeOffset StartedUtc = DateTimeOffset.UtcNow;
        internal int? ProcessId;
        internal long SourceRevision;
        internal string? GameBuildId;
        internal readonly BuildDiagnosticStore.Job Diagnostics = diagnostics;
        internal string Directory => Diagnostics.Directory;
        internal Process? Process;
        internal OwnedProcessOutput? Output;
        internal ProcessDiagnosticSnapshot Snapshot(bool completed, int maximumOutputCharacters) => new(
            "build:" + Path.GetFileName(Directory), "Build", StartedUtc, ProcessId, null, null, null, GameBuildId,
            Path.GetFileName(Directory), SourceRevision, completed, completed,
            ProcessStreamDiagnostic.Read(Output?.Stdout, maximumOutputCharacters), ProcessStreamDiagnostic.Read(Output?.Stderr, maximumOutputCharacters), null);
    }
}
