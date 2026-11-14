using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Examples.AetherHost;

internal static class RuntimeApplication
{
    internal static async Task RunAsync(RuntimeBootstrap bootstrap, bool consoleDiagnostics)
    {
        var launch = bootstrap.Launch;
        await using var app = App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions
            { JsonDirectory = Path.Combine(launch.ReportDirectory, "diagnostics", "runtime"), JsonUseRunDirectories = true, ConsoleOutput = consoleDiagnostics });
            context.UseAether(options =>
            {
                options.HostContractId = launch.Identity.HostContractId; options.GameBuildId = launch.Identity.GameBuildId;
                options.ReportDirectory = launch.ReportDirectory; options.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
            });
            context.UseAetherSurfaceRendering();
            if (launch.Role == RuntimeRole.EditPreview) context.UseAetherPreviewRuntime(launch, bootstrap.ParentProcessId);
            else context.UseAetherRuntime(launch, bootstrap.ParentProcessId);
        });
        try { await app.StartAsync(); await app.Services.Resolve<AetherRuntimeHost>()!.RunAsync(); }
        finally { await app.StopAsync(); }
    }
}
