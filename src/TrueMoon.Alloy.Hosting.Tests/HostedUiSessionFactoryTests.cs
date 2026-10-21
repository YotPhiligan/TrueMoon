using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class HostedUiSessionFactoryTests
{
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
        var child = Text.Plain("child"); Exception? failure = null;
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
        using var root = Text.Plain("construction");
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

    private static UiSession Create(HostedUiSessionFactory factory, Element? root = null)
        => factory.Create(root ?? Text.Plain("session"), new Target(), new(100, 100));

    private static void AssertCreateBlocked(HostedUiSessionFactory factory, Backend backend)
    {
        var count = backend.Surfaces.Count; using var unowned = Text.Plain("rejected");
        Assert.Throws<ObjectDisposedException>(() => Create(factory, unowned));
        Assert.False(unowned.IsAttached); Assert.False(unowned.IsDisposed);
        Assert.Equal(count, backend.Surfaces.Count);
    }

    private sealed class Target : UiRenderTarget;
    private sealed class Backend : IRenderBackend
    {
        public List<Surface> Surfaces { get; } = [];
        public Action? BeforeCreate { get; set; }
        public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport)
        { BeforeCreate?.Invoke(); var surface = new Surface(); Surfaces.Add(surface); return surface; }
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
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose()
        { Disposals++; BeforeDispose?.Invoke(); if (DisposalError != null) throw DisposalError; }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(10, 10); }
    private sealed class Cleanup(Action dispose) : IDisposable
    { public void Dispose() => dispose(); }
}
