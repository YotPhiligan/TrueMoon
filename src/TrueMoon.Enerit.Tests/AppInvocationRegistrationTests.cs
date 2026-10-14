using System.Buffers;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;

namespace TrueMoon.Enerit.Tests;

public class AppInvocationRegistrationTests
{
    private static void Metadata()
    {
        InvocationServiceStorage.Shared.Register<IMarker, Proxy>(r => r.Singleton<IMarker>(p => new Proxy(p.Resolve<IInvocationClient<IMarker>>())));
        InvocationServiceStorage.Shared.RegisterHandler<IInvocationServerHandler<IMarker>, Handler<IMarker>>(r => r.Singleton<IInvocationServerHandler<IMarker>>(p => new Handler<IMarker>(p.Resolve<IMarker>())));
    }
    private static IAppBuilder Builder(bool di) => di ? App.Builder(c => c.UseDI()) : App.Builder(_ => { });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProxyFactories_AreAppLocalIdempotentAndShareStartStopLifetime(bool di)
    {
        Metadata(); var firstFactory = new ClientFactory(); var secondFactory = new ClientFactory();
        await using var first = Builder(di).Setup(c => c.Services(r => r.Instance<IInvocationClientFactory>(firstFactory)).UseInvocationService<IMarker>().UseInvocationService<IMarker>()).Build();
        await using var second = Builder(di).Setup(c => c.Services(r => r.Instance<IInvocationClientFactory>(secondFactory)).UseInvocationService<IMarker>()).Build();
        var proxy = Assert.IsType<Proxy>(first.Services.Resolve<IMarker>());
        Assert.Same(proxy, first.Services.Resolve<IMarker>());
        Assert.Same(proxy.Client, first.Services.Resolve<IInvocationClient<IMarker>>());
        Assert.NotSame(proxy, second.Services.Resolve<IMarker>());
        Assert.NotSame(proxy.Client, second.Services.Resolve<IInvocationClient<IMarker>>());
        Assert.Equal(1, firstFactory.Calls); Assert.Equal(1, secondFactory.Calls);
        Assert.Same(Assert.Single(first.Services.ResolveAll<IStartable>()), Assert.Single(first.Services.ResolveAll<IStoppable>()));
        await first.StartAsync(); await first.StopAsync(); await first.StopAsync();
        Assert.Equal(1, ((Client<IMarker>)proxy.Client).Releases);
        Assert.Equal(0, ((Client<IMarker>)second.Services.Resolve<IInvocationClient<IMarker>>()!).Releases);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupFailure_RollsBackPreviouslyResolvedClientBeforeAppDispose(bool di)
    {
        Metadata(); var factory = new ClientFactory(); var original = new ApplicationException("startup-failed");
        await using var app = Builder(di).Setup(c => c.Services(r => r.Instance<IInvocationClientFactory>(factory))
            .UseInvocationService<IMarker>().Services(r => r.Singleton<IStartable>(_ => new ThrowStart(original)))).Build();
        var client = (Client<IMarker>)Assert.IsType<Proxy>(app.Services.Resolve<IMarker>()).Client;
        var error = await Assert.ThrowsAsync<ApplicationException>(() => app.StartAsync());
        Assert.Same(original, error); Assert.Equal(1, client.Releases);
        await app.StopAsync(); Assert.Equal(1, client.Releases);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingLocalService_IsPreservedAndListenerStartsAndStopsOnce(bool di)
    {
        Metadata(); var local = new Local(); var serverFactory = new ServerFactory(); var unused = new ClientFactory();
        await using var app = Builder(di).Setup(c => c.Services(r => r.Instance<IMarker>(local).Instance<IInvocationServerFactory>(serverFactory).Instance<IInvocationClientFactory>(unused))
            .UseInvocationService<IMarker>().ListenInvocationService<IMarker, Local>().ListenInvocationService<IMarker, Local>()).Build();
        Assert.Same(local, app.Services.Resolve<IMarker>());
        Assert.Same(local, Assert.IsType<Handler<IMarker>>(app.Services.Resolve<IInvocationServerHandler<IMarker>>()).Service);
        Assert.Null(app.Services.GetService(typeof(IInvocationClient<IMarker>)));
        await app.StartAsync(); await app.StartAsync();
        var server = (Server<IMarker>)app.Services.Resolve<IInvocationServer<IMarker>>()!;
        Assert.Equal(1, server.Starts); Assert.Equal(1, serverFactory.Calls); Assert.Equal(0, unused.Calls);
        Assert.Same(Assert.Single(app.Services.ResolveAll<IStartable>()), Assert.Single(app.Services.ResolveAll<IStoppable>()));
        await app.StopAsync(); await app.StopAsync(); Assert.Equal(1, server.Releases); Assert.Equal(0, local.Disposals);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PipeFactories_ResolveDefaultOrScopedEndpointsOnBothProviders(bool di, bool scoped)
    {
        Metadata(); var endpoint = scoped ? "app-factory-" + Guid.NewGuid().ToString("N") : typeof(IMarker).FullName!;
        IAppConfigurationContext Transport(IAppConfigurationContext context) => scoped ? context.PipesServiceTransport(_ => endpoint) : context.PipesServiceTransport();
        await using var serverApp = Builder(di).Setup(c => { c.UseDiagnostics(); Transport(c); c.Services(r => r.Instance<IMarker>(new Local())).ListenInvocationService<IMarker, Local>(); }).Build();
        await using var clientApp = Builder(di).Setup(c => { c.UseDiagnostics(); Transport(c); c.UseInvocationService<IMarker>(); }).Build();
        await serverApp.StartAsync(); await clientApp.StartAsync();
        var client = (PipesInvocationClient<IMarker>)clientApp.Services.Resolve<IInvocationClient<IMarker>>()!;
        var server = clientApp.Services.GetService(typeof(IInvocationServer<IMarker>));
        Assert.Null(server);
        var activeServer = (PipesInvocationServer<IMarker>)serverApp.Services.Resolve<IInvocationServer<IMarker>>()!;
        Assert.Equal("tm_" + endpoint, client.Name); Assert.Equal(endpoint, activeServer.Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!client.IsConnected) await Task.Delay(10, timeout.Token);
        Assert.True(client.IsConnected);
        await clientApp.StopAsync(); await serverApp.StopAsync(); Assert.False(client.IsConnected);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExplicitEndpoint_InvalidValueIsRejectedInsteadOfDefaultFallback(string? endpoint)
    {
        await using var app = Builder(true).Setup(c => c.UseDiagnostics()).Build();
        var events = app.Services.Resolve<IEventsSourceFactory>()!;
        var clients = new PipesInvocationClientFactory(events, _ => endpoint!);
        var servers = new PipesInvocationServerFactory(events, new HandlerResolver(), _ => endpoint!);
        Assert.Equal("pipeName", Assert.ThrowsAny<ArgumentException>(() => clients.Create<IMarker>()).ParamName);
        Assert.Equal("pipeName", Assert.ThrowsAny<ArgumentException>(() => servers.Create<IMarker>()).ParamName);
    }
    public interface IMarker { Task<int> PingAsync(CancellationToken cancellationToken = default); }
    private sealed class Proxy(IInvocationClient<IMarker> client) : IMarker
    { internal IInvocationClient<IMarker> Client => client; public Task<int> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(7); }
    public sealed class Local : IMarker, IDisposable
    { internal int Disposals; public Task<int> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(42); public void Dispose() => Disposals++; }
    private sealed class ThrowStart(Exception error) : IStartable
    { public Task StartAsync(CancellationToken token = default) => Task.FromException(error); }
    private sealed class ClientFactory : IInvocationClientFactory
    { internal int Calls; public IInvocationClient<T> Create<T>() { Calls++; return new Client<T>(); } }
    private sealed class ServerFactory : IInvocationServerFactory
    { internal int Calls; public IInvocationServer<T> Create<T>() { Calls++; return new Server<T>(); } }
    private sealed class HandlerResolver : IInvocationServerHandlerResolver
    { public IInvocationServerHandler<T> Resolve<T>() => new Handler<T>(default!); }
    private sealed class Handler<T>(T service) : IInvocationServerHandler<T>
    {
        internal T Service => service;
        public Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> writer, CancellationToken token = default)
            => Task.FromResult<(bool, Exception?)>((true, null));
    }
    private sealed class Server<T> : IInvocationServer<T>, IAsyncDisposable
    {
        internal int Starts, Releases; private bool _disposed;
        public string Id => "fake";
        public Task StartAsync(CancellationToken token = default) { Starts++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { if (!_disposed) { _disposed = true; Releases++; } return ValueTask.CompletedTask; }
    }
    private sealed class Client<T> : IInvocationClient<T>, IDisposable
    {
        internal int Releases; private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; Releases++; } }
        public Task<TResult> InvokeAsync<TResult>(byte code, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func, CancellationToken token = default) => throw new NotSupportedException();
        public Task InvokeAsync(byte code, Action<IBufferWriter<byte>>? action, CancellationToken token = default) => throw new NotSupportedException();
        public TResult Invoke<TResult>(byte code, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func) => throw new NotSupportedException();
        public void Invoke(byte code, Action<IBufferWriter<byte>>? action) => throw new NotSupportedException();
    }
}
