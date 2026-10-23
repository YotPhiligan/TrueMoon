using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// Kept out of the legacy game fixture method so JIT never resolves Game.Logic for this child.
internal static class PreviewRuntimeEntry
{
    private static SessionLaunch Read()
    {
        var text = Environment.GetEnvironmentVariable(NativeRuntimeFactory.BootstrapKey) ?? throw new InvalidDataException("Missing preview bootstrap.");
        if (text.Length > 16384) throw new InvalidDataException("Oversized preview bootstrap.");
        return JsonSerializer.Deserialize<SessionLaunch>(text) ?? throw new InvalidDataException("Invalid preview bootstrap.");
    }
    internal static bool IsPreview() => Read().Role == RuntimeRole.EditPreview;
    internal static async Task<int> RunAsync()
    {
        var launch = Read();
        if (launch.Identity.HostContractId != BuildIdentity.HostId) throw new InvalidDataException("Preview host mismatch.");
        var parentId = int.Parse(Environment.GetEnvironmentVariable("-p") ?? throw new InvalidDataException("Missing preview parent."));
        await using var app = App.Build(context =>
        {
            context.UseAether(options =>
            { options.HostContractId = launch.Identity.HostContractId; options.GameBuildId = launch.Identity.GameBuildId; options.ReportDirectory = launch.ReportDirectory; });
            context.UseAetherPreviewRuntime(launch, parentId);
        });
        await app.StartAsync();
        var runtime = app.Services.Resolve<AetherRuntimeHost>(); var engine = app.Services.Resolve<AetherHost>();
        try { await runtime.RunAsync(); }
        finally
        {
            await runtime.DisposeAsync(); await app.StopAsync();
            Records.Write(launch.ReportDirectory, $"preview-runtime-{launch.Identity.Generation}.json", new
            {
                Pid = Environment.ProcessId, launch.Role, launch.Build, launch.RequireRestore,
                GameLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"),
                Validation = engine.Gpu.LastSurfaceProducer, Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount
            });
        }
        return 0;
    }
}
