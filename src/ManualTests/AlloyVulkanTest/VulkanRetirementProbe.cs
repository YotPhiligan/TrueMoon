using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

internal sealed class VulkanRetirementProbe
{
    private readonly List<WeakReference> _released = [];
    private readonly Publisher _publisher = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private UiSession? _ui;
    private Button? _button;
    private VulkanHostCompositor? _compositor;
    private UiViewport _sceneSize;
    private int _cycle, _phase, _appliedPhase = -1, _frames, _minimizes;
    private double? _restoreAt;
    private VulkanPresentationResources? _baseline;
    internal static void Run(bool validation, int cycles, bool legacy)
    {
        if (cycles is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(cycles));
        new VulkanRetirementProbe().RunCore(validation, cycles, legacy);
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private void RunCore(bool validation, int cycles, bool legacy)
    {
        using var app = App.Builder(b => b.UseDI()).Setup(a => a.UseAlloy(o => o.UseSkiaVulkan().UseExternalHost())).Build();
        app.StartAsync().GetAwaiter().GetResult();
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        using var window = new SilkWindowHost(400, 240, "TrueMoon Vulkan retirement soak");
        var device = new VulkanDevice(window.NativeWindow, validation ? Console.Error.WriteLine : null, !legacy);
        using (device)
        {
            var presenter = new VulkanWindowPresenter(device);
            Console.WriteLine($"Retirement soak: {cycles} cycles; {device.PresentFenceExtension ?? "legacy reacquisition"}.");
            try
            {
                window.Input += input => _ui?.HandleInput(input);
                window.NativeWindow.Update += _ =>
                {
                    if (_restoreAt is { } time && _elapsed.Elapsed.TotalSeconds >= time)
                    { window.NativeWindow.WindowState = WindowState.Normal; _restoreAt = null; }
                    if (_elapsed.Elapsed.TotalSeconds > Math.Max(60, cycles)) window.Close();
                };
                window.RenderRequested += () =>
                {
                    if (_cycle >= cycles) { window.Close(); return; }
                    if (_appliedPhase != _phase)
                    {
                        _appliedPhase = _phase;
                        if (_phase == 0)
                        {
                            window.NativeWindow.Size = new Vector2D<int>(400, 240);
                            CreateSession(factory, device, window);
                            presenter.InvalidateSwapchain();
                        }
                        if (_phase == 4)
                        { window.NativeWindow.Size = new Vector2D<int>(480, 280); presenter.InvalidateSwapchain(); }
                        if (_phase == 6 && _cycle % 20 == 0)
                        {
                            window.NativeWindow.WindowState = WindowState.Minimized;
                            _ui!.Resize(new UiViewport(0, 0));
                            Require(!_ui.Update(), "Minimized UI must suspend.");
                            _restoreAt = _elapsed.Elapsed.TotalSeconds + .05; _minimizes++;
                        }
                    }
                    if (window.NativeWindow.WindowState == WindowState.Minimized) return;
                    var viewport = window.Viewport with { Scale = new[] { 1f, 1.5f, 2f }[_cycle % 3] };
                    if (viewport.IsEmpty) return;
                    var ui = _ui!;
                    if (ui.Viewport != viewport) ui.Resize(viewport);
                    if (_phase == 8)
                        Require(ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)).PointerCaptured, "Expected capture before disposal.");
                    _publisher.Raise(); ui.Update();
                    if (_compositor == null || viewport.Width != _sceneSize.Width || viewport.Height != _sceneSize.Height)
                    {
                        _compositor?.Dispose(); _compositor = new VulkanHostCompositor(device, viewport.Width, viewport.Height);
                        _sceneSize = viewport;
                    }
                    var lease = ui.AcquireVulkanTexture();
                    var layout = (ImageLayout)lease.Info.Layout;
                    try { _compositor.Compose(lease.Info, _cycle % 2, state => layout = state, false); }
                    finally { lease.Return(layout); }
                    if (!presenter.PresentImage(_compositor.Output, viewport, _compositor.OutputStateChanged)) return;
                    _frames++;
                    Require(presenter.Resources.PeakLiveSwapchains <= 4, "Retired swapchains must not accumulate across cycles.");
                    if (++_phase != 10) return;
                    CheckSteadyResources(presenter);
                    ReleaseSession(factory);
                    _phase = 0; _appliedPhase = -1; _cycle++;
                    if (_cycle % 25 == 0)
                        Console.WriteLine($"Soak progress: {_cycle}/{cycles}, live swapchains/semaphores/fences: {presenter.Resources.LiveSwapchains}/{presenter.Resources.LiveSemaphores}/{presenter.Resources.LiveFences}; subscriptions: {_publisher.Count}.");
                };
                window.Run();
                Require(_cycle == cycles && factory.ActiveSessionCount == 0 && _publisher.Count == 0, "All cycles/sessions/subscriptions must finish.");
                Require(presenter.SwapchainGenerations >= cycles * 2 && presenter.Resources.ConfirmedPresentations > 0, "Expected repeated swapchains and completion proofs.");
            }
            finally
            {
                ReleaseSession(factory); _compositor?.Dispose(); _compositor = null;
                presenter.Dispose();
            }
            var resources = presenter.Resources;
            Require(resources.LiveSwapchains == 0 && resources.LiveSemaphores == 0 && resources.LiveFences == 0 && resources.LiveCommandPools == 0 && resources.RetiredSwapchains == 0,
                "All presenter-owned Vulkan objects must be destroyed.");
            Require(resources.CreatedSwapchains == resources.DestroyedSwapchains, "Every created swapchain must be destroyed.");
            app.StopAsync().GetAwaiter().GetResult(); device.WaitIdle();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Require(_released.All(reference => !reference.IsAlive), "Disposed UI sessions/trees must not remain rooted by host or publisher.");
            Console.WriteLine($"Retirement soak passed: {_cycle} sessions, {_frames} frames, {resources.CreatedSwapchains} created/destroyed swapchains, {_minimizes} minimize/restore cycles, peak {resources.PeakLiveSwapchains} generations, {resources.ConfirmedPresentations} completion proofs; owned Vulkan objects, subscriptions and retained UI references: 0.");
        }
        Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Validation errors/warnings including teardown.");
        if (validation) Console.WriteLine("Vulkan core/synchronization validation: 0 errors, 0 warnings, including retirement soak teardown.");
    }
    private void CheckSteadyResources(VulkanWindowPresenter presenter)
    {
        var current = presenter.Resources;
        Require(current.LiveSwapchains == 1 && current.RetiredSwapchains == 0, "Retired generations must drain during presentation.");
        _baseline ??= current;
        Require(current.LiveSemaphores == _baseline.Value.LiveSemaphores && current.LiveFences == _baseline.Value.LiveFences && current.LiveCommandPools == 1,
            "Owned synchronization objects must remain at the steady-state baseline.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CreateSession(HostedUiSessionFactory factory, VulkanDevice device, SilkWindowHost window)
    {
        var root = new Panel(); var button = new Button($"Soak session {_cycle}"); root.Add(button);
        root.Own(_publisher.Subscribe(() => root.Invalidate()));
        _ui = factory.Create(root, SilkVulkanHost.CreateTarget(device), window.Viewport, window); _button = button;
        Require(factory.ActiveSessionCount == 1 && _publisher.Count == 1, "Exactly one session/subscription should be active.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReleaseSession(HostedUiSessionFactory factory)
    {
        if (_ui == null) return;
        _released.Add(new WeakReference(_ui)); _released.Add(new WeakReference(_ui.Root));
        _ui.Dispose(); _ui = null;
        Require(!_button!.IsPressed && factory.ActiveSessionCount == 0 && _publisher.Count == 0, "Session disposal must cancel capture and unregister subscriptions.");
        _button = null; _publisher.Raise();
    }
    private sealed class Publisher
    {
        private event Action? Tick;
        internal int Count { get; private set; }
        internal IDisposable Subscribe(Action callback)
        { Tick += callback; Count++; return new Subscription(() => { Tick -= callback; Count--; }); }
        internal void Raise() => Tick?.Invoke();
        private sealed class Subscription(Action unsubscribe) : IDisposable
        {
            private Action? _unsubscribe = unsubscribe;
            public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
