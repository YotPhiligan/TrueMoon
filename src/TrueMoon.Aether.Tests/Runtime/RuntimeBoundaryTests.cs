using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Runtime;

public sealed class RuntimeBoundaryTests
{
    [Fact]
    public void RuntimeAssembly_ExcludesWorkflowAndUi_AndArtifactCarriesNoCompilerWitness()
    {
        var assembly = typeof(RuntimeEngine).Assembly;
        Assert.Equal("TrueMoon.Aether.Runtime", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name is "TrueMoon.Aether" or "TrueMoon.Aether.Tooling" or "TrueMoon.Aether.Editor" ||
            reference.Name!.StartsWith("TrueMoon.Alloy", StringComparison.Ordinal) || reference.Name.StartsWith("TrueMoon.Argentis", StringComparison.Ordinal));
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.Name is "GameBuildCompiler" or "GameEditor" or "SceneAuthoringSession" or "AetherHost");
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.Name == "SceneWorldLoadError");
        Assert.Equal(typeof(TrueMoon.Aether.Abstractions.Ecs.Loading.SceneWorldLoadError),
            typeof(TrueMoon.Aether.Runtime.Ecs.Loading.SceneWorldLoadException).GetProperty("Error")!.PropertyType);
        var artifact = typeof(TrueMoon.Aether.Runtime.Builds.GameBuildArtifact);
        Assert.Null(artifact.GetMethod("Publish")); Assert.Null(artifact.GetProperty("Inputs")); Assert.Null(artifact.GetProperty("Compilation"));
        Assert.Same(typeof(TrueMoon.Aether.Abstractions.Scenes.SceneDocument).Assembly, typeof(TrueMoon.Aether.Abstractions.Sessions.IGameSession).Assembly);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeOnlyApp_SnapshotsOptions_RegistersNoWorkflow_AndDisposesFactoryOnce(bool microsoft)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-runtime-boundary-" + Guid.NewGuid().ToString("N"));
        var factory = new NeverFactory(); var calls = 0; RuntimeOptions mutable = null!;
        var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context => context.UseAetherRuntime(options =>
        {
            mutable = options; options.HostContractId = new('A', 64); options.GameBuildId = new('B', 64);
            options.ReportDirectory = directory; options.RuntimeFactory = _ => { calls++; return factory; };
        })).Build();
        try
        {
            Assert.Equal(0, calls); mutable.RuntimeFactory = _ => throw new IOException("Late mutation escaped the snapshot.");
            mutable.ReportDirectory = "";
            await app.StartAsync();
            var engine = app.Services.Resolve<RuntimeEngine>()!;
            Assert.Same(engine, Assert.Single(app.Services.ResolveAll<RuntimeEngine>()));
            Assert.Equal(1, calls); Assert.Empty(app.Services.ResolveAll<AetherHost>()); Assert.Empty(app.Services.ResolveAll<GameBuildCompiler>());
            Assert.False(app.Services.Resolve<IProcessDiagnosticsSource>()!.Read(1, 0).BuildAvailable);
            Assert.False(Directory.Exists(directory)); Assert.Equal(0, engine.Gpu.ResourceCount);
            await app.StopAsync(); await engine.StopAsync(); await app.DisposeAsync();
            Assert.Equal(1, factory.Disposals); Assert.Equal(0, factory.Starts);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, factory.Disposals);
    }

    private sealed class NeverFactory : IRuntimeFactory, IDisposable
    {
        public int Starts { get; private set; }
        public int Disposals { get; private set; }
        public IRuntimeLease Create(SessionLaunch launch) { Starts++; throw new InvalidOperationException("Headless idle App started a process."); }
        public void Dispose() => Disposals++;
    }
}
