using TrueMoon.Services;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Diagnostics;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime.Hosting;

/// <summary>Configures the App-owned engine session without starting a game.</summary>
public class RuntimeOptions
{
    /// <summary>Gets or sets the SHA256 identity of the matching host/contracts build.</summary>
    public string HostContractId { get; set; } = "";
    /// <summary>Gets or sets the SHA256 identity of the selected game build.</summary>
    public string GameBuildId { get; set; } = "";
    /// <summary>Gets or sets the report directory, resolved during registration.</summary>
    public string ReportDirectory { get; set; } = "";
    /// <summary>Independent child diagnostic lane limits, copied during registration.</summary>
    public ChildDiagnosticDeliveryOptions ChildDiagnostics { get; set; } = new();
    /// <summary>Gets or sets bounded command, startup and heartbeat deadlines.</summary>
    public SessionDeadlines Deadlines { get; set; } = new();
    /// <summary>Gets or sets the snapshotted replacement/recovery limits, with total at most 45 seconds.</summary>
    public ReplacementDeadlines ReplacementDeadlines { get; set; } = new();
    /// <summary>Gets or sets an optional session status observer.</summary>
    public Action<SessionStatus>? StatusChanged { get; set; }
    /// <summary>Optional App-owned viewport strategy factory. Null selects built-in CPU transport.</summary>
    /// <remarks>Keep allocation lazy until Connect. The strategy borrows the supplied GPU owner; FrameSession owns
    /// its connections and any disposable strategy. Do not register disposable aliases. UI/context callbacks remain borrowed.</remarks>
    public Func<GpuResourceHost, IGameViewportTransport>? ViewportTransport { get; set; }
    /// <summary>Creates the App-local viewport factory. Its results belong to FrameSession.</summary>
    /// <remarks>Choose this or ViewportTransport. The factory may borrow application services.</remarks>
    public Func<IServiceResolver, IFactory<IGameViewportTransport, GpuResourceHost>>? ViewportTransportFactory { get; set; }
    /// <summary>Explicit immutable native runtime command. Null permits headless/common services but native Start fails without a custom factory.</summary>
    public NativeRuntimeLaunch? RuntimeLaunch { get; set; }
    /// <summary>Gets or sets a factory creating a fresh App-owned runtime factory.</summary>
    /// <remarks>Dependencies may be resolved from the supplied resolver. Do not return
    /// a borrowed or separately registered disposable factory: the App provider owns the result.</remarks>
    public Func<IServiceResolver, IRuntimeFactory> RuntimeFactory { get; set; } = static resolver =>
        new NativeRuntimeFactory(resolver.Resolve<IFactory<PipesInvocationClient<IEngineControl>, string>>(),
            resolver.Resolve<IEventsSource<NativeRuntimeFactory>>(),
            resolver.TryResolve<IDiagnosticRecordImporter>() == null ? null : resolver.Resolve<IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>>(),
            resolver.Resolve<ChildDiagnosticConfiguration>().Copy(), resolver.TryResolve<NativeRuntimeLaunch>());

    internal RuntimeOptions Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ReportDirectory);
        ArgumentNullException.ThrowIfNull(Deadlines);
        ArgumentNullException.ThrowIfNull(ReplacementDeadlines);
        ArgumentNullException.ThrowIfNull(RuntimeFactory);
        ArgumentNullException.ThrowIfNull(ChildDiagnostics);
        var childDiagnostics = ChildDiagnostics.Snapshot();
        if (ViewportTransport != null && ViewportTransportFactory != null)
            throw new ArgumentException("Configure one viewport transport factory.");
        new SessionIdentity(Guid.NewGuid(), 1, HostContractId, GameBuildId, new string('0', 64)).Validate();
        Deadlines.Validate();
        ReplacementDeadlines.Validate();
        return new()
        {
            HostContractId = HostContractId, GameBuildId = GameBuildId,
            ReportDirectory = Path.GetFullPath(ReportDirectory), Deadlines = Deadlines with { },
            ReplacementDeadlines = ReplacementDeadlines with { },
            ChildDiagnostics = childDiagnostics,
            StatusChanged = StatusChanged, RuntimeFactory = RuntimeFactory, RuntimeLaunch = RuntimeLaunch, ViewportTransport = ViewportTransport,
            ViewportTransportFactory = ViewportTransportFactory
        };
    }
}
