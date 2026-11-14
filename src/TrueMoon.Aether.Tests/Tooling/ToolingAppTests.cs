using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Tooling;

public sealed class ToolingAppTests
{
    private static readonly string Hash = new('A', 64);
    private static IAppBuilder Builder(bool microsoft) => App.Builder(builder => { if (microsoft) builder.UseDI(); });
    private static void Configure(ToolingOptions options, string directory)
    { options.HostContractId = Hash; options.BuildDirectory = Path.Combine(directory, "builds"); }

    [Fact]
    public void ToolingAssembly_ContainsHeadlessServicesAndExportData_WithoutUiReference()
    {
        var assembly = typeof(ToolingHost).Assembly;
        Assert.Equal("TrueMoon.Aether.Tooling", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("TrueMoon.Aether.Editor", StringComparison.Ordinal) ||
            reference.Name.StartsWith("TrueMoon.Alloy", StringComparison.Ordinal) || reference.Name.StartsWith("TrueMoon.Argentis", StringComparison.Ordinal));
        foreach (var type in new[] { typeof(EditorProjects), typeof(SceneDocumentSession), typeof(SceneAuthoringSession),
                     typeof(GameBuildCompiler), typeof(TrueMoon.Aether.Tooling.SourceTracking.EditorSourceWatch),
                     typeof(TrueMoon.Aether.Tooling.Diagnostics.EditorDiagnosticSnapshot), typeof(TrueMoon.Aether.Tooling.Diagnostics.EditorDiagnosticReportWriter) })
            Assert.Same(assembly, type.Assembly);
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.Name is "EditorView" or "EditorDiagnosticEntry" or "TransformEditor");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptyStartup_PerformsNoProjectSdkOrArtifactWork_AndTerminalStopIsRepeatable(bool microsoft)
    {
        var root = Path.Combine(Path.GetTempPath(), "TrueMoon-Tooling-idle-" + Guid.NewGuid().ToString("N"));
        var probe = new NoSdk(); ToolingOptions mutable = null!;
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectToolchain>(_ => probe));
            context.UseAetherTooling(options => { Configure(options, root); mutable = options; });
        }).Build();
        mutable.BuildDirectory = ""; mutable.Authoring.MaxHistoryEntries = -1; mutable.SourceTracking.PollInterval = TimeSpan.Zero;
        await app.StartAsync();
        var host = app.Services.Resolve<ToolingHost>()!;
        Assert.Same(host, Assert.Single(app.Services.ResolveAll<ToolingHost>()));
        Assert.Empty(app.Services.ResolveAll<RuntimeEngine>()); Assert.Empty(app.Services.ResolveAll<AetherHost>());
        Assert.Empty(app.Services.ResolveAll<GameBuildCompiler>()); // No second disposable alias for the owned product.
        Assert.Null(host.Projects.Active); Assert.Null(host.LastGoodBuild); Assert.Null(host.Builds.DiagnosticsDirectory);
        Assert.False(Directory.Exists(root)); Assert.Equal(0, probe.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.BuildAsync(0));
        await app.StopAsync(); await host.DisposeAsync();
        Assert.True(host.IsClosed); Assert.True(host.Projects.IsClosed); Assert.Null(host.Builds.ProcessId);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.BuildAsync(0));
        Assert.False(Directory.Exists(root)); Assert.Equal(0, probe.Calls);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void HeadlessAndFullWorkflow_RejectBothRegistrationOrders(bool microsoft, bool fullFirst)
    {
        Assert.Throws<InvalidOperationException>(() => Builder(microsoft).Setup(context =>
        {
            void Headless() => context.UseAetherTooling(options => Configure(options, "tooling-registration-fixture"));
            void Full() => context.UseAether(options => { options.HostContractId = Hash; options.GameBuildId = Hash; options.ReportDirectory = "full-registration-fixture"; });
            if (fullFirst) { Full(); Headless(); } else { Headless(); Full(); }
        }).Build());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TerminalCleanupFailure_RetainsAuthoringProductForRetry_AndThenReleasesItOnce(bool microsoft)
    {
        var root = Path.Combine(Path.GetTempPath(), "TrueMoon-Tooling-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root, "Scenes")); Directory.CreateDirectory(Path.Combine(root, "Content"));
        await File.WriteAllTextAsync(Path.Combine(root, "Game.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var factory = new FailingAuthoringFactory();
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectToolchain>(_ => new NoSdk(false))
                .Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => factory));
            context.UseAetherTooling(options => Configure(options, root));
        }).Build();
        try
        {
            await app.StartAsync(); var host = app.Services.Resolve<ToolingHost>()!;
            var session = await host.Projects.CreateAsync(Path.Combine(root, "room.aetherproj"),
                new(1, Guid.NewGuid(), "cleanup", "Game.csproj", "Game.dll", "Game.Entry", "Scenes", "Content"));
            await session.Documents!.NewAsync(new(new(Guid.NewGuid()), 0, [], [], []));
            var owner = Assert.Single(factory.Products);
            await Assert.ThrowsAsync<AggregateException>(() => host.DisposeAsync().AsTask());
            Assert.Equal(1, owner.DisposeCalls); Assert.Equal(0, owner.InnerDisposals); Assert.True(host.IsClosed);
            await host.DisposeAsync(); await app.StopAsync();
            Assert.Equal(2, owner.DisposeCalls); Assert.Equal(1, owner.InnerDisposals);
            Assert.Null(host.Projects.Active); Assert.True(session.IsClosed); Assert.Null(host.Builds.ProcessId);
        }
        finally
        {
            await app.DisposeAsync();
            var resolved = Path.GetFullPath(root);
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar, resolved, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(resolved, true);
        }
    }

    private sealed class NoSdk(bool reject = true) : IProjectToolchain
    {
        public int Calls { get; private set; }
        public Task<ProjectToolchainInfo> ValidateAsync(string gameProjectPath, CancellationToken cancellationToken = default)
        { Calls++; if (reject) throw new InvalidOperationException("Idle registration probed SDK."); return Task.FromResult(new ProjectToolchainInfo("fixture")); }
    }
    private sealed class FailingAuthoringFactory : IFactory<ISceneAuthoringSession, SceneDocument>
    {
        public List<FailOnce> Products { get; } = [];
        public ISceneAuthoringSession Create(SceneDocument arguments)
        {
            var inner = new SceneAuthoringSessionFactory(new SceneResolutionService(
                new Factory<EntityResolver, SceneDocument>(scene => new(scene.Definitions, scene.Schemas))), new()).Create(arguments);
            var result = new FailOnce(inner); Products.Add(result); return result;
        }
    }
    private sealed class FailOnce(ISceneAuthoringSession inner) : ISceneAuthoringSession
    {
        public int DisposeCalls { get; private set; }
        public int InnerDisposals { get; private set; }
        public SceneAuthoringState Read() => inner.Read();
        public SceneAuthoringResult Apply(TransformEditBatch batch, CancellationToken token = default) => inner.Apply(batch, token);
        public SceneAuthoringResult Reparent(ReparentBatch batch, CancellationToken token = default) => inner.Reparent(batch, token);
        public SceneAuthoringResult EditComponents(ComponentEditBatch batch, CancellationToken token = default) => inner.EditComponents(batch, token);
        public SceneAuthoringResult Undo(AuthoringOperation operation, CancellationToken token = default) => inner.Undo(operation, token);
        public SceneAuthoringResult Redo(AuthoringOperation operation, CancellationToken token = default) => inner.Redo(operation, token);
        public void Dispose() { if (++DisposeCalls == 1) throw new IOException("Injected authoring cleanup failure."); inner.Dispose(); InnerDisposals++; }
    }
}
