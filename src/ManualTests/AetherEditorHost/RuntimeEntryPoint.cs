using System.Text.Json;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Configuration;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Process bootstrap only. RuntimeProtocol, game loading, transport, rendering and ownership stay in Aether.
internal static class RuntimeEntryPoint
{
    internal static async Task RunAsync()
    {
        var bootstrap = RuntimeBootstrap.ReadEnvironment();
        var launch = bootstrap.Launch;
        await using var app = App.Build(context => { ConfigureApp(context, bootstrap); });
        var engine = app.Services.Resolve<RuntimeEngine>()!;
        AetherRuntimeHost? runtime=null;var started=false;Exception? failure=null;string[] assemblies=[];
        try { await app.StartAsync();started=true;runtime=app.Services.Resolve<AetherRuntimeHost>()!;
            assemblies=AppDomain.CurrentDomain.GetAssemblies().Select(assembly=>assembly.GetName().Name??"").ToArray();await runtime.RunAsync(); }
        catch(Exception error){failure=error;throw;}
        finally
        {
            if(runtime!=null)await runtime.DisposeAsync(); await app.StopAsync();
            File.WriteAllText(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"), JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, Validation = engine.Gpu.LastSurfaceProducer, Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount,
                GameLoaded=assemblies.Contains("RenderSurface.Game"),EditorLoaded=assemblies.Any(name=>name.StartsWith("TrueMoon.Aether.Editor",StringComparison.Ordinal)),
                Role=launch.Role.ToString(),BuildId=launch.Build?.BuildId,DeliveryGameLoaded=assemblies.Contains("DeliveryGame"),
                ToolingLoaded=assemblies.Contains("TrueMoon.Aether.Tooling"),
                Started=started,RuntimeActivated=runtime!=null,FailurePhase=failure==null?null:!started?"AppStartup":runtime==null?"GameActivation":"Run",
                FailureType=failure?.GetType().FullName,NoGpuSurfaceProduced=engine.Gpu.LastSurfaceProducer==null,
                ChildDiagnostics = runtime?.ChildDiagnosticsStatus }));
        }
    }

    internal static void ConfigureApp(IAppConfigurationContext context, RuntimeBootstrap bootstrap)
    {
        context.UseConfiguration(ConfigurationProfiles.Create(profile =>
        {
            profile.AddSchema(DiagnosticsConfigurationSchema.Default); profile.AddSchema(RuntimeConfigurationSchemas.Engine);
            profile.Override("Diagnostics:JsonDirectory", Path.Combine(bootstrap.Launch.ReportDirectory, "diagnostics", "runtime"));
            profile.Override("Diagnostics:JsonUseRunDirectories", true);
        }));
        context.UseConfiguredDiagnostics();
        context.UseConfiguredAetherRuntime(bootstrap);
        context.UseAetherSurfaceRendering();
    }
}
