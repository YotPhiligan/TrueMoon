using TrueMoon.Aether.Editor;
using System.Text.Json;
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Process bootstrap only. RuntimeProtocol, game loading, transport, rendering and ownership stay in Aether.
internal static class RuntimeEntryPoint
{
    internal static async Task RunAsync()
    {
        var bootstrap = RuntimeBootstrap.ReadEnvironment();
        var launch = bootstrap.Launch; var parentId = bootstrap.ParentProcessId;
        var preview = launch.Role == RuntimeRole.EditPreview;
        await using var app = App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions
            {
                JsonDirectory = Path.Combine(launch.ReportDirectory, "diagnostics", "runtime"),
                JsonUseRunDirectories = true
            });
            context.UseAether(options =>
            {
                options.HostContractId = launch.Identity.HostContractId; options.GameBuildId = launch.Identity.GameBuildId; options.ReportDirectory = launch.ReportDirectory;
                options.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
            });
            if (preview) context.UseAetherPreviewRuntime(launch, parentId);
            else context.UseAetherRuntime(launch, parentId);
        });
        await app.StartAsync();
        var runtime = app.Services.Resolve<AetherRuntimeHost>()!;
        var engine = app.Services.Resolve<AetherHost>()!;
        try { await runtime.RunAsync(); }
        finally
        {
            await runtime.DisposeAsync(); await app.StopAsync();
            File.WriteAllText(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"), JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, Validation = engine.Gpu.LastSurfaceProducer, Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount,
                ChildDiagnostics = runtime.ChildDiagnosticsStatus }));
        }
    }
}
