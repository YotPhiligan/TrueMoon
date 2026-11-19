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
    internal readonly record struct References(Guid? Mesh, Guid Binding, Guid? Material, string? Diagnostic);

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
    internal Guid? DefaultBinding(Guid mesh) => _defaults.GetValueOrDefault(mesh)?.Binding;

    internal Guid? BindingForMaterial(Guid mesh, Guid material)
    {
        if (!_defaults.TryGetValue(mesh, out var original) ||
            _entries.GetValueOrDefault(material)?.Definition is not OpaqueUnlitMaterialDefinition) return null;
        if (original.Material == material) return original.Binding;
        var id = MaterialBindingId(mesh, material);
        return _entries.ContainsKey(id) || id == Guid.Empty ? null : id;
    }

    internal static References ResolveReferences(SceneRenderAssets? assets, string meshReference, string bindingReference,
        bool allowMissingMesh = false, IReadOnlyDictionary<Guid, RenderAssetEntry>? generated = null)
    {
        if (!Guid.TryParseExact(meshReference, "D", out var mesh) || mesh == Guid.Empty)
            return new(null, default, null, "Ссылка на меш должна содержать непустой ID.");
        var validBinding = Guid.TryParseExact(bindingReference, "D", out var binding) && binding != Guid.Empty;
        if (assets == null && mesh == RenderContentProfile.CubeAssetId)
        {
            if (!validBinding) return new(null, binding, null, InvalidBinding(mesh));
            return binding == RenderContentProfile.CubeBindingSetAssetId ? default : new(null, binding, null, MissingBinding(mesh, binding));
        }
        var geometry = assets?._entries.GetValueOrDefault(mesh);
        if (geometry?.Definition is not MeshDefinition definition)
        {
            if (allowMissingMesh && validBinding) return default;
            return new(null, binding, null, geometry == null
                ? $"Меш {mesh:D} отсутствует. Выберите его импорт в каталоге сцены."
                : $"По ссылке {mesh:D} ожидается меш.");
        }
        if (!validBinding)
            return new(mesh, binding, null, InvalidBinding(mesh));

        var entry = assets!._entries.GetValueOrDefault(binding) ?? generated?.GetValueOrDefault(binding);
        if (entry != null)
        {
            if (entry.Definition is not MaterialBindingSetDefinition set)
                return new(mesh, binding, null, $"По ссылке {binding:D} ожидается привязка материала для меша {mesh:D}.");
            if (set.Mesh != geometry.Key)
                return new(mesh, binding, null, $"Привязка материала {binding:D} относится к мешу {set.Mesh.AssetId:D}. Требуется привязка для меша {mesh:D}.");
            return new(mesh, binding, set.Bindings.Length == 1 ? set.Bindings[0].Material.AssetId : null, null);
        }
        if (definition.Slots.Length == 1 && assets!.FindOverrideMaterial(mesh, binding) is { } material)
            return new(mesh, binding, material, null);
        return new(mesh, binding, null, MissingBinding(mesh, binding));
    }

    private static string InvalidBinding(Guid mesh) => $"Ссылка на привязку материала меша {mesh:D} должна содержать непустой ID.";

    private static string MissingBinding(Guid mesh, Guid binding) =>
        $"Привязка материала {binding:D} для меша {mesh:D} отсутствует. Выберите импорт материала или назначьте материал заново.";

    private Guid? FindOverrideMaterial(Guid mesh, Guid binding)
    {
        Guid? material = null;
        foreach (var choice in Materials)
        {
            if (BindingForMaterial(mesh, choice.Id) != binding) continue;
            if (material.HasValue) return null;
            material = choice.Id;
        }
        return material;
    }

    internal static GameBuildRenderContent? PrepareBuild(SceneRenderAssets? assets, ResolvedSceneSnapshot scene, bool requireAll, CancellationToken token)
    {
        if (assets == null && !requireAll) return null;
        var added = new Dictionary<Guid, RenderAssetEntry>();
        foreach (var entity in scene.Entities.Values)
        {
            token.ThrowIfCancellationRequested();
            foreach (var renderer in entity.Components.Values.Where(component => component.TypeId == MeshRenderingComponents.MeshRendererId))
            {
                var references = ResolveReferences(assets, renderer.Properties["mesh"].Value.GetString(),
                    renderer.Properties["bindings"].Value.GetString(), allowMissingMesh: !requireAll, generated: added);
                if (references.Diagnostic is { } error)
                    throw new InvalidOperationException($"Сущность {entity.Id.Value:D}. {error}");
                if (assets == null || references.Mesh is not { } mesh || references.Material is not { } material) continue;
                var binding = references.Binding;
                if (assets._entries.ContainsKey(binding) || added.ContainsKey(binding)) continue;

                var geometry = assets._entries[mesh];
                var definition = (MeshDefinition)geometry.Definition;
                var variant = new MaterialBindingSetDefinition(geometry.Key, [new(definition.Slots[0].SlotId, assets._entries[material].Key)]);
                added.Add(binding, new(variant.CreateKey(geometry.Key.ProjectId, binding), variant));
                if (assets._entries.Count + added.Count > RenderContentProfile.MaximumManifestEntries)
                    throw new InvalidOperationException("Превышен лимит ресурсов в сборке сцены.");
            }
        }
        if (added.Count == 0) return assets?._content;
        var content = assets!._content;
        return new(content.Entries[0].Key.ProjectId, [.. content.Entries, .. added.Values]);
    }

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
