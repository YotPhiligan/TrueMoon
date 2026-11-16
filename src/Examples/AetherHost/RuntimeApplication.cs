using System.Text.Json;
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
            context.UseAetherRuntime(options =>
            {
                options.HostContractId = launch.Identity.HostContractId; options.GameBuildId = launch.Identity.GameBuildId;
                options.ReportDirectory = launch.ReportDirectory; options.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
            });
            context.UseAetherSurfaceRendering();
            if (launch.Role == RuntimeRole.EditPreview) context.UseAetherPreviewRuntime(bootstrap);
            else context.UseAetherRuntime(bootstrap);
        });
        var engine = app.Services.Resolve<RuntimeEngine>()!;
        AetherRuntimeHost? runtime = null; Exception? failure = null; string[] assemblies = []; var gameLoaded = false;
        try
        {
            await app.StartAsync(); runtime = app.Services.Resolve<AetherRuntimeHost>()!;
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            assemblies = loaded.Select(assembly => assembly.GetName().Name ?? "").ToArray();
            var gameRoot = launch.Build == null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(launch.Build.DirectoryPath)) + Path.DirectorySeparatorChar;
            gameLoaded = gameRoot != null && loaded.Any(assembly => !assembly.IsDynamic && assembly.Location.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase));
            await runtime.RunAsync();
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { if (runtime != null) await runtime.DisposeAsync(); await app.StopAsync(); }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); throw; }
            finally
            {
                Directory.CreateDirectory(launch.ReportDirectory);
                await File.WriteAllTextAsync(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"), JsonSerializer.Serialize(new
                {
                    Pid = Environment.ProcessId, launch.Role, GameLoaded = gameLoaded,
                    EditorLoaded = assemblies.Any(name => name.StartsWith("TrueMoon.Aether.Editor", StringComparison.Ordinal)),
                    ToolingLoaded = assemblies.Contains("TrueMoon.Aether.Tooling"), Validation = engine.Gpu.LastSurfaceProducer,
                    Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount, Error = failure?.ToString()
                }));
            }
        }
    }
}
