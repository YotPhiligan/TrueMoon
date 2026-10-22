using System.Text.Json;
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;

namespace TrueMoon.Aether.Editor;

// Process bootstrap only. RuntimeProtocol, game loading, transport, rendering and ownership stay in Aether.
internal static class RuntimeEntryPoint
{
    internal static async Task RunAsync()
    {
        var text = Environment.GetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey) ?? throw new InvalidDataException("Missing runtime bootstrap.");
        if (text.Length > 16384) throw new InvalidDataException("Oversized runtime bootstrap.");
        var launch = JsonSerializer.Deserialize<SessionLaunch>(text) ?? throw new InvalidDataException("Invalid runtime bootstrap.");
        launch.Identity.Validate();
        if (launch.Identity.HostContractId != BuildIdentity.HostId || launch.Build == null || launch.Build.BuildId != launch.Identity.GameBuildId ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new InvalidDataException("Runtime requires a matching selected game build.");
        var parentId = int.Parse(Environment.GetEnvironmentVariable("-p") ?? throw new InvalidDataException("Missing runtime parent."));
        await using var app = App.Build(context =>
        {
            context.UseAether(options =>
            { options.HostContractId = launch.Identity.HostContractId; options.GameBuildId = launch.Identity.GameBuildId; options.ReportDirectory = launch.ReportDirectory; });
            context.UseAetherRuntime(launch, parentId);
        });
        await app.StartAsync();
        var runtime = app.Services.Resolve<AetherRuntimeHost>();
        var engine = app.Services.Resolve<AetherHost>()!;
        try { await runtime.RunAsync(); }
        finally
        {
            await runtime.DisposeAsync(); await app.StopAsync();
            File.WriteAllText(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"), JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, Validation = engine.Gpu.LastSurfaceProducer, Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount }));
        }
    }
}
