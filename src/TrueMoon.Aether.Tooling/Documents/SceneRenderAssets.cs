using System.Security.Cryptography;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;

namespace TrueMoon.Aether.Tooling.Documents;

// The selection owns the exact CPU resources. A scene-specific binding is published only into a Build.
internal sealed class SceneRenderAssets
{
    internal sealed record Choice(Guid Id, string Name);

    private readonly GameBuildRenderContent _content;
    private readonly Dictionary<Guid, RenderAssetEntry> _entries;
    private readonly Dictionary<Guid, PublishedMeshBinding> _defaults;
    private sealed record PublishedMeshBinding(Guid Binding, Guid Material);

    internal SceneRenderAssets(GameBuildRenderContent content, IReadOnlyList<SceneModelImportSelection> imports)
    {
        _content = content;
        _entries = content.Entries.ToDictionary(entry => entry.Key.AssetId);
        _defaults = imports.SelectMany(model => model.Watch.Published.Meshes)
            .ToDictionary(mesh => mesh.AssetId, mesh => new PublishedMeshBinding(mesh.BindingSetId, mesh.MaterialId));
        Meshes = Choices(Importing.ModelSubassetKind.Mesh);
        Materials = Choices(Importing.ModelSubassetKind.Material);

        IReadOnlyList<Choice> Choices(Importing.ModelSubassetKind kind) => Array.AsReadOnly(imports
            .SelectMany(model => model.Watch.Published.Bindings.Where(binding => binding.Kind == kind)
                .Select(binding => new Choice(binding.Id, $"{model.Name} · {binding.Key.Locator}")))
            .OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray());
    }

    internal IReadOnlyList<Choice> Meshes { get; }
    internal IReadOnlyList<Choice> Materials { get; }
    internal bool ContainsMesh(Guid mesh) => _defaults.ContainsKey(mesh);
    internal Guid? DefaultBinding(Guid mesh) => _defaults.GetValueOrDefault(mesh)?.Binding;

    internal Guid? BindingForMaterial(Guid mesh, Guid material)
    {
        if (!_defaults.TryGetValue(mesh, out var original) ||
            _entries.GetValueOrDefault(material)?.Definition is not OpaqueUnlitMaterialDefinition) return null;
        if (original.Material == material) return original.Binding;
        var id = MaterialBindingId(mesh, material);
        return _entries.ContainsKey(id) || id == Guid.Empty ? null : id;
    }

    internal Guid? MaterialForBinding(Guid mesh, Guid binding)
    {
        if (!_defaults.ContainsKey(mesh)) return null;
        if (_entries.TryGetValue(binding, out var entry))
            return entry.Definition is MaterialBindingSetDefinition set && set.Mesh == _entries[mesh].Key &&
                set.Bindings.Length == 1 ? set.Bindings[0].Material.AssetId : null;
        Guid? material = null;
        foreach (var choice in Materials)
        {
            if (BindingForMaterial(mesh, choice.Id) != binding) continue;
            if (material.HasValue) return null;
            material = choice.Id;
        }
        return material;
    }

    internal GameBuildRenderContent PrepareBuild(ResolvedSceneSnapshot scene, bool requireAll, CancellationToken token)
    {
        var added = new Dictionary<Guid, RenderAssetEntry>();
        foreach (var entity in scene.Entities.Values)
        {
            token.ThrowIfCancellationRequested();
            foreach (var renderer in entity.Components.Values.Where(component => component.TypeId == MeshRenderingComponents.MeshRendererId))
            {
                var mesh = Guid.ParseExact(renderer.Properties["mesh"].Value.GetString(), "D");
                var binding = Guid.ParseExact(renderer.Properties["bindings"].Value.GetString(), "D");
                if (!_entries.TryGetValue(mesh, out var geometry) || geometry.Definition is not MeshDefinition definition)
                {
                    if (!requireAll) continue;
                    throw MissingImport();
                }
                var entry = _entries.GetValueOrDefault(binding) ?? added.GetValueOrDefault(binding);
                if (entry != null)
                {
                    if (entry.Definition is not MaterialBindingSetDefinition set || set.Mesh != geometry.Key)
                        throw new InvalidOperationException("Привязка материала относится к другому мешу.");
                    continue;
                }
                if (definition.Slots.Length != 1 || MaterialForBinding(mesh, binding) is not { } material)
                    throw MissingImport();
                var variant = new MaterialBindingSetDefinition(geometry.Key, [new(definition.Slots[0].SlotId, _entries[material].Key)]);
                added.Add(binding, new(variant.CreateKey(geometry.Key.ProjectId, binding), variant));
                if (_entries.Count + added.Count > RenderContentProfile.MaximumManifestEntries)
                    throw new InvalidOperationException("Превышен лимит ресурсов в сборке сцены.");
            }
        }
        return added.Count == 0 ? _content : new(_content.Entries[0].Key.ProjectId, [.. _content.Entries, .. added.Values]);
    }

    private static InvalidOperationException MissingImport() =>
        new("Для размещённой модели выберите импорт в каталоге моделей проекта.");

    private static Guid MaterialBindingId(Guid mesh, Guid material)
    {
        ReadOnlySpan<byte> domain = "Aether.Scene.MaterialBinding/1"u8;
        Span<byte> input = stackalloc byte[domain.Length + 32];
        domain.CopyTo(input);
        mesh.TryWriteBytes(input[domain.Length..]);
        material.TryWriteBytes(input[(domain.Length + 16)..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new(hash[..16]);
    }
}
