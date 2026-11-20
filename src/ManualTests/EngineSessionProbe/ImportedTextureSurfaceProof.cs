using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Nom;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Nom;
using TrueMoon.Services;

namespace EngineSessionProbe;

public static partial class MeshSurfaceProbe
{
    private static async Task<(TextureBuild[] Builds, object Evidence)> BuildImportedTextureCases(string gameProject,
        string workerDirectory, string reportDirectory, IServiceProvider services, GameBuildCompiler compiler, CancellationToken token)
    {
        var sourceId = new Guid("74fb9396-d2ac-4e50-b9b9-b71029fb5e20");
        var projectId = new Guid("6e74f7cc-bf80-49c1-a4a2-4c0ab74cdbe0");
        var sourceDirectory = Path.Combine(reportDirectory, "model-source");
        var ledgerArguments = new ModelImportLedgerArguments(sourceId, Path.Combine(reportDirectory, "model.tmledger"));
        var worker = new ModelImportWorkerDeployment(workerDirectory, Environment.ProcessPath!,
            Directory.EnumerateFiles(workerDirectory, "*", SearchOption.AllDirectories).ToDictionary(
                path => Path.GetRelativePath(workerDirectory, path).Replace('\\', '/'),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));
        var captures = services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!;
        var imports = services.Resolve<IFactory<ModelImportOperation, ModelImportArguments>>()!;
        var ledgers = services.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>()!;
        var bridge = services.Resolve<NomModelContent>()!;
        var stores = services.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>()!;

        WriteTexturedModel(sourceDirectory);
        var pngPath = Path.Combine(sourceDirectory, "textures", "color.png");
        WritePng(pngPath, TexturePixels(revised: false));
        var oldSource = await Capture();
        var oldModel = await Import(oldSource, 0, expectedPrevious: null);
        var original = await Freshness(oldModel);
        Check(original.Status == ModelImportFreshnessStatus.Current, "Original texture import is not current.");

        await using var store = stores.Create(new(Path.Combine(reportDirectory, "nom"), projectId, Guid.NewGuid(), AssetStoreOpenMode.Create))!;
        var a = await PublishBuild(oldModel, revised: false);
        WritePng(pngPath, TexturePixels(revised: true));
        var stale = await Freshness(oldModel);
        Check(stale is { Status: ModelImportFreshnessStatus.Stale, InputChanged: true, SettingsChanged: false },
            "Changing only the PNG failed to make the import stale.");
        var newSource = await Capture();
        var oldFiles = oldSource.Files.ToDictionary(file => file.Name, file => file.Sha256);
        Check(oldFiles.Count == 3 && newSource.Files.Count == 3 &&
            newSource.Files.Where(file => oldFiles[file.Name] != file.Sha256).Select(file => file.Name).SequenceEqual(["textures/color.png"]) &&
            stale.CapturedInputRevision == newSource.InputRevision, "The PNG must be the only changed source dependency.");
        var newModel = await Import(newSource, oldModel.Generation, oldModel);
        var refreshed = await Freshness(newModel);
        Check(refreshed.Status == ModelImportFreshnessStatus.Current, "Reimport did not restore freshness.");
        var b = await PublishBuild(newModel, revised: true);
        var oldNom = await bridge.CaptureAsync(store, a.SnapshotId, token);
        Check(a.SnapshotId != b.SnapshotId && a.Build.Artifact.BuildId != b.Build.Artifact.BuildId &&
            a.Build.Key.AssetId == b.Build.Key.AssetId && a.Build.Key.Revision != b.Build.Key.Revision &&
            oldNom.AssetKeys.Contains(a.Build.Key) && !oldNom.AssetKeys.Contains(b.Build.Key),
            "The same texture ID must have two revisions in distinct retained Nom snapshots and builds.");
        Check(oldModel.Bindings.SequenceEqual(newModel.Bindings) &&
            oldModel.Meshes.Single().Geometry.Revision == newModel.Meshes.Single().Geometry.Revision,
            "PNG-only reimport changed ledger associations or mesh geometry.");

        return ([a.Build, b.Build], new
        {
            Freshness = new[] { original.Status.ToString(), stale.Status.ToString(), refreshed.Status.ToString() },
            ChangedDependency = "textures/color.png", OnlyPngChanged = true, StableBindings = true, OldNomSnapshotRetained = true,
            OldInputRevision = oldSource.InputRevision, NewInputRevision = newSource.InputRevision,
            OldPngHash = oldFiles["textures/color.png"], NewPngHash = newSource.GetFile("textures/color.png").Sha256,
            TextureAssetId = a.Build.Key.AssetId, OldTextureRevision = a.Build.Key.Revision.ToString(), NewTextureRevision = b.Build.Key.Revision.ToString(),
            MeshRevision = oldModel.Meshes.Single().Geometry.Revision.ToString(),
            OldNomSnapshot = a.SnapshotId, NewNomSnapshot = b.SnapshotId
        });

        async Task<CapturedModelSource> Capture()
        {
            await using var capture = captures.Create(new(sourceId, sourceDirectory, "scene.gltf"))!;
            var result = await capture.CaptureAsync(token);
            Check(capture.OpenHandleCount == 0, "Source capture retained handles.");
            return result;
        }

        async Task<ModelImportFreshness> Freshness(PublishedModelImport model)
        {
            await using var capture = captures.Create(new(sourceId, sourceDirectory, "scene.gltf"))!;
            return await capture.CheckFreshnessAsync(model, cancellationToken: token);
        }

        async Task<PublishedModelImport> Import(CapturedModelSource source, long generation, PublishedModelImport? expectedPrevious)
        {
            await using var operation = imports.Create(new(source, worker, Path.Combine(reportDirectory, "worker-scratch")))!;
            var candidate = await operation.ImportAsync(token);
            Check(operation.WorkerReaped, "The isolated Assimp worker was not reaped.");
            await using var ledger = ledgers.Create(ledgerArguments)!;
            var previous = await ledger.OpenAsync(token);
            Check(previous?.Generation == expectedPrevious?.Generation &&
                previous?.Texture?.AssetId == expectedPrevious?.Texture?.AssetId &&
                previous?.Texture?.Definition.Revision == expectedPrevious?.Texture?.Definition.Revision,
                "Reopening the import ledger did not preserve the previous texture.");
            var result = await ledger.PublishAsync(candidate, generation, cancellationToken: token);
            Check(result.Meshes.Single().Geometry.Uv0.Length == 4 && result.Instances.Single().World == Matrix4x4.Identity,
                "Imported mesh UV0 or fixture transform is missing.");
            return result;
        }

        async Task<(TextureBuild Build, string SnapshotId)> PublishBuild(PublishedModelImport model, bool revised)
        {
            var texture = model.Texture ?? throw new InvalidDataException("No texture in the published model.");
            var snapshot = await bridge.PublishAsync(store, model, store.ContentGeneration, token);
            var renderContent = await bridge.CaptureAsync(store, snapshot.Id, token);
            var expected = new BaseColorTextureDefinition(2, 2, TexturePixels(revised)).CreateKey(projectId, texture.AssetId);
            Check(renderContent.AssetKeys.Contains(expected), "Nom stored a different texture revision than the expected pixels.");
            var build = await BuildTexture(gameProject, compiler, renderContent, expected, texture.MaterialId, TexturePixels(revised), token);
            return (build, snapshot.Id);
        }
    }

    private static void WriteTexturedModel(string directory)
    {
        Directory.CreateDirectory(Path.Combine(directory, "textures"));
        File.WriteAllText(Path.Combine(directory, "scene.gltf"), """
            {
              "asset":{"version":"2.0"}, "scene":0, "scenes":[{"nodes":[0]}],
              "nodes":[{"name":"Plane","mesh":0}],
              "meshes":[{"name":"Quad","primitives":[{"attributes":{"POSITION":0,"TEXCOORD_0":1},"indices":2,"material":0}]}],
              "materials":[{"name":"BaseColor","pbrMetallicRoughness":{"baseColorTexture":{"index":0}}}],
              "images":[{"uri":"textures/color.png"}], "textures":[{"source":0,"sampler":0}],
              "samplers":[{"magFilter":9728,"minFilter":9728,"wrapS":33071,"wrapT":33071}],
              "buffers":[{"uri":"quad.bin","byteLength":92}],
              "bufferViews":[{"buffer":0,"byteLength":48,"target":34962},
                {"buffer":0,"byteOffset":48,"byteLength":32,"target":34962},
                {"buffer":0,"byteOffset":80,"byteLength":12,"target":34963}],
              "accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3","min":[-1.2,-1.2,0],"max":[1.2,1.2,0]},
                {"bufferView":1,"componentType":5126,"count":4,"type":"VEC2"},
                {"bufferView":2,"componentType":5123,"count":6,"type":"SCALAR"}]
            }
            """);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "quad.bin")));
        foreach (var value in new[] { -1.2f, 1.2f, 0, 1.2f, 1.2f, 0, 1.2f, -1.2f, 0, -1.2f, -1.2f, 0,
            -.25f, -.25f, 1.25f, -.25f, 1.25f, 1.25f, -.25f, 1.25f }) writer.Write(value);
        foreach (var index in new ushort[] { 0, 1, 2, 0, 2, 3 }) writer.Write(index);
    }

    private static void WritePng(string path, byte[] pixels)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Opaque));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(path);
        encoded.SaveTo(output);
    }
}
