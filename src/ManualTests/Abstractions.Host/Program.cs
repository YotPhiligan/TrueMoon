using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using TrueMoon.Aether.Ecs;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.ContractHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args.Length == 3 && args[2] != "microsoft") throw new ArgumentException("ContractHost <extension.dll> <report.json> [microsoft]");
        var path = Path.GetFullPath(args[0]); var load = new ContractLoadContext(path);
        IEcsSystem? system = null; var stopped = false; var disposed = false;
        try
        {
            var assembly = load.LoadFromAssemblyPath(path);
            var type = assembly.GetType("TrueMoon.ManualTests.ContractExtension.ExtensionSystem", true)!;
            var metadata = (EcsSystemMetadata)type.GetMethod("Describe")!.Invoke(null, null)!;
            var calls = 0;
            await using var app = App.Builder(builder => { if (args.Length == 3) builder.UseDI(); }).Setup(context => context.Services(services =>
                services.Singleton<IFactory<IEcsSystem>>(_ => new Factory<IEcsSystem>(() => { calls++; return (IEcsSystem)Activator.CreateInstance(type)!; })))).Build();
            if (calls != 0) throw new InvalidOperationException("Eager factory activation.");
            await app.StartAsync(); system = app.Services.Resolve<IFactory<IEcsSystem>>()!.Create()!;
            var access = new ProbeAccess(metadata);
            var context = new EcsSystemContext(access, 1, 7, TimeSpan.FromSeconds(1d / 60), EcsPhase.Gameplay);
            system.Execute(context); access.End(); system.Stop(); stopped = true; system.Dispose(); disposed = true;
            if (calls != 1 || access.QueryCallbacks != 1 || access.Commands != 2 || access.ValueRecorded != 1) throw new InvalidOperationException("Typed contract dispatch mismatch.");
            var shared = assembly.GetType("TrueMoon.ManualTests.ContractExtension.ExtensionSystem")!.GetInterfaces().Single(t => t.FullName == typeof(IEcsSystem).FullName);
            if (!ReferenceEquals(shared, typeof(IEcsSystem))) throw new InvalidOperationException("Duplicated contract identity.");
            var own = AssemblyLoadContext.GetLoadContext(assembly) == load;
            var neutral = load.Assemblies.All(a => a.GetName().Name != "TrueMoon.Aether.Abstractions" && a.GetName().Name != "TrueMoon.Contracts");
            var forbidden = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is { } name &&
                (name == "TrueMoon.Aether" || name.StartsWith("TrueMoon.Aether.Runtime") || name.StartsWith("TrueMoon.Aether.Tooling") || name.StartsWith("TrueMoon.Aether.Editor") || name.StartsWith("Silk.NET") || name.StartsWith("SkiaSharp"))).Select(a => a.GetName().Name).ToArray();
            if (!own || !neutral || forbidden.Length != 0) throw new InvalidOperationException("Forbidden runtime/backend assembly graph.");
            int Value(string name) => (int)type.GetProperty(name)!.GetValue(system)!;
            if (Value("Executions") != 1 || Value("Stops") != 1 || Value("Disposals") != 1) throw new InvalidOperationException("System owner lifecycle mismatch.");
            await app.StopAsync();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
            await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new
            {
                Completed = true,
                Provider = args.Length == 3 ? "Microsoft" : "Cobalt",
                FactoryCalls = calls,
                PrivateExtension = own,
                SharedContracts = neutral,
                ForbiddenAssemblies = forbidden,
                access.QueryCallbacks,
                access.Commands,
                access.ValueRecorded,
                Executions = Value("Executions"),
                Stops = Value("Stops"),
                Disposals = Value("Disposals"),
                ContractAssembly = typeof(IEcsSystem).Assembly.Location,
                ExtensionAssembly = assembly.Location,
                Unload = "Unload requested after references leave scope; collection is not claimed",
                RealWorld = false
            }, new JsonSerializerOptions { WriteIndented = true }));
            system = null; return 0;
        }
        finally
        {
            if (system != null) { try { if (!stopped) system.Stop(); } finally { if (!disposed) system.Dispose(); } }
            load.Unload();
        }
    }
}
