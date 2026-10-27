using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class PreviewRuntimeRegistrationTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public static IEnumerable<object[]> InvalidPreviewLaunches()
    {
        foreach (var di in new[] { false, true })
        foreach (var kind in new[] { "game-role", "unknown-role", "build", "restore", "game-identity", "parent-zero", "parent-self",
                     "endpoint-empty", "endpoint-prefix", "endpoint-length", "report-empty", "identity-nonce", "identity-generation" })
            yield return [di, kind];
    }

    [Theory]
    [MemberData(nameof(InvalidPreviewLaunches))]
    public void Registration_RejectsInvalidPreviewLaunchBeforeActivatingAnyFactory(bool di, string kind)
    {
        var calls = 0; var launch = Launch(); var parent = int.MaxValue;
        launch = kind switch
        {
            "game-role" => launch with { Role = RuntimeRole.Game },
            "unknown-role" => launch with { Role = (RuntimeRole)99 },
            "build" => launch with { Build = new("unused", launch.Identity.GameBuildId) },
            "restore" => launch with { RequireRestore = true },
            "game-identity" => launch with { Identity = launch.Identity with { GameBuildId = Hash } },
            "endpoint-empty" => launch with { Endpoint = " " },
            "endpoint-prefix" => launch with { Endpoint = "other-preview" },
            "endpoint-length" => launch with { Endpoint = "engine-" + new string('x', 94) },
            "report-empty" => launch with { ReportDirectory = " " },
            "identity-nonce" => launch with { Identity = launch.Identity with { Nonce = "invalid" } },
            "identity-generation" => launch with { Identity = launch.Identity with { Generation = 0 } },
            _ => launch
        };
        if (kind == "parent-zero") parent = 0;
        if (kind == "parent-self") parent = Environment.ProcessId;
        var builder = Builder(di).Setup(context =>
        {
            context.UseAether(Configure);
            context.Services(services => services.Singleton<IFactory<AetherRuntimeHost, SessionLaunch>>(_ =>
                new global::TrueMoon.Factory<AetherRuntimeHost, SessionLaunch>(_ => { calls++; throw new IOException("unexpected activation"); })));
            context.UseAetherPreviewRuntime(launch, parent);
        });
        if (kind.StartsWith("identity-", StringComparison.Ordinal))
            Assert.Throws<InvalidDataException>(() => builder.Build());
        else if (kind.StartsWith("parent-", StringComparison.Ordinal))
            Assert.Equal("parentId", Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build()).ParamName);
        else Assert.ThrowsAny<ArgumentException>(() => builder.Build());
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Registration_RejectsNullInputsAndGameEntryPointRejectsPreviewRole()
    {
        var launch = Launch();
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => RuntimeConfigurationExtensions.UseAetherPreviewRuntime(null!, launch, 1)).ParamName);
        var builder = Builder(false).Setup(context =>
        {
            Assert.Equal("launch", Assert.Throws<ArgumentNullException>(() => context.UseAetherPreviewRuntime(null!, 1)).ParamName);
            Assert.Equal("launch", Assert.Throws<ArgumentException>(() => context.UseAetherRuntime(
                launch with { Build = new("unused", launch.Identity.GameBuildId) }, int.MaxValue)).ParamName);
            throw new InvalidOperationException("validation complete");
        });
        Assert.Equal("validation complete", Assert.Throws<InvalidOperationException>(() => builder.Build()).Message);
    }

    [Theory]
    [InlineData(false, "missing-aether")]
    [InlineData(true, "missing-aether")]
    [InlineData(false, "preview-preview")]
    [InlineData(true, "preview-preview")]
    [InlineData(false, "game-preview")]
    [InlineData(true, "game-preview")]
    [InlineData(false, "preview-game")]
    [InlineData(true, "preview-game")]
    public void Registration_RequiresAetherAndRejectsDuplicateRuntimeBeforeFactoryActivation(bool di, string scenario)
    {
        var calls = 0; var preview = Launch(); var game = GameLaunch();
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<AetherRuntimeHost, SessionLaunch>>(_ =>
                new global::TrueMoon.Factory<AetherRuntimeHost, SessionLaunch>(_ => { calls++; throw new IOException("unexpected activation"); })));
            if (scenario != "missing-aether") context.UseAether(Configure);
            if (scenario == "game-preview") context.UseAetherRuntime(game, int.MaxValue);
            else context.UseAetherPreviewRuntime(preview, int.MaxValue);
            if (scenario == "preview-game") context.UseAetherRuntime(game, int.MaxValue);
            else if (scenario != "missing-aether") context.UseAetherPreviewRuntime(preview, int.MaxValue);
        });
        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(scenario == "missing-aether" ? "Call UseAether before UseAetherRuntime." : "The runtime is already registered.", error.Message);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 20)]
    [InlineData(false, 100)]
    [InlineData(true, 100)]
    public async Task Registration_UsesNormalizedLazyLaunchSnapshotAndPreservesFactoryOverride(bool di, int endpointLength)
    {
        var original = Launch() with { Endpoint = "engine-" + new string('x', endpointLength - 7) };
        var launch = original; var expected = new IOException("injected preview runtime factory");
        SessionLaunch? received = null; var calls = 0;
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<AetherRuntimeHost, SessionLaunch>>(_ =>
                new global::TrueMoon.Factory<AetherRuntimeHost, SessionLaunch>(snapshot => { received = snapshot; calls++; throw expected; })));
            context.UseAether(Configure);
            Assert.Same(context, context.UseAetherPreviewRuntime(launch, int.MaxValue));
            launch = launch with { ReportDirectory = "changed-after-registration", Role = RuntimeRole.Game };
        }).Build();
        Assert.Equal(0, calls);
        Assert.Same(expected, Assert.Throws<IOException>(() => app.Services.Resolve<AetherRuntimeHost>()));
        Assert.Equal(1, calls); Assert.NotSame(original, received);
        Assert.Equal(original with { ReportDirectory = Path.GetFullPath(original.ReportDirectory) }, received);
        Assert.Equal(RuntimeRole.EditPreview, received!.Role); Assert.Equal(BuildIdentity.PreviewId, received.Identity.GameBuildId);
        Assert.Equal(endpointLength, received.Endpoint.Length);
        Assert.Null(received.Build); Assert.False(received.RequireRestore);
        Assert.Equal("preview-runtime-tests", original.ReportDirectory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeHost_UsesInjectedPreviewProductWithoutCreatingGameLoaderAndOwnsOnlyProduct(bool di)
    {
        var previews = new PreviewFactory(); var gameLoads = 0; var runtimeLaunches = 0;
        var app = Builder(di).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<PreviewGameSession>>(_ => previews);
                services.Singleton<IFactory<GameBuildLoader, GameBuildReference>>(_ => new global::TrueMoon.Factory<GameBuildLoader, GameBuildReference>(_ =>
                { gameLoads++; throw new IOException("preview must not load a game"); }));
            });
            context.UseAether(options => { Configure(options); options.RuntimeFactory = _ => new NeverRuntimeFactory(() => runtimeLaunches++); });
            context.UseAetherPreviewRuntime(Launch(), int.MaxValue);
        }).Build();
        try
        {
            Assert.Empty(previews.Products); Assert.Equal(0, gameLoads);
            var host = app.Services.Resolve<AetherRuntimeHost>()!;
            Assert.Same(host, app.Services.Resolve<AetherRuntimeHost>());
            var product = Assert.Single(previews.Products);
            product.Load(new("injected-preview", 4, []));
            Assert.Equal(4, product.Revision); Assert.Empty(product.CaptureRenderScene().Primitives);
            await host.DisposeAsync(); await host.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => product.CaptureRenderScene());
            Assert.Throws<ObjectDisposedException>(() => product.Tick());
            Assert.Equal(0, previews.Disposals); Assert.Equal(0, gameLoads); Assert.Equal(0, runtimeLaunches);
            Assert.Same(previews, app.Services.Resolve<IFactory<PreviewGameSession>>());
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, previews.Disposals); Assert.Equal(0, gameLoads); Assert.Equal(0, runtimeLaunches);
    }

    [Theory]
    [InlineData(false, "frames")]
    [InlineData(true, "frames")]
    [InlineData(false, "server")]
    [InlineData(true, "server")]
    public async Task RuntimeHost_FailedCreationRollsBackPreviewAndKeepsFactoryBorrowed(bool di, string failureStage)
    {
        var previews = new PreviewFactory(); var gameLoads = 0; var expected = new IOException("preview activation " + failureStage);
        var app = Builder(di).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<PreviewGameSession>>(_ => previews);
                services.Singleton<IFactory<GameBuildLoader, GameBuildReference>>(_ => new global::TrueMoon.Factory<GameBuildLoader, GameBuildReference>(_ =>
                { gameLoads++; throw new IOException("preview must not load a game"); }));
                if (failureStage == "frames") services.Singleton<IFactory<FrameProducer>>(_ => new global::TrueMoon.Factory<FrameProducer>(() => throw expected));
                else services.Singleton<IFactory<PipesInvocationServer<IEngineControl>, (SessionLaunch, RuntimeProtocol)>>(_ =>
                    new global::TrueMoon.Factory<PipesInvocationServer<IEngineControl>, (SessionLaunch, RuntimeProtocol)>(_ => throw expected));
            });
            context.UseAether(Configure);
            context.UseAetherPreviewRuntime(Launch(), int.MaxValue);
        }).Build();
        try
        {
            Assert.Same(expected, Assert.Throws<IOException>(() => app.Services.Resolve<AetherRuntimeHost>()));
            var product = Assert.Single(previews.Products);
            Assert.Throws<ObjectDisposedException>(() => product.Load(new("after-failure", 1, [])));
            Assert.Throws<ObjectDisposedException>(() => product.CaptureRenderScene());
            Assert.Equal(0, previews.Disposals); Assert.Equal(0, gameLoads);
            Assert.Same(previews, app.Services.Resolve<IFactory<PreviewGameSession>>());
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, previews.Disposals); Assert.Equal(0, gameLoads);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AetherHost_OwnsInjectedPreviewLazilyAndAppShutdownClosesItWithoutStartingChildren(bool di, bool stopStartedApp)
    {
        var runtimes = new PreviewFixture.RuntimeFactory();
        var viewport = new PreviewFixture.Transport(ViewportTransportKind.Gpu);
        FrameSession? received = null; ScenePreview? product = null; var calls = 0;
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<ScenePreview, FrameSession>>(_ =>
                new global::TrueMoon.Factory<ScenePreview, FrameSession>(frames =>
                { received = frames; calls++; return product = new(frames); })));
            context.UseAether(options =>
            {
                Configure(options); options.RuntimeFactory = _ => runtimes; options.ViewportTransport = _ => viewport;
            });
        }).Build();
        try
        {
            Assert.Equal(0, calls); Assert.Empty(runtimes.Leases); Assert.Empty(viewport.Connections);
            var host = app.Services.Resolve<AetherHost>()!;
            Assert.Same(host, app.Services.Resolve<AetherHost>());
            Assert.Equal(1, calls); Assert.Same(host.Frames, received); Assert.Same(product, host.Preview);
            Assert.Null(host.Preview.Document); Assert.Null(host.Preview.LastPresented);
            Assert.Empty(runtimes.Leases); Assert.Empty(viewport.Connections);
            Assert.Equal(0, host.Frames.MappingCount); Assert.Equal(0, host.Gpu.ResourceCount); Assert.Equal(0, viewport.Disposals);
            if (stopStartedApp) { await app.StartAsync(); await app.StopAsync(); }
            else await app.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Preview.ShowAsync(PreviewFixture.Scene(), 32, 16));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Preview.ResizeAsync(32, 16));
            Assert.Equal(SessionState.Stopped, host.Supervisor.Status.State);
            Assert.Null(host.Preview.Document); Assert.Null(host.Preview.LastPresented);
            Assert.Empty(runtimes.Leases); Assert.Empty(viewport.Connections);
            Assert.Equal(0, host.Frames.MappingCount); Assert.Equal(0, host.Gpu.ResourceCount);
            Assert.Equal(1, calls); Assert.Equal(1, viewport.Disposals);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, viewport.Disposals); Assert.Empty(runtimes.Leases); Assert.Empty(viewport.Connections);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AetherHost_FailedLaterActivationRollsBackOwnedPreviewAndTransportWithoutStartingChildren(bool di)
    {
        var runtimes = new PreviewFixture.RuntimeFactory();
        var viewport = new PreviewFixture.Transport(ViewportTransportKind.Gpu);
        var expected = new IOException("compiler activation after preview");
        ScenePreview? product = null; FrameSession? received = null; var previewCalls = 0; var compilerCalls = 0;
        var app = Builder(di).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<ScenePreview, FrameSession>>(_ => new global::TrueMoon.Factory<ScenePreview, FrameSession>(frames =>
                { received = frames; previewCalls++; return product = new(frames); }));
                services.Singleton<IFactory<GameBuildCompiler>>(_ => new global::TrueMoon.Factory<GameBuildCompiler>(() =>
                { compilerCalls++; throw expected; }));
            });
            context.UseAether(options =>
            {
                Configure(options); options.RuntimeFactory = _ => runtimes; options.ViewportTransport = _ => viewport;
            });
        }).Build();
        try
        {
            Assert.Equal(0, previewCalls); Assert.Equal(0, compilerCalls);
            Assert.Same(expected, Assert.Throws<IOException>(() => app.Services.Resolve<AetherHost>()));
            Assert.Equal(1, previewCalls); Assert.Equal(1, compilerCalls);
            Assert.NotNull(product); Assert.NotNull(received);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => product.ResizeAsync(32, 16));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => product.ShowAsync(PreviewFixture.Scene(), 32, 16));
            Assert.Equal(0, received.MappingCount); Assert.Null(received.Presentation);
            Assert.Equal(1, viewport.Disposals); Assert.Empty(viewport.Connections); Assert.Empty(runtimes.Leases);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, viewport.Disposals); Assert.Empty(viewport.Connections); Assert.Empty(runtimes.Leases);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
    private static void Configure(AetherOptions options)
    {
        options.HostContractId = Hash; options.GameBuildId = Hash; options.ReportDirectory = "preview-runtime-app-tests";
        options.RuntimeFactory = _ => new NeverRuntimeFactory(() => { });
    }
    private static SessionLaunch Launch() => new(new(Guid.NewGuid(), 1, Hash, BuildIdentity.PreviewId, new('B', 64)),
        "engine-preview-" + Guid.NewGuid().ToString("N"), "preview-runtime-tests", Role: RuntimeRole.EditPreview);
    private static SessionLaunch GameLaunch() => new(new(Guid.NewGuid(), 1, Hash, Hash, new('B', 64)),
        "engine-game-" + Guid.NewGuid().ToString("N"), "game-runtime-tests", Build: new(Path.GetFullPath("unused-game"), Hash));
    private sealed class PreviewFactory : IFactory<PreviewGameSession>, IDisposable
    {
        public List<PreviewGameSession> Products { get; } = [];
        public int Disposals { get; private set; }
        public PreviewGameSession Create() { var product = new PreviewGameSession(); Products.Add(product); return product; }
        public PreviewGameSession Create<TData>(TData? data = default) => data is null ? Create() : throw new ArgumentException("Preview creation has no runtime arguments.", nameof(data));
        public void Dispose() => Disposals++;
    }
    private sealed class NeverRuntimeFactory(Action created) : IRuntimeFactory
    {
        public IRuntimeLease Create(SessionLaunch launch) { created(); throw new InvalidOperationException("Registration must remain lazy."); }
    }
}
