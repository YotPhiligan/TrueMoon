using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using EngineRuntimeHost = TrueMoon.Aether.Runtime.AetherRuntimeHost;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.ManualTests.AetherRuntimeHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is not ([] or ["--runtime"]))
            {
                Console.Error.WriteLine("Usage: AetherRuntimeHost [--runtime]; requires an owned Aether runtime bootstrap.");
                return 2;
            }
            await RunAsync();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task RunAsync()
    {
        // Self-check precedes App construction, including diagnostic directory creation.
        var bootstrap = RuntimeBootstrap.ReadEnvironment();
        var launch = bootstrap.Launch;
        await using var app = App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions
            {
                JsonDirectory = Path.Combine(launch.ReportDirectory, "diagnostics", "runtime"),
                JsonUseRunDirectories = true
            });
            context.UseAether(options =>
            {
                options.HostContractId = launch.Identity.HostContractId;
                options.GameBuildId = launch.Identity.GameBuildId;
                options.ReportDirectory = launch.ReportDirectory;
                options.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
            });
            if (launch.Role == RuntimeRole.EditPreview)
                context.UseAetherPreviewRuntime(launch, bootstrap.ParentProcessId);
            else context.UseAetherRuntime(launch, bootstrap.ParentProcessId);
        });
        await app.StartAsync();
        var runtime = app.Services.Resolve<EngineRuntimeHost>()!;
        var engine = app.Services.Resolve<AetherHost>()!;
        // Capture while the selected collectible game is still loaded, not after Unload.
        var assemblies = ReadAssemblies();
        Exception? failure = null;
        try { await runtime.RunAsync(); }
        catch (Exception error) { failure = error; }
        finally
        {
            assemblies = assemblies.Concat(ReadAssemblies()).Distinct().ToArray();
            try { await runtime.DisposeAsync(); }
            catch (Exception error) { failure = Combine(failure, error); }
            try { await app.StopAsync(); }
            catch (Exception error) { failure = Combine(failure, error); }
            Directory.CreateDirectory(launch.ReportDirectory);
            var gameRoot = launch.Build == null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(launch.Build.DirectoryPath)) + Path.DirectorySeparatorChar;
            var gameAssemblies = assemblies.Where(a => a.Name == "Game.Logic" || gameRoot != null &&
                a.Path.StartsWith(gameRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).ToArray();
            await File.WriteAllTextAsync(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"),
                JsonSerializer.Serialize(new
                {
                    Pid = Environment.ProcessId, ParentProcessId = bootstrap.ParentProcessId,
                    EntryAssemblyPath = Assembly.GetEntryAssembly()!.Location,
                    launch.RuntimeIdentity!.HostAssemblyPath, launch.RuntimeIdentity.RuntimeAssemblyPath,
                    launch.Identity.HostContractId, launch.Identity.GameBuildId, launch.Identity.Generation,
                    launch.Role, launch.Build, launch.RequireRestore,
                    EditorLoaded = assemblies.Any(a => a.Name.StartsWith("TrueMoon.Aether.Editor", StringComparison.Ordinal)),
                    UiLoaded = assemblies.Any(a => a.Name.StartsWith("TrueMoon.Alloy", StringComparison.Ordinal) ||
                        a.Name.StartsWith("TrueMoon.Argentis", StringComparison.Ordinal) ||
                        a.Name.StartsWith("SkiaSharp", StringComparison.Ordinal) ||
                        a.Name.StartsWith("Silk.NET.OpenGL", StringComparison.Ordinal) ||
                        a.Name.StartsWith("Silk.NET.Windowing", StringComparison.Ordinal)),
                    GameLoaded = gameAssemblies.Length != 0,
                    GameAssemblyPaths = gameAssemblies.Select(a => a.Path).ToArray(),
                    LoadedAssemblies = assemblies,
                    Validation = engine.Gpu.LastSurfaceProducer,
                    Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount,
                    ChildDiagnostics = runtime.ChildDiagnosticsStatus, Error = failure?.ToString()
                }));
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed record LoadedAssembly(string Name, string Path);

    private static LoadedAssembly[] ReadAssemblies() => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => new LoadedAssembly(assembly.GetName().Name ?? "", assembly.IsDynamic ? "" : assembly.Location)).ToArray();

    private static Exception Combine(Exception? original, Exception error)
        => original == null ? error : new AggregateException(original, error);
}
