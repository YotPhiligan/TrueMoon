using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Views;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class HostedUiSessionFactoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommonFactoryBridge_PreservesCreationArgumentsAndSessionOwnership(bool genericBridge)
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var root = new Text("typed session");
        var target = new Target(); var viewport = new UiViewport(320, 240, 1.5f);
        var clipboard = new Clipboard();
        IFactory<UiSession, UiSessionCreation> typed = factory;
        var arguments = new UiSessionCreation(root, target, viewport, clipboard);
        using var session = genericBridge ? ((IFactory<UiSession>)typed).Create<object>(arguments)! : typed.Create(arguments);
        Assert.Same(root, session.Root); Assert.True(root.IsAttached);
        Assert.Same(target, backend.LastTarget); Assert.Equal(viewport, backend.LastViewport); Assert.Equal(viewport, session.Viewport);
        session.SetClipboardText("through bridge");
        Assert.Equal("through bridge", session.GetClipboardText()); Assert.Equal(2, clipboard.Accesses);
        Assert.Equal(1, factory.ActiveSessionCount); Assert.Equal(0, Assert.Single(backend.Surfaces).Disposals);
        session.Dispose();
        Assert.True(root.IsDisposed); Assert.Equal(0, factory.ActiveSessionCount); Assert.Equal(1, backend.Surfaces[0].Disposals);
        factory.Dispose();
        Assert.Equal(1, backend.Surfaces[0].Disposals); Assert.Equal(0, clipboard.Disposals);
        clipboard.Dispose(); Assert.Equal(1, clipboard.Disposals);
    }

    [Theory]
    [InlineData("typed-null")]
    [InlineData("generic-null")]
    [InlineData("mismatch")]
    [InlineData("no-arguments")]
    public void CommonFactoryBridge_InvalidArgumentsDoNotTakeRootOwnership(string invalid)
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var root = new Text("unowned");
        IFactory<UiSession, UiSessionCreation> typed = factory;
        IFactory<UiSession> bridge = typed;
        switch (invalid)
        {
            case "typed-null": Assert.Equal("arguments", Assert.Throws<ArgumentNullException>(() => typed.Create(null!)).ParamName); break;
            case "generic-null": Assert.Equal("arguments", Assert.Throws<ArgumentNullException>(() => bridge.Create<UiSessionCreation>(null)).ParamName); break;
            case "mismatch": Assert.Equal("data", Assert.Throws<ArgumentException>(() => bridge.Create("wrong arguments")).ParamName); break;
            default: Assert.Throws<InvalidOperationException>(() => bridge.Create()); break;
        }
        Assert.False(root.IsAttached); Assert.False(root.IsDisposed); Assert.Empty(backend.Surfaces); Assert.Equal(0, factory.ActiveSessionCount);
        using var session = typed.Create(new(root, new Target(), new(100, 100)));
        Assert.Same(root, session.Root); Assert.True(root.IsAttached); Assert.Equal(1, factory.ActiveSessionCount);
    }

    [Fact]
    public void Dispose_DuringUpdateCanBeRetriedAfterUpdateEnds()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var session = Create(factory); Exception? failure = null;
        session.Post(() => failure = Record.Exception(factory.Dispose));
        session.Update();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("Update", failure!.Message);
        Assert.False(session.IsDisposed); Assert.False(session.Root.IsDisposed);
        Assert.Equal(1, factory.ActiveSessionCount); Assert.Equal(0, backend.Surfaces[0].Disposals);
        AssertCreateBlocked(factory, backend);

        factory.Dispose();
        Assert.True(session.IsDisposed); Assert.True(session.Root.IsDisposed);
        Assert.Equal(0, factory.ActiveSessionCount); Assert.Equal(1, backend.Surfaces[0].Disposals);
        factory.Dispose(); Assert.Equal(1, backend.Surfaces[0].Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_PartialFailureCleansOtherSessionsAndRetryOnlyCleansRemaining(bool failingFirst)
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var first = Create(factory); using var second = Create(factory);
        var busy = failingFirst ? first : second; var other = failingFirst ? second : first;
        var busySurface = backend.Surfaces[failingFirst ? 0 : 1]; var otherSurface = backend.Surfaces[failingFirst ? 1 : 0];
        Exception? failure = null;
        busy.Post(() => failure = Record.Exception(factory.Dispose)); busy.Update();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.False(busy.IsDisposed); Assert.True(other.IsDisposed); Assert.True(other.Root.IsDisposed);
        Assert.Equal(1, factory.ActiveSessionCount);
        Assert.Equal(0, busySurface.Disposals); Assert.Equal(1, otherSurface.Disposals);

        factory.Dispose();
        Assert.True(busy.IsDisposed); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.Equal(1, busySurface.Disposals); Assert.Equal(1, otherSurface.Disposals);
    }

    [Fact]
    public void Dispose_MultipleBusySessionsPreserveErrorsAndMakeProgressAcrossRetries()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var outer = Create(factory); using var inner = Create(factory);
        Exception? bothBusy = null, outerBusy = null; var afterBoth = -1; var afterInner = -1;
        outer.Post(() =>
        {
            inner.Post(() => { bothBusy = Record.Exception(factory.Dispose); afterBoth = factory.ActiveSessionCount; });
            inner.Update();
            outerBusy = Record.Exception(factory.Dispose); afterInner = factory.ActiveSessionCount;
        });
        outer.Update();
        var aggregate = Assert.IsType<AggregateException>(bothBusy);
        var errors = aggregate.Flatten().InnerExceptions; Assert.Equal(2, errors.Count);
        Assert.All(errors, e => { Assert.IsType<InvalidOperationException>(e); Assert.Contains("Update", e.Message); });
        Assert.IsType<InvalidOperationException>(outerBusy);
        Assert.Equal(2, afterBoth); Assert.Equal(1, afterInner);
        Assert.False(outer.IsDisposed); Assert.True(inner.IsDisposed);
        Assert.Equal(0, backend.Surfaces[0].Disposals); Assert.Equal(1, backend.Surfaces[1].Disposals);
        factory.Dispose(); Assert.True(outer.IsDisposed); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public async Task StopAsync_BorrowedFramePreflightRetainsAllSessionsUntilOwnerRetries()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var first = Create(factory); using var second = Create(factory);
        backend.Surfaces[0].Borrowed = true;
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.StopAsync());
            Assert.Equal("Frame is borrowed.", error.Message);
            Assert.Equal(2, factory.ActiveSessionCount); Assert.False(first.IsDisposed); Assert.False(second.IsDisposed);
            Assert.All(backend.Surfaces, s => Assert.Equal(0, s.Disposals));
            AssertCreateBlocked(factory, backend);
        }
        finally { backend.Surfaces[0].Borrowed = false; }

        await factory.StopAsync(); await factory.StopAsync();
        Assert.Equal(0, factory.ActiveSessionCount); Assert.True(first.IsDisposed); Assert.True(second.IsDisposed);
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public void Dispose_ForeignThreadFailureRetainsOwnershipAndOwnerCanRetry()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var session = Create(factory); Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(factory.Dispose));
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(3)));
        Assert.IsType<InvalidOperationException>(failure); Assert.Contains("owner thread", failure!.Message);
        Assert.False(session.IsDisposed); Assert.Equal(1, factory.ActiveSessionCount);
        Assert.Equal(0, backend.Surfaces[0].Disposals); AssertCreateBlocked(factory, backend);

        factory.Dispose();
        Assert.True(session.IsDisposed); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.Equal(1, backend.Surfaces[0].Disposals);
    }

    [Fact]
    public void Dispose_TreeMutationGuardRetainsSessionUntilMutationCompletes()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        var root = new Panel(); using var session = Create(factory, root);
        var child = new Text("child"); Exception? failure = null;
        child.AttachmentChanged += () => { if (child.IsAttached) failure = Record.Exception(factory.Dispose); };
        root.Add(child);
        Assert.IsType<InvalidOperationException>(failure); Assert.Contains("reentrant", failure!.Message);
        Assert.False(session.IsDisposed); Assert.False(root.IsDisposed); Assert.Same(root, child.Parent);
        Assert.Equal(1, factory.ActiveSessionCount); Assert.Equal(0, backend.Surfaces[0].Disposals);
        AssertCreateBlocked(factory, backend);
        factory.Dispose();
        Assert.True(session.IsDisposed); Assert.True(root.IsDisposed); Assert.True(child.IsDisposed);
        Assert.Equal(0, factory.ActiveSessionCount); Assert.Equal(1, backend.Surfaces[0].Disposals);
    }

    [Fact]
    public void Dispose_ReentrantCleanupReturnsToOuterAttemptWithoutRepeatingDisposal()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var first = Create(factory); using var second = Create(factory); var callbacks = 0;
        foreach (var surface in backend.Surfaces) surface.BeforeDispose = () =>
        {
            callbacks++; factory.Dispose(); AssertCreateBlocked(factory, backend);
        };
        factory.Dispose();
        Assert.Equal(2, callbacks); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.True(first.IsDisposed); Assert.True(second.IsDisposed);
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
        factory.Dispose(); Assert.Equal(2, callbacks);
    }

    [Fact]
    public void Dispose_PreservesCleanupCausesAndDoesNotRepeatAlreadyDisposedSessions()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var first = Create(factory); using var second = Create(factory);
        var rootError = new InvalidDataException("root subscription");
        var renderError = new IOException("renderer cleanup");
        first.Root.Own(new Cleanup(() => throw rootError)); backend.Surfaces[0].DisposalError = renderError;
        var error = Assert.Throws<AggregateException>(factory.Dispose);
        var causes = error.Flatten().InnerExceptions;
        Assert.Equal(2, causes.Count); Assert.Contains(rootError, causes); Assert.Contains(renderError, causes);
        Assert.True(first.IsDisposed); Assert.True(first.Root.IsDisposed); Assert.True(second.IsDisposed);
        Assert.Equal(0, factory.ActiveSessionCount); Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
        factory.Dispose(); Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals)); AssertCreateBlocked(factory, backend);
    }

    [Fact]
    public void Dispose_PreflightExceptionIsPreservedAndRetryAfterGuardRecoveryWorks()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var session = Create(factory); var guardError = new InvalidOperationException("guard fixture");
        backend.Surfaces[0].GuardError = guardError;
        try
        {
            Assert.Same(guardError, Assert.Throws<InvalidOperationException>(factory.Dispose));
            Assert.Equal(1, factory.ActiveSessionCount); Assert.False(session.IsDisposed);
            Assert.Equal(0, backend.Surfaces[0].Disposals); AssertCreateBlocked(factory, backend);
        }
        finally { backend.Surfaces[0].GuardError = null; }
        factory.Dispose(); Assert.True(session.IsDisposed); Assert.Equal(0, factory.ActiveSessionCount);
    }

    [Fact]
    public async Task StopAsync_EmptyRegistryRejectsNewCreationAndRemainsIdempotent()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        await factory.StopAsync(); factory.Dispose(); await factory.StopAsync();
        Assert.Equal(0, factory.ActiveSessionCount); AssertCreateBlocked(factory, backend); Assert.Empty(backend.Surfaces);
    }

    [Fact]
    public void SessionDisposal_RemovesRegistryEntryWithoutStoppingFactory()
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var first = Create(factory); first.Dispose(); Assert.Equal(0, factory.ActiveSessionCount);
        using var second = Create(factory); Assert.Equal(1, factory.ActiveSessionCount); Assert.False(second.IsDisposed);
        factory.Dispose(); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_ReentrantStopIsRejectedWithoutTakingRootOwnership(bool attachmentCallback)
    {
        var backend = new Backend(); using var factory = new HostedUiSessionFactory(backend);
        using var root = new Text("construction");
        Action stopWhenAttached = () => { if (root.IsAttached) factory.Dispose(); };
        if (attachmentCallback) root.AttachmentChanged += stopWhenAttached;
        else backend.BeforeCreate = factory.Dispose;
        var error = Assert.Throws<InvalidOperationException>(() => Create(factory, root));
        Assert.Contains("creation", error.Message);
        Assert.False(root.IsDisposed); Assert.False(root.IsAttached); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.Equal(attachmentCallback ? 1 : 0, backend.Surfaces.Count);
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
        root.AttachmentChanged -= stopWhenAttached; backend.BeforeCreate = null;
        using var session = Create(factory, root); Assert.Equal(1, factory.ActiveSessionCount);
        Assert.False(root.IsDisposed); factory.Dispose(); Assert.True(root.IsDisposed);
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public async Task UseAlloy_UsesInjectedControlFactoryWithoutChangingOtherAppOrDirectSessions()
    {
        var custom = new UiControlFactories();
        custom.Register(new Factory<Text>(() => new CountingText()));
        await using var first = BuildApp(new Backend(), custom);
        await using var second = BuildApp(new Backend());

        using var firstRoot = new DeferredRow();
        using var secondRoot = new DeferredRow();
        using var directRoot = new DeferredRow();
        using var firstSession = Required<IUiSessionFactory>(first).Create(firstRoot, new Target(), new(100, 100));
        using var secondSession = Required<IUiSessionFactory>(second).Create(secondRoot, new Target(), new(100, 100));
        using var directSession = new UiSession(directRoot, new Surface(), new(100, 100));

        Assert.IsType<CountingText>(firstRoot.Label);
        Assert.IsType<Text>(secondRoot.Label);
        Assert.IsType<Text>(directRoot.Label);
    }

    [Fact]
    public async Task UseAlloy_FailedDeferredFactoryReleasesDraftAndSurfaceAndAllowsFreshRoot()
    {
        var backend = new Backend();
        var factories = new UiControlFactories();
        var drafts = new List<CountingText>();
        var failure = new InvalidOperationException("Row factory failed");
        var fail = true;
        factories.Register(new Factory<Text>(() =>
        {
            var text = new CountingText();
            drafts.Add(text);
            return text;
        }));
        factories.Register(new Factory<HStack>(() => fail ? throw failure : new HStack()));
        await using var app = BuildApp(backend, factories);
        var sessions = Required<IUiSessionFactory>(app);
        var registry = Required<HostedUiSessionFactory>(app);
        using var root = new DeferredRow();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => sessions.Create(root, new Target(), new(100, 100))));
        var draft = Assert.Single(drafts);
        var surface = Assert.Single(backend.Surfaces);
        Assert.True(draft.IsDisposed);
        Assert.Equal(1, draft.Disposals);
        Assert.Equal(1, surface.Disposals);
        Assert.Equal(0, registry.ActiveSessionCount);
        Assert.False(root.IsAttached);
        Assert.False(root.IsDisposed);
        Assert.Null(root.Child);
        root.Dispose();

        fail = false;
        using var fresh = new DeferredRow();
        using var retry = sessions.Create(fresh, new Target(), new(100, 100));
        Assert.Equal(1, registry.ActiveSessionCount);
        Assert.Equal(2, drafts.Count);
        Assert.Same(drafts[1], fresh.Label);
        Assert.True(fresh.Label.IsAttached);
        Assert.Equal(2, backend.Surfaces.Count);
        Assert.Equal(0, backend.Surfaces[1].Disposals);
        retry.Dispose();
        Assert.Equal(0, registry.ActiveSessionCount);
        Assert.All(drafts, text => Assert.Equal(1, text.Disposals));
        Assert.All(backend.Surfaces, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public async Task BorrowAfterPresentation_BlocksOwnerCleanupButNotSiblingInputAndFrame()
    {
        var backend = new Backend();
        using var registry = new HostedUiSessionFactory(backend);
        var loop = new UiOwnerLoop();
        var timeout = TimeSpan.FromSeconds(5);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sibling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        UiSession first = null!, second = null!;
        var firstPump = new UiFramePump(UiFrameMode.OnDemand, 60, loop.Wake);
        var secondPump = new UiFramePump(UiFrameMode.OnDemand, 60, loop.Wake);
        var clicks = 0;
        try
        {
            loop.Schedule(() =>
            {
                first = Create(registry);
                firstPump.Attach(first);
            }, () =>
            {
                Assert.True(firstPump.Tick(false, () => true));
                // Model a FramePresented subscriber retaining the texture after successful presentation.
                ((Surface)first.Rendering).Borrowed = true;
                return true;
            }, () => { firstPump.Dispose(); first.Dispose(); }, () => false, new TaskCompletionSource(), closed);
            loop.Schedule(() =>
            {
                var action = new Button("Continue"); action.Click += () => clicks++;
                second = Create(registry, action);
                secondPump.Attach(second);
            }, () =>
            {
                if (!closed.Task.IsCompleted) return false;
                Assert.False(first.IsDisposed);
                Assert.Equal(0, ((Surface)first.Rendering).Disposals);
                Assert.Equal(2, registry.ActiveSessionCount);
                Assert.True(secondPump.Tick(false, () => true, immediate: true));
                second.HandleInput(new(InputKind.PointerDown, 5, 5));
                second.HandleInput(new(InputKind.PointerUp, 5, 5));
                second.RequestFrame();
                Assert.True(secondPump.Tick(false, () => true, immediate: true));
                return true;
            }, () =>
            {
                // The harness deliberately retains the first session so its owner can retry.
                ((Surface)first.Rendering).Borrowed = false;
                UiCleanup.Complete(null, first.Dispose, secondPump.Dispose, second.Dispose);
            }, () => false, new TaskCompletionSource(), sibling);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => closed.Task.WaitAsync(timeout));
            Assert.Equal("Frame is borrowed.", error.Message);
            await sibling.Task.WaitAsync(timeout);
            Assert.Equal(1, clicks);
            Assert.Equal(0, registry.ActiveSessionCount);
            Assert.All(backend.Surfaces, surface => Assert.Equal(1, surface.Disposals));
        }
        finally { await loop.StopAsync().WaitAsync(timeout); }
    }

    private static IApp BuildApp(Backend backend, IUiControlFactories? factories = null) => App.Build(context =>
    {
        if (factories != null) context.Services(services => services.Singleton<IUiControlFactories>(_ => factories));
        context.UseAlloy(options => options.UseRenderer(() => backend).UseExternalHost());
    });

    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));

    private sealed class DeferredRow : View
    {
        public DeferredRow() => Content(ui => ui.HStack(ui.Create<Text>().Element));
        public Text Label => Assert.IsAssignableFrom<Text>(Assert.Single(Assert.IsType<HStack>(Child).Items));
    }

    private sealed class CountingText : Text
    {
        public int Disposals { get; private set; }
        public override void Dispose() { Disposals++; base.Dispose(); }
    }

    private static UiSession Create(HostedUiSessionFactory factory, Element? root = null)
        => factory.Create(root ?? new Text("session"), new Target(), new(100, 100));

    private static void AssertCreateBlocked(HostedUiSessionFactory factory, Backend backend)
    {
        var count = backend.Surfaces.Count; using var unowned = new Text("rejected");
        Assert.Throws<ObjectDisposedException>(() => Create(factory, unowned));
        Assert.False(unowned.IsAttached); Assert.False(unowned.IsDisposed);
        Assert.Equal(count, backend.Surfaces.Count);
    }

    private sealed class Target : UiRenderTarget;
    private sealed class Backend : IRenderBackend
    {
        public List<Surface> Surfaces { get; } = [];
        public Action? BeforeCreate { get; set; }
        public UiRenderTarget? LastTarget { get; private set; }
        public UiViewport LastViewport { get; private set; }
        public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport)
        { BeforeCreate?.Invoke(); LastTarget = target; LastViewport = viewport; var surface = new Surface(); Surfaces.Add(surface); return surface; }
    }
    private sealed class Surface : IUiRenderSurface
    {
        public int Disposals { get; private set; }
        public bool Borrowed { get; set; }
        public Exception? GuardError { get; set; }
        public Exception? DisposalError { get; set; }
        public Action? BeforeDispose { get; set; }
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable()
        { if (GuardError != null) throw GuardError; if (Borrowed) throw new InvalidOperationException("Frame is borrowed."); }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose()
        { Disposals++; BeforeDispose?.Invoke(); if (DisposalError != null) throw DisposalError; }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(10, 10); }
    private sealed class Cleanup(Action dispose) : IDisposable
    { public void Dispose() => dispose(); }
    private sealed class Clipboard : IUiClipboard, IDisposable
    {
        private string? _text;
        public int Accesses, Disposals;
        public string? GetText() { Accesses++; return _text; }
        public void SetText(string text) { Accesses++; _text = text; }
        public void Dispose() => Disposals++;
    }
}
