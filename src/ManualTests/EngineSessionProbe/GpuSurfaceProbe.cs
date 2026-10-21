using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public sealed class GpuSurfaceView : View
{
    public GpuSurfaceView() => Content(() => VStack.Create().Padding(16).WithChildren(
        Text.Plain("TrueMoon ENG4c — reusable GPU surfaces"), Text.Plain("Checking slots, resize and runtime recovery."),
        Text.Plain("The ordinary viewport continues to use CPU transport.")));
}

public static class GpuSurfaceProbe
{
    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180)); var token = deadline.Token;
        var factory = new NativeRuntimeFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(c =>
        {
            c.UseAether(o => { o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory; });
            c.UsePresentation<GpuSurfaceView>(o => { o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w => { w.Width = 800; w.Height = 260; w.Title = "TrueMoon ENG4c surface proof"; w.EnableDiagnostics = true; }); });
            c.Services(r => r.Transient<GpuSurfaceView>());
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!; var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); GpuSurfaceView? view = null;
        window.FramePresented += ui => { view = (GpuSurfaceView)ui.Root; shown.TrySetResult(); };
        var cases = new List<object>(); var frames = new List<object>(); var ownerThread = 0; Action verify = null!;
        GpuOpenGlContext context = null!; GpuAdapterId adapter = null!; Task shutdown = Task.CompletedTask; var passed = false; Exception? failure = null; var failOwnerCleanup = false;
        Task Owner(Action action)
        {
            if (Environment.CurrentManagedThreadId == ownerThread) { action(); return Task.CompletedTask; }
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(_ => { try { verify(); action(); done.TrySetResult(); } catch (Exception error) { done.TrySetException(error); } }); return done.Task;
        }
        async Task Reject<T>(Func<Task> action) where T : Exception
        { var rejected = false; try { await action(); } catch (T) { rejected = true; } Probe.Check(rejected, "Expected rejection " + typeof(T).Name); }
        async Task Fault(Func<Task> action)
        { Exception? actual = null; try { await action(); } catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException) { actual = error; } Probe.Check(actual != null, "GPU surface fault was not observed."); }
        async Task<(NativeRuntimeLease Runtime, GpuSurfaceSession Session)> Start(string behavior = "gpu-surface")
        {
            await host.Editor.StopAsync(); await host.Supervisor.StartAsync(authoring.Snapshot(), behavior, token);
            var runtime = factory.Leases[^1]; var session = await host.Gpu.ConnectSurfaceAsync(runtime, adapter, context, token);
            await session.ConfigureAsync(64, 32, token); return (runtime, session);
        }
        async Task<GpuSurfaceReadback> Read(GpuSurfaceSession session, GpuFrameCompletion ready)
        {
            var offer = session.Surface!.Slots[ready.Stamp.Slot]; var read = await session.ReadAsync(ready, token);
            Probe.Check(read.Completion == ready && read.Pixels.AsSpan().SequenceEqual(GpuReferencePattern.Create(offer.Width, offer.Height, ready.Stamp)), "Reused GPU pixels/completion mismatch.");
            frames.Add(new { ready.Stamp, offer.Width, offer.Height, Sha256 = Convert.ToHexString(SHA256.HashData(read.Pixels)), Pixels = read.Pixels.Length / 4 }); return read;
        }
        async Task<GpuFrameCompletion> Roundtrip(GpuSurfaceSession session, int slot)
        { var ready = await session.ProduceAsync(slot, token); var read = await Read(session, ready); Probe.Check(await session.ReturnAsync(read.Completion, token) == ready, "Retirement identity mismatch."); return ready; }
        async Task Stop(NativeRuntimeLease runtime, GpuSurfaceSession session)
        {
            await session.DisposeAsync(); await host.Editor.StopAsync();
            Probe.Check(session.IsReleased && !session.IsAvailable && session.CleanupError == null && runtime.Reaped && runtime.ResourcesReleased && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && view is { IsDisposed: false }, "GPU session stop/reap/owner cleanup failed.");
        }
        try
        {
            await app.StartAsync(token); await shown.Task.WaitAsync(token);
            var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(w =>
            {
                try
                {
                    ownerThread = Environment.CurrentManagedThreadId; var gl = w.NativeWindow.GLContext!; verify = gl.MakeCurrent; verify();
                    nint Resolve(string n) => gl.TryGetProcAddress(n, out var address) ? address : 0;
                    context = new(Resolve, () => { verify(); if (failOwnerCleanup) throw new InvalidOperationException("Injected GL owner cleanup failure."); }, Owner);
                    var identity = new SessionIdentity(Guid.NewGuid(), 1, BuildIdentity.HostId, BuildIdentity.GameId, new('A', 64));
                    var capabilities = GpuCapabilityCollector.OpenGl(identity, Resolve).Capabilities;
                    Probe.Check(capabilities.Error == null && capabilities.Devices.Count == 1 && capabilities.Features is { RequiredProcedures: true, OptimalTiling: true }, "GL surface profile unavailable.");
                    adapter = capabilities.Devices[0]; initialized.TrySetResult();
                }
                catch (Exception error) { initialized.TrySetException(error); }
            });
            await initialized.Task.WaitAsync(token);
            window.Closing += async _ => { shutdown = host.DisposeAsync().AsTask(); await shutdown; Probe.Check(view is { IsDisposed: false } && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0, "UI owner destroyed before GPU surface cleanup."); return true; };
            var main = await Start(); var session = main.Session; var offerSnapshot = JsonSerializer.Serialize(session.Surface); var held = new List<GpuFrameCompletion>();
            await Reject<InvalidOperationException>(() => host.Gpu.ConnectSurfaceAsync(main.Runtime, adapter, context, token));
            for (var slot = 0; slot < 3; slot++) held.Add(await session.ProduceAsync(slot, token));
            await Reject<InvalidOperationException>(() => session.ProduceAsync(0, token)); await Reject<InvalidOperationException>(() => session.ReturnAsync(held[0], token));
            Probe.Check(session.Slots.All(s => s.Phase == "Ready") && session.Surface!.Slots.Count == 3 && host.Gpu.ResourceCount == 1 && host.Gpu.OpenHandleCount == 0, "Bounded slots were overwritten or leaked handles.");
            foreach (var slot in new[] { 2, 0, 1 }) { var read = await Read(session, held[slot]); await session.ReturnAsync(read.Completion, token); }
            cases.Add(new { Case = "three-slots-exhaustion", Outstanding = 3, ReturnOrder = new[] { 2, 0, 1 } });
            for (var frame = 0; frame < 60; frame++) await Roundtrip(session, frame % 3);
            Probe.Check(JsonSerializer.Serialize(session.Surface) == offerSnapshot && session.Slots.All(s => s.Stamp!.FrameId == 21 && s.Phase == "Idle"), "Slot allocation changed during persistent reuse.");
            cases.Add(new { Case = "persistent-reuse", Frames = 60, session.Generation, Offers = session.Surface });
            var next = await session.ProduceAsync(0, token); await Reject<InvalidDataException>(() => session.ReadAsync(held[0], token));
            var readNext = await Read(session, next); await Reject<InvalidDataException>(() => session.ReturnAsync(held[0], token)); await session.ReturnAsync(readNext.Completion, token);
            cases.Add(new { Case = "stale-frame", Previous = held[0], Next = next });
            held.Clear(); for (var slot = 0; slot < 3; slot++) held.Add(await session.ProduceAsync(slot, token));
            await session.ConfigureAsync(320, 180, token);
            Probe.Check(session.Generation == 2 && session.Surface!.Slots.All(s => s.Width == 320 && s.Height == 180) && session.Slots.All(s => s.Phase == "Idle"), "Resize did not drain old Ready slots.");
            var resized = await session.ProduceAsync(0, token); await Reject<InvalidDataException>(() => session.ReadAsync(held[0], token)); var resizedRead = await Read(session, resized); await session.ReturnAsync(resizedRead.Completion, token);
            cases.Add(new { Case = "resize-drains-ready", Old = held[0].Stamp, Current = resized.Stamp });
            await session.ConfigureAsync(0, 0, token); Probe.Check(session.Generation == 3 && session.Surface == null && session.Slots.Count == 0, "Suspend retained allocations.");
            await Reject<InvalidOperationException>(() => session.ProduceAsync(0, token)); await session.ConfigureAsync(1280, 720, token);
            for (var slot = 0; slot < 3; slot++) await Roundtrip(session, slot);
            cases.Add(new { Case = "suspend-resume", session.Generation });
            var unchanged = session.Surface; await Reject<ArgumentOutOfRangeException>(() => session.ConfigureAsync(0, 16, token));
            Probe.Check(ReferenceEquals(unchanged, session.Surface) && session.IsAvailable, "Invalid extent changed the active surface."); cases.Add(new { Case = "invalid-extent-preserves-surface" });
            foreach (var size in new[] { (16, 16), (128, 64), (64, 32) })
            {
                await session.ConfigureAsync(0, 0, token); await session.ConfigureAsync(size.Item1, size.Item2, token); await Roundtrip(session, 2);
                Probe.Check(host.Gpu.ResourceCount == 1 && host.Gpu.OpenHandleCount == 0 && session.Slots.Count == 3, "Surface resize retained old local resources.");
            }
            cases.Add(new { Case = "resize-trend", Configurations = 6, session.Generation });
            Records.Write(directory, $"surface-gate-{main.Runtime.Identity.Generation}.json", new { Hold = true });
            var queuedProduce = session.ProduceAsync(0, token);
            await Records.WaitAsync(Path.Combine(directory, $"surface-gated-{main.Runtime.Identity.Generation}.json"));
            await Reject<InvalidOperationException>(() => session.ProduceAsync(2, token));
            var priorGeneration = session.Generation; var queuedResize = session.ConfigureAsync(64, 32, token);
            await Reject<InvalidOperationException>(() => session.ProduceAsync(2, token)); await Reject<InvalidOperationException>(() => session.ConfigureAsync(16, 16, token));
            Probe.Check(!queuedProduce.IsCompleted && !queuedResize.IsCompleted && session.Generation == priorGeneration, "Resize overtook accepted GPU work.");
            Records.Write(directory, $"surface-release-{main.Runtime.Identity.Generation}.json", new { Release = true });
            var priorReady = await queuedProduce; await queuedResize;
            Probe.Check(session.Generation == priorGeneration + 1 && session.Slots.All(s => s.Phase == "Idle"), "Queued resize did not drain the accepted old frame.");
            await Reject<InvalidDataException>(() => session.ReadAsync(priorReady, token)); await Roundtrip(session, 0);
            cases.Add(new { Case = "queued-resize", PriorGeneration = priorGeneration, CurrentGeneration = session.Generation });
            var completed = await session.ProduceAsync(0, token); await Read(session, completed); await session.ProduceAsync(1, token);
            failOwnerCleanup = true; await Reject<AggregateException>(() => session.DisposeAsync().AsTask());
            Probe.Check(!session.IsReleased && session.CleanupError != null && host.Gpu.ResourceCount == 1, "Failed import cleanup lost ownership.");
            await Reject<InvalidOperationException>(() => host.Gpu.ConnectSurfaceAsync(main.Runtime, adapter, context, token));
            failOwnerCleanup = false; await session.DisposeAsync(); Probe.Check(session.IsReleased && session.CleanupError == null, "Explicit surface cleanup retry failed.");
            cases.Add(new { Case = "cleanup-retry", RetainedUntilRetry = true });
            await Stop(main.Runtime, session); await Reject<ObjectDisposedException>(() => session.ReadAsync(completed, token)); cases.Add(new { Case = "stop-live-slots", main.Runtime.Reaped });
            foreach (var scenario in new[] { "cancel", "deadline", "crash-before-ready", "crash-after-ready", "crash-after-gl-use" })
            {
                var behavior = scenario switch { "crash-before-ready" => "gpu-surface-crash", "crash-after-ready" => "gpu-surface-crash-second", "crash-after-gl-use" => "gpu-surface-crash-return", _ => "gpu-surface-hold" };
                var current = await Start(behavior); GpuFrameCompletion? oldReady = null; Task work;
                using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token); var clock = Stopwatch.StartNew();
                if (scenario == "crash-after-gl-use") { oldReady = await current.Session.ProduceAsync(0, token); var read = await Read(current.Session, oldReady); work = current.Session.ReturnAsync(read.Completion, token); }
                else if (scenario == "crash-after-ready") { oldReady = await current.Session.ProduceAsync(0, token); work = current.Session.ProduceAsync(1, token); }
                else work = current.Session.ProduceAsync(0, cancel.Token);
                if (scenario is "cancel" or "deadline")
                {
                    await Records.WaitAsync(Path.Combine(directory, $"surface-{current.Runtime.Identity.Generation}-Produce-1.json"));
                    if (scenario == "cancel") cancel.Cancel();
                }
                await Fault(() => work); Probe.Check(!current.Session.IsAvailable, "Failed surface connection remained reusable.");
                if (scenario == "deadline") Probe.Check(clock.Elapsed >= TimeSpan.FromSeconds(7) && clock.Elapsed < TimeSpan.FromSeconds(15), "Surface deadline was unbounded.");
                if (oldReady != null) await Reject<ObjectDisposedException>(() => current.Session.ReadAsync(oldReady, token));
                await Stop(current.Runtime, current.Session); cases.Add(new { Case = scenario, current.Runtime.Reaped });
                var restarted = await Start(); var fresh = await restarted.Session.ProduceAsync(0, token);
                if (oldReady != null) await Reject<InvalidDataException>(() => restarted.Session.ReadAsync(oldReady, token));
                var freshRead = await Read(restarted.Session, fresh); await restarted.Session.ReturnAsync(freshRead.Completion, token); await Stop(restarted.Runtime, restarted.Session);
                Probe.Check(fresh.Stamp.Identity.Generation > current.Runtime.Identity.Generation && fresh.Stamp.SurfaceGeneration == 1 && fresh.Stamp.FrameId == 1, "Restart did not reset slot generation under a fresh identity.");
                cases.Add(new { Case = "restart-" + scenario, Previous = current.Runtime.Identity, Current = fresh.Stamp });
            }
            var closing = await Start("gpu-surface-hold-second"); var closingReady = await closing.Session.ProduceAsync(0, token); await Read(closing.Session, closingReady);
            var pending = closing.Session.ProduceAsync(1, token); await Records.WaitAsync(Path.Combine(directory, $"surface-{closing.Runtime.Identity.Generation}-Produce-2.json"));
            using (var process = Process.GetCurrentProcess()) { process.Refresh(); Probe.Check(process.CloseMainWindow(), "Native surface close failed."); }
            await window.Completion.WaitAsync(token); await shutdown; await Fault(() => pending); await app.StopAsync(token);
            await Reject<ObjectDisposedException>(() => host.Gpu.ConnectSurfaceAsync(closing.Runtime, adapter, context, token));
            Probe.Check(closing.Session.IsReleased && closing.Runtime.Reaped && closing.Runtime.ResourcesReleased, "Native close left accepted GPU work/live imports."); cases.Add(new { Case = "native-close-pending-live-imports" });
            Probe.Check(before == JsonSerializer.Serialize(authoring) && sessions.ActiveSessionCount == 0 && view!.IsDisposed && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "Final surface proof cleanup failed.");
            var cleanups = Directory.GetFiles(directory, "surface-cleanup-*.json"); Probe.Check(cleanups.Length == 9, "Cooperative surface cleanup evidence missing.");
            foreach (var file in cleanups)
            {
                var cleanup = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file)); var producer = cleanup.GetProperty("LastSurfaceProducer");
                Probe.Check(cleanup.GetProperty("ResourceCount").GetInt32() == 0 && cleanup.GetProperty("OpenHandleCount").GetInt32() == 0 && cleanup.GetProperty("Error").ValueKind == JsonValueKind.Null &&
                    producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 && producer.GetProperty("SurfacesCreated").GetInt32() == producer.GetProperty("SurfacesReleased").GetInt32() &&
                    producer.GetProperty("SlotsCreated").GetInt32() == producer.GetProperty("SlotsReleased").GetInt32() && producer.GetProperty("LiveSlots").GetInt32() == 0 && producer.GetProperty("OpenHandles").GetInt32() == 0 &&
                    producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("SurfacesCreated").GetInt32() && producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("DevicesReleased").GetInt32() &&
                    producer.GetProperty("ValidationEnabled").GetBoolean() == (Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1"), "Surface producer validation/cleanup failed: " + file);
            }
            passed = true;
        }
        catch (Exception error) { failure = error; Console.Error.WriteLine(error); }
        finally
        {
            try { await host.Gpu.DisposeAsync(); await host.Editor.StopAsync(); window.Close(); await app.StopAsync(); }
            catch (Exception cleanup) { passed = false; failure = failure == null ? cleanup : new AggregateException(failure, cleanup); Console.Error.WriteLine(cleanup); }
            Records.Write(directory, "gpu-surface-report.json", new { Completed = passed, Failure = failure?.ToString(), Cases = cases, Frames = frames, Adapter = adapter,
                ValidationRequested = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1", Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount,
                ActiveUiSessions = sessions.ActiveSessionCount, EditorPreserved = before == JsonSerializer.Serialize(authoring), CpuViewportDefault = true,
                Children = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased, l.StandardError }) });
        }
        return passed ? 0 : 1;
    }
}
