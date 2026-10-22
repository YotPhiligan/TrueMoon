using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public sealed class FactoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RegisteredAdapters_CreateSeparateCallerOwnedResults(bool di, bool typed)
    {
        var calls = 0;
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c => c.Services(services =>
        {
            if (typed) services.Singleton<IFactory<OwnedValue, int>>(_ => new Factory<OwnedValue, int>(argument => new(argument + ++calls)));
            else services.Singleton<IFactory<OwnedValue>>(_ => new Factory<OwnedValue>(() => new(++calls)));
        })).Build();
        OwnedValue first, second;
        if (typed)
        {
            var factory = app.Services.Resolve<IFactory<OwnedValue, int>>()!;
            first = factory.Create(10); second = factory.Create(20);
            Assert.Equal(11, first.Value); Assert.Equal(22, second.Value);
        }
        else
        {
            var factory = app.Services.Resolve<IFactory<OwnedValue>>()!;
            first = factory.Create()!; second = factory.Create<object>()!;
            Assert.Equal(1, first.Value); Assert.Equal(2, second.Value);
        }
        Assert.NotSame(first, second); Assert.Equal(2, calls);
        await app.DisposeAsync();
        Assert.Equal(0, first.Disposals); Assert.Equal(0, second.Disposals);
        first.Dispose(); second.Dispose();
        await app.DisposeAsync();
        Assert.Equal(1, first.Disposals); Assert.Equal(1, second.Disposals);
    }

    [Fact]
    public void ParameterlessAdapter_RejectsArgumentsBeforeCallbackAndAcceptsNullBridge()
    {
        var calls = 0; IFactory<int> factory = new Factory<int>(() => ++calls);
        Assert.Equal("data", Assert.Throws<ArgumentException>(() => factory.Create(42)).ParamName);
        Assert.Equal(0, calls);
        Assert.Equal(1, factory.Create<string>(null));
        Assert.Equal(2, factory.Create());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void TypedAdapter_GenericBridgePreservesReferenceArgumentsIncludingNull()
    {
        var arguments = new List<string?>();
        IFactory<string?, string?> typed = new Factory<string?, string?>(argument => { arguments.Add(argument); return argument; });
        IFactory<string?> bridge = typed;
        Assert.Equal("typed", typed.Create("typed"));
        Assert.Equal("boxed", bridge.Create<object>("boxed"));
        Assert.Null(bridge.Create<string>(null));
        Assert.Equal(new string?[] { "typed", "boxed", null }, arguments);
        Assert.Equal("data", Assert.Throws<ArgumentException>(() => bridge.Create(4)).ParamName);
        Assert.Throws<InvalidOperationException>(() => bridge.Create());
        Assert.Equal(3, arguments.Count);
    }

    [Fact]
    public void TypedAdapter_ValueBridgeRejectsNullAndMismatchedArgumentsWithoutCallback()
    {
        var calls = 0; IFactory<int> bridge = new Factory<int, int>(argument => { calls++; return argument * 2; });
        Assert.Equal(0, bridge.Create(0));
        Assert.Equal(14, bridge.Create<object>(7));
        Assert.Equal("data", Assert.Throws<ArgumentException>(() => bridge.Create<string>(null)).ParamName);
        Assert.Equal("data", Assert.Throws<ArgumentException>(() => bridge.Create("7")).ParamName);
        Assert.Throws<InvalidOperationException>(() => bridge.Create());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Adapters_GuardCallbacksAndPropagateOriginalFailure()
    {
        Assert.Equal("create", Assert.Throws<ArgumentNullException>(() => new Factory<object>(null!)).ParamName);
        Assert.Equal("create", Assert.Throws<ArgumentNullException>(() => new Factory<object, int>(null!)).ParamName);
        var expected = new IOException("creation failed");
        IFactory<object> parameterless = new Factory<object>(() => throw expected);
        IFactory<object> typed = new Factory<object, int>(_ => throw expected);
        Assert.Same(expected, Assert.Throws<IOException>(() => parameterless.Create()));
        Assert.Same(expected, Assert.Throws<IOException>(() => typed.Create(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedLifecycleAliases_ForwardAppCallsAndDisposeOwnerExactlyOnce(bool di)
    {
        var owner = new LifecycleOwner();
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c => c.Services(services => services
            .Singleton<LifecycleOwner>(_ => owner)
            .Singleton<IStartable>(resolver => new AppServiceLifecycle(startable: resolver.Resolve<LifecycleOwner>()))
            .Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<LifecycleOwner>())))).Build();
        await app.StartAsync();
        Assert.Equal(1, owner.Starts); Assert.Equal(0, owner.Stops); Assert.Equal(0, owner.Disposals);
        await app.StopAsync();
        Assert.Equal(1, owner.Stops); Assert.Equal(0, owner.Disposals);
        await app.DisposeAsync(); await app.DisposeAsync();
        Assert.Equal(1, owner.Starts); Assert.Equal(1, owner.Stops); Assert.Equal(1, owner.Disposals);
    }

    private sealed class LifecycleOwner : IStartable, IStoppable, IDisposable
    {
        public int Starts, Stops, Disposals;
        public Task StartAsync(CancellationToken cancellationToken = default) { Starts++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default) { Stops++; return Task.CompletedTask; }
        public void Dispose() => Disposals++;
    }

    private sealed class OwnedValue(int value) : IDisposable
    {
        public int Value { get; } = value;
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
}
