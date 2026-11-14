using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.ManualTests.AetherEditorHost;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class ManualRuntimeBootstrapTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ChildComposition_UsesExactRuntimeSchemaAndRuntimeEngine_WithoutToolingOrNativeActivation(bool microsoft)
    {
        var reports = Path.Combine(Path.GetTempPath(), "TrueMoon-ARCH35-child-" + Guid.NewGuid().ToString("N"));
        var identity = new SessionIdentity(Guid.NewGuid(), 1, new('A', 64), BuildIdentity.PreviewId, new('B', 64));
        var bootstrap = new RuntimeBootstrap(new(identity, "engine-" + Guid.NewGuid().ToString("N"), reports, Role: RuntimeRole.EditPreview), 77);
        await using var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context => RuntimeEntryPoint.ConfigureApp(context, bootstrap)).Build();
        var engine = app.Services.Resolve<RuntimeEngine>()!;
        Assert.NotNull(engine); Assert.Empty(app.Services.ResolveAll<AetherHost>());
        Assert.Equal(SessionState.Stopped, engine.Supervisor.Status.State); Assert.Equal(0, engine.Gpu.ResourceCount);
        var options = app.Services.Resolve<IConfigurationReader>()!.Current.GetOptions(RuntimeConfigurationSchemas.Engine);
        Assert.Equal(Path.GetFullPath(reports), options.ReportDirectory);
        Assert.Empty(((NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!).Leases);
    }
}
