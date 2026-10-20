using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Alloy;
using TrueMoon.Argentis;
using System.ComponentModel;

namespace EngineSessionProbe;

public sealed class GpuImportView : View
{
    public GpuImportView() => Content(() => VStack.Create().Padding(16).WithChildren(
        Text.Plain("TrueMoon ENG4b — shared image proof"),
        Text.Plain("Checking imported pixels, completion and cleanup."),
        Text.Plain("The ordinary viewport continues to use CPU transport.")));
}

public static class GpuImportProbe
{
    private static nint _storage;
    [UnmanagedCallersOnly]
    private static unsafe void InvalidStorage(uint target, int levels, uint format, int width, int height, uint memory, ulong offset)
        => ((delegate* unmanaged<uint, int, uint, int, int, uint, ulong, void>)_storage)(target, levels, 0, width, height, memory, offset);

    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180)); var token = deadline.Token;
        var factory = new NativeRuntimeFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(c =>
        {
            c.UseAether(o => { o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory; });
            c.UsePresentation<GpuImportView>(o => { o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w => { w.Width = 800; w.Height = 260; w.Title = "TrueMoon ENG4b import proof"; w.EnableDiagnostics = true; }); });
            c.Services(r => r.Transient<GpuImportView>());
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!; var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var cases = new List<object>(); var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); GpuImportView? view = null;
        window.FramePresented += ui => { view = (GpuImportView)ui.Root; shown.TrySetResult(); };
        Func<string, nint> resolve = null!; Action verify = null!; int ownerThread = 0;
        Task Owner(Action action)
        {
            if (Environment.CurrentManagedThreadId == ownerThread) { action(); return Task.CompletedTask; }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(_ => { try { verify(); action(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } }); return completion.Task;
        }
        var passed = false; Exception? failure = null; Task shutdown = Task.CompletedTask;
        GpuOpenGlContext context = null!; GpuAdapterId adapter = null!;
        try
        {
            await app.StartAsync(token); await shown.Task.WaitAsync(token);
            var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(w =>
            {
                try
                {
                    ownerThread = Environment.CurrentManagedThreadId; var gl = w.NativeWindow.GLContext!; verify = gl.MakeCurrent;
                    resolve = n => gl.TryGetProcAddress(n, out var p) ? p : 0; verify(); context = new(resolve, verify, Owner);
                    // Correlate actual GL device; Vulkan producer independently validates both UUIDs again.
                    var identity = new TrueMoon.Aether.Contracts.SessionIdentity(Guid.NewGuid(), 1, BuildIdentity.HostId, BuildIdentity.GameId, new('A', 64));
                    var capabilities = GpuCapabilityCollector.OpenGl(identity, resolve).Capabilities;
                    Probe.Check(capabilities.Error == null && capabilities.Devices.Count == 1 && capabilities.Features is { RequiredProcedures: true, OptimalTiling: true }, "Current GL context is not eligible.");
                    adapter = capabilities.Devices[0]; initialized.TrySetResult();
                }
                catch (Exception error) { initialized.TrySetException(error); }
            });
            await initialized.Task.WaitAsync(token);
            window.Closing += async _ =>
            {
                shutdown = host.DisposeAsync().AsTask(); await shutdown;
                Probe.Check(view is { IsDisposed: false } && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0, "Window disposed before GPU cleanup."); return true;
            };
            async Task<(NativeRuntimeLease Runtime, GpuFrameConnection Connection, GpuFrameOffer Offer)> Start(string behavior = "gpu-import", int width = 64, int height = 32)
            {
                await host.Editor.StopAsync(); await host.Supervisor.StartAsync(authoring.Snapshot(), behavior, token);
                var runtime = factory.Leases[^1]; var connection = await host.Gpu.ConnectAsync(runtime, token); var offer = await connection.CreateAsync(adapter, width, height, token);
                return (runtime, connection, offer);
            }
            async Task<GpuFrameCompletion> Complete(OpenGlSharedFrame imported)
            {
                GpuFrameCompletion? complete = null; var clock = Stopwatch.StartNew();
                while (complete == null)
                {
                    await Owner(() => complete = imported.PollCompletion());
                    if (clock.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("GL completion remains unconfirmed.");
                    if (complete == null) await Task.Delay(2, token);
                }
                return complete;
            }
            async Task Expect<T>(string name, Func<Task> action) where T : Exception
            {
                Exception? actual = null;
                try { await action(); } catch (T error) { actual = error; }
                Probe.Check(actual != null, name + " did not reject the operation.");
                Probe.Check(host.Gpu.OpenHandleCount == 0, name + " leaked duplicated NT handles.");
                cases.Add(new { Case = name, Rejected = true, Error = actual!.GetType().Name });
            }
            // Reject descriptor/identity before duplication; unwind partial handles and real GL allocations on import failure.
            var failed = await Start();
            await Expect<InvalidDataException>("invalid-descriptor", () => Owner(() => host.Gpu.Import(failed.Runtime, failed.Offer with { Offset = 1 }, adapter, context)));
            await Expect<InvalidDataException>("stale-offer", () => Owner(() => host.Gpu.Import(failed.Runtime, failed.Offer with { Stamp = failed.Offer.Stamp with { Identity = failed.Offer.Stamp.Identity with { Generation = 999 } } }, adapter, context)));
            await Expect<Win32Exception>("duplicate-handle-failure", () => Owner(() => host.Gpu.Import(failed.Runtime, failed.Offer with { ReadyHandle = (ulong)long.MaxValue - 123 }, adapter, context)));
            await Expect<NotSupportedException>("missing-gl-procedure", () => Owner(() =>
            {
                var missing = new GpuOpenGlContext(n => n == "glTexStorageMem2DEXT" ? 0 : resolve(n), verify, Owner);
                host.Gpu.Import(failed.Runtime, failed.Offer, adapter, missing);
            }));
            await Expect<IOException>("driver-storage-failure", () => Owner(() =>
            {
                unsafe
                {
                    _storage = resolve("glTexStorageMem2DEXT");
                    var broken = new GpuOpenGlContext(n => n == "glTexStorageMem2DEXT" ? (nint)(delegate* unmanaged<uint, int, uint, int, int, uint, ulong, void>)&InvalidStorage : resolve(n), verify, Owner);
                    host.Gpu.Import(failed.Runtime, failed.Offer, adapter, broken);
                }
            }));
            await failed.Connection.DisposeAsync(); await host.Editor.StopAsync();
            foreach (var size in new[] { (16, 16), (64, 32), (1280, 720) })
            {
                var transfer = await Start(width: size.Item1, height: size.Item2); OpenGlSharedFrame imported = null!;
                await Owner(() => imported = host.Gpu.Import(transfer.Runtime, transfer.Offer, adapter, context));
                Probe.Check(host.Gpu.OpenHandleCount == 0, "Duplicated NT handles survived successful GL import.");
                if (size.Item1 == 16)
                {
                    await Expect<InvalidDataException>("early-ready", () => Owner(() => imported.Read(new(transfer.Offer.Stamp, false))));
                    await Expect<InvalidDataException>("stale-ready", () => Owner(() => imported.Read(new(transfer.Offer.Stamp with { SurfaceGeneration = 2 }, true))));
                    Probe.Check(imported.Phase == "Imported", "Rejected ready advanced the GL phase.");
                }
                await transfer.Connection.ImportedAsync(token); var ready = await transfer.Connection.ProduceAsync(token); byte[] pixels = null!;
                await Owner(() => pixels = imported.Read(ready)); var complete = await Complete(imported); var retired = await transfer.Connection.ReturnAsync(complete, token);
                Probe.Check(retired.FenceCompleted && pixels.AsSpan().SequenceEqual(GpuReferencePattern.Create(size.Item1, size.Item2)), "Actual imported RGBA pixels/retirement mismatch.");
                await imported.DisposeAsync(); await transfer.Connection.DisposeAsync(); await host.Editor.StopAsync();
                cases.Add(new { Case = "roundtrip", Width = size.Item1, Height = size.Item2, transfer.Offer, Ready = ready, Complete = complete, Retired = retired, Sha256 = Convert.ToHexString(SHA256.HashData(pixels)), Pixels = pixels.Length / 4 });
            }
            foreach (var scenario in new[] { "cancel-before-ready", "timeout-before-ready", "crash-before-ready" })
            {
                var transfer = await Start(scenario == "crash-before-ready" ? "gpu-import-crash" : "gpu-import-hold"); OpenGlSharedFrame imported = null!;
                await Owner(() => imported = host.Gpu.Import(transfer.Runtime, transfer.Offer, adapter, context)); await transfer.Connection.ImportedAsync(token);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token); var clock = Stopwatch.StartNew();
                var produce = transfer.Connection.ProduceAsync(cancellation.Token);
                await Records.WaitAsync(Path.Combine(directory, $"gpu-{transfer.Offer.Stamp.Identity.Generation}-Produce.json"));
                if (scenario == "cancel-before-ready") cancellation.Cancel();
                if (scenario == "crash-before-ready") await Expect<IOException>(scenario, () => produce);
                else await Expect<OperationCanceledException>(scenario, () => produce);
                Probe.Check(imported.Phase == "Imported" && transfer.Connection.IsReleased, "Unknown result authorized GPU wait/channel reuse.");
                if (scenario == "timeout-before-ready") Probe.Check(clock.Elapsed >= TimeSpan.FromSeconds(7) && clock.Elapsed < TimeSpan.FromSeconds(15), "GPU command deadline was not bounded.");
                await imported.DisposeAsync(); await transfer.Connection.DisposeAsync(); await host.Editor.StopAsync();
                Probe.Check(transfer.Runtime.Reaped && transfer.Runtime.ResourcesReleased, "Failed GPU runtime was not reaped.");
            }
            // A native WM_CLOSE must drain actual live imports before the borrowed context/view is destroyed.
            var live = await Start(); OpenGlSharedFrame closingImport = null!;
            await Owner(() => closingImport = host.Gpu.Import(live.Runtime, live.Offer, adapter, context)); await live.Connection.ImportedAsync(token);
            var closingReady = await live.Connection.ProduceAsync(token); byte[] closingPixels = null!;
            await Owner(() => closingPixels = closingImport.Read(closingReady)); var closingCompletion = await Complete(closingImport);
            Probe.Check(closingPixels.AsSpan().SequenceEqual(GpuReferencePattern.Create(64, 32)) && !closingImport.IsReleased && host.Gpu.ResourceCount >= 2, "Native close had no live completed imports.");
            using (var process = Process.GetCurrentProcess()) { process.Refresh(); Probe.Check(process.CloseMainWindow(), "Native GPU window close request failed."); }
            await window.Completion.WaitAsync(token); await shutdown; await app.StopAsync(token);
            cases.Add(new { Case = "native-close-live-imports", Ready = closingReady, Complete = closingCompletion, Released = closingImport.IsReleased, live.Runtime.Reaped, live.Runtime.ResourcesReleased });
            // Keep this one-frame proof isolated from the ordinary CPU viewport.
            Probe.Check(before == JsonSerializer.Serialize(authoring) && factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "GPU proof changed authoring/retained a runtime.");
            var cleanupFiles = Directory.GetFiles(directory, "gpu-cleanup-*.json");
            Probe.Check(cleanupFiles.Length == 7, "Cooperative GPU cleanup evidence missing (one deliberately crashed child is expected).");
            foreach (var file in cleanupFiles)
            {
                var cleanup = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
                var producer = cleanup.GetProperty("LastProducer");
                Probe.Check(cleanup.GetProperty("ResourceCount").GetInt32() == 0 && cleanup.GetProperty("OpenHandleCount").GetInt32() == 0 &&
                    cleanup.GetProperty("Error").ValueKind == JsonValueKind.Null && producer.GetProperty("Released").GetBoolean() &&
                    producer.GetProperty("OpenProcessHandles").GetInt32() == 0 && producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 &&
                    producer.GetProperty("ValidationEnabled").GetBoolean() == (Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1"), "Producer validation/cleanup failed: " + file);
            }
            Probe.Check(sessions.ActiveSessionCount == 0 && view!.IsDisposed && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0, "GPU proof final cleanup failed."); passed = true;
        }
        catch (Exception error) { failure = error; Console.Error.WriteLine(error); }
        finally
        {
            try { await host.Gpu.DisposeAsync(); await host.Editor.StopAsync(); window.Close(); await app.StopAsync(); }
            catch (Exception cleanup) { passed = false; failure = failure == null ? cleanup : new AggregateException(failure, cleanup); Console.Error.WriteLine(cleanup); }
            Records.Write(directory, "gpu-import-report.json", new { Completed = passed, Failure = failure?.ToString(), Cases = cases, Adapter = adapter,
                ValidationRequested = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1",
                Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount, ActiveUiSessions = sessions.ActiveSessionCount,
                EditorPreserved = before == JsonSerializer.Serialize(authoring), CpuViewportDefault = true, Diagnostics = window.Diagnostics?.Snapshot,
                Children = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased, l.StandardError }) });
        }
        return passed ? 0 : 1;
    }
}
