using System.Numerics;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;

namespace RenderingCpuConsumer;

// Compiled unchanged by Verify.ps1 in independent source/package consumer projects.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var report = new Dictionary<string, object?> { ["Completed"] = false, ["GpuRun"] = false, ["WindowRun"] = false };
        try
        {
            var project = Guid.Parse("248a7b09-7334-43fb-b303-3c731acb0401");
            var build = RenderContentHash.Compute("render-cpu-consumer/v1"u8);
            var defaults = BuiltinRenderContent.Create(project, build, Guid.NewGuid());
            var meshEntry = defaults.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
            var mesh = (MeshDefinition)meshEntry.Definition;
            Require(mesh.Vertices.Length == 8 && mesh.Indices.Length == 36 && mesh.Slots.Length == 1, "Builtin cube geometry.");
            Require(RenderContentCodec.Encode(mesh).Length == 324, "Canonical cube byte length.");
            var material = new OpaqueUnlitMaterialDefinition(new(.5f));
            const string golden = "544d524101000200010000006fc9fce0954742f5b4dff05f306f1301010000000000003f0000003f0000003f";
            Require(Convert.ToHexString(RenderContentCodec.Encode(material)).Equals(golden, StringComparison.OrdinalIgnoreCase), "Independent canonical material bytes.");
            Require(material.Revision.ToString() == "71388556d5f405739b9b192d9147d35104e216980868d9c29c14d0f4a0f87cb7", "Independent material SHA256.");
            Require(RenderContentCodec.Decode(Convert.FromHexString(golden)).Revision == material.Revision, "Strict public decode.");
            var materialKey = material.CreateKey(project, RenderContentProfile.WhiteMaterialAssetId);
            var bindings = new MaterialBindingSetDefinition(meshEntry.Key, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
            var bindingKey = bindings.CreateKey(project, RenderContentProfile.CubeBindingSetAssetId);
            var entries = new RenderAssetEntry[] { meshEntry, new(materialKey, material), new(bindingKey, bindings) };
            var content = new RenderContentManifest(project, build, Guid.NewGuid(), entries);
            var reordered = new RenderContentManifest(project, build, Guid.NewGuid(), entries.Reverse().ToArray());
            Require(content.Digest == reordered.Digest && content.Generation != reordered.Generation, "Content order/generation independence.");
            Require(RenderContentCodec.EncodeManifest(content).Length == 312, "Canonical manifest byte length.");

            var app = App.Builder(_ => { }).Setup(context =>
            {
                context.UseAetherEcs();
                context.UseAetherSceneLoading(_ => MeshRenderingComponents.Register(PrimitiveEcsComponents.Register(new()), content).Build());
            }).Build();
            try
            {
                await app.StartAsync();
                var factory = app.Services.Resolve<IFactory<SceneWorld, SceneWorldLoadRequest>>() ?? throw new InvalidOperationException("Missing scene factory.");
                using var scene = factory.Create(new(new SceneWorldData(Document())));
                Require(scene.World.EntityCount == 3, "Complete scene loading through App factory.");
                var first = scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity(1)]);
                var second = scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity(2)]);
                Require(first.Mesh == second.Mesh && first.Bindings == second.Bindings, "Exact shared mesh/material bindings.");
                var firstColor = RenderColor.Encode(material.EffectiveLinearColor(first.TintLinear, first.TintEnabled));
                var secondColor = RenderColor.Encode(material.EffectiveLinearColor(second.TintLinear, second.TintEnabled));
                Near(new(.360780214f), firstColor); Near(new(.5f), secondColor);
                var reset = first with { TintEnabled = false };
                Near(new(.5f), RenderColor.Encode(material.EffectiveLinearColor(reset.TintLinear, reset.TintEnabled)));
                Require(second.TintEnabled == false && material.BaseColorSrgb == new Vector3(.5f), "Instance tint/reset leaves shared data intact.");
                var camera = scene.World.GetComponent<PerspectiveCamera>(scene.Entities[Entity(3)]);
                var projection = camera.CreateViewProjection(Matrix4x4.CreateTranslation(0, 0, 6), 320, 180);
                Require(float.IsFinite(projection.M43) && projection.M22 < 0, "Explicit CPU perspective.");
                var shear = new Matrix4x4(2, 0, 0, 0, 1, 3, 0, 0, 0, 0, 1, 0, 4, -2, 1, 1);
                var bounds = mesh.Bounds.Transform(shear);
                Near(new(2.5f, -3.5f, .5f), bounds.Minimum); Near(new(5.5f, -.5f, 1.5f), bounds.Maximum);
                report["Entities"] = scene.World.EntityCount;
                report["SourceComponents"] = scene.Scene.Entities.Values.Sum(entity => entity.Components.Count);
                report["FirstOutputSrgb"] = new[] { firstColor.X, firstColor.Y, firstColor.Z };
                report["SecondOutputSrgb"] = new[] { secondColor.X, secondColor.Y, secondColor.Z };
            }
            finally { await app.DisposeAsync(); }
            report["Assets"] = content.Entries.Length; report["ManifestDigest"] = content.Digest.ToString();
            report["MaterialRevision"] = material.Revision.ToString(); report["Completed"] = true;
            return 0;
        }
        catch (Exception error) { report["Error"] = error.ToString(); return 1; }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
    }
    private static SceneDocument Document()
    {
        var definitions = new List<EntityDefinition>(); var entities = new List<EntityInstance>();
        for (var i = 1; i <= 3; i++)
        {
            var definition = new EntityDefinitionId(Id(100 + i));
            var components = new List<ComponentData> { new(new(Id(200 + i)), BuiltInComponents.TransformId, 1) };
            if (i == 3) components.Add(new(new(Id(300 + i)), MeshRenderingComponents.CameraId, 1));
            else components.Add(new(new(Id(300 + i)), MeshRenderingComponents.MeshRendererId, 1,
                i == 1 ? [new("tintEnabled", PropertyValue.From(true)), new("tintR", PropertyValue.From(.5)),
                    new("tintG", PropertyValue.From(.5)), new("tintB", PropertyValue.From(.5))] : null));
            definitions.Add(new(definition, new(components))); entities.Add(new(Entity(i), definition));
        }
        return new(new(Id(50)), 9, entities, definitions, BuiltInComponents.Schemas.Concat(MeshRenderingComponents.Schemas));
    }
    private static Guid Id(int value) => Guid.Parse($"248a7b09-7334-43fb-b303-{value:000000000000}");
    private static EntityId Entity(int value) => new(Id(value));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(Vector3 expected, Vector3 actual) => Require(Vector3.Distance(expected, actual) < .00001f, $"Literal vector mismatch: {expected} / {actual}.");
}
