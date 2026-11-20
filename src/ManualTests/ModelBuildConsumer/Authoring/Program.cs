using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelImportResearch;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Rendering;
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
using TrueMoon.Nom.Resources;

if (args.Length is < 7 or > 8 || args[0] is not ("microsoft" or "cobalt") ||
    args.Length == 8 && args[7] is not ("--editor" or "--editor-imports" or "--editor-reimport" or "--editor-persisted-imports" or "--editor-model-catalog" or "--editor-model-placement" or "--editor-render-inspector" or "--editor-render-inspector-restart" or "--editor-render-material-reset" or "--editor-render-reference-diagnostics" or "--editor-render-material-slots" or "--editor-render-material-slots-restart" or "--editor-initial-model-import" or "--editor-imported-texture-restart" or "--editor-imported-texture-reimport" or "--editor-imported-texture-replace" or "--editor-playing-texture-workflow" or "--revisions" or "--multi-model")) return 2;
var restartSlotOverride = args.Length == 8 && args[7] == "--editor-render-material-slots-restart";
var restartImportedTexture = args.Length == 8 && args[7] == "--editor-imported-texture-restart";
var textureWorkflow = args.Length == 8 && args[7] == "--editor-playing-texture-workflow";
var replaceTexture = textureWorkflow || args.Length == 8 && args[7] == "--editor-imported-texture-replace";
var reimportTexture = replaceTexture || args.Length == 8 && args[7] == "--editor-imported-texture-reimport";
var editorInitialImport = restartSlotOverride || restartImportedTexture || reimportTexture || args.Length == 8 && args[7] == "--editor-initial-model-import";
var renderSlots = args.Length == 8 && args[7] == "--editor-render-material-slots";
var renderReferenceDiagnostics = args.Length == 8 && args[7] == "--editor-render-reference-diagnostics";
var renderMaterialReset = args.Length == 8 && args[7] == "--editor-render-material-reset";
var renderInspectorRestart = args.Length == 8 && args[7] == "--editor-render-inspector-restart";
var renderInspector = renderSlots || renderReferenceDiagnostics || renderMaterialReset || renderInspectorRestart || args.Length == 8 && args[7] == "--editor-render-inspector";
var editorPlacement = args.Length == 8 && args[7] == "--editor-model-placement";
var editorCatalog = editorPlacement || args.Length == 8 && args[7] == "--editor-model-catalog";
var editorPersistence = editorCatalog || args.Length == 8 && args[7] == "--editor-persisted-imports";
var editorReimport = editorPersistence || args.Length == 8 && args[7] == "--editor-reimport";
var editorImports = editorReimport || renderInspector || args.Length == 8 && args[7] == "--editor-imports";
var editorProbe = editorImports || args.Length == 8 && args[7] == "--editor";
var revisionProbe = args.Length == 8 && args[7] == "--revisions";
var multiModelProbe = editorImports && !renderSlots || args.Length == 8 && args[7] == "--multi-model";
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
var secondSourceDirectory = Path.Combine(evidence, "source-green");
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
if (restartImportedTexture || reimportTexture)
{
    original = WriteTexturedModel(sourceDirectory);
}
else if (renderSlots || editorInitialImport)
{
    original["meshes"]![0]!["extras"] = JsonNode.Parse("""{"truemoon":{"combineMaterialSlots":true}}""");
    primitives[0]!["extras"] = JsonNode.Parse("""{"truemoon":{"subasset":"left"}}""");
    primitives[1]!["extras"] = JsonNode.Parse("""{"truemoon":{"subasset":"right"}}""");
    original["nodes"] = JsonNode.Parse("""[{"name":"two slots","mesh":0}]""");
    var bin = Path.Combine(sourceDirectory, "scene.bin");
    var bytes = File.ReadAllBytes(bin);
    float[] positions = [-1.55f, -.65f, 0, -.4f, -.65f, 0, -1.55f, .5f, 0, .4f, -.65f, 0, 1.55f, -.65f, 0, .4f, .5f, 0];
    for (var index = 0; index < positions.Length; index++)
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * sizeof(float)), positions[index]);
    File.WriteAllBytes(bin, bytes);
    original["accessors"]![0]!["min"] = new JsonArray(-1.55, -.65, 0);
    original["accessors"]![0]!["max"] = new JsonArray(-.4, .5, 0);
    original["accessors"]![1]!["min"] = new JsonArray(.4, -.65, 0);
    original["accessors"]![1]!["max"] = new JsonArray(1.55, .5, 0);
}
else
{
    primitives.RemoveAt(1);
    original["nodes"]![0]!["translation"] = new JsonArray(0, 0, 0);
    original["nodes"]![1]!["translation"] = new JsonArray(-1.5, -1.25, 0);
    original["nodes"]![2]!["translation"] = new JsonArray(-1.5, .25, 0);
}
var sourceJson = original.ToJsonString();
File.WriteAllText(path, sourceJson);
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
var placement = renderSlots ? Matrix4x4.Identity : Matrix4x4.CreateRotationZ(.1f) * Matrix4x4.CreateTranslation(.25f, 0, 0);
var scenePath = Path.Combine(evidence, "placed.aetherscene");
if (editorInitialImport)
{
    var scenes = Directory.CreateDirectory(Path.Combine(evidence, "Scenes"));
    Directory.CreateDirectory(Path.Combine(evidence, "Content"));
    var scene = Path.Combine(scenes.FullName, "placed.aetherscene");
    await using (var authoring = App.Build(context => context.UseAetherAuthoring()))
    {
        var codec = new SceneFileCodec(authoring.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!);
        await File.WriteAllBytesAsync(scene, codec.Encode(initialScene));
    }
    var manifest = Path.Combine(evidence, "Delivery.aetherproj");
    await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
    {
        formatVersion = 1, id = projectId, name = "Initial model import",
        gameProject = "game/DeliveryGame.csproj", entryAssembly = "DeliveryGame.dll", entryType = "DeliveryGame.Entry",
        scenesRoot = "Scenes", contentRoot = "Content"
    }));
    string[] arguments = [runtimeAssembly, "--model-build-probe", args[0], evidence, manifest, scene, workerPath, dotnet];
    if (reimportTexture)
    {
        // Same 2×2 PNG with only the first texel changed to 2194D5FF.
        const string replacement = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAHUlEQVR4AQESAO3/ACGU1f/kCRH/APIWC//8Axb/TywIralGM+UAAAAASUVORK5CYII=";
        File.WriteAllBytes(Path.Combine(evidence, "revised-color.png"), Convert.FromBase64String(replacement));
        var mode = textureWorkflow ? "--initial-import-playing-texture-workflow" :
            replaceTexture ? "--initial-import-replace-texture" : "--initial-import-reimport-texture";
        var reimported = await RunChild("import-reimported", [.. arguments, mode], TimeSpan.FromSeconds(120));
        Check(reimported.ExitCode == 0, reimported.Error);
        return 0;
    }
    var saveMode = restartImportedTexture ? "--initial-import-save-texture" :
        restartSlotOverride ? "--initial-import-save-slot" : "--initial-import-save";
    var reopenMode = restartImportedTexture ? "--initial-import-reopen-texture" :
        restartSlotOverride ? "--initial-import-reopen-slot" : "--initial-import-reopen";
    var saved = await RunChild("import-saved", [.. arguments, saveMode], TimeSpan.FromSeconds(120));
    Check(saved.ExitCode == 0, saved.Error);
    var reopened = await RunChild("import-reopened", [.. arguments, reopenMode], TimeSpan.FromSeconds(120));
    Check(reopened.ExitCode == 0 && saved.Pid != reopened.Pid, reopened.Error);
    return 0;
}

static JsonObject WriteTexturedModel(string directory)
{
    // Opaque 2×2 RGBA PNG, in row order: D51006FF, E40911FF, F2160BFF, FC0316FF.
    const string png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAHUlEQVR4AQESAO3/ANUQBv/kCRH/APIWC//8Axb/Rr8IDi5SSecAAAAASUVORK5CYII=";
    var textures = Directory.CreateDirectory(Path.Combine(directory, "textures"));
    File.WriteAllBytes(Path.Combine(textures.FullName, "color.png"), Convert.FromBase64String(png));
    using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "scene.bin")));
    foreach (var value in new[]
    {
        -1.55f, .60f, 0, -.40f, .60f, 0, -.40f, -.65f, 0, -1.55f, -.65f, 0,
        .40f, -.65f, 0, 1.55f, -.65f, 0, .40f, .50f, 0,
        -.25f, -.25f, 1.25f, -.25f, 1.25f, 1.25f, -.25f, 1.25f
    }) writer.Write(value);
    foreach (var index in new uint[] { 0, 1, 2, 0, 2, 3, 0, 1, 2 }) writer.Write(index);

    return JsonNode.Parse("""
        {
          "asset":{"version":"2.0"}, "scene":0, "scenes":[{"nodes":[0]}],
          "nodes":[{"name":"Textured model","mesh":0}],
          "meshes":[{"name":"Two slots","extras":{"truemoon":{"combineMaterialSlots":true}},"primitives":[
            {"attributes":{"POSITION":0,"TEXCOORD_0":2},"indices":3,"material":0,"extras":{"truemoon":{"subasset":"left"}}},
            {"attributes":{"POSITION":1},"indices":4,"material":1,"extras":{"truemoon":{"subasset":"right"}}}
          ]}],
          "materials":[{"name":"PNG color","pbrMetallicRoughness":{"baseColorTexture":{"index":0}}},
            {"name":"Green","pbrMetallicRoughness":{"baseColorFactor":[0,1,0,1]}}],
          "images":[{"uri":"textures/color.png"}], "textures":[{"source":0,"sampler":0}],
          "samplers":[{"magFilter":9728,"minFilter":9728,"wrapS":33071,"wrapT":33071}],
          "buffers":[{"uri":"scene.bin","byteLength":152}],
          "bufferViews":[{"buffer":0,"byteLength":48,"target":34962},
            {"buffer":0,"byteOffset":48,"byteLength":36,"target":34962},
            {"buffer":0,"byteOffset":84,"byteLength":32,"target":34962},
            {"buffer":0,"byteOffset":116,"byteLength":24,"target":34963},
            {"buffer":0,"byteOffset":140,"byteLength":12,"target":34963}],
          "accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3","min":[-1.55,-0.65,0],"max":[-0.4,0.6,0]},
            {"bufferView":1,"componentType":5126,"count":3,"type":"VEC3","min":[0.4,-0.65,0],"max":[1.55,0.5,0]},
            {"bufferView":2,"componentType":5126,"count":4,"type":"VEC2"},
            {"bufferView":3,"componentType":5125,"count":6,"type":"SCALAR"},
            {"bufferView":4,"componentType":5125,"count":3,"type":"SCALAR"}]
        }
        """)!.AsObject();
}

GameBuildResult oldBuild, newBuild, legacyBuild;
PublishedModelImport oldModel, newModel;
byte[]? inspectorBaseline = null;
string oldSnapshotId, newSnapshotId;
GameBuildResult? scaledBuild = null;
PublishedModelImport? scaledModel = null;
PublishedModelImport? greenModel = null;
string? greenSnapshotId = null;
object? revisionEvidence = null;
object? watchEvidence = null;
var freshnessEvidence = new Dictionary<string, object>();
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
    var watchFactory = app.Services.Resolve<IFactory<ModelSourceWatch, ModelSourceWatchArguments>>()!;
    var importFactory = app.Services.Resolve<IFactory<ModelImportOperation, ModelImportArguments>>()!;
    var ledgerFactory = app.Services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
    var bridge = app.Services.Resolve<NomModelContent>()!;
    var compiler = app.Services.Resolve<ToolingHost>()!.Builds;
    var request = new GameBuildRequest(project, "DeliveryGame.dll", "DeliveryGame.Entry", 1) { RuntimeAccess = true };
    var codec = new SceneFileCodec(app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!);
    await using var ledger = ledgerFactory.Create(new(sourceId, Path.Combine(evidence, "ledger", "scene.tmledger")))!;
    async Task<CapturedModelSource> Capture(Guid? identity = null, string? directory = null)
    {
        await using var capture = captureFactory.Create(new(identity ?? sourceId, directory ?? sourceDirectory, "scene.gltf"))!;
        return await capture.CaptureAsync();
    }
    async Task<ModelImportFreshness> CheckImport(string stage, PublishedModelImport model, string directory, float scale = 1,
        CancellationToken token = default)
    {
        await using var capture = captureFactory.Create(new(model.Candidate.SourceId, directory, "scene.gltf"))!;
        var result = await capture.CheckFreshnessAsync(model, scale, token);
        Check(capture.OpenHandleCount == 0, "Freshness check retained source handles.");
        freshnessEvidence.Add(stage, new
        {
            Status = result.Status.ToString(), result.CapturedInputRevision, result.InputChanged, result.SettingsChanged,
            Error = result.CaptureError?.GetType().Name
        });
        return result;
    }
    async Task<PublishedModelImport> Import(long generation, CapturedModelSource source, float scale = 1,
        ModelImportLedgerSession? target = null)
    {
        await using var operation = importFactory.Create(new(source, worker, Path.Combine(evidence, "scratch")) { GeometryScale = scale })!;
        var candidate = await operation.ImportAsync();
        Check(operation.WorkerReaped, "Import worker must exit before publication.");
        return await (target ?? ledger).PublishAsync(candidate, generation);
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
        Task<GameBuildRenderContent> CaptureBuildContent(string modelSnapshotId) =>
            greenSnapshotId is { } greenCaptureId
                ? bridge.CaptureSnapshotsAsync(store, [modelSnapshotId, greenCaptureId])
                : bridge.CaptureAsync(store, modelSnapshotId);

        async Task<GameBuildResult> BuildModel(string modelSnapshotId) =>
            await compiler.BuildAsync(request with { RenderContent = await CaptureBuildContent(modelSnapshotId) });

        Task<GameBuildResult> BuildChecked(string modelSnapshotId, ModelImportFreshness red, ModelImportFreshness green)
        {
            if (red.Status != ModelImportFreshnessStatus.Current || green.Status != ModelImportFreshnessStatus.Current)
                throw new InvalidOperationException("Both model imports must be current to build new content.");
            return BuildModel(modelSnapshotId);
        }

        var rootHash = Hash(path);
        var vertices = Path.Combine(sourceDirectory, "scene.bin");
        var originalBinHash = Hash(vertices);
        var originalBinWriteTime = File.GetLastWriteTimeUtc(vertices);
        oldModel = await Import(0, await Capture());
        if (editorImports)
            File.Copy(Path.Combine(evidence, "ledger", "scene.tmledger"), Path.Combine(evidence, "ledger", "red-baseline.tmledger"));
        var placed = ImportedModelScene.Place(initialScene, oldModel, placement);
        var redOnly = placed;
        Check(placed.Entities.Skip(1).Select(entity => entity.Id.Value).SequenceEqual(oldModel.Instances.Select(instance => instance.Id)) &&
            placed.Definitions.Length == 2 && placed.Entities[0].Id == cameraId && placed.Revision == 41,
            "Model placement lost stable instances or the existing scene entries.");
        Reject(() => ImportedModelScene.Place(placed, oldModel, placement), "already in the scene");
        var shear = Matrix4x4.Identity;
        shear.M12 = .25f;
        Reject(() => ImportedModelScene.Place(initialScene, oldModel, shear), "positive-scale scene transform");
        var snapshot = await bridge.PublishAsync(store, oldModel, store.ContentGeneration);
        oldSnapshotId = snapshot.Id;
        var greenPlacement = Matrix4x4.CreateTranslation(2, 0, 0);
        if (multiModelProbe)
        {
            Directory.CreateDirectory(secondSourceDirectory);
            var greenRoot = original.DeepClone();
            greenRoot["meshes"]![0]!["primitives"]![0]!["material"] = 1;
            File.WriteAllText(Path.Combine(secondSourceDirectory, "scene.gltf"), greenRoot.ToJsonString());
            var greenBuffer = Path.Combine(secondSourceDirectory, "scene.bin");
            if (renderInspector)
            {
                var greenVertices = File.ReadAllBytes(vertices);
                BinaryPrimitives.WriteSingleLittleEndian(greenVertices.AsSpan(12), .55f);
                File.WriteAllBytes(greenBuffer, greenVertices);
            }
            else File.Copy(vertices, greenBuffer);
            var greenSourceId = Guid.Parse("65710c6d-6781-4476-9bb8-735fc518f008");
            await using var greenLedger = ledgerFactory.Create(new(greenSourceId, Path.Combine(evidence, "ledger", "green.tmledger")))!;
            greenModel = await Import(0, await Capture(greenSourceId, secondSourceDirectory), target: greenLedger);
            Check(!greenModel.Ledger.Entries.Select(entry => entry.Id).Intersect(oldModel.Ledger.Entries.Select(entry => entry.Id)).Any(),
                "Models with separate sources share persistent IDs.");
            placed = ImportedModelScene.Place(placed, greenModel, greenPlacement);
            greenSnapshotId = (await bridge.PublishAsync(store, greenModel, store.ContentGeneration)).Id;
        }
        var sceneBytes = codec.Encode(placed);
        File.WriteAllBytes(scenePath, editorPlacement ? codec.Encode(ImportedModelScene.Place(initialScene, greenModel!, greenPlacement)) :
            renderReferenceDiagnostics ? codec.Encode(redOnly) : sceneBytes);
        if (greenModel != null)
        {
            var red = await CheckImport("RedBeforeEdit", oldModel, sourceDirectory);
            var green = await CheckImport("GreenBeforeEdit", greenModel, secondSourceDirectory);
            oldBuild = await BuildChecked(oldSnapshotId, red, green);
            watchEvidence = await ModelWatchProbe.RunAsync(watchFactory,
                new(new(sourceId, sourceDirectory, "scene.gltf"), oldModel),
                new(new(greenModel.Candidate.SourceId, secondSourceDirectory, "scene.gltf"), greenModel), vertices);

            var settings = await CheckImport("RedSettingsOnly", oldModel, sourceDirectory, scale: 1.5f);
            Check(settings is { Status: ModelImportFreshnessStatus.Stale, InputChanged: false, SettingsChanged: true } &&
                settings.CapturedInputRevision == oldModel.Candidate.InputRevision,
                "A settings-only edit did not invalidate the selected import.");
            var moved = vertices + ".held";
            File.Move(vertices, moved);
            try
            {
                var missing = await CheckImport("RedMissingDependency", oldModel, sourceDirectory);
                Check(missing is { Status: ModelImportFreshnessStatus.Unavailable, InputChanged: null,
                    CapturedInputRevision: null, CaptureError: FileNotFoundException }, "Missing dependency was not reported separately.");
                var intact = await CheckImport("GreenWhileRedMissing", greenModel, secondSourceDirectory);
                Check(intact.Status == ModelImportFreshnessStatus.Current, "A missing dependency affected the other model.");
            }
            finally { File.Move(moved, vertices); }
            await RejectAsync<OperationCanceledException>(
                () => CheckImport("Cancelled", oldModel, sourceDirectory, token: new CancellationToken(canceled: true)));
        }
        else oldBuild = await BuildModel(oldSnapshotId);

        var bytes = File.ReadAllBytes(vertices);
        if (renderInspector) inspectorBaseline = bytes.ToArray();
        // The dependency-only cases change the corner's Y inside the original accessor bounds.
        var dependencyOnly = revisionProbe || multiModelProbe || renderSlots;
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(dependencyOnly ? 16 : 12), renderSlots ? -.2f : dependencyOnly ? .5f : 2f);
        File.WriteAllBytes(vertices, bytes);
        string? detectedInputRevision = null;
        if (greenModel != null)
        {
            File.SetLastWriteTimeUtc(vertices, originalBinWriteTime);
            Check(File.GetLastWriteTimeUtc(vertices) == originalBinWriteTime && new FileInfo(vertices).Length == bytes.Length &&
                Hash(path) == rootHash && Hash(vertices) != originalBinHash,
                "Expected a BIN-only edit with matching size and timestamp.");
            var red = await CheckImport("RedAfterEdit", oldModel, sourceDirectory);
            var green = await CheckImport("GreenAfterEdit", greenModel, secondSourceDirectory);
            Check(red is { Status: ModelImportFreshnessStatus.Stale, InputChanged: true, SettingsChanged: false } &&
                red.CapturedInputRevision != oldModel.Candidate.InputRevision &&
                green is { Status: ModelImportFreshnessStatus.Current, InputChanged: false, SettingsChanged: false } &&
                green.CapturedInputRevision == greenModel.Candidate.InputRevision,
                "A dependency-only edit must invalidate just the model that references it.");
            detectedInputRevision = red.CapturedInputRevision;
            await RejectAsync<InvalidOperationException>(() => BuildChecked(oldSnapshotId, red, green), "must be current");
            Check((await ledger.OpenAsync())?.Candidate.Revision == oldModel.Candidate.Revision,
                "Freshness checks modified the published import.");
        }
        if (!dependencyOnly)
        {
            original["accessors"]![0]!["max"]![0] = 2;
            File.WriteAllText(path, original.ToJsonString());
        }
        if (renderSlots)
        {
            var first = primitives[0];
            primitives.RemoveAt(0);
            primitives.Add(first);
            File.WriteAllText(path, original.ToJsonString());
        }
        var dependencyCapture = await Capture();
        Check(greenModel == null || detectedInputRevision == dependencyCapture.InputRevision,
            "The new import did not capture the revision identified by preflight.");
        newModel = await Import(1, dependencyCapture);
        var reimportedScene = ImportedModelScene.Place(initialScene, newModel, placement);
        if (greenModel != null) reimportedScene = ImportedModelScene.Place(reimportedScene, greenModel, greenPlacement);
        Check(sceneBytes.SequenceEqual(codec.Encode(reimportedScene)),
            "Asset-only reimport changed the placed scene document.");
        var latest = await bridge.PublishAsync(store, newModel, store.ContentGeneration);
        newSnapshotId = latest.Id;
        Check(oldSnapshotId != newSnapshotId, "Nom snapshots have the same identity after reimport.");
        var oldMesh = oldModel.Meshes.Single();
        var meshRevision = snapshot.Members.Single(member => member.AssetId == oldMesh.AssetId);
        Check(meshRevision.Digest == oldMesh.Geometry.Revision.ToString() &&
            latest.Members.Single(member => member.AssetId == oldMesh.AssetId).Digest != meshRevision.Digest,
            "Nom did not retain distinct exact mesh revisions for the same asset ID.");
        if (greenModel != null)
        {
            var red = await CheckImport("RedAfterReimport", newModel, sourceDirectory);
            var green = await CheckImport("GreenAfterReimport", greenModel, secondSourceDirectory);
            newBuild = await BuildChecked(newSnapshotId, red, green);
        }
        else newBuild = await BuildModel(newSnapshotId);
        if (renderSlots)
        {
            var newMesh = newModel.Meshes.Single();
            var reopened = await ledger.OpenAsync() ?? throw new InvalidDataException("Grouped model ledger did not reopen.");
            var publishedSlots = oldModel.Bindings.Where(item => item.Kind == ModelSubassetKind.Slot).ToArray();
            Check(oldModel.Instances.Count == 1 && oldMesh.Slots.Length == 2 &&
                oldMesh.Slots.SequenceEqual(newMesh.Slots) && oldMesh.Slots.SequenceEqual(reopened.Meshes.Single().Slots) &&
                publishedSlots.Select(item => item.Key.Locator).SequenceEqual(["slot/mesh/shared/primitive/left", "slot/mesh/shared/primitive/right"]) &&
                oldMesh.Geometry.Slots.Select(slot => slot.SlotId).SequenceEqual(publishedSlots.Select(item => item.Id)) &&
                oldMesh.Slots.Select(slot => slot.MaterialId).Distinct().Count() == 2,
                "Reordering primitives during reimport changed a persistent slot, its material or the instance.");
            var oldSet = (MaterialBindingSetDefinition)oldBuild.Artifact.RenderContent!.Entries
                .Single(entry => entry.Key.AssetId == oldMesh.BindingSetId).Definition;
            var newSet = (MaterialBindingSetDefinition)newBuild.Artifact.RenderContent!.Entries
                .Single(entry => entry.Key.AssetId == newMesh.BindingSetId).Definition;
            Check(oldSet.Bindings.SequenceEqual(newSet.Bindings) && oldSet.Mesh.AssetId == newSet.Mesh.AssetId && oldSet.Mesh != newSet.Mesh,
                "Nom or Build lost the stable default material assignments across geometry revisions.");
            File.WriteAllText(Path.Combine(evidence, "import-render-material-slots.json"), JsonSerializer.Serialize(new
            {
                Completed = true, Mesh = oldMesh.AssetId, Slots = publishedSlots, OldGeneration = oldModel.Generation,
                NewGeneration = newModel.Generation, OldSnapshot = oldSnapshotId, NewSnapshot = newSnapshotId,
                OldBuild = oldBuild.BuildId, NewBuild = newBuild.BuildId
            }));
        }
        var repeatedOld = await BuildModel(oldSnapshotId);
        Check(repeatedOld.BuildId == oldBuild.BuildId, "Capturing an old Nom snapshot followed the current head.");

        if (greenSnapshotId is { } greenId)
        {
            Check(rootHash == Hash(path) && originalBinHash != Hash(vertices), "Dependency-only edit changed the root model.");
            AssetResourceVersion[] greenResources;
            await using (var lease = await store.OpenSnapshotAsync(greenId))
                greenResources = lease.Manifest.Members;
            var greenAlias = await store.CreateSnapshotAsync(greenResources, 256);
            Check(greenAlias.Id != greenId, "Expected equivalent assets in a distinct snapshot.");
            var reordered = await bridge.CaptureSnapshotsAsync(store, [greenAlias.Id, oldSnapshotId, greenId, oldSnapshotId]);
            var reorderedBuild = await compiler.BuildAsync(request with { RenderContent = reordered });
            Check(reorderedBuild.BuildId == oldBuild.BuildId, "Snapshot order or duplicate resources changed BuildId.");

            await RejectAsync<InvalidDataException>(
                () => bridge.CaptureSnapshotsAsync(store, [greenId, oldSnapshotId, newSnapshotId]), "Conflicting render revisions");
            await RejectAsync<KeyNotFoundException>(
                () => bridge.CaptureSnapshotsAsync(store, [oldSnapshotId, new string('a', 64)]), "Unknown snapshot");
            await RejectAsync<ArgumentOutOfRangeException>(() => bridge.CaptureSnapshotsAsync(store, []), "1..32");
            await RejectAsync<ArgumentOutOfRangeException>(
                () => bridge.CaptureSnapshotsAsync(store, Enumerable.Repeat(greenId, 33).ToArray()), "1..32");
            await RejectAsync<OperationCanceledException>(
                () => bridge.CaptureSnapshotsAsync(store, [oldSnapshotId, greenId], new CancellationToken(canceled: true)));
            foreach (var id in new[] { oldSnapshotId, newSnapshotId, greenId, greenAlias.Id })
                await store.RemoveSnapshotAsync(id); // Removal verifies success and failure paths released their read leases.
        }

        if (revisionProbe)
        {
            var updatedBinHash = Hash(vertices);
            Check(rootHash == Hash(path) && originalBinHash != updatedBinHash &&
                oldModel.Candidate.InputRevision != newModel.Candidate.InputRevision, "Dependency edit was not isolated to the external buffer.");
            try
            {
                await using var invalid = importFactory.Create(new(dependencyCapture, worker, Path.Combine(evidence, "scratch")) { GeometryScale = float.NaN });
                throw new InvalidOperationException("Invalid scale reached a usable import operation.");
            }
            catch (ArgumentOutOfRangeException error) when (error.Message.Contains("geometry scale", StringComparison.Ordinal)) { }

            scaledModel = await Import(2, dependencyCapture, 1.5f);
            var reopened = await ledger.OpenAsync();
            Check(reopened?.Candidate.GeometryScale == 1.5f && reopened.Candidate.Revision == scaledModel.Candidate.Revision &&
                scaledModel.Candidate.InputRevision == newModel.Candidate.InputRevision &&
                scaledModel.Candidate.Revision != newModel.Candidate.Revision &&
                scaledModel.Ledger.Entries.SequenceEqual(oldModel.Ledger.Entries) &&
                sceneBytes.SequenceEqual(codec.Encode(ImportedModelScene.Place(initialScene, scaledModel, placement))),
                "Settings changed the input identity, scene or IDs, or failed to persist in the ledger.");
            var scaledSnapshot = await bridge.PublishAsync(store, scaledModel, store.ContentGeneration);
            scaledBuild = await compiler.BuildAsync(request with { RenderContent = await bridge.CaptureAsync(store, scaledSnapshot.Id) });
            var snapshots = new[] { snapshot, latest, scaledSnapshot };
            var builds = new[] { oldBuild, newBuild, scaledBuild };
            Check(snapshots.Select(item => item.Id).Distinct().Count() == 3 &&
                builds.Select(build => build.BuildId).Distinct().Count() == 3 &&
                builds.Select(build => Hash(Path.Combine(build.DirectoryPath, build.EntryAssembly))).Distinct().Count() == 1,
                "Dependency or setting did not produce a distinct immutable build with the same game DLL.");

            var ids = scaledSnapshot.Members.Select(member => member.AssetId).ToArray();
            var expectedGeneration = store.ContentGeneration;
            var deletedGeneration = await store.DeleteResourcesAsync(expectedGeneration, ids);
            Check(deletedGeneration == expectedGeneration + 1 && (await store.QueryAssetsAsync(new(Source: sourceId))).Items.Length == 0 &&
                (await store.QueryAssetsAsync(new(Source: sourceId, IncludeDeleted: true))).Items.Select(item => item.Id).Order().SequenceEqual(ids.Order()),
                "Deleting the resources did not leave the exact persistent tombstones.");
            foreach (var id in ids)
                Check(await store.FindResourceAsync(id) == null, "Deleted asset still has a current head.");
            for (var i = 0; i < snapshots.Length; i++)
            {
                var retainedBuild = await compiler.BuildAsync(request with { RenderContent = await bridge.CaptureAsync(store, snapshots[i].Id) });
                Check(retainedBuild.BuildId == builds[i].BuildId, "Deleted head changed a retained exact snapshot.");
                await store.RemoveSnapshotAsync(snapshots[i].Id);
            }

            // This fresh store made exactly three publication reservations, one per variant.
            await store.PruneResourceOperationsAsync(3);
            var plan = await store.PlanGarbageCollectionAsync();
            var expectedRevisions = snapshots.SelectMany(item => item.Members).Select(member => member.Revision).Distinct().Count();
            var expectedObjects = snapshots.SelectMany(item => item.Members).Select(member => member.Digest).Distinct().Count();
            Check(plan.RevisionsToPrune == expectedRevisions && plan.ObjectsToDelete == expectedObjects,
                "The garbage plan did not include the stored model payloads.");
            var garbage = await store.CollectGarbageAsync(plan);
            Check(!garbage.CleanupPending && garbage.RevisionsPruned == expectedRevisions && garbage.ObjectsDeleted == expectedObjects,
                "Deleted model data was not reclaimed from Nom.");
            foreach (var saved in snapshots)
            {
                try
                {
                    await bridge.CaptureAsync(store, saved.Id);
                    throw new InvalidOperationException("A removed Nom snapshot could still supply a build.");
                }
                catch (KeyNotFoundException) { }
            }
            revisionEvidence = new
            {
                RootSha256 = rootHash, OriginalBinSha256 = originalBinHash, UpdatedBinSha256 = updatedBinHash,
                OriginalInput = oldModel.Candidate.InputRevision, DependencyInput = newModel.Candidate.InputRevision,
                ScaledInput = scaledModel.Candidate.InputRevision, AppliedScale = reopened!.Candidate.GeometryScale,
                Snapshots = snapshots.Select(item => item.Id), DeletedIds = ids, DeletionGeneration = deletedGeneration, Garbage = garbage
            };
        }
        if (editorReimport)
            await bridge.PublishAsync(store, oldModel, store.ContentGeneration);
    }
    Check(oldModel.Ledger.Entries.SequenceEqual(newModel.Ledger.Entries), "Reimport changed persistent subasset identities.");
    Check(oldBuild.BuildId != newBuild.BuildId && oldBuild.SourceRevision == newBuild.SourceRevision,
        "Geometry changed without changing build identity.");
    Check(Hash(Path.Combine(oldBuild.DirectoryPath, oldBuild.EntryAssembly)) == Hash(Path.Combine(newBuild.DirectoryPath, newBuild.EntryAssembly)),
        "Managed binaries changed in the asset-only build scenario.");
    if (multiModelProbe)
    {
        var arguments = new ModelSourceWatchArguments(new(sourceId, sourceDirectory, "scene.gltf"), newModel);
        await using var watch = watchFactory.Create(arguments)!;
        await ModelWatchProbe.WaitAsync(watch, ModelImportFreshnessStatus.Current);
        await app.StopAsync();
        Check(watch.IsClosed, "App Stop did not close its active model watch.");
        await RejectAsync<ObjectDisposedException>(() => Task.FromResult(watchFactory.Create(arguments)));
        ModelSourceWatch unstartedWatch;
        await using (var unstarted = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); })
            .Setup(context => context.UseAetherModelSourceCapture()).Build())
        {
            var factory = unstarted.Services.Resolve<IFactory<ModelSourceWatch, ModelSourceWatchArguments>>()!;
            unstartedWatch = factory.Create(arguments)!;
            await ModelWatchProbe.WaitAsync(unstartedWatch, ModelImportFreshnessStatus.Current);
        }
        Check(unstartedWatch.IsClosed, "App disposal without Start did not close its model watch.");
    }
    else await app.StopAsync();
}

// Runtime receives only artifacts. The Editor import probe also needs the authoring sources.
if (!editorReimport) Directory.Move(storeDirectory, storeDirectory + "-offline");
if (!editorImports)
{
    Directory.Move(sourceDirectory, sourceDirectory + "-offline");
    if (multiModelProbe) Directory.Move(secondSourceDirectory, secondSourceDirectory + "-offline");
}
if (editorProbe)
{
    if (inspectorBaseline != null)
        File.WriteAllBytes(Path.Combine(sourceDirectory, "scene.bin"), inspectorBaseline);
    if (renderSlots) File.WriteAllText(path, sourceJson);
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
    string[] editorArguments = [runtimeAssembly, "--model-build-probe", args[0], evidence, manifest, scene,
        oldBuild.DirectoryPath, newBuild.DirectoryPath];
    if (renderInspectorRestart)
    {
        var saved = await RunChild("editor-saved", [.. editorArguments, "--render-inspector-save"], TimeSpan.FromSeconds(120));
        Check(saved.ExitCode == 0, saved.Error);
        var reopened = await RunChild("editor-reopened", [.. editorArguments, "--render-inspector-reopen"], TimeSpan.FromSeconds(120));
        Check(reopened.ExitCode == 0, reopened.Error);
        Check(saved.Pid != reopened.Pid, "Expected separate editor processes.");
        foreach (var (phase, pid) in new[] { ("editor-saved", saved.Pid), ("editor-reopened", reopened.Pid) })
        {
            using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidence, phase, "inspector-restart.json")));
            Check(report.RootElement.GetProperty("EditorPid").GetInt32() == pid, $"Incorrect editor process for {phase}.");
        }
        return 0;
    }
    if (editorReimport) editorArguments = [.. editorArguments, editorPlacement ? "--model-placement" :
        editorCatalog ? "--model-catalog" : editorPersistence ? "--persisted-imports" : "--reimport", workerPath, dotnet];
    else if (editorImports) editorArguments = [.. editorArguments, renderSlots ? "--render-material-slots" : renderReferenceDiagnostics ? "--render-reference-diagnostics" :
        renderMaterialReset ? "--render-material-reset" : renderInspector ? "--render-inspector" : "--imports"];
    var result = await RunChild("editor", editorArguments, TimeSpan.FromSeconds(120));
    Check(result.ExitCode == 0 && File.Exists(Path.Combine(evidence, renderSlots ? "editor-render-material-slots.json" : renderReferenceDiagnostics ? "editor-render-reference-diagnostics.json" :
        renderMaterialReset ? "editor-render-material-reset.json" : renderInspector ? "editor-render-inspector.json" :
        editorImports ? "editor-imports.json" : "editor-model.json")), result.Error);
    return 0;
}
var oldProof = await ReadBuild(oldBuild, "old");
var newProof = await ReadBuild(newBuild, "new");
if (multiModelProbe)
{
    var reopened = await ReadBuild(oldBuild, "old-reopened");
    var sceneHash = Hash(scenePath);
    AssertGeometry(oldProof, oldModel, 1, 0, oldBuild, sceneHash);
    AssertGeometry(newProof, newModel, 1, .5f, newBuild, sceneHash);
    AssertGeometry(reopened, oldModel, 1, 0, oldBuild, sceneHash);
    AssertGeometry(oldProof, greenModel!, 1, 0, oldBuild, sceneHash);
    AssertGeometry(newProof, greenModel!, 1, 0, newBuild, sceneHash);
    AssertGeometry(reopened, greenModel!, 1, 0, oldBuild, sceneHash);
    var baselineMask = oldProof.GetProperty("Mask");
    var updatedMask = newProof.GetProperty("Mask");
    var restoredMask = reopened.GetProperty("Mask");
    Check(baselineMask.GetProperty("Sha256").GetString() != updatedMask.GetProperty("Sha256").GetString() &&
        baselineMask.GetProperty("Sha256").GetString() == restoredMask.GetProperty("Sha256").GetString() &&
        baselineMask.GetProperty("GreenPixels").GetInt32() > 100 &&
        baselineMask.GetProperty("GreenSha256").GetString() == updatedMask.GetProperty("GreenSha256").GetString() &&
        baselineMask.GetProperty("GreenSha256").GetString() == restoredMask.GetProperty("GreenSha256").GetString(),
        "Reimport changed the other model or failed to change the intended model on GPU.");
    File.WriteAllText(Path.Combine(evidence, "multi-model.json"), JsonSerializer.Serialize(new
    {
        Completed = true, Provider = args[0], SceneSha256 = sceneHash, OldSnapshot = oldSnapshotId,
        NewSnapshot = newSnapshotId, GreenSnapshot = greenSnapshotId, Freshness = freshnessEvidence, Watch = watchEvidence,
        Old = oldProof, New = newProof, Reopened = reopened
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
if (revisionProbe)
{
    var scaledProof = await ReadBuild(scaledBuild!, "scaled");
    var reopened = await ReadBuild(oldBuild, "old-reopened");
    var sceneHash = Hash(scenePath);
    AssertGeometry(oldProof, oldModel, 1, 0, oldBuild, sceneHash);
    AssertGeometry(newProof, newModel, 1, .5f, newBuild, sceneHash);
    AssertGeometry(scaledProof, scaledModel!, 1.5f, .75f, scaledBuild!, sceneHash);
    AssertGeometry(reopened, oldModel, 1, 0, oldBuild, sceneHash);
    string Mask(JsonElement proof) => proof.GetProperty("Mask").GetProperty("Sha256").GetString()!;
    Check(Mask(oldProof) == Mask(reopened) && new[] { Mask(oldProof), Mask(newProof), Mask(scaledProof) }.Distinct().Count() == 3 &&
        scaledProof.GetProperty("Mask").GetProperty("RedPixels").GetInt32() > 1.6 * newProof.GetProperty("Mask").GetProperty("RedPixels").GetInt32(),
        "Archived builds did not render all three distinct revisions after deletion.");
    File.WriteAllText(Path.Combine(evidence, "revisions.json"), JsonSerializer.Serialize(new
    {
        Completed = true, Provider = args[0], SceneSha256 = sceneHash, Revisions = revisionEvidence,
        Original = oldProof, Dependency = newProof, Scaled = scaledProof, Reopened = reopened
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
var reopenedProof = await ReadBuild(oldBuild, "old-reopened");
var sceneSha256 = Hash(scenePath);
AssertGeometry(oldProof, oldModel, 1, 0, oldBuild, sceneSha256);
AssertGeometry(newProof, newModel, 2, 0, newBuild, sceneSha256);
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

void AssertGeometry(JsonElement proof, PublishedModelImport model, float x, float y, GameBuildResult build, string sceneHash)
{
    var expected = model.Meshes.Single();
    var meshes = proof.GetProperty("Meshes");
    Check(meshes.GetArrayLength() == (multiModelProbe ? 2 : 1), "Build delivered the wrong mesh count.");
    var mesh = meshes.EnumerateArray().Single(entry => entry.GetProperty("Id").GetGuid() == expected.AssetId);
    Check(proof.GetProperty("BuildId").GetString() == build.BuildId && proof.GetProperty("ProjectId").GetGuid() == projectId,
        "Runtime reported the wrong build/project identity.");
    Check(proof.GetProperty("SceneSha256").GetString() == sceneHash && proof.GetProperty("SceneId").GetGuid() == sceneId.Value &&
        proof.GetProperty("SceneRevision").GetInt64() == (multiModelProbe ? 42 : 41), "Player rendered an unexpected scene revision.");
    Check(mesh.GetProperty("Id").GetGuid() == expected.AssetId && mesh.GetProperty("Slot").GetGuid() == expected.SlotId &&
        mesh.GetProperty("Binding").GetGuid() == expected.BindingSetId && mesh.GetProperty("Material").GetGuid() == expected.MaterialId,
        "Runtime mesh, slot, binding or material identity changed.");
    Check(mesh.GetProperty("VertexX").GetSingle() == x && mesh.GetProperty("VertexY").GetSingle() == y &&
        mesh.GetProperty("MaximumX").GetSingle() == x &&
        mesh.GetProperty("Indices").EnumerateArray().Select(value => value.GetUInt32()).SequenceEqual(new uint[] { 0, 1, 2 }) &&
        mesh.GetProperty("Revision").GetString() == expected.Geometry.Revision.ToString(),
        "Runtime received incorrect vertex, topology, bounds or revision.");
}

async Task<JsonElement> ReadBuild(GameBuildResult build, string name)
{
    var result = await RunRuntime(build.DirectoryPath, build.BuildId, name);
    Check(result.ExitCode == 0, result.Error);
    using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidence, name + ".json")));
    return document.RootElement.Clone();
}

Task<(int ExitCode, string Error, int Pid)> RunRuntime(string directory, string buildId, string name) =>
    RunChild(name, [runtimeAssembly, args[0], directory, buildId, hostId, Path.Combine(evidence, name + ".json"), scenePath],
        TimeSpan.FromSeconds(90));

async Task<(int ExitCode, string Error, int Pid)> RunChild(string name, string[] arguments, TimeSpan timeout)
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
    return (child.ExitCode, await stderr, child.Id);
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

static async Task RejectAsync<TException>(Func<Task> action, string? message = null) where TException : Exception
{
    try { await action(); }
    catch (TException error) when (message == null || error.Message.Contains(message, StringComparison.Ordinal)) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name} from the checked operation.");
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
