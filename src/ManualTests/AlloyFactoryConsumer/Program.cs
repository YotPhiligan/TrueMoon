using System.Collections.Concurrent;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;

if (args.Length < 4) throw new ArgumentException("Expected provider, opengl/vulkan, factory/all, report path, optional --validation.");
var provider = args[0]; var graphics = args[1]; var mode = args[2];
if (provider is not ("cobalt" or "di") || graphics is not ("opengl" or "vulkan") || mode is not ("factory" or "all")) throw new ArgumentException("Invalid consumer mode.");
var messages = new ConcurrentQueue<string>(); var cases = new List<object>(); Exception? failure = null;
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
try
{
    foreach (var scenario in mode == "factory" ? new[] { "factory" } : new[] { "factory", "legacy", "throw", "null", "disposed", "parented" })
        cases.Add(await Run(scenario));
    Check(messages.IsEmpty, "Vulkan validation messages remained.");
}
catch (Exception error) { failure = error; Console.Error.WriteLine(error); }
var report = Path.GetFullPath(args[3]); Directory.CreateDirectory(Path.GetDirectoryName(report)!);
File.WriteAllText(report, JsonSerializer.Serialize(new { Completed = failure == null, Failure = failure?.ToString(), Provider = provider, Graphics = graphics,
    Cases = cases, Registry = 0, ValidationMessages = messages.Count,
    Assemblies = new[] { typeof(HostedUiWindow).Assembly.Location, typeof(UiSession).Assembly.Location, typeof(App).Assembly.Location,
        typeof(SkiaOpenGLRenderBackend).Assembly.Location, typeof(FactoryRoot).Assembly.Location } }));
return failure == null ? 0 : 1;

async Task<object> Run(string scenario)
{
    var calls = 0; var owner = 0; var cleanupOwner = 0; var marker = new Marker(); var cause = new IOException("root factory failure");
    FactoryRoot? root = null; Panel? parent = null; UiSession? session = null; AlloyOptions? leaked = null; SilkUiWindowOptions? leakedWindow = null;
    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Element CreateRoot(IServiceResolver resolver)
    {
        owner = Environment.CurrentManagedThreadId; Interlocked.Increment(ref calls);
        Check(ReferenceEquals(marker, resolver.Resolve<Marker>()) && marker.Disposals == 0, "Root lost its application dependency.");
        if (scenario == "throw") throw cause;
        if (scenario == "null") return null!;
        root = new FactoryRoot(marker); root.Own(new Cleanup(() => cleanupOwner = Environment.CurrentManagedThreadId));
        if (scenario == "disposed") root.Dispose();
        if (scenario == "parented") { parent = new Panel(); parent.Add(root); }
        return root;
    }
    var builder = App.Builder(o => { if (provider == "di") o.UseDI(); });
    builder.Setup(context =>
    {
        context.Services(s => s.Singleton<Marker>(_ => marker));
        void Configure(AlloyOptions options)
        {
            leaked = options;
            if (graphics == "opengl") options.UseSkiaOpenGL(); else options.UseSkiaVulkan();
            options.UseSilkWindow(w =>
            {
                leakedWindow = w; w.GraphicsApi = graphics == "opengl" ? UiWindowGraphicsApi.OpenGL : UiWindowGraphicsApi.Vulkan;
                w.Title = $"INF6 {provider}/{graphics}/{scenario}"; w.Width = 260; w.Height = 180;
                w.FrameMode = UiFrameMode.OnDemand; w.EnableDiagnostics = true;
                if (args.Contains("--validation")) w.ValidationMessage = messages.Enqueue;
            });
        }
        if (scenario == "legacy")
        {
            context.UsePresentation<FactoryRoot>(Configure);
            // Preserve the legacy consumer-visible graph; the new overload requires neither registration.
            context.Services(s => s.Transient<FactoryRoot>().Transient<FactoryRoot>(resolver => (FactoryRoot)CreateRoot(resolver)));
        }
        else context.UsePresentation(CreateRoot, Configure);
        context.Services(s => Check(s.Exist<FactoryRoot>() == (scenario == "legacy"), "Unexpected inferred generic view registration."));
        // Mutate before Build/service resolution: constructor-only copying would not pass this regression.
        leaked!.UseSkiaRaster(); leakedWindow!.Width = -1; leakedWindow.Title = "mutated";
        leakedWindow.GraphicsApi = graphics == "opengl" ? UiWindowGraphicsApi.Vulkan : UiWindowGraphicsApi.OpenGL;
    });
    var app = builder.Build(); HostedUiSessionFactory? factory = null; HostedUiWindow? window = null; Exception? expectedWindowFailure = null;
    try
    {
        Check(calls == 0 && root == null, "Build created a root eagerly.");
        factory = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
        Check(factory.ActiveSessionCount == 0 && calls == 0, "Resolving window allocated UI resources.");
        Check(factory.Renderer == (graphics == "opengl" ? UiRendererKind.OpenGL : UiRendererKind.Vulkan), "Renderer selection was not snapshotted.");
        window.FramePresented += ui => { session = ui; ready.TrySetResult(); };
        var startupFailure = await Observe(() => app.StartAsync(deadline.Token));
        if (scenario is "throw" or "null" or "disposed" or "parented")
        {
            Check(startupFailure != null && calls == 1 && !window.IsClosed, "Invalid root did not fault startup once.");
            var windowFailure = await Observe(() => window.Completion.WaitAsync(deadline.Token));
            expectedWindowFailure = windowFailure;
            Check(windowFailure != null && factory.ActiveSessionCount == 0, "Failed root retained sessions.");
            if (scenario == "throw") Check(Flatten(windowFailure!).Contains(cause), "Factory cause was lost.");
            else Check(Flatten(windowFailure!).Any(e => e is InvalidOperationException), "Invalid-root diagnostic missing.");
            if (scenario == "parented") Check(root is { IsDisposed: false } && ReferenceEquals(root.Parent, parent), "Rejected borrowed root was disposed or detached.");
        }
        else
        {
            if (startupFailure != null) throw startupFailure;
            await ready.Task.WaitAsync(deadline.Token);
            Check(calls == 1 && root is { IsAttached: true, IsDisposed: false } && factory.ActiveSessionCount == 1, "Root creation/ownership failed.");
            var native = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(host =>
            {
                try
                {
                    Check(Environment.CurrentManagedThreadId == owner, "Root affinity failed.");
                    Check(Math.Abs(host.Viewport.Width / host.Viewport.Scale - 260) <= 1 && host.NativeWindow.Title == $"INF6 {provider}/{graphics}/{scenario}", "Window option snapshot failed.");
                    Check((host.NativeWindow.GLContext != null) == (graphics == "opengl"), "Wrong native target/presenter selected."); native.SetResult();
                }
                catch (Exception error) { native.TrySetException(error); throw; }
            });
            await native.Task.WaitAsync(deadline.Token);
            if (scenario == "factory")
            {
                // An already attached root belongs to the first window even when the second startup rejects it.
                var duplicate = new HostedUiWindow(new SilkUiWindowOptions { GraphicsApi = graphics == "opengl" ? UiWindowGraphicsApi.OpenGL : UiWindowGraphicsApi.Vulkan,
                    Width = 160, Height = 100, ValidationMessage = args.Contains("--validation") ? messages.Enqueue : null }, factory, () => root!);
                Check(await Observe(() => duplicate.StartAsync(deadline.Token)) is InvalidOperationException, "Attached root was accepted.");
                Check(await Observe(() => duplicate.Completion) is InvalidOperationException && !root!.IsDisposed && factory.ActiveSessionCount == 1, "Second startup damaged the original root.");
            }
            Check(await window.RequestCloseAsync(deadline.Token), "Close failed.");
            Check(root!.IsDisposed && cleanupOwner == owner && marker.Disposals == 0, "Cleanup affinity or dependency lifetime failed.");
            Check(session!.IsDisposed && window.Diagnostics!.Snapshot.Queue.Pending == 0, "Session cleanup failed.");
            await app.StopAsync(deadline.Token);
        }
    }
    finally
    {
        var disposal = await Observe(() => app.DisposeAsync().AsTask());
        if (disposal != null && (expectedWindowFailure == null || Flatten(disposal).Any(e => !Flatten(expectedWindowFailure).Contains(e)))) throw disposal;
        if (parent != null) parent.Dispose();
    }
    Check(marker.Disposals == 1 && factory!.ActiveSessionCount == 0, "App dependency cleanup failed.");
    return new { Scenario = scenario, Calls = calls, Owner = owner, CleanupOwner = cleanupOwner, Registry = factory.ActiveSessionCount,
        DependencyDisposals = marker.Disposals, NoViewRegistration = scenario != "legacy", Passed = true };
}
static async Task<Exception?> Observe(Func<Task> action) { try { await action(); return null; } catch (Exception error) { return error; } }
static IEnumerable<Exception> Flatten(Exception error) => error is AggregateException aggregate ? aggregate.InnerExceptions.SelectMany(Flatten) : [error];
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
sealed class Marker : IDisposable { internal int Disposals; public void Dispose() => Disposals++; }
sealed class FactoryRoot : Panel
{
    public FactoryRoot(Marker marker) { if (marker.Disposals != 0) throw new ObjectDisposedException(nameof(Marker)); Background = new Color(27, 61, 95); }
}
sealed class Cleanup(Action action) : IDisposable { public void Dispose() => action(); }
