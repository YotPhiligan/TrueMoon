using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public sealed class GpuCapabilityView : View
{
    public Text Status { get; } = Text.Plain("Collecting read-only GPU capabilities…");
    public GpuCapabilityView() => Content(() => VStack.Create().Padding(16).WithChildren(
        Text.Plain("TrueMoon ENG4a — capability probe"), Status, Text.Plain("CPU transport remains selected. No GPU imports or shared allocations.")));
}

public static class GpuCapabilityProbe
{
    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(45)); var token = limit.Token;
        var factory = new NativeRuntimeFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(c =>
        {
            c.UseAether(o => { o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory; });
            c.UsePresentation<GpuCapabilityView>(o => { o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w => { w.Width = 800; w.Height = 260; w.Title = "TrueMoon ENG4a"; w.EnableDiagnostics = true; }); });
            c.Services(r => r.Transient<GpuCapabilityView>());
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!; var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        GpuProbeDecision? decision = null; GpuProducerCapabilities? producer = null; GpuConsumerCapabilities? consumer = null; object? glRaw = null;
        Exception? failure = null; var completed = false; GpuCapabilityView? view = null; JsonElement native = default;
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.FramePresented += ui => { view = (GpuCapabilityView)ui.Root; shown.TrySetResult(); };
        try
        {
            await app.StartAsync(token);
            await shown.Task.WaitAsync(token);
            window.Closing += async _ => { await host.DisposeAsync(); Probe.Check(view is { IsDisposed: false }, "GPU probe disposed UI before engine cleanup."); return true; };
            await host.Supervisor.StartAsync(authoring.Snapshot(), "gpu-capabilities", token); var identity = host.Supervisor.Status.Identity!;
            var file = Path.Combine(directory, $"gpu-producer-{identity.Generation}.json"); await Records.WaitAsync(file);
            if (new FileInfo(file).Length > 65536) throw new InvalidDataException("Oversized capability diagnostics.");
            native = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
            producer = native.GetProperty("Capabilities").Deserialize<GpuProducerCapabilities>() ?? throw new InvalidDataException("Missing producer report.");
            var collected = new TaskCompletionSource<(GpuConsumerCapabilities, object)>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(w =>
            {
                try
                {
                    var gl = w.NativeWindow.GLContext ?? throw new NotSupportedException("No current GL context."); gl.MakeCurrent();
                    collected.TrySetResult(GpuCapabilityCollector.OpenGl(identity, name => gl.TryGetProcAddress(name, out var address) ? address : 0));
                }
                catch (Exception error) { collected.TrySetException(error); }
            });
            (consumer, glRaw) = await collected.Task.WaitAsync(token);
            decision = GpuTransportProbe.Evaluate(identity, producer, consumer);
            Probe.Check(!decision.Eligible || decision.ProducerIndex is >= 0 && decision.Issues.Count == 0, "Invalid capability decision.");
            var display = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(_ => { view!.Status.Value = decision.Eligible ? "Capabilities allow the next import probe. CPU transport remains selected." :
                "CPU transport selected: " + string.Join("; ", decision.Issues.Select(i => i.Code + ": " + i.Detail)); display.TrySetResult(); });
            await display.Task.WaitAsync(token);
            await host.Supervisor.GetStatusAsync(token); await host.Editor.StopAsync();
            Probe.Check(host.Frames.MappingCount == 0 && factory.Leases.Count == 1 && factory.Leases.All(l => l.Reaped && l.ResourcesReleased) &&
                before == JsonSerializer.Serialize(authoring) && !AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Capability probe changed state or retained owned child.");
            window.Close(); await window.Completion.WaitAsync(token); await app.StopAsync(token);
            Probe.Check(sessions.ActiveSessionCount == 0 && view!.IsDisposed && window.Diagnostics!.Snapshot.Present.Failed == 0, "Capability UI cleanup failed."); completed = true;
        }
        catch (Exception error) { failure = error; Console.Error.WriteLine(error); }
        finally
        {
            await host.Editor.StopAsync(); window.Close(); await app.StopAsync();
            Records.Write(directory, "gpu-report.json", new { Completed = completed, Failure = failure?.ToString(), Decision = decision, Producer = producer, Consumer = consumer,
                Vulkan = native.ValueKind == JsonValueKind.Undefined ? (JsonElement?)null : native, OpenGl = glRaw, CpuTransportSelected = true, ImportTested = false,
                Mappings = host.Frames.MappingCount, ActiveUiSessions = sessions.ActiveSessionCount, EditorPreserved = before == JsonSerializer.Serialize(authoring),
                Children = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased }), Diagnostics = window.Diagnostics?.Snapshot });
        }
        return completed ? 0 : 1;
    }
}
