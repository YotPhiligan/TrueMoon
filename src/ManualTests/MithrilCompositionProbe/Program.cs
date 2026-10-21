using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;
using TrueMoon.Services;
using MithrilCompositionProbe;

if (args is ["--idle"])
{
    Console.WriteLine("READY");
    using var idleDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    while (await Console.In.ReadLineAsync(idleDeadline.Token) is { } line) if (line == "qe") return 0;
    return 0;
}
if (Environment.GetEnvironmentVariable("-u") is not null) return await Fixture.ChildAsync();
if (args is ["--parent", var parentDirectory, var parentProvider])
{
    Environment.SetEnvironmentVariable("TM_MITHRIL_DIR", parentDirectory);
    Environment.SetEnvironmentVariable("TM_MITHRIL_PROVIDER", parentProvider);
    await using var app = Fixture.Build("composition", parentProvider);
    await app.StartAsync();
    var ready = await Fixture.ReadyAsync(parentDirectory);
    File.WriteAllText(Path.Combine(parentDirectory, "parent.json"), JsonSerializer.Serialize(new { ParentPid = Environment.ProcessId, ChildPid = ready.Pid }));
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 3;
}
if (args is not [var scenario, var provider, var directory] || !Fixture.Scenarios.Contains(scenario) || provider is not ("cobalt" or "di")) return 2;
try
{
    Directory.CreateDirectory(directory);
    Environment.SetEnvironmentVariable("TM_MITHRIL_DIR", directory);
    Environment.SetEnvironmentVariable("TM_MITHRIL_PROVIDER", provider);
    var result = await Fixture.RunAsync(scenario, provider, directory);
    Console.WriteLine("RESULT " + JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

namespace MithrilCompositionProbe
{
    internal static class Fixture
    {
        internal static readonly string[] Scenarios = ["main", "composition", "external", "startup-failure", "cancel-start", "control-eof", "parent-exit"];
        internal static IApp Build(string scenario, string provider)
        {
            var builder = provider == "di" ? App.Builder(c => c.UseDI()) : App.Builder(_ => { });
            return builder.Setup(context =>
            {
                context.AddUnit(unit => unit.Configuration(c => c.Set("local-marker", "configured"))
                    .Services(r => r.Composite<LocalService, IStartable, IStoppable>()), config =>
                {
                    config.RestartPolicy = UnitRestartPolicy.Never;
                    config.TerminationDelay = 3000;
                    if (scenario == "main") config.ExecuteInMainProcess();
                    if (scenario == "external") config.ExecuteExternal(Path.Combine(AppContext.BaseDirectory, "MithrilCompositionProbe.exe"));
                    if (scenario is "startup-failure" or "cancel-start") config.StartDelayed();
                });
                if (scenario == "startup-failure")
                    context.AddUnit(_ => { }, config => { config.RestartPolicy = UnitRestartPolicy.Never; config.ExecuteExternal(Path.Combine(AppContext.BaseDirectory, "missing-owned-unit.exe")); });
            }).Build();
        }

        internal static async Task<int> ChildAsync()
        {
            var provider = Environment.GetEnvironmentVariable("TM_MITHRIL_PROVIDER")!;
            await using var app = Build("composition", provider);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await app.StartAsync(deadline.Token);
            await app.Services.Resolve<IAppLifetimeHandler>()!.WaitAsync(deadline.Token);
            await app.StopAsync();
            var listener = app.Services.ResolveAll<IStartable>().OfType<UnitParentProcessEventsHandler>().Single();
            await listener.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            return 0;
        }

        internal static async Task<object> RunAsync(string scenario, string provider, string directory)
        {
            if (scenario == "parent-exit") return await ParentExitAsync(provider, directory);
            await using var app = Build(scenario, provider);
            var controller = app.Services.Resolve<IUnitsController>()!;
            if (scenario == "main")
            {
                await app.StartAsync();
                var service = app.Services.ResolveAll<IStartable>().OfType<LocalService>().Single();
                Check(service.Starts == 1 && service.Pid == Environment.ProcessId, "Local unit did not run in the current App.");
                Check(app.Configuration.Get<string>("local-marker") == "configured", "Local configuration was not applied.");
                await app.StopAsync();
                Check(service.Stops == 1, "Local unit was not stopped exactly once.");
                AssertControllerEmpty(controller!);
                return Report(scenario, provider, Environment.ProcessId, 0, true, true);
            }
            if (scenario is "startup-failure" or "cancel-start") await controller.SpawnUnitAsync(1);
            else await app.StartAsync();
            var ready = await ReadyAsync(directory);
            using var child = Process.GetProcessById(ready.Pid);
            _ = child.SafeHandle;
            Check(ready.Parent == Environment.ProcessId && ready.Unit == 1 && ready.Pid != Environment.ProcessId, "Child identity mismatch.");
            Check(ready.Provider == provider, "Wrong child DI provider.");
            if (scenario == "startup-failure")
            {
                try { await app.StartAsync(); throw new InvalidOperationException("Missing executable did not fail."); }
                catch (System.ComponentModel.Win32Exception) { }
            }
            else if (scenario == "cancel-start")
            {
                using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                try { await app.StartAsync(cancelled.Token); throw new InvalidOperationException("Cancelled App started."); }
                catch (OperationCanceledException) { }
                await app.DisposeAsync();
            }
            else if (scenario == "control-eof")
            {
                using (var pipe = new NamedPipeClientStream(".", ready.Endpoint, PipeDirection.Out, PipeOptions.Asynchronous))
                    await pipe.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Check(!app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested, "Uncontrolled child exit cancelled the parent App.");
                await app.StopAsync();
            }
            else await app.StopAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Check(child.ExitCode == 0, "Owned child did not exit gracefully.");
            var stopped = JsonSerializer.Deserialize<Stopped>(await File.ReadAllTextAsync(Path.Combine(directory, "stopped.json")))!;
            Check(stopped.Pid == ready.Pid && stopped.Stops == 1, "Child App shutdown did not stop its service exactly once.");
            if (scenario != "cancel-start") await app.StopAsync();
            AssertControllerEmpty(controller!);
            return Report(scenario, provider, child.Id, child.ExitCode, true, true);
        }

        private static async Task<object> ParentExitAsync(string provider, string directory)
        {
            using var parent = Launch("--parent", directory, provider);
            var stderr = parent.StandardError.ReadToEndAsync();
            Process? child = null;
            try
            {
                await WaitFileAsync(Path.Combine(directory, "parent.json"));
                var ready = await ReadyAsync(directory);
                Check(ready.Parent == parent.Id && ready.Pid != parent.Id, "Grandchild identity mismatch.");
                child = Process.GetProcessById(ready.Pid); _ = child.SafeHandle;
                parent.Kill(entireProcessTree: false);
                await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Check(child.ExitCode == 0, "Parent exit did not trigger graceful child App shutdown.");
                var stopped = JsonSerializer.Deserialize<Stopped>(await File.ReadAllTextAsync(Path.Combine(directory, "stopped.json")))!;
                Check(stopped.Pid == child.Id && stopped.Stops == 1, "Parent-exit service cleanup was not observed.");
                Check(string.IsNullOrEmpty(await stderr), "Parent stderr was not empty.");
                return Report("parent-exit", provider, child.Id, child.ExitCode, true, true);
            }
            finally
            {
                if (!parent.HasExited) { parent.Kill(entireProcessTree: true); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                if (child is not null) { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } child.Dispose(); }
            }
        }

        private static Process Launch(params string[] args)
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
            foreach (var arg in args) info.ArgumentList.Add(arg);
            return Process.Start(info)!;
        }
        internal static async Task<Ready> ReadyAsync(string directory)
        {
            var path = Path.Combine(directory, "ready.json"); await WaitFileAsync(path);
            return JsonSerializer.Deserialize<Ready>(await File.ReadAllTextAsync(path))!;
        }
        private static async Task WaitFileAsync(string path)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!File.Exists(path)) await Task.Delay(10, deadline.Token);
        }
        private static object Report(string scenario, string provider, int childPid, int exitCode, bool cleanup, bool stopped)
            => new { Scenario = scenario, Provider = provider, ParentPid = Environment.ProcessId, ChildPid = childPid, ExitCode = exitCode, Cleanup = cleanup, ServiceStopped = stopped, SameProcess = scenario == "main" };
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void AssertControllerEmpty(IUnitsController controller)
        {
            var handles = (System.Collections.IDictionary)typeof(UnitsController).GetField("_unitHandles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            Check(handles.Count == 0, "Controller retained owned unit handles after cleanup.");
        }
        internal sealed record Ready(int Pid, int Parent, int Unit, string Provider, string Endpoint);
        private sealed record Stopped(int Pid, int Stops);
    }

    public sealed class LocalService : IStartable, IStoppable
    {
        public int Starts, Stops, Pid;
        public Task StartAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Starts++; Pid = Environment.ProcessId;
            var dir = Environment.GetEnvironmentVariable("TM_MITHRIL_DIR")!;
            var path = Path.Combine(dir, "ready.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new Fixture.Ready(Pid,
                int.TryParse(Environment.GetEnvironmentVariable("-p"), out var parent) ? parent : 0,
                int.TryParse(Environment.GetEnvironmentVariable("-u"), out var unit) ? unit : 1,
                Environment.GetEnvironmentVariable("TM_MITHRIL_PROVIDER")!, Environment.GetEnvironmentVariable(UnitParentProcessEventsHandler.ControlPipeKey) ?? "")));
            File.Move(path + ".tmp", path, overwrite: true);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken token = default)
        {
            Stops++;
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("TM_MITHRIL_DIR")!, "stopped.json"), JsonSerializer.Serialize(new { Pid, Stops }));
            return Task.CompletedTask;
        }
    }
}
