using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

if (args.Length != 5) return 2;
try
{
    await using var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); })
        .Setup(context => context.UseAetherRenderResources()).Build();
    await app.StartAsync();
    using var game = GameBuildLoader.Load(new(args[1], args[2]), args[3]);
    var manifest = game.Artifact.RenderContent ?? throw new InvalidDataException("Build did not deliver render resources.");
    var host = app.Services.Resolve<RenderResourceHost>()!;
    var catalog = app.Services.Resolve<IFactory<RenderManifestLease, RenderContentManifest>>()!;
    object[] meshes;
    using (var lease = catalog.Create(manifest)!)
    {
        if (lease.Manifest.BuildId != RenderContentHash.Parse(args[2]) || host.Content.Counts.ScopeCount != 1)
            throw new InvalidOperationException("Wrong build scope in render catalog.");
        meshes = lease.Manifest.Entries.Where(entry => entry.Definition is MaterialBindingSetDefinition).Select(entry =>
        {
            var binding = (MaterialBindingSetDefinition)entry.Definition;
            var mesh = (MeshDefinition)lease.Manifest.Resolve(binding.Mesh).Definition;
            var material = lease.Manifest.Resolve(binding.Bindings.Single().Material);
            return (object)new
            {
                Id = binding.Mesh.AssetId, Slot = mesh.Slots.Single().SlotId, Binding = entry.Key.AssetId,
                Material = material.Key.AssetId, Revision = binding.Mesh.Revision.ToString(),
                MaximumX = mesh.Bounds.Maximum.X, VertexX = mesh.Vertices[1].X,
                Indices = mesh.Indices.ToArray()
            };
        }).ToArray();
    }
    var counts = host.Content.Counts;
    if (counts.ScopeCount != 0 || counts.RevisionCount != 0)
        throw new InvalidOperationException("Runtime render resources were retained after lease disposal.");
    var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(value => value.GetName().Name).ToArray();
    if (assemblies.Any(name => name is "TrueMoon.Nom" or "TrueMoon.Aether.Nom" or "TrueMoon.Aether.Tooling" or "AssimpNetter"))
        throw new InvalidOperationException("Runtime loaded an authoring dependency.");
    File.WriteAllText(args[4], JsonSerializer.Serialize(new
    {
        game.Artifact.BuildId, manifest.ProjectId, Meshes = meshes, CatalogAfterRelease = counts, LoadedAssemblies = assemblies
    }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}
