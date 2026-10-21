using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class HostedUiWindowCloseTests
{
    [Fact]
    public async Task RequestBeforeStartup_ClosesWithoutAllocatingOrCallingHandlers()
    {
        await using var window = new HostedUiWindow(new SilkUiWindowOptions(), new NoSessions(), () => throw new Exception("Root allocated"));
        window.Closing += _ => throw new Exception("No owner or UI exists yet");
        Assert.False(window.IsClosed); Assert.False(window.IsCloseRequested);
        Assert.True(await window.RequestCloseAsync()); Assert.True(window.IsClosed); Assert.False(window.IsCloseRequested);
        Assert.True(window.Completion.IsCompletedSuccessfully); Assert.Null(window.CloseError);
        Assert.True(await window.RequestCloseAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => window.StartAsync());
        Assert.Throws<ObjectDisposedException>(window.RequestFrame);
        Assert.Throws<ObjectDisposedException>(() => window.PostWindow(_ => { }));
    }

    [Fact]
    public async Task PreCanceledRequest_DoesNotAcceptClose()
    {
        await using var window = new HostedUiWindow(new SilkUiWindowOptions(), new NoSessions(), () => throw new Exception("Root allocated"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => window.RequestCloseAsync(new CancellationToken(true)));
        Assert.False(window.IsCloseRequested); Assert.False(window.IsClosed); Assert.False(window.Completion.IsCompleted);
        window.Close(); await window.Completion; Assert.True(window.IsClosed);
    }

    [Fact]
    public async Task RejectedStartup_FaultsCloseWaitAndNeverReportsClosedSuccess()
    {
        await using var scope = new HostedUiDispatcher(); await scope.StopAsync();
        var window = new HostedUiWindow(new SilkUiWindowOptions(), new NoSessions(), () => throw new Exception("Root allocated"), null, scope);
        var startup = await Record.ExceptionAsync(() => window.StartAsync()); Assert.NotNull(startup);
        Assert.Same(startup, await Record.ExceptionAsync(() => window.RequestCloseAsync()));
        Assert.False(window.IsClosed); Assert.False(window.IsCloseRequested);
        Assert.Same(startup, await Record.ExceptionAsync(() => window.StopAsync()));
    }

    private sealed class NoSessions : IUiWindowSessionFactory
    {
        public bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi) => true;
        public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
            => throw new Exception("Session allocated");
    }
}
