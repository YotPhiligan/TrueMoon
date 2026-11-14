using TrueMoon.Aether.Runtime;
using System.Text.Json;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Tooling.Builds;

namespace TrueMoon.ManualTests.RuntimeArtifactTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--scene")
        {
            var schema = new ComponentSchema(new(new Guid("47a88cb9-70af-4f50-90df-9296f2ee26aa")), 1,
                [new("value", PropertyValueKind.Integer, PropertyValue.From(0L))]);
            var component = new ComponentData(new(Guid.NewGuid()), schema.TypeId, schema.Version);
            var definition = new EntityDefinition(new(Guid.NewGuid()), new EntityPatch(addedComponents: [component]));
            var document = new SceneDocument(new(Guid.NewGuid()), 41, [new(new(Guid.NewGuid()), definition.Id)], [definition], [schema]);
            var resolution = new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(scene => new(scene.Definitions, scene.Schemas)));
            File.WriteAllBytes(args[1], new SceneDocumentCodec(resolution).Encode(document));
            return 0;
        }
        if (args.Length is < 5 or > 6 || args.Length == 6 && args[5] != "runtime")
            throw new ArgumentException("RuntimeArtifactTool <output-directory> <consumer.dll> <artifact-root> <entry.dll> <entry-type> [runtime]");
        var build = GameArtifactPublisher.Publish(args[0], args[2], BuildIdentity.ForPlayer(Path.GetFullPath(args[1])), args[3], args[4], 41,
            runtimeAccess: args.Length == 6);
        Console.WriteLine(JsonSerializer.Serialize(new { build.BuildId, build.DirectoryPath, build.HostContractId }));
        return 0;
    }
}
