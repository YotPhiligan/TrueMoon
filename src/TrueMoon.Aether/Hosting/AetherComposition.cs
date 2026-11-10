using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Services;

namespace TrueMoon.Aether.Hosting;

/// <summary>Explicit identity and borrowed construction callbacks, never serialized by sources.</summary>
public sealed record AetherComposition
{
    /// <summary>Exact host/contracts fingerprint.</summary>
    public string HostContractId { get; init; } = "";
    /// <summary>Exact game fingerprint or explicit preview identity.</summary>
    public string GameBuildId { get; init; } = "";
    /// <summary>Immutable runtime command.</summary>
    public NativeRuntimeLaunch? RuntimeLaunch { get; init; }
    /// <summary>Creates one App-owned runtime factory.</summary>
    public Func<IServiceResolver, IRuntimeFactory>? RuntimeFactory { get; init; }
    /// <summary>Borrowed observer.</summary>
    public Action<SessionStatus>? StatusChanged { get; init; }
    /// <summary>Borrowed viewport strategy construction.</summary>
    public Func<GpuResourceHost, IGameViewportTransport>? ViewportTransport { get; init; }
    /// <summary>Borrowed DI-aware viewport factory construction.</summary>
    public Func<IServiceResolver, IFactory<IGameViewportTransport, GpuResourceHost>>? ViewportTransportFactory { get; init; }
    /// <summary>Applies composition without altering user settings.</summary>
    /// <param name="options">Engine options.</param>
    public void ApplyTo(AetherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.HostContractId = HostContractId; options.GameBuildId = GameBuildId; options.RuntimeLaunch = RuntimeLaunch;
        if (RuntimeFactory is not null) options.RuntimeFactory = RuntimeFactory;
        options.StatusChanged = StatusChanged; options.ViewportTransport = ViewportTransport; options.ViewportTransportFactory = ViewportTransportFactory;
    }
}
