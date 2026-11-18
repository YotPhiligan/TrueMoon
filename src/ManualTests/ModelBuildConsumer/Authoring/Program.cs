using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelImportResearch;
using TrueMoon;
using TrueMoon.Aether.Nom;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Nom;

if (args.Length != 7 || args[0] is not ("microsoft" or "cobalt")) return 2;
var evidence = Path.GetFullPath(args[1]);
if (Directory.Exists(evidence)) throw new IOException("Use a fresh output directory.");
Directory.CreateDirectory(evidence);
var workerPath = Path.GetFullPath(args[2]);
var runtimeAssembly = Path.GetFullPath(args[3]);
var dotnet = Path.GetFullPath(args[4]);
var framework = args[5];
var sdk = args[6];
var worker = new ModelImportWorkerDeployment(workerPath, dotnet,
    Directory.EnumerateFiles(workerPath, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(workerPath, path).Replace('\\', '/'), Hash));
var hostId = new string('A', 64);
var projectId = Guid.Parse("ab0e80cb-9a94-4b86-9bff-308678bd91ab");
var storeId = Guid.Parse("ae24f96d-89a8-4ecc-bc22-4bd0cf2f093e");
var sourceId = Guid.Parse("ce662048-9dcb-4c60-aaf0-213342054fa6");
var sourceDirectory = Path.Combine(evidence, "source");
var storeDirectory = Path.Combine(evidence, "nom");
var gameDirectory = Path.Combine(evidence, "game");
Fixtures.Create(sourceDirectory);
Directory.CreateDirectory(gameDirectory);
File.WriteAllText(Path.Combine(gameDirectory, "global.json"), JsonSerializer.Serialize(new { sdk = new { version = sdk, rollForward = "disable" } }));
File.WriteAllText(Path.Combine(gameDirectory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
File.WriteAllText(Path.Combine(gameDirectory, "Directory.Build.props"), "<Project/>");
File.WriteAllText(Path.Combine(gameDirectory, "Directory.Packages.props"), "<Project/>");
var project = Path.Combine(gameDirectory, "DeliveryGame.csproj");
File.WriteAllText(project, $$"""
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>{{framework}}</TargetFramework>
        <Nullable>enable</Nullable>
        <DebugType>none</DebugType>
        <Deterministic>true</Deterministic>
      </PropertyGroup>
      <ItemGroup>
        <Reference Include="TrueMoon.Aether.Abstractions" HintPath="$(AetherAbstractionsPath)" Private="false" />
        <Reference Include="TrueMoon.Contracts" HintPath="$(TrueMoonContractsPath)" Private="false" />
      </ItemGroup>
    </Project>
    """);
File.WriteAllText(Path.Combine(gameDirectory, "Entry.cs"), """
    using TrueMoon.Aether.Abstractions.Runtime;
    using TrueMoon.Aether.Abstractions.Scenes;
    using TrueMoon.Aether.Abstractions.Sessions;
    using TrueMoon.Services;
    namespace DeliveryGame;
    public sealed class Entry : IGameSession, IGameSessionEntry
    {
        public static IGameSession Create(IServiceResolver? services) => new Entry();
        public ulong TickNumber { get; private set; }
        public long Revision { get; private set; }
        public uint PatternColor => 0;
        public void Load(SceneSnapshot scene) => Revision = scene.Revision;
        public void Tick() => TickNumber++;
        public void Dispose() { }
    }
    """);
var path = Path.Combine(sourceDirectory, "scene.gltf");
var original = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
var primitives = original["meshes"]![0]!["primitives"]!.AsArray();
primitives.RemoveAt(1);
File.WriteAllText(path, original.ToJsonString());
GameBuildResult oldBuild, newBuild, legacyBuild;
PublishedModelImport oldModel, newModel;
string oldSnapshotId, newSnapshotId;
await using (var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); }).Setup(context =>
{
    context.UseNom();
    context.UseAetherModelSourceCapture();
    context.UseAetherModelImport();
    context.UseAetherNomContent();
    context.UseAetherTooling(options =>
    {
        options.HostContractId = hostId;
        options.BuildDirectory = Path.Combine(evidence, "builds");
    });
}).Build())
{
    await app.StartAsync();
    var storeFactory = app.Services.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>()!;
    var captureFactory = app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!;
    var importFactory = app.Services.Resolve<IFactory<ModelImportOperation, ModelImportArguments>>()!;
    var ledgerFactory = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
    var bridge = app.Services.Resolve<NomModelContent>()!;
    var compiler = app.Services.Resolve<ToolingHost>()!.Builds;
    var request = new GameBuildRequest(project, "DeliveryGame.dll", "DeliveryGame.Entry", 1);
    await using var ledger = ledgerFactory.Create(new(sourceId, Path.Combine(evidence, "ledger", "scene.tmledger")))!;
    async Task<PublishedModelImport> Import(long generation)
    {
        await using var capture = captureFactory.Create(new(sourceId, sourceDirectory, "scene.gltf"))!;
        await using var operation = importFactory.Create(new(await capture.CaptureAsync(), worker, Path.Combine(evidence, "scratch")))!;
        var candidate = await operation.ImportAsync();
        Check(operation.WorkerReaped, "Import worker must exit before publication.");
        return await ledger.PublishAsync(candidate, generation);
    }
    legacyBuild = await compiler.BuildAsync(request);
    Check(legacyBuild.Artifact.RenderContent == null && !File.Exists(Path.Combine(legacyBuild.DirectoryPath, GameBuildArtifact.RenderContentFile)),
        "Managed-only artifact contract changed.");
    await using (var store = storeFactory.Create(new(storeDirectory, projectId, storeId, AssetStoreOpenMode.Create))!)
    {
        oldModel = await Import(0);
        var snapshot = await bridge.PublishAsync(store, oldModel, store.ContentGeneration);
        oldSnapshotId = snapshot.Id;
        oldBuild = await compiler.BuildAsync(request with { RenderContent = await bridge.CaptureAsync(store, oldSnapshotId) });

        var vertices = Path.Combine(sourceDirectory, "scene.bin");
        var bytes = File.ReadAllBytes(vertices);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 2);
        File.WriteAllBytes(vertices, bytes);
        original["accessors"]![0]!["max"]![0] = 2;
        File.WriteAllText(path, original.ToJsonString());
        newModel = await Import(1);
        var latest = await bridge.PublishAsync(store, newModel, store.ContentGeneration);
        newSnapshotId = latest.Id;
        Check(oldSnapshotId != newSnapshotId, "Nom snapshots have the same identity after reimport.");
        var oldMesh = oldModel.Meshes.Single();
        var meshRevision = snapshot.Members.Single(member => member.AssetId == oldMesh.AssetId);
        Check(meshRevision.Digest == oldMesh.Geometry.Revision.ToString() &&
            latest.Members.Single(member => member.AssetId == oldMesh.AssetId).Digest != meshRevision.Digest,
            "Nom did not retain distinct exact mesh revisions for the same asset ID.");
        newBuild = await compiler.BuildAsync(request with { RenderContent = await bridge.CaptureAsync(store, newSnapshotId) });
        var repeatedOld = await compiler.BuildAsync(request with { RenderContent = await bridge.CaptureAsync(store, oldSnapshotId) });
        Check(repeatedOld.BuildId == oldBuild.BuildId, "Capturing an old Nom snapshot followed the current head.");
    }
    Check(oldModel.Ledger.Entries.SequenceEqual(newModel.Ledger.Entries), "Reimport changed persistent subasset identities.");
    Check(oldBuild.BuildId != newBuild.BuildId && oldBuild.SourceRevision == newBuild.SourceRevision,
        "Geometry changed without changing build identity.");
    Check(Hash(Path.Combine(oldBuild.DirectoryPath, oldBuild.EntryAssembly)) == Hash(Path.Combine(newBuild.DirectoryPath, newBuild.EntryAssembly)),
        "Managed binaries changed in the asset-only build scenario.");
    await app.StopAsync();
}

// Only the artifact copies are supplied to Runtime. Rename the closed store and the source directory.
Directory.Move(storeDirectory, storeDirectory + "-offline");
Directory.Move(sourceDirectory, sourceDirectory + "-offline");
var oldProof = await ReadBuild(oldBuild, "old");
var newProof = await ReadBuild(newBuild, "new");
foreach (var (proof, model, width, build) in new[] { (oldProof, oldModel, 1f, oldBuild), (newProof, newModel, 2f, newBuild) })
{
    var mesh = proof.GetProperty("Meshes").EnumerateArray().Single();
    var expected = model.Meshes.Single();
    Check(proof.GetProperty("BuildId").GetString() == build.BuildId && proof.GetProperty("ProjectId").GetGuid() == projectId,
        "Runtime reported the wrong build/project identity.");
    Check(mesh.GetProperty("Id").GetGuid() == expected.AssetId && mesh.GetProperty("Slot").GetGuid() == expected.SlotId &&
        mesh.GetProperty("Binding").GetGuid() == expected.BindingSetId && mesh.GetProperty("Material").GetGuid() == expected.MaterialId,
        "Runtime mesh, slot, binding or material identity changed.");
    Check(mesh.GetProperty("VertexX").GetSingle() == width && mesh.GetProperty("MaximumX").GetSingle() == width &&
        mesh.GetProperty("Indices").EnumerateArray().Select(value => value.GetUInt32()).SequenceEqual(new uint[] { 0, 1, 2 }) &&
        mesh.GetProperty("Revision").GetString() == expected.Geometry.Revision.ToString(),
        "Runtime received incorrect vertex, topology, bounds or revision.");
}
var damaged = Path.Combine(evidence, "damaged", oldBuild.BuildId);
Directory.CreateDirectory(damaged);
foreach (var file in Directory.EnumerateFiles(oldBuild.DirectoryPath)) File.Copy(file, Path.Combine(damaged, Path.GetFileName(file)));
var payload = Path.Combine(damaged, GameBuildArtifact.RenderContentFile);
var corrupted = File.ReadAllBytes(payload);
corrupted[^1] ^= 1;
File.WriteAllBytes(payload, corrupted);
var failure = await RunRuntime(damaged, oldBuild.BuildId, "damaged");
Check(failure.ExitCode == 1 && failure.Error.Contains("render payload hash mismatch", StringComparison.Ordinal) &&
    !File.Exists(Path.Combine(evidence, "damaged.json")), "Runtime did not reject a corrupted build payload.");
File.WriteAllText(Path.Combine(evidence, "report.json"), JsonSerializer.Serialize(new
{
    Completed = true, Provider = args[0], OldSnapshot = oldSnapshotId, NewSnapshot = newSnapshotId,
    OldBuild = oldBuild.BuildId, NewBuild = newBuild.BuildId, LegacyBuild = legacyBuild.BuildId,
    StableIds = newModel.Ledger.Entries.Select(entry => new { entry.Key, entry.Id }), Old = oldProof, New = newProof
}, new JsonSerializerOptions { WriteIndented = true }));
return 0;

async Task<JsonElement> ReadBuild(GameBuildResult build, string name)
{
    var result = await RunRuntime(build.DirectoryPath, build.BuildId, name);
    Check(result.ExitCode == 0, result.Error);
    using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidence, name + ".json")));
    return document.RootElement.Clone();
}

async Task<(int ExitCode, string Error)> RunRuntime(string directory, string buildId, string name)
{
    var info = new ProcessStartInfo(dotnet)
    {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        WorkingDirectory = evidence
    };
    foreach (var argument in new[] { runtimeAssembly, args[0], directory, buildId, hostId, Path.Combine(evidence, name + ".json") })
        info.ArgumentList.Add(argument);
    using var child = Process.Start(info) ?? throw new IOException("Runtime child could not start.");
    var stdout = child.StandardOutput.ReadToEndAsync();
    var stderr = child.StandardError.ReadToEndAsync();
    try
    {
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
    }
    finally
    {
        if (!child.HasExited) child.Kill(entireProcessTree: true);
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        File.WriteAllText(Path.Combine(evidence, name + ".stdout.log"), await stdout);
        File.WriteAllText(Path.Combine(evidence, name + ".stderr.log"), await stderr);
    }
    return (child.ExitCode, await stderr);
}

static string Hash(string path)
{
    using var file = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(file));
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
