using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Replacements;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Tests.Frames;

public sealed class ViewportTransportTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static SceneSnapshot Scene() => new("room", 7, []);
    private static FrameMetadata Metadata(ulong frame = 1) => new(0, frame, 0, 0, 1);
    [Fact]
    public async Task StartResizeSuspend_RequireCurrentActualPresentationAndDrainConnectionBeforeChildStop()
    {
        var factory = new ReplacementRuntimeFixture(); var transport = new Transport();
        await using var supervisor = new SessionSupervisor(factory, Hash, Hash, "viewport-tests"); await using var frames = new FrameSession(supervisor, transport);
        var start = frames.StartAsync(Scene(), 32, 16); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3));
        var first = frames.Presentation!; Assert.Equal(ViewportTransportKind.Gpu, frames.TransportKind); Assert.Null(frames.Display); Assert.Null(frames.Surface); Assert.Equal(0, frames.MappingCount);
        var ticket = first.Issue(Metadata()); Assert.False(start.IsCompleted); Assert.True(frames.Presented(ticket)); await start;
        var resize = frames.ResizeAsync(64, 32); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3)); var second = frames.Presentation!;
        Assert.False(frames.Presented(ticket)); Assert.False(resize.IsCompleted); Assert.True(first.FirstPresented.IsCompletedSuccessfully);
        Assert.True(frames.Presented(second.Issue(Metadata()))); await resize; Assert.True(second.Surface.Generation > first.Surface.Generation);
        await frames.ResizeAsync(0, 0); Assert.True(frames.Presentation!.Surface.Suspended); Assert.Single(transport.Connections);
        var resume = frames.ResizeAsync(16, 16); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3)); // suspended configure also released one signal
        await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(frames.Presented(frames.Presentation!.Issue(Metadata()))); await resume;
        factory.Leases[0].BeforeStop = () => { Assert.True(transport.Connections[0].Disposed); return Task.CompletedTask; };
        await frames.StopAsync(); Assert.Null(frames.Presentation); Assert.True(factory.Leases[0].ResourcesReleased); Assert.Equal(1, transport.Connections[0].Disposals);
        Assert.False(frames.Presented(ticket));
    }
    [Fact]
    public async Task Stop_CancelsPendingFirstPresentAndFreshStartRejectsOldTicket()
    {
        var factory = new ReplacementRuntimeFixture(); var transport = new Transport();
        await using var supervisor = new SessionSupervisor(factory, Hash, Hash, "viewport-tests"); await using var frames = new FrameSession(supervisor, transport);
        var start = frames.StartAsync(Scene(), 32, 16); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3)); var old = frames.Presentation!.Issue(Metadata());
        await frames.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.True(factory.Leases[0].ResourcesReleased); Assert.True(transport.Connections[0].Disposed);
        var next = frames.StartAsync(Scene(), 32, 16); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3)); Assert.False(frames.Presented(old)); Assert.False(next.IsCompleted);
        Assert.True(frames.Presented(frames.Presentation!.Issue(Metadata()))); await next; Assert.Equal(2, factory.Leases.Count); await frames.StopAsync();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConnectOrConfigureFailure_PreservesOriginalCauseAndCleansOwnedRuntime(bool configure)
    {
        var factory = new ReplacementRuntimeFixture(); var failure = new IOException("transport setup failed");
        var transport = new Transport { ConnectError = configure ? null : failure, ConfigureError = configure ? failure : null };
        await using var supervisor = new SessionSupervisor(factory, Hash, Hash, "viewport-tests"); await using var frames = new FrameSession(supervisor, transport);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => frames.StartAsync(Scene(),32,16)));
        Assert.Null(frames.Presentation); Assert.True(factory.Leases[0].ResourcesReleased); Assert.Same(failure, supervisor.Status.Failure!.Cause);
        if (configure) Assert.True(Assert.Single(transport.Connections).Disposed); else Assert.Empty(transport.Connections);
    }
    [Fact]
    public async Task FailedConnectionCleanup_BlocksProcessStopAndNewStartUntilExplicitRetry()
    {
        var factory = new ReplacementRuntimeFixture(); var transport = new Transport();
        await using var supervisor = new SessionSupervisor(factory, Hash, Hash, "viewport-tests"); await using var frames = new FrameSession(supervisor, transport);
        var start = frames.StartAsync(Scene(),32,16); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3)); frames.Presented(frames.Presentation!.Issue(Metadata())); await start;
        var connection = transport.Connections[0]; connection.FailDispose = true;
        await Assert.ThrowsAsync<IOException>(() => frames.StopAsync()); Assert.Equal(0,factory.Leases[0].Stops); Assert.False(connection.Disposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => frames.StartAsync(Scene(),32,16)); Assert.Single(factory.Leases);
        connection.FailDispose=false; await frames.StopAsync(); Assert.True(connection.Disposed); Assert.Equal(2,connection.Disposals); Assert.True(factory.Leases[0].ResourcesReleased);
    }
    [Fact]
    public async Task AppStrategy_IsLazyAndOwnsDisposableStrategyExactlyOnce()
    {
        var factory = new ReplacementRuntimeFixture(); var strategy = new Transport(); var creates=0;
        await using var app=App.Build(c=>c.UseAether(o=>{o.HostContractId=Hash;o.GameBuildId=Hash;o.ReportDirectory="viewport-app-tests";o.RuntimeFactory=_=>factory;o.ViewportTransport=_=>{creates++;return strategy;};}));
        await app.StartAsync(); var host=app.Services.Resolve<AetherHost>()!; Assert.Equal(1,creates); Assert.Empty(factory.Leases); Assert.Empty(strategy.Connections); Assert.Equal(0,host.Gpu.ResourceCount);
        var one=host.Frames.DisposeAsync().AsTask(); var two=host.Frames.DisposeAsync().AsTask(); await Task.WhenAll(one,two); Assert.Equal(1,strategy.Disposals);
        await app.StopAsync(); await app.DisposeAsync(); Assert.Equal(1,strategy.Disposals); Assert.True(host.Editor.Status.Closed);
    }
    [Fact]
    public async Task FailedStartupAndCleanup_RetainsBothCausesAndBlocksChildStopUntilRetry()
    {
        var factory=new ReplacementRuntimeFixture();var cause=new IOException("configure cause");var transport=new Transport{ConfigureError=cause,FailNewCleanup=true};
        await using var supervisor=new SessionSupervisor(factory,Hash,Hash,"viewport-tests");await using var frames=new FrameSession(supervisor,transport);
        var error=await Assert.ThrowsAsync<AggregateException>(()=>frames.StartAsync(Scene(),32,16));
        Assert.Same(cause,error.InnerExceptions[0]);Assert.IsType<IOException>(error.InnerExceptions[1]);Assert.Equal(0,factory.Leases[0].Stops);Assert.False(factory.Leases[0].ResourcesReleased);
        transport.Connections[0].FailDispose=false;await frames.StopAsync();Assert.True(factory.Leases[0].ResourcesReleased);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WorkerFailure_FaultsPendingOrPresentedViewportAndReapsRuntime(bool afterPresentation)
    {
        var factory=new ReplacementRuntimeFixture();var transport=new Transport();var faulted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var supervisor=new SessionSupervisor(factory,Hash,Hash,"viewport-tests",trace:s=>{if(s.State==SessionState.Faulted)faulted.TrySetResult();});
        await using var frames=new FrameSession(supervisor,transport);var start=frames.StartAsync(Scene(),32,16);await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3));
        if(afterPresentation){frames.Presented(frames.Presentation!.Issue(Metadata()));await start;}
        var cause=new IOException("delivery worker failed");transport.Connections[0].Fail(cause);
        if(!afterPresentation)Assert.Same(cause,await Assert.ThrowsAsync<IOException>(()=>start));
        await faulted.Task.WaitAsync(TimeSpan.FromSeconds(3));await frames.StopAsync();
        Assert.Null(frames.Presentation);Assert.True(transport.Connections[0].Disposed);Assert.True(factory.Leases[0].ResourcesReleased);Assert.Same(cause,supervisor.Status.Failure!.Cause);
    }
    [Fact]
    public async Task BackgroundCleanupFailure_IsObservableAndSuccessfulRetryDoesNotReplayFaultedWatcher()
    {
        var factory=new ReplacementRuntimeFixture();var transport=new Transport();await using var supervisor=new SessionSupervisor(factory,Hash,Hash,"viewport-tests");await using var frames=new FrameSession(supervisor,transport);
        var start=frames.StartAsync(Scene(),32,16);await transport.Configured.WaitAsync(TimeSpan.FromSeconds(3));frames.Presented(frames.Presentation!.Issue(Metadata()));await start;
        var cause=new IOException("background delivery failure");var connection=transport.Connections[0];connection.FailDispose=true;connection.Fail(cause);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));while(frames.TransportError is not AggregateException)await Task.Delay(5,deadline.Token);
        var error=Assert.IsType<AggregateException>(frames.TransportError);Assert.Same(cause,error.InnerExceptions[0]);Assert.False(connection.Disposed);Assert.Equal(0,factory.Leases[0].Stops);
        connection.FailDispose=false;await frames.StopAsync().WaitAsync(deadline.Token);Assert.True(connection.Disposed);Assert.True(factory.Leases[0].ResourcesReleased);Assert.Same(error,frames.TransportError);
        await frames.StopAsync();Assert.Equal(1,factory.Leases[0].Stops);
    }
    private sealed class Transport : IGameViewportTransport,IAsyncDisposable
    {
        internal readonly List<Connection> Connections=[]; internal readonly SemaphoreSlim Configured=new(0); internal Exception? ConnectError,ConfigureError; internal int Disposals;internal bool FailNewCleanup;
        public ViewportTransportKind Kind=>ViewportTransportKind.Gpu;
        public Task<IGameViewportConnection> ConnectAsync(IRuntimeLease runtime,CancellationToken token)
        {token.ThrowIfCancellationRequested();if(ConnectError!=null)throw ConnectError;var connection=new Connection(this){FailDispose=FailNewCleanup};Connections.Add(connection);return Task.FromResult<IGameViewportConnection>(connection);}
        public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
    }
    private sealed class Connection(Transport owner):IGameViewportConnection
    {
        private TaskCompletionSource _work=new(TaskCreationOptions.RunContinuationsAsynchronously); internal bool Disposed,FailDispose; internal int Disposals;
        public Task Completion=>_work.Task;
        internal void Fail(Exception cause)=>_work.TrySetException(cause);
        public Task ConfigureAsync(ViewportPresentation presentation,CancellationToken token)
        {_work.TrySetCanceled();_work=new(TaskCreationOptions.RunContinuationsAsynchronously);if(owner.ConfigureError!=null)throw owner.ConfigureError;owner.Configured.Release();return Task.CompletedTask;}
        public ValueTask DisposeAsync(){Disposals++;if(FailDispose)throw new IOException("retained connection cleanup");Disposed=true;_work.TrySetCanceled();return ValueTask.CompletedTask;}
    }
}
