using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelImportResearch;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Nom;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Nom;

if (args.Length is < 7 or > 8 || args[0] is not ("microsoft" or "cobalt") ||
    args.Length == 8 && args[7] != "--editor") return 2;
var editorProbe = args.Length == 8;
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
var hostId = BuildIdentity.ForPlayer(runtimeAssembly);
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
        <Reference Include="TrueMoon.Aether.Runtime" HintPath="$(AetherRuntimePath)" Private="false" />
        <Reference Include="TrueMoon.Contracts" HintPath="$(TrueMoonContractsPath)" Private="false" />
      </ItemGroup>
    </Project>
    """);
File.WriteAllText(Path.Combine(gameDirectory, "Entry.cs"), """
    using System;
    using System.Threading;
    using TrueMoon.Aether.Abstractions.Checkpoints;
    using TrueMoon.Aether.Abstractions.Rendering;
    using TrueMoon.Aether.Abstractions.Runtime;
    using TrueMoon.Aether.Abstractions.Scenes;
    using TrueMoon.Aether.Abstractions.Sessions;
    using TrueMoon.Aether.Runtime;
    using TrueMoon.Aether.Runtime.Ecs;
    using TrueMoon.Aether.Runtime.Ecs.Loading;
    using TrueMoon.Aether.Runtime.Ecs.Rendering;
    using TrueMoon.Aether.Runtime.Ecs.Runtime;
    using TrueMoon.Aether.Runtime.Ecs.Transforms;
    using TrueMoon.Aether.Runtime.Rendering.Snapshots;
    using TrueMoon;
    using TrueMoon.Services;
    namespace DeliveryGame;
    public sealed class Legacy : IGameSession, IGameSessionEntry
    {
        public static IGameSession Create(IServiceResolver? services) => new Legacy();
        public ulong TickNumber { get; private set; }
        public long Revision { get; private set; }
        public uint PatternColor => 0;
        public void Load(SceneSnapshot scene) => Revision = scene.Revision;
        public void Tick() => TickNumber++;
        public void Dispose() { }
    }
    public sealed class Entry(SceneWorldLoader loader, IFactory<TransformSystem, World> transforms, RenderContentManifest content)
        : SceneWorldGameSession(loader), IGameBuildSessionEntry, IRenderSnapshotGameSession, ICheckpointGameSession
    {
        private TransformSystem? _transform;
        public static IGameSession Create(IServiceResolver? services, RenderContentManifest content)
        {
            ArgumentNullException.ThrowIfNull(services);
            var registry = MeshRenderingComponents.Register(PrimitiveEcsComponents.Register(new ComponentRegistryBuilder()), content).Build();
            var loader = services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>().Create(registry);
            return new Entry(loader, services.Resolve<IFactory<TransformSystem, World>>(), content);
        }
        public override uint PatternColor => 0xff000000;
        protected override IDisposable PrepareSceneWorld(SceneWorld scene, CancellationToken cancellationToken)
        {
            var transform = transforms.Create(scene.World);
            try
            {
                transform.InitializeScene(scene, cancellationToken);
                transform.Update(0, cancellationToken);
                _transform = transform;
                return transform;
            }
            catch { transform.Dispose(); throw; }
        }
        protected override void TickWorld(SceneWorld scene, ulong nextTick) => _transform!.Update(nextTick);
        public GameCheckpointState CaptureCheckpoint() => new(TickNumber, new(0, 0, 5, 0, 0, 0), false);
        public void RestoreCheckpoint(GameCheckpointState state)
        {
            if (state with { Tick = TickNumber } != CaptureCheckpoint())
                throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
            if (state.Tick != 0) _transform!.Update(state.Tick);
            RestoreSceneTick(state.Tick);
        }
        public RenderSceneSource ReadRenderScene()
        {
            var scene = ActiveScene!;
            var cameraId = new EntityId(Guid.Parse("3c26b955-01fa-46f1-bab5-e24a07464745"));
            return new(scene.World, content, scene.Scene.Id, Revision, RenderCameraSource.FromEntity(scene.Entities[cameraId]));
        }
    }
    """);
var path = Path.Combine(sourceDirectory, "scene.gltf");
var original = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
var primitives = original["meshes"]![0]!["primitives"]!.AsArray();
primitives.RemoveAt(1);
original["nodes"]![0]!["translation"] = new JsonArray(0, 0, 0);
original["nodes"]![1]!["translation"] = new JsonArray(-1.5, -1.25, 0);
original["nodes"]![2]!["translation"] = new JsonArray(-1.5, .25, 0);
File.WriteAllText(path, original.ToJsonString());
var sceneId = new SceneId(Guid.Parse("4716d534-193a-4b12-a058-73a71d1fb340"));
var cameraId = new EntityId(Guid.Parse("3c26b955-01fa-46f1-bab5-e24a07464745"));
var cameraDefinition = new EntityDefinitionId(Guid.Parse("585db33c-e0bc-4dfc-a251-d55c005dbc23"));
var initialScene = new SceneDocument(sceneId, 40, [new(cameraId, cameraDefinition)],
    [new(cameraDefinition, new EntityPatch(addedComponents:
    [
        new(new(Guid.Parse("e6fa2583-22fc-4d02-b740-4ad263e419e4")), BuiltInComponents.TransformId, 1,
            new Dictionary<string, PropertyValue> { ["z"] = PropertyValue.From(5d) }),
        new(new(Guid.Parse("33f571d0-da11-4e0c-b4ba-d1068cd827fc")), MeshRenderingComponents.CameraId, 1)
    ]))], [BuiltInComponents.Schemas[0], MeshRenderingComponents.CameraSchema]);
var placement = Matrix4x4.CreateRotationZ(.1f) * Matrix4x4.CreateTranslation(.25f, 0, 0);
var scenePath = Path.Combine(evidence, "placed.aetherscene");
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
    var request = new GameBuildRequest(project, "DeliveryGame.dll", "DeliveryGame.Entry", 1) { RuntimeAccess = true };
    var codec = new SceneFileCodec(app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!);
    await using var ledger = ledgerFactory.Create(new(sourceId, Path.Combine(evidence, "ledger", "scene.tmledger")))!;
    async Task<PublishedModelImport> Import(long generation)
    {
        await using var capture = captureFactory.Create(new(sourceId, sourceDirectory, "scene.gltf"))!;
        await using var operation = importFactory.Create(new(await capture.CaptureAsync(), worker, Path.Combine(evidence, "scratch")))!;
        var candidate = await operation.ImportAsync();
        Check(operation.WorkerReaped, "Import worker must exit before publication.");
        return await ledger.PublishAsync(candidate, generation);
    }
    legacyBuild = await compiler.BuildAsync(request with { EntryType = "DeliveryGame.Legacy" });
    Check(legacyBuild.Artifact.RenderContent == null && !File.Exists(Path.Combine(legacyBuild.DirectoryPath, GameBuildArtifact.RenderContentFile)),
        "Managed-only artifact contract changed.");
    using (var legacy = GameBuildLoader.Load(new(legacyBuild.DirectoryPath, legacyBuild.BuildId), hostId))
        Check(legacy.Session.GetType().Name == "Legacy", "Legacy entry activation changed.");
    var missingContent = await compiler.BuildAsync(request);
    Reject(() => GameBuildLoader.Load(new(missingContent.DirectoryPath, missingContent.BuildId), hostId),
        "requires frozen render content");
    await using (var store = storeFactory.Create(new(storeDirectory, projectId, storeId, AssetStoreOpenMode.Create))!)
    {
        oldModel = await Import(0);
        var placed = ImportedModelScene.Place(initialScene, oldModel, placement);
        var sceneBytes = codec.Encode(placed);
        File.WriteAllBytes(scenePath, sceneBytes);
        Check(placed.Entities.Skip(1).Select(entity => entity.Id.Value).SequenceEqual(oldModel.Instances.Select(instance => instance.Id)) &&
            placed.Definitions.Length == 2 && placed.Entities[0].Id == cameraId && placed.Revision == 41,
            "Model placement lost stable instances or the existing scene entries.");
        Reject(() => ImportedModelScene.Place(placed, oldModel, placement), "already in the scene");
        var shear = Matrix4x4.Identity;
        shear.M12 = .25f;
        Reject(() => ImportedModelScene.Place(initialScene, oldModel, shear), "positive-scale scene transform");
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
        Check(sceneBytes.SequenceEqual(codec.Encode(ImportedModelScene.Place(initialScene, newModel, placement))),
            "Asset-only reimport changed the placed scene document.");
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
if (editorProbe)
{
    var scenes = Directory.CreateDirectory(Path.Combine(evidence, "Scenes"));
    Directory.CreateDirectory(Path.Combine(evidence, "Content"));
    var scene = Path.Combine(scenes.FullName, Path.GetFileName(scenePath));
    File.Move(scenePath, scene);
    var manifest = Path.Combine(evidence, "Delivery.aetherproj");
    File.WriteAllText(manifest, JsonSerializer.Serialize(new
    {
        formatVersion = 1, id = projectId, name = "Imported model delivery",
        gameProject = "game/DeliveryGame.csproj", entryAssembly = "DeliveryGame.dll", entryType = "DeliveryGame.Entry",
        scenesRoot = "Scenes", contentRoot = "Content"
    }));
    var result = await RunChild("editor", [runtimeAssembly, "--model-build-probe", args[0], evidence, manifest, scene,
        oldBuild.DirectoryPath, newBuild.DirectoryPath], TimeSpan.FromSeconds(120));
    Check(result.ExitCode == 0 && File.Exists(Path.Combine(evidence, "editor-model.json")), result.Error);
    return 0;
}
var oldProof = await ReadBuild(oldBuild, "old");
var newProof = await ReadBuild(newBuild, "new");
var reopenedProof = await ReadBuild(oldBuild, "old-reopened");
var sceneSha256 = Hash(scenePath);
foreach (var (proof, model, width, build) in new[] { (oldProof, oldModel, 1f, oldBuild), (newProof, newModel, 2f, newBuild) })
{
    var mesh = proof.GetProperty("Meshes").EnumerateArray().Single();
    var expected = model.Meshes.Single();
    Check(proof.GetProperty("BuildId").GetString() == build.BuildId && proof.GetProperty("ProjectId").GetGuid() == projectId,
        "Runtime reported the wrong build/project identity.");
    Check(proof.GetProperty("SceneSha256").GetString() == sceneSha256 && proof.GetProperty("SceneId").GetGuid() == sceneId.Value &&
        proof.GetProperty("SceneRevision").GetInt64() == 41, "Player rendered an unexpected scene revision.");
    Check(mesh.GetProperty("Id").GetGuid() == expected.AssetId && mesh.GetProperty("Slot").GetGuid() == expected.SlotId &&
        mesh.GetProperty("Binding").GetGuid() == expected.BindingSetId && mesh.GetProperty("Material").GetGuid() == expected.MaterialId,
        "Runtime mesh, slot, binding or material identity changed.");
    Check(mesh.GetProperty("VertexX").GetSingle() == width && mesh.GetProperty("MaximumX").GetSingle() == width &&
        mesh.GetProperty("Indices").EnumerateArray().Select(value => value.GetUInt32()).SequenceEqual(new uint[] { 0, 1, 2 }) &&
        mesh.GetProperty("Revision").GetString() == expected.Geometry.Revision.ToString(),
        "Runtime received incorrect vertex, topology, bounds or revision.");
}
var oldMask = oldProof.GetProperty("Mask");
var newMask = newProof.GetProperty("Mask");
Check(oldMask.GetProperty("Sha256").GetString() == reopenedProof.GetProperty("Mask").GetProperty("Sha256").GetString() &&
    reopenedProof.GetProperty("BuildId").GetString() == oldBuild.BuildId &&
    reopenedProof.GetProperty("SceneSha256").GetString() == sceneSha256, "Reopened old build changed its GPU geometry.");
Check(oldMask.GetProperty("Width").GetInt32() == newMask.GetProperty("Width").GetInt32() &&
    oldMask.GetProperty("Height").GetInt32() == newMask.GetProperty("Height").GetInt32() &&
    Math.Abs(newMask.GetProperty("Left").GetInt32() - oldMask.GetProperty("Left").GetInt32()) <= 1 &&
    newMask.GetProperty("Right").GetInt32() > oldMask.GetProperty("Right").GetInt32() + 20 &&
    newMask.GetProperty("RedPixels").GetInt32() > oldMask.GetProperty("RedPixels").GetInt32() * 1.6,
    "Reimported GPU geometry did not extend to the right at the same placement.");
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
    Scene = scenePath, SceneSha256 = sceneSha256,
    StableIds = newModel.Ledger.Entries.Select(entry => new { entry.Key, entry.Id }), Old = oldProof, New = newProof, Reopened = reopenedProof
}, new JsonSerializerOptions { WriteIndented = true }));
return 0;

async Task<JsonElement> ReadBuild(GameBuildResult build, string name)
{
    var result = await RunRuntime(build.DirectoryPath, build.BuildId, name);
    Check(result.ExitCode == 0, result.Error);
    using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidence, name + ".json")));
    return document.RootElement.Clone();
}

Task<(int ExitCode, string Error)> RunRuntime(string directory, string buildId, string name) =>
    RunChild(name, [runtimeAssembly, args[0], directory, buildId, hostId, Path.Combine(evidence, name + ".json"), scenePath],
        TimeSpan.FromSeconds(90));

async Task<(int ExitCode, string Error)> RunChild(string name, string[] arguments, TimeSpan timeout)
{
    var info = new ProcessStartInfo(dotnet)
    {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        WorkingDirectory = evidence
    };
    foreach (var argument in arguments)
        info.ArgumentList.Add(argument);
    using var child = Process.Start(info) ?? throw new IOException("Runtime child could not start.");
    var stdout = child.StandardOutput.ReadToEndAsync();
    var stderr = child.StandardError.ReadToEndAsync();
    try
    {
        await child.WaitForExitAsync().WaitAsync(timeout);
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

static void Reject(Func<object> action, string message)
{
    try
    {
        var value = action();
        (value as IDisposable)?.Dispose();
    }
    catch (InvalidDataException error) when (error.Message.Contains(message, StringComparison.Ordinal))
    {
        return;
    }
    throw new InvalidOperationException("Expected rejection: " + message);
}
