using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using EngineRuntime = TrueMoon.Aether.Runtime.AetherRuntimeHost;

namespace PackageRuntime;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 0) return 2;
            var bootstrap = RuntimeBootstrap.ReadEnvironment();
            var launch = bootstrap.Launch;
            await using var app = App.Build(c =>
            {
                c.UseDiagnostics(new DiagnosticPipelineOptions
                {
                    JsonDirectory = Path.Combine(launch.ReportDirectory, "diagnostics", "runtime", launch.Identity.Generation.ToString()),
                    JsonUseRunDirectories = true
                });
                c.UseAether(o =>
                {
                    o.HostContractId = launch.Identity.HostContractId; o.GameBuildId = launch.Identity.GameBuildId;
                    o.ReportDirectory = launch.ReportDirectory;
                    o.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
                });
                if (launch.Role == RuntimeRole.EditPreview) c.UseAetherPreviewRuntime(launch, bootstrap.ParentProcessId);
                else c.UseAetherRuntime(launch, bootstrap.ParentProcessId);
            });
            await app.StartAsync();
            var runtime = app.Services.Resolve<EngineRuntime>()!;
            var host = app.Services.Resolve<AetherHost>()!;
            var source = app.Services.Resolve<IEventsSourceFactory>()!.Create("TrueMoon.Package.Runtime");
            Console.WriteLine("PACKAGE_RUNTIME_STDOUT"); Console.Error.WriteLine("PACKAGE_RUNTIME_STDERR");
            var work = runtime.RunAsync();
            var diagnostics = Task.Run(async () =>
            {
                do
                {
                    source.Write(DiagnosticLevel.Warning, 991, "Package.Runtime.Ready", "Package-owned runtime is running");
                    await Task.Delay(20);
                } while (!work.IsCompleted);
            });
            var assemblies = ReadAssemblies(); Exception? failure = null;
            try { await work; }
            catch (Exception error) { failure = error; }
            finally
            {
                await diagnostics;
                assemblies = assemblies.Concat(ReadAssemblies()).Distinct().ToArray();
                try { await runtime.DisposeAsync(); } catch (Exception error) { failure = Combine(failure, error); }
                try { await app.StopAsync(); } catch (Exception error) { failure = Combine(failure, error); }
                Directory.CreateDirectory(launch.ReportDirectory);
                await File.WriteAllTextAsync(Path.Combine(launch.ReportDirectory, $"runtime-{launch.Identity.Generation}.json"),
                    JsonSerializer.Serialize(new
                    {
                        Pid = Environment.ProcessId, bootstrap.ParentProcessId, launch.Identity.Generation, launch.Identity.GameBuildId,
                        EntryAssemblyPath = Assembly.GetEntryAssembly()!.Location, launch.RuntimeIdentity,
                        EditorLoaded = assemblies.Any(a => a.Name == "TrueMoon.Aether.Editor"),
                        UiLoaded = assemblies.Any(a => a.Name.StartsWith("TrueMoon.Alloy", StringComparison.Ordinal) ||
                            a.Name is "TrueMoon.Argentis" or "SkiaSharp"),
                        GameLoaded = assemblies.Any(a => a.Name == "Game.Logic"),
                        GameAssemblyPaths = assemblies.Where(a => a.Name == "Game.Logic").Select(a => a.Path).ToArray(),
                        LoadedAssemblies = assemblies, Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount,
                        runtime.ChildDiagnosticsStatus, Error = failure?.ToString()
                    }));
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private sealed record LoadedAssembly(string Name, string Path);
    private static LoadedAssembly[] ReadAssemblies() => AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
        .Select(a => new LoadedAssembly(a.GetName().Name ?? "", a.Location)).ToArray();
    private static Exception Combine(Exception? first, Exception second) => first == null ? second : new AggregateException(first, second);
}
