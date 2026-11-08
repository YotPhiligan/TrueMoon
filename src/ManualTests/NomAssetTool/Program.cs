using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Hosting;

namespace TrueMoon.Nom.AssetTool;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        ToolArguments? arguments = null; string? failure = null; long? generation = null; var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidDataException(name); checks.Add(name); }
        try
        {
            arguments = ToolArguments.Parse(args);
            await using var app = App.Build(context =>
            {
                context.UseNom();
                context.Services(services => services.Singleton<AssetTransferWorkflow>(resolver => new(
                    resolver.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>(),
                    resolver.Resolve<IFactory<IProjectAssetStore, RestoreAssetStoreArguments>>())));
            });
            await app.StartAsync();
            var host = app.Services.Resolve<NomStoreHost>() ?? throw new InvalidOperationException("Nom host registration missing.");
            var workflow = app.Services.Resolve<AssetTransferWorkflow>() ?? throw new InvalidOperationException("Asset workflow registration missing.");
            Check(host.ActiveStoreCount == 0, "AppCompositionOpensNoStore");
            generation = await workflow.ExecuteAsync(arguments, Check);
            Check(host.ActiveStoreCount == 0, "WorkflowProductsDisposedBeforeApp");
            await app.StopAsync(); Check(host.ActiveStoreCount == 0 && host.IsReleased, "AppStopConfirmsActualOwnerRelease");
            Check(typeof(IProjectAssetStore).Assembly != typeof(Program).Assembly && typeof(Program).Assembly.GetName().Name == "NomAssetTool", "IndependentConsumerUsesSeparateNomAssembly");
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        if (arguments == null)
        {
            Console.Error.WriteLine("Usage: create|verify|collect|verify-current ROOT PROJECT STORE RECEIPT --report REPORT; export ROOT PROJECT STORE RECEIPT BACKUP --report REPORT; restore BACKUP ROOT PROJECT STORE RECEIPT --report REPORT");
            return 2;
        }
        var module = typeof(IProjectAssetStore).Assembly;
        var report = new
        {
            Completed = failure == null,
            ScenarioName = arguments.Command,
            Checks = checks,
            Consumer = typeof(Program).Assembly.GetName().Name,
            ModulePath = module.Location,
            ModuleSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.Location))),
            arguments.ProjectId,
            arguments.StoreId,
            Generation = generation,
            Failure = failure
        };
        await using (var output = new FileStream(arguments.Report, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { await JsonSerializer.SerializeAsync(output, report, DemoReceipt.Json); await output.FlushAsync(); output.Flush(true); }
        Console.WriteLine($"NomAssetTool {arguments.Command}: {(failure == null ? "passed" : "failed")}, {checks.Count} checks");
        return failure == null ? 0 : 1;
    }
}
