using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Engine.Probe.Contracts;
using TrueMoon.Generated.EngineSessionProbe;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;

namespace EngineSessionProbe;

public static class BuildIdentity
{
    public static string HostId => Manifest([typeof(BuildIdentity).Assembly.Location, typeof(SessionIdentity).Assembly.Location,
        typeof(PipesInvocationClient<>).Assembly.Location, typeof(ChildProcessUnitHandle).Assembly.Location]);
    // Editor fingerprints bytes without loading Game.Logic into its CLR. Only the
    // child creates/resolves game types; a future replacement must not pin a DLL in Editor.
    public static string GameId => Manifest([Path.Combine(AppContext.BaseDirectory, "Game.Logic.dll")]);
    private static string Manifest(IEnumerable<string> files)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", files.OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => Path.GetFileName(path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))))));
}

public sealed class NativeRuntimeFactory : IRuntimeFactory
{
    public ConcurrentQueue<UnitProcessDiagnostic> Diagnostics { get; } = new();
    public List<NativeRuntimeLease> Leases { get; } = [];
    public IRuntimeLease Create(SessionLaunch launch)
    {
        var lease = new NativeRuntimeLease(launch, item => { if (item is UnitProcessDiagnostic value) Diagnostics.Enqueue(value); });
        Leases.Add(lease); return lease;
    }
}

internal sealed class EngineChild(SessionLaunch launch, Action<object> observe) : ChildProcessUnitHandle(
    new UnitConfiguration(1) { RestartPolicy = UnitRestartPolicy.Never, RestartLimit = 0, IsControlAppLifetime = false, TerminationDelay = 3000 },
    new ProbeEvents<EngineChild>(observe))
{
    internal Process? OwnedProcess => Process;
    protected override async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = await base.GetProcessStartInfoAsync(cancellationToken).ConfigureAwait(false);
        info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        if (launch.Behavior == "launch-failure") info.FileName = Path.Combine(AppContext.BaseDirectory, "missing-engine-owned.exe");
        return info;
    }
    protected override void ProcessLaunchArguments(Collection<string> arguments) => arguments.Add("--runtime");
    protected override void AddEnvironmentVariables(ProcessStartInfo info)
    {
        base.AddEnvironmentVariables(info);
        info.Environment[RuntimeHost.BootstrapKey] = JsonSerializer.Serialize(launch);
        if (launch.Behavior is "ignore-stop" or "hang-stop") info.Environment.Remove(UnitParentProcessEventsHandler.ControlPipeKey);
    }
}

public sealed class NativeRuntimeLease(SessionLaunch launch, Action<object> observe) : IRuntimeLease
{
    private readonly EngineChild _child = new(launch, observe);
    private PipesInvocationClient<IEngineControl>? _client;
    private IEngineControl? _service;
    private Task<string>? _stdout, _stderr;
    public int ProcessId { get; private set; }
    public Task Exited => _child.Completion;
    public bool ResourcesReleased { get; private set; }
    public bool Reaped { get; private set; }
    public string StandardError { get; private set; } = "";
    public string StandardOutput { get; private set; } = "";
    public async Task LaunchAsync(CancellationToken token)
    {
        if (await _child.StartAsync(token).ConfigureAwait(false) != true) throw new IOException("Owned child did not start.");
        var process = _child.OwnedProcess ?? throw new IOException("Missing owned process.");
        ProcessId = process.Id;
        // Drain immediately so even a verbose/failing runtime cannot block on a full pipe.
        _stdout = process.StandardOutput.ReadToEndAsync(); _stderr = process.StandardError.ReadToEndAsync();
        _client = new(new ProbeEvents<PipesInvocationClient<IEngineControl>>(), launch.Endpoint);
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
        if (_stdout != null) StandardOutput = await _stdout.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        if (_stderr != null) StandardError = await _stderr.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        await _child.DisposeAsync().ConfigureAwait(false);
        ResourcesReleased = true;
    }
    public ValueTask DisposeAsync() => new(StopAsync());
}
