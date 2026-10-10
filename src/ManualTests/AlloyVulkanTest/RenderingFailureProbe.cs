using AlloyTest;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

internal static class RenderingFailureProbe
{
    private static readonly UiViewport Viewport = new(900, 1100);

    internal static async Task RunAsync(bool validation)
    {
        Exercise(new SkiaRasterRenderBackend(), new RasterUiTarget(), ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); });
        using (var window = new SilkWindowHost(320, 240, "Rendering failure OpenGL", true))
        using (var gl = GL.GetApi(window.NativeWindow))
        {
            Exercise(new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window), ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); });
            Require(gl.GetError() == GLEnum.NoError, "Failure/recreation left an OpenGL error.");
        }
        var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
        using (device)
        {
            var target = SilkVulkanHost.CreateTarget(device);
            Exercise(new SkiaVulkanRenderBackend(), target, ui =>
            {
                var lease = ui.AcquireVulkanTexture(); var layout = (ImageLayout)lease.Info.Layout;
                try { return VulkanHostReadback.Read(device, lease.Info, state => layout = state); }
                finally { lease.Return(layout); }
            });
            using var ui = UiSession.Create(new View1(new SettingsModel()), new SkiaVulkanRenderBackend(), target, Viewport);
            ui.Update(); var borrowed = ui.AcquireVulkanTexture(); var info = borrowed.Info;
            var hostFailure = Failure(); ui.ReportRenderingFailure(hostFailure);
            Reject<InvalidOperationException>(ui.Dispose); Require(!ui.IsDisposed && ui.Root.IsAttached, "Fault disposed a borrowed frame.");
            Reject<ArgumentException>(() => borrowed.AbandonAfterDeviceLoss(hostFailure));
            Require(borrowed.Info.Image == info.Image, "Rejected abandonment invalidated the lease.");
            borrowed.Return((ImageLayout)info.Layout); ui.Dispose(); device.WaitIdle();
        }
        Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Failure Vulkan validation failed, including teardown.");
        var classified = Reject<UiRenderingException>(() => VulkanDevice.Check(Result.ErrorDeviceLost, "controlled classification"));
        Require(classified.Kind == UiRenderingFailureKind.DeviceLost && classified.InnerException is InvalidOperationException, "Vulkan device-loss classification lost its cause.");
        Reject<InvalidOperationException>(() => VulkanDevice.Check(Result.ErrorOutOfDeviceMemory, "ordinary Vulkan error"));
        VulkanDevice.Check(Result.Success, "success");
        RegistryCleanup();
        foreach (var external in new[] { false, true })
        {
            await WindowAsync(openGL: true, validation: false, external);
            await WindowAsync(openGL: false, validation, external);
        }
        await CreationFailureAsync(openGL: true, validation: false);
        await CreationFailureAsync(openGL: false, validation);
        Console.WriteLine("Rendering failure passed: raster/OpenGL/Vulkan terminal injection, blocked input/export/update, explicit shared-model recreation, borrowed lease protection/return, Vulkan result classification, registry best-effort cleanup, OpenGL/Vulkan Completion/StopAsync and cleanup errors, explicit new windows. Vulkan validation: 0 errors, 0 warnings, including teardown. Physical device/context loss was not induced.");
    }

    private static void Exercise(IRenderBackend backend, UiRenderTarget target, Func<UiSession, byte[]> read)
    {
        var model = new SettingsModel { Name = "Сохранено 👩‍💻" }; model.AddRow(); model.Rows[0].Name = "Строка Б"; var row = model.Rows[0];
        var view = new View1(model); var root = new FailingRoot(view); using var ui = UiSession.Create(root, backend, target, Viewport);
        Require(ui.Update() && read(ui).Length == Viewport.Width * Viewport.Height * 4, "Initial real backend readback failed.");
        ui.Focus(view.NameEditor); ui.Capture(view.NameEditor); var reports = 0; var failure = Failure();
        ui.RenderingFailed += error => { Require(ReferenceEquals(error, failure), "Failure notification replaced original error."); reports++; };
        root.Failure = failure; root.Background = new Color(1, 0, 0);
        Require(ReferenceEquals(Reject<UiRenderingException>(() => ui.Update()), failure), "Update replaced its primary failure.");
        Reject<InvalidOperationException>(() => ui.Update()); Reject<InvalidOperationException>(() => ui.Resize(new UiViewport(100, 100)));
        Reject<InvalidOperationException>(() => ui.HandleInput(new UiInput(InputKind.Text, Text: "blocked"))); Reject<InvalidOperationException>(() => read(ui));
        Require(reports == 1 && !view.NameEditor.IsFocused && model.Name == "Сохранено 👩‍💻", "Failure did not cancel focus or mutated model.");
        ui.Dispose(); Require(root.IsDisposed && view.IsDisposed && !view.IsAttached, "Failed tree was not released.");
        model.Name = "Пересоздано 🧑‍💻"; Require(view.NameEditor.Value == "Сохранено 👩‍💻", "Old view remained subscribed.");
        var nextView = new View1(model); using var next = UiSession.Create(nextView, backend, target, Viewport);
        Require(next.Update() && !next.Update() && read(next).Length == Viewport.Width * Viewport.Height * 4, "Explicit new session did not draw/read back.");
        Require(nextView.NameEditor.Value == model.Name && ReferenceEquals(row, model.Rows[0]) && nextView.RowList.Items.Count == model.Rows.Count, "Recreation lost model/list identity.");
        Require(!nextView.NameEditor.IsFocused && !next.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured, "New session retained transient input.");
        Console.WriteLine($"{backend.GetType().Name}: controlled terminal draw failure, release and explicit recreation passed.");
    }

    private static async Task WindowAsync(bool openGL, bool validation, bool external)
    {
        var model = new SettingsModel { Name = "До отказа" }; var primary = Failure(); var cleanup = new Exception("controlled tree cleanup"); var close = new Exception("controlled close callback");
        var messages = new List<string>(); var closed = 0; var reports = 0; FailingRoot? root = null; View1? view = null; UiSession? oldSession = null;
        using var factory = new HostedUiSessionFactory(openGL ? new SkiaOpenGLRenderBackend() : new SkiaVulkanRenderBackend());
        var options = Options(openGL, validation, messages);
        var window = new HostedUiWindow(options, factory, () =>
        { view = new View1(model); root = new FailingRoot(view); root.Own(new ThrowingResource(cleanup)); return root; }, () => { closed++; throw close; });
        window.FramePresented += ui =>
        {
            oldSession = ui; ui.RenderingFailed += error => { Require(ReferenceEquals(error, primary), "Window lost primary failure."); reports++; };
            model.Name = "Сохранено окном 👩‍💻"; model.AddRow(); root!.Failure = primary; root.Background = new Color(1, 0, 0);
            if (external) ui.ReportRenderingFailure(primary);
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await window.StartAsync(timeout.Token);
        var completion = await RejectAsync<AggregateException>(() => window.Completion.WaitAsync(timeout.Token));
        var failures = OrderedErrors(completion).ToArray();
        Require(failures.Length == 3 && ReferenceEquals(failures[0], primary) && ReferenceEquals(failures[1], cleanup) && ReferenceEquals(failures[2], close), "Window cleanup masked/reordered its original error: " + completion);
        var stop = await RejectAsync<AggregateException>(() => window.StopAsync(timeout.Token)); Require(ReferenceEquals(stop, completion), "Stop lost Completion error identity.");
        Require(closed == 1 && reports == 1 && window.PresentedFrames == 1 && factory.ActiveSessionCount == 0 && oldSession!.IsFaulted && oldSession.IsDisposed && root!.IsDisposed && view!.IsDisposed, "Window failure/registry lifecycle failed.");
        Require(model.Name == "Сохранено окном 👩‍💻", "Window failure modified the retained model.");
        var row = model.Rows[0]; var oldValue = view!.NameEditor.Value; model.Name = "Новое окно 🧑‍💻"; Require(view.NameEditor.Value == oldValue, "Window left old bindings attached.");
        var recreatedFrames = 0; View1? nextView = null;
        var recreated = new HostedUiWindow(options, factory, () => nextView = new View1(model));
        recreated.FramePresented += ui =>
        {
            Require(!ui.IsFaulted && nextView!.NameEditor.Value == model.Name && ReferenceEquals(row, model.Rows[0]), "New window lost model state.");
            Require(!nextView!.NameEditor.IsFocused, "New window restored old focus.");
            if (++recreatedFrames == 3) recreated.Close();
        };
        await recreated.StartAsync(timeout.Token); await recreated.Completion.WaitAsync(timeout.Token); await recreated.StopAsync(timeout.Token);
        Require(recreatedFrames >= 3 && factory.ActiveSessionCount == 0 && nextView!.IsDisposed, "Explicit new window did not release its session.");
        Require(messages.Count == 0, "Standalone failure/recreation produced Vulkan validation messages.");
        Console.WriteLine($"Hosted {(openGL ? "OpenGL" : "Vulkan")} {(external ? "external report" : "draw failure")}: primary + tree + close errors preserved, once notification, registry zero, new shared-model window passed.");
    }

    private static async Task CreationFailureAsync(bool openGL, bool validation)
    {
        var primary = Failure(); var cleanup = new Exception("controlled creation rollback"); var closed = 0; Panel? root = null;
        var messages = new List<string>(); using var factory = new HostedUiSessionFactory(new FailingBackend(primary));
        var window = new HostedUiWindow(Options(openGL, validation, messages), factory,
            () => { root = new Panel(); root.Own(new ThrowingResource(cleanup)); return root; }, () => closed++);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ready = await RejectAsync<AggregateException>(() => window.StartAsync(timeout.Token));
        var completion = await RejectAsync<AggregateException>(() => window.Completion.WaitAsync(timeout.Token));
        var errors = OrderedErrors(completion).ToArray();
        Require(ReferenceEquals(ready, completion) && errors.Length == 2 && ReferenceEquals(errors[0], primary) && ReferenceEquals(errors[1], cleanup), "Creation rollback lost original/cleanup errors.");
        Require(root is { IsDisposed: true, IsAttached: false } && factory.ActiveSessionCount == 0 && closed == 1 && messages.Count == 0, "Creation failure leaked its host-owned root/resources.");
        await RejectAsync<AggregateException>(() => window.StopAsync(timeout.Token));
        Console.WriteLine($"Hosted {(openGL ? "OpenGL" : "Vulkan")} creation failure: primary/rollback errors, Ready/Completion, root release, registry zero and teardown passed.");
    }

    private static SilkUiWindowOptions Options(bool openGL, bool validation, List<string> messages)
    {
        SilkUiWindowOptions? options = null; var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UsePresentation<View1>(alloy =>
        {
            if (openGL) alloy.UseSkiaOpenGL(); else alloy.UseSkiaVulkan();
            alloy.UseSilkWindow(settings =>
            {
                options = settings; settings.Width = 900; settings.Height = 1100; settings.Title = "Controlled terminal failure";
                settings.ValidationMessage = validation ? messages.Add : null;
            });
        }).Services(services => services.Singleton<SettingsModel>()));
        // Select the backend through public configuration, then drive windows directly to inspect cleanup errors.
        using var configuration = builder.Build(); return options!;
    }

    private static void RegistryCleanup()
    {
        var backend = new CountingBackend(); using var factory = new HostedUiSessionFactory(backend);
        var error = new Exception("registry cleanup"); var firstRoot = new Panel(); firstRoot.Own(new ThrowingResource(error));
        var first = factory.Create(firstRoot, new Target(), Viewport); var second = factory.Create(new Panel(), new Target(), Viewport);
        var failures = Reject<AggregateException>(factory.Dispose).Flatten().InnerExceptions;
        Require(failures.Count == 1 && ReferenceEquals(failures[0], error) && first.IsDisposed && second.IsDisposed && backend.Disposals == 2 && factory.ActiveSessionCount == 0, "Registry stopped cleanup at the first failed session.");
        var rejected = new Panel(); try { Reject<ObjectDisposedException>(() => factory.Create(rejected, new Target(), Viewport)); } finally { rejected.Dispose(); }
    }

    private sealed class FailingRoot : Panel
    {
        internal UiRenderingException? Failure;
        internal FailingRoot(View1 view) => Add(view);
        protected override void DrawCore(IDrawingContext context) { if (Failure != null) throw Failure; }
    }
    private sealed class ThrowingResource(Exception error) : IDisposable { public void Dispose() => throw error; }
    private sealed class Target : UiRenderTarget;
    private sealed class FailingBackend(Exception error) : IRenderBackend
    { public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport) => throw error; }
    private sealed class CountingBackend : IRenderBackend
    {
        internal int Disposals;
        public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport) => new Surface(this);
        private sealed class Surface(CountingBackend owner) : IUiRenderSurface
        {
            public ITextLayoutService TextLayout => throw new NotSupportedException();
            public void Resize(UiViewport viewport) { }
            public void Render(Element root, UiViewport viewport) { }
            public void VerifyAvailable() { }
            public void Dispose() => owner.Disposals++;
        }
    }
    private static UiRenderingException Failure() => new("controlled", "DrawCore", UiRenderingFailureKind.BackendFailure, new InvalidOperationException("controlled terminal graphics failure"));
    private static IEnumerable<Exception> OrderedErrors(Exception error) => error is AggregateException aggregate
        ? aggregate.InnerExceptions.SelectMany(OrderedErrors) : new[] { error };
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static T Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T error) { return error; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
    private static async Task<T> RejectAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T error) { return error; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
