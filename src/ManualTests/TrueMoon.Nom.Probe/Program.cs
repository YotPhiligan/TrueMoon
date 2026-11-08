using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Probe;

internal static class Program
{
    internal static int Main(string[] args)
    {
        if (args[0] == "vm-child") return args.Length == 2 ? NomVmCrashProof.Child(args[1]).GetAwaiter().GetResult() : 64;
        if (args[0] == "vm-verify") return args.Length == 4 ? NomVmCrashProof.Verify(args[1], args[2], args[3]).GetAwaiter().GetResult() : 64;
        if (args[0] == "vm-protocol-proof") return args.Length == 2 ? NomVmCrashProof.ProtocolProof(args[1]).GetAwaiter().GetResult() : 64;
        if (args[0] == "kv-proof") return NomKvProof.Run(args[1]).GetAwaiter().GetResult();
        if (args[0] == "kv-child") return NomKvProof.Child(args);
        if (args[0] == "fault-proof") return NomFaultProof.Run(args[1]).GetAwaiter().GetResult();
        if (args[0] == "fault-child") return NomFaultProof.Child(args);
        if (args[0] == "maintenance-proof") return NomMaintenanceProof.Run(args[1]).GetAwaiter().GetResult();
        if (args[0] == "maintenance-child") return NomMaintenanceProof.Child(args);
        if (args[0] == "retention-proof") return NomRetentionProof.Run(args[1]).GetAwaiter().GetResult();
        if (args[0] == "retention-child") return NomRetentionProof.Child(args);
        if (args[0] == "performance-proof") return NomPerformanceProof.Run(args[1],args.Length>2 ? args[2] : "all").GetAwaiter().GetResult();
        if (args[0] == "performance-open") return NomPerformanceProof.OpenChild(args);
        if (args[0] == "backup-proof") return NomBackupProof.Run(args[1]).GetAwaiter().GetResult();
        if (args[0] == "backup-child") return NomBackupProof.Child(args);
        if (args[0] == "capacity-proof") return NomCapacityProof.Run(args[1]);
        if (args[0] == "resource-crash") return StreamingProof.ChildCrash(args);
        if (args[0] == "child") return Child(args);
        return CatalogProof.Run(args[0]).GetAwaiter().GetResult();
    }
    internal static object LibraryInfo()
    {
        var module = typeof(CatalogFactory).Assembly;
        CatalogProof.Check(module.GetName().Name == "TrueMoon.Nom" && module != typeof(Program).Assembly &&
            Path.GetDirectoryName(module.Location) == Path.GetDirectoryName(typeof(Program).Assembly.Location), "separate output-local TrueMoon.Nom module assembly");
        var assembly = module;
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
        CatalogProof.Check(Path.GetFullPath(Path.GetDirectoryName(assembly.Location)!) == Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), "output-local library hash");
        var version = assembly.GetName().Version!;
        CatalogProof.Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "LiteDB"), "no removed backend assembly loaded");
        return new { Path = assembly.Location, Sha256 = hash, Version = version.ToString(), Framework = Environment.Version.ToString(), ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), Module = module.GetName().Name, ModuleSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.Location))) };
    }
    private static int Child(string[] args)
    {
        try
        {
            var root = args[2]; var project = Guid.Parse(args[3]); var store = Guid.Parse(args[4]);
            Console.WriteLine("attempt"); Console.Out.Flush();
            if (args[1] == "raw")
            {
                using var file = new FileStream(Path.Combine(root, "journal", "00000000000000000001.nom"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
            else
            {
                using var factory = new CatalogFactory(); using var owner = factory.Create(new(root, project, store, false));
                _ = owner.ReadAsync().GetAwaiter().GetResult();
            }
            Console.WriteLine("opened"); return 0;
        }
        catch (IOException ex) { Console.WriteLine("denied:" + ex.GetType().FullName); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    internal static async Task<string> ChildProbe(string mode, OpenArguments arguments)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Assembly.GetExecutingAssembly().Location, "child", mode, arguments.Root, arguments.ProjectId.ToString(), arguments.StoreId.ToString() }) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info }; var started = false;
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Probe start failed."); started = true;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            CatalogProof.Check(process.ExitCode == 0 && (await error).Length == 0, "child probe exit");
            var result = await output; CatalogProof.Check(result.StartsWith("attempt", StringComparison.Ordinal), "child reached attempt");
            return result;
        }
        finally
        {
            if (started && !process.HasExited) { process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        }
    }
}
