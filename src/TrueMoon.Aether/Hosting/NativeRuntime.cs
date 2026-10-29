using System.Collections.Concurrent;

using System.Diagnostics;

using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Builds;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;
using TrueMoon.Mithril;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;
using TrueMoon.Aether.Diagnostics;

namespace TrueMoon.Aether.Hosting;


public sealed class NativeRuntimeFactory : IRuntimeFactory, IRuntimeDiagnostics
{
    private readonly ConcurrentQueue<NativeRuntimeLease> _diagnosticLeases = new();
    private readonly IFactory<PipesInvocationClient<IEngineControl>, string> _clients;
    private readonly IEventsSource<NativeRuntimeFactory>? _events;
    private readonly IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>? _diagnostics;
    private readonly ChildDiagnosticDeliveryOptions _deliveryOptions;
    private readonly NativeRuntimeLaunch? _runtime;
    /// <summary>Creates a standalone factory with an explicit runtime command; leases own all process resources.</summary>
    /// <param name="runtime">Immutable command including its managed entry assembly.</param>
    public NativeRuntimeFactory(NativeRuntimeLaunch runtime)
        : this(new PipesInvocationClientFactory<IEngineControl>(new AetherEvents<PipesInvocationClient<IEngineControl>>()), null, null, null,
            runtime ?? throw new ArgumentNullException(nameof(runtime))) { }
    /// <summary>Creates the standalone native factory with default diagnostics.</summary>
    public NativeRuntimeFactory() : this(new PipesInvocationClientFactory<IEngineControl>(new AetherEvents<PipesInvocationClient<IEngineControl>>())) { }
    /// <summary>Creates leases with an injected endpoint client factory. The lease owns each client.</summary>
    /// <param name="clients">Borrowed client factory.</param>
    public NativeRuntimeFactory(IFactory<PipesInvocationClient<IEngineControl>, string> clients)
        : this(clients, null) { }
    /// <summary>Creates leases borrowing the endpoint factory and the App-owned diagnostic source.</summary>
    /// <param name="clients">Borrowed endpoint client factory.</param>
    /// <param name="events">Borrowed diagnostic source.</param>
    public NativeRuntimeFactory(IFactory<PipesInvocationClient<IEngineControl>, string> clients,
        IEventsSource<NativeRuntimeFactory>? events)
        : this(clients, events, null, null) { }
    /// <summary>Creates leases borrowing an optional host receiver factory; products belong to each lease.</summary>
    /// <param name="clients">Control clients.</param><param name="events">Process events.</param>
    /// <param name="diagnostics">Optional receiver factory.</param><param name="options">Copied lane limits.</param>
    /// <param name="runtime">Explicit runtime command; null allows configuration without children but rejects launch.</param>
    public NativeRuntimeFactory(IFactory<PipesInvocationClient<IEngineControl>, string> clients,
        IEventsSource<NativeRuntimeFactory>? events, IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>? diagnostics,
        ChildDiagnosticDeliveryOptions? options = null, NativeRuntimeLaunch? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(clients); _clients = clients; _events = events; _diagnostics = diagnostics; _runtime = runtime;
        _deliveryOptions = (options ?? new()).Snapshot();
    }
    /// <summary>Process-local bootstrap passed to an owned game runtime.</summary>
    public const string BootstrapKey = "TM_ENGINE_BOOTSTRAP";
    public ConcurrentQueue<UnitProcessDiagnostic> Diagnostics { get; } = new();
    public List<NativeRuntimeLease> Leases { get; } = [];
    public IRuntimeLease Create(SessionLaunch launch)
    {
        launch = launch with { DiagnosticDelivery = _diagnostics != null && _deliveryOptions.Enabled ? _deliveryOptions.Snapshot() : null };
        var lease = new NativeRuntimeLease(launch, item =>
        {
            if (item is not UnitProcessDiagnostic value) return;
            Diagnostics.Enqueue(value);
            while (Diagnostics.Count > 256) Diagnostics.TryDequeue(out _);
            AetherDiagnosticEvents.Process(_events, launch, value);
        }, _clients, _diagnostics, _runtime);
        Leases.Add(lease);
        _diagnosticLeases.Enqueue(lease);
        while (_diagnosticLeases.Count > 64) _diagnosticLeases.TryDequeue(out _);
        // This is bounded diagnostic tracking; the supervisor retains actual lease ownership.
        if (Leases.Count > 64) Leases.RemoveAt(0);
        return lease;
    }

    /// <summary>Returns bounded recent memory snapshots, safe to read concurrently with process creation/reap.</summary>
    public IReadOnlyList<ProcessDiagnosticSnapshot> ReadDiagnostics(int maximumCount, int maximumOutputCharacters)
    {
        ProcessDiagnosticSnapshot.ValidateLimits(maximumCount, maximumOutputCharacters);
        return Array.AsReadOnly(_diagnosticLeases.ToArray().TakeLast(maximumCount).Reverse()
            .Select(lease => lease.ReadDiagnostics(maximumOutputCharacters)).ToArray());
    }
}

internal sealed class EngineChild(SessionLaunch launch, Action<object> observe, NativeRuntimeLaunch? runtime) : ProcessUnitHandle(
    new UnitConfiguration(1) { RestartPolicy = UnitRestartPolicy.Never, RestartLimit = 0, IsControlAppLifetime = false, TerminationDelay = 3000 },
    new AetherEvents<EngineChild>(observe))
{
    internal Process? OwnedProcess => Process;
    protected override Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (runtime == null) throw new InvalidOperationException("Configure AetherOptions.RuntimeLaunch or an explicit native runtime factory before starting a child.");
        BuildIdentity.ValidateHost(launch);
        var info = runtime.StartInfo();
        if (launch.Behavior == "launch-failure") info.FileName = Path.Combine(AppContext.BaseDirectory, "missing-engine-owned.exe");
        return Task.FromResult(info);
    }
    protected override void AddEnvironmentVariables(ProcessStartInfo info)
    {
        base.AddEnvironmentVariables(info);
        info.Environment[NativeRuntimeFactory.BootstrapKey] = RuntimeBootstrap.Encode(launch,
            allowLegacyGame: launch.RuntimeIdentity is { } identity && BuildIdentity.SamePath(identity.HostAssemblyPath, identity.RuntimeAssemblyPath));
        if (launch.Behavior is "ignore-stop" or "hang-stop") info.Environment.Remove(UnitParentProcessEventsHandler.ControlPipeKey);
    }
}

public sealed class NativeRuntimeLease : IRuntimeLease
{
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private readonly SessionLaunch launch;
    private readonly IFactory<PipesInvocationClient<IEngineControl>, string>? clients;
    private readonly IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>? _diagnostics;
    /// <summary>Creates a caller-owned lease with legacy local-only capture.</summary>
    /// <param name="launch">Launch.</param><param name="observe">Process observer.</param><param name="clients">Borrowed control factory.</param>
    public NativeRuntimeLease(SessionLaunch launch, Action<object> observe, IFactory<PipesInvocationClient<IEngineControl>, string>? clients = null)
        : this(launch, observe, clients, null) { }
    /// <summary>Creates a lease owning an optional generation-scoped receiver through process reap.</summary>
    /// <param name="launch">Launch and copied lane limits.</param><param name="observe">Process observer.</param>
    /// <param name="clients">Borrowed control factory.</param><param name="diagnostics">Borrowed receiver factory.</param>
    /// <param name="runtime">Explicit immutable runtime command; null rejects native launch.</param>
    public NativeRuntimeLease(SessionLaunch launch, Action<object> observe, IFactory<PipesInvocationClient<IEngineControl>, string>? clients,
        IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>? diagnostics, NativeRuntimeLaunch? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(launch); ArgumentNullException.ThrowIfNull(observe);
        this.launch = launch with
        {
            DiagnosticDelivery = launch.DiagnosticDelivery?.Snapshot(),
            RuntimeIdentity = runtime == null ? launch.RuntimeIdentity : BuildIdentity.CurrentLaunchIdentity(runtime.RuntimeAssemblyPath)
        };
        this.clients = clients; _diagnostics = diagnostics; _child = new(this.launch, observe, runtime);
    }
    /// <summary>Identity fixed when this owned process lease was created.</summary>
    public SessionIdentity Identity => launch.Identity;
    internal string GpuEndpoint => launch.Endpoint + "-gpu";
    internal Microsoft.Win32.SafeHandles.SafeProcessHandle GpuProcessHandle =>
        !ResourcesReleased && !Exited.IsCompleted ? _child.OwnedProcess?.SafeHandle ?? throw new IOException("Missing owned GPU runtime process.")
            : throw new IOException("GPU runtime is no longer alive.");
    private readonly EngineChild _child;
    private ChildDiagnosticReceiver? _diagnosticReceiver;
    private ChildDiagnosticStatus? _diagnosticStatus;
    /// <summary>Live or final independent receiver health; null for legacy local-only capture.</summary>
    public ChildDiagnosticStatus? ChildDiagnosticsStatus => _diagnosticReceiver?.Read() ?? _diagnosticStatus;
    private PipesInvocationClient<IEngineControl>? _client;
    private IEngineControl? _service;
    private OwnedProcessOutput? _output;
    private IDisposable? _buildPin;
    public int ProcessId { get; private set; }
    public Task Exited => _child.Completion;
    public bool ResourcesReleased { get; private set; }
    public bool Reaped { get; private set; }
    /// <summary>Bounded live tail of stderr; stream choice does not determine event severity.</summary>
    public string StandardError => StandardErrorStatus?.Text ?? "";
    /// <summary>Bounded live tail of stdout, available before process exit.</summary>
    public string StandardOutput => StandardOutputStatus?.Text ?? "";
    /// <summary>Live stderr capture counters, including file failure and truncation.</summary>
    public ProcessOutputSnapshot? StandardErrorStatus => _output?.Stderr.Read();
    /// <summary>Live stdout capture counters, including file failure and truncation.</summary>
    public ProcessOutputSnapshot? StandardOutputStatus => _output?.Stdout.Read();

    internal ProcessDiagnosticSnapshot ReadDiagnostics(int maximumOutputCharacters)
    {
        var output = _output; var identity = Identity;
        return new($"runtime:{identity.SessionId:D}:{identity.Generation}", "Runtime", _startedUtc, ProcessId == 0 ? null : ProcessId,
            identity.SessionId, identity.Generation, launch.Role.ToString(), identity.GameBuildId, null, null, Reaped, ResourcesReleased,
            ProcessStreamDiagnostic.Read(output?.Stdout, maximumOutputCharacters), ProcessStreamDiagnostic.Read(output?.Stderr, maximumOutputCharacters), ChildDiagnosticsStatus);
    }
    public async Task LaunchAsync(CancellationToken token)
    {
        if (launch.Build != null)
            _buildPin = GameBuildArtifact.Open(launch.Build.DirectoryPath, launch.Identity.HostContractId, launch.Identity.GameBuildId).Pin();
        if (await _child.StartAsync(token).ConfigureAwait(false) != true) throw new IOException("Owned child did not start.");
        var process = _child.OwnedProcess ?? throw new IOException("Missing owned process.");
        ProcessId = process.Id;
        // Drain immediately so even a verbose/failing runtime cannot block on a full pipe.
        // Four fixed slots per stream bound normal-session disk retention to eight 1MiB files.
        var slot = (launch.Identity.Generation % 4).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var outputRoot = Path.Combine(launch.ReportDirectory, "runtime-output");
        _output = new(process.StandardOutput, process.StandardError, process.StandardInput, outputRoot, "runtime-" + slot);
        if (_diagnostics != null && launch.DiagnosticDelivery?.Enabled == true)
        {
            try { _diagnosticReceiver = _diagnostics.Create(new(launch, ProcessId)); }
            catch (Exception error)
            {
                var name = error.GetType().FullName; if (name?.Length > 256) name = name[..256];
                _diagnosticStatus = new(true, true, true, false, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, name);
            }
        }
        _client = (clients ?? new PipesInvocationClientFactory<IEngineControl>(new AetherEvents<PipesInvocationClient<IEngineControl>>())).Create(launch.Endpoint);
        _service = new EngineControlGeneratedImplementation(new BoundedControlClient(_client));
    }
    public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
    {
        while (!_client!.IsConnected)
        {
            if (Exited.IsCompleted) throw new IOException("Runtime exited before connecting.");
            await Task.Delay(10, token).ConfigureAwait(false);
        }
        return await _service!.SendAsync(request, token).ConfigureAwait(false);
    }
    public async Task StopAsync(CancellationToken gracefulDeadline = default)
    {
        if (ResourcesReleased) return;
        // Close the client first to fence late RPC replies; Mithril retains process ownership.
        _client?.Dispose(); _client = null; _service = null;
        if (await _child.StopAsync(gracefulDeadline).ConfigureAwait(false) != true) throw new IOException("Owned process kill/reap failed.");
        await _child.Completion.ConfigureAwait(false); Reaped = true;
        if (_output != null) await _output.DrainAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        if (_diagnosticReceiver != null)
        {
            await _diagnosticReceiver.StopAsync().ConfigureAwait(false);
            _diagnosticStatus = _diagnosticReceiver.Read(); _diagnosticReceiver = null;
        }
        await _child.DisposeAsync().ConfigureAwait(false);
        _buildPin?.Dispose(); _buildPin = null;
        ResourcesReleased = true;
    }
    public ValueTask DisposeAsync() => new(StopAsync());
}
