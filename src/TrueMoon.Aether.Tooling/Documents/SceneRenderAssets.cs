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
    internal readonly record struct References(Guid? Mesh, Guid Binding, MaterialBindingSetDefinition? Definition, string? Diagnostic);

    private readonly GameBuildRenderContent _content;
    private readonly Dictionary<Guid, RenderAssetEntry> _entries;
    private readonly Dictionary<Guid, Guid> _defaults;
    private readonly Dictionary<Guid, string> _slotNames;

    internal SceneRenderAssets(GameBuildRenderContent content, IReadOnlyList<SceneModelImportSelection> imports)
    {
        _content = content;
        _entries = content.Entries.ToDictionary(entry => entry.Key.AssetId);
        _defaults = imports.SelectMany(model => model.Watch.Published.Meshes)
            .ToDictionary(mesh => mesh.AssetId, mesh => mesh.BindingSetId);
        _slotNames = imports.SelectMany(model => model.Watch.Published.Bindings)
            .Where(binding => binding.Kind == Importing.ModelSubassetKind.Slot)
            .ToDictionary(binding => binding.Id, binding => Uri.UnescapeDataString(binding.Key.Locator[(binding.Key.Locator.LastIndexOf('/') + 1)..]));
        Meshes = Choices(Importing.ModelSubassetKind.Mesh);
        Materials = Choices(Importing.ModelSubassetKind.Material);

        IReadOnlyList<Choice> Choices(Importing.ModelSubassetKind kind) => Array.AsReadOnly(imports
            .SelectMany(model => model.Watch.Published.Bindings.Where(binding => binding.Kind == kind)
                .Select(binding => new Choice(binding.Id, $"{model.Name} · {binding.Key.Locator}")))
            .OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray());
    }

    internal IReadOnlyList<Choice> Meshes { get; }
    internal IReadOnlyList<Choice> Materials { get; }
    internal Guid? DefaultBinding(Guid mesh) => _defaults.TryGetValue(mesh, out var binding) ? binding : null;
    internal MaterialBindingSetDefinition? DefaultDefinition(Guid mesh) =>
        DefaultBinding(mesh) is { } binding ? (MaterialBindingSetDefinition)_entries[binding].Definition : null;
    internal string SlotName(Guid slot) => _slotNames[slot];

    internal Guid? BindingForMaterial(Guid mesh, Guid slot, Guid material)
    {
        if (DefaultDefinition(mesh) is not { } original ||
            _entries.GetValueOrDefault(material)?.Definition is not OpaqueUnlitMaterialDefinition) return null;
        var imported = original.Bindings.FirstOrDefault(value => value.SlotId == slot);
        if (imported.SlotId == Guid.Empty) return null;
        if (imported.Material.AssetId == material) return _defaults[mesh];
        var id = MaterialBindingId(mesh, original.Bindings.Length == 1 ? null : slot, material);
        return _entries.ContainsKey(id) || id == Guid.Empty ? null : id;
    }

    internal string? BindingWithSlotMaterial(Guid mesh, string current, Guid slot, Guid material)
    {
        if (DefaultDefinition(mesh) is not { } original ||
            ResolveReferences(this, mesh.ToString("D"), current).Definition is not { } previous ||
            _entries.GetValueOrDefault(material)?.Definition is not OpaqueUnlitMaterialDefinition) return null;

        var overrides = previous.Bindings.Select(item =>
                (item.SlotId, MaterialId: item.SlotId == slot ? material : item.Material.AssetId))
            .Where((item, index) => item.MaterialId != original.Bindings[index].Material.AssetId).ToArray();
        if (overrides.Length == 0) return _defaults[mesh].ToString("D");
        if (overrides.Length == 1)
            return BindingForMaterial(mesh, overrides[0].SlotId, overrides[0].MaterialId)?.ToString("D");
        var value = SceneMaterialBindingReference.Format(overrides);
        return SceneMaterialBindingReference.TryParse(mesh, value, out var binding, out _) && !_entries.ContainsKey(binding) ? value : null;
    }

    internal static References ResolveReferences(SceneRenderAssets? assets, string meshReference, string bindingReference,
        bool allowMissingMesh = false, IReadOnlyDictionary<Guid, RenderAssetEntry>? generated = null)
    {
        if (!Guid.TryParseExact(meshReference, "D", out var mesh) || mesh == Guid.Empty)
            return new(null, default, null, "Ссылка на меш должна содержать непустой ID.");
        var validBinding = SceneMaterialBindingReference.TryParse(mesh, bindingReference, out var binding, out var overrides);
        if (assets == null && mesh == RenderContentProfile.CubeAssetId)
        {
            if (!validBinding || overrides.Length != 0) return new(null, binding, null, InvalidBinding(mesh));
            return binding == RenderContentProfile.CubeBindingSetAssetId ? default : new(null, binding, null, MissingBinding(mesh, binding));
        }
        var geometry = assets?._entries.GetValueOrDefault(mesh);
        if (geometry?.Definition is not MeshDefinition)
        {
            if (allowMissingMesh && validBinding) return default;
            return new(null, binding, null, geometry == null
                ? $"Меш {mesh:D} отсутствует. Выберите его импорт в каталоге сцены."
                : $"По ссылке {mesh:D} ожидается меш.");
        }
        if (!validBinding)
            return new(mesh, binding, null, InvalidBinding(mesh));

        if (overrides.Length != 0)
        {
            var combined = assets!.ResolveCombinedBinding(mesh, overrides);
            if (combined == null || assets._entries.ContainsKey(binding) ||
                generated?.GetValueOrDefault(binding) is { } existing && existing.Definition.Revision != combined.Revision)
                return new(mesh, binding, null, MissingBinding(mesh, binding));
            return new(mesh, binding, combined, null);
        }
        var entry = assets!._entries.GetValueOrDefault(binding) ?? generated?.GetValueOrDefault(binding);
        if (entry != null)
        {
            if (entry.Definition is not MaterialBindingSetDefinition set)
                return new(mesh, binding, null, $"По ссылке {binding:D} ожидается привязка материала для меша {mesh:D}.");
            if (set.Mesh != geometry.Key)
                return new(mesh, binding, null, $"Привязка материала {binding:D} относится к мешу {set.Mesh.AssetId:D}. Требуется привязка для меша {mesh:D}.");
            return new(mesh, binding, set, null);
        }
        if (assets.FindOverrideBinding(mesh, binding) is { } variant)
            return new(mesh, binding, variant, null);
        return new(mesh, binding, null, MissingBinding(mesh, binding));
    }

    private static string InvalidBinding(Guid mesh) => $"Ссылка на привязку материала меша {mesh:D} должна содержать ID или список переопределений слотов.";

    private static string MissingBinding(Guid mesh, Guid binding) =>
        $"Привязка материала {binding:D} для меша {mesh:D} отсутствует. Выберите импорт материала или назначьте материал заново.";

    private MaterialBindingSetDefinition? ResolveCombinedBinding(Guid mesh, (Guid SlotId, Guid MaterialId)[] overrides)
    {
        if (DefaultDefinition(mesh) is not { } original) return null;
        var bindings = original.Bindings.ToBuilder();
        foreach (var (slot, material) in overrides)
        {
            var index = original.Bindings.IndexOf(original.Bindings.FirstOrDefault(item => item.SlotId == slot));
            if (index < 0 || _entries.GetValueOrDefault(material) is not { Definition: OpaqueUnlitMaterialDefinition } entry) return null;
            bindings[index] = new(slot, entry.Key);
        }
        return new(original.Mesh, bindings);
    }

    private MaterialBindingSetDefinition? FindOverrideBinding(Guid mesh, Guid binding)
    {
        if (DefaultDefinition(mesh) is not { } original) return null;
        MaterialBindingSetDefinition? result = null;
        for (var i = 0; i < original.Bindings.Length; i++)
        {
            var slot = original.Bindings[i];
            Guid? identitySlot = original.Bindings.Length == 1 ? null : slot.SlotId;
            foreach (var choice in Materials)
            {
                if (choice.Id == slot.Material.AssetId || MaterialBindingId(mesh, identitySlot, choice.Id) != binding) continue;
                if (result != null) return null;
                var slots = original.Bindings.SetItem(i, slot with { Material = _entries[choice.Id].Key });
                result = new(original.Mesh, slots);
            }
        }
        return result;
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
                if (assets == null || references.Definition is not { } variant) continue;
                var binding = references.Binding;
                if (assets._entries.ContainsKey(binding) || added.ContainsKey(binding)) continue;

                added.Add(binding, new(variant.CreateKey(variant.Mesh.ProjectId, binding), variant));
                if (assets._entries.Count + added.Count > RenderContentProfile.MaximumManifestEntries)
                    throw new InvalidOperationException("Превышен лимит ресурсов в сборке сцены.");
            }
        }
        if (added.Count == 0) return assets?._content;
        var content = assets!._content;
        return new(content.Entries[0].Key.ProjectId, [.. content.Entries, .. added.Values]);
    }

    private static Guid MaterialBindingId(Guid mesh, Guid? slot, Guid material)
    {
        ReadOnlySpan<byte> domain = slot.HasValue ? "Aether.Scene.MaterialBinding/2"u8 : "Aether.Scene.MaterialBinding/1"u8;
        Span<byte> input = stackalloc byte[domain.Length + (slot.HasValue ? 48 : 32)];
        domain.CopyTo(input);
        mesh.TryWriteBytes(input[domain.Length..]);
        material.TryWriteBytes(input[(domain.Length + 16)..]);
        if (slot is { } id) id.TryWriteBytes(input[(domain.Length + 32)..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new(hash[..16]);
    }
}
