using System.Security.Cryptography;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Tooling.Documents;

// An immutable logical resolver over selected imports and project catalog headers. Geometry and pixels materialize only for a frozen closure.
internal sealed partial class SceneRenderAssets
{
    internal sealed record Choice(Guid Id, string Name);
    internal readonly record struct References(Guid? Mesh, Guid Binding, MaterialBindingSetDefinition? Definition, string? Diagnostic);
    internal sealed record ResourceCollision(Guid Id)
    {
        internal string Message => $"ID ресурса {Id:D} используется несколькими ресурсами проекта.";
    }

    private readonly Guid _projectId;
    private readonly GameBuildRenderContent? _content;
    private readonly bool _hasFullContent, _rebuildBase;
    private readonly IReadOnlyDictionary<Guid, RenderAssetEntry> _entries;
    private readonly Dictionary<Guid, Guid> _defaults;
    private readonly Dictionary<Guid, string> _slotNames;
    private readonly HashSet<Guid>? _importIds;
    private readonly HashSet<Guid>? _fullSlotIds;
    private readonly Dictionary<Guid, RenderAssetKey> _textureKeys;
    private readonly Dictionary<Guid, ProjectTexture>? _projectTextures;
    private readonly ProjectTextureCatalog? _textureCatalog;
    private readonly AuthoredMaterials? _authored;
    private readonly AuthoredMeshes? _meshes;
    private readonly Lock _variantGate = new();
    private Dictionary<(Guid Mesh, string Binding), References>? _variants;

    internal SceneRenderAssets(GameBuildRenderContent content, IReadOnlyList<SceneModelImportSelection> imports)
    {
        _projectId = content.Entries[0].Key.ProjectId;
        _content = content;
        _entries = content.Entries.ToDictionary(entry => entry.Key.AssetId);
        var bindings = imports.SelectMany(model => model.Watch.Published.Bindings.Select(binding => (model.Name, Binding: binding))).ToArray();
        _importIds = [];
        _slotNames = [];
        foreach (var item in bindings)
        {
            var binding = item.Binding;
            if (!_importIds.Add(binding.Id)) Collision ??= new(binding.Id);
            if (binding.Kind == ModelSubassetKind.Slot)
            {
                var locator = binding.Key.Locator;
                _slotNames.TryAdd(binding.Id, Uri.UnescapeDataString(locator[(locator.LastIndexOf('/') + 1)..]));
            }
        }
        _defaults = [];
        foreach (var model in imports)
            foreach (var mesh in model.Watch.Published.Meshes)
                if (!_defaults.TryAdd(mesh.AssetId, mesh.BindingSetId)) Collision ??= new(mesh.AssetId);
        Meshes = SortChoices(PublishedChoices(ModelSubassetKind.Mesh));
        Materials = SortChoices(PublishedChoices(ModelSubassetKind.Material));
        _textureKeys = content.Entries.Where(entry => entry.Definition is BaseColorTextureDefinition)
            .ToDictionary(entry => entry.Key.AssetId, entry => entry.Key);
        var textureNames = new Dictionary<Guid, string>();
        foreach (var choice in PublishedChoices(ModelSubassetKind.Texture)) textureNames.TryAdd(choice.Id, choice.Name);
        Textures = SortChoices(_textureKeys.Keys.Select(id => new Choice(id, textureNames.GetValueOrDefault(id) ?? id.ToString("D"))));

        IEnumerable<Choice> PublishedChoices(ModelSubassetKind kind) => bindings.Where(item => item.Binding.Kind == kind)
            .Select(item => new Choice(item.Binding.Id, $"{item.Name} · {item.Binding.Key.Locator}"));
    }

    internal static SceneRenderAssets? Capture(Guid projectId, SceneRenderAssets? imports,
        ProjectMaterialCatalogSnapshot? materials, GameBuildRenderContent? fullContent = null,
        ProjectTextureCatalogSnapshot? textures = null, ProjectTextureCatalog? textureCatalog = null,
        ProjectMeshCatalogSnapshot? meshes = null, ProjectMeshCatalog? meshCatalog = null) =>
        materials is { Materials.Count: > 0 } || textures is { Textures.Count: > 0 } || meshes is { Meshes.Count: > 0 } || fullContent != null
            ? new(projectId, imports, materials, fullContent, textures, textureCatalog, meshes, meshCatalog)
            : imports;

    private SceneRenderAssets(Guid projectId, SceneRenderAssets? imports, ProjectMaterialCatalogSnapshot? materials,
        GameBuildRenderContent? fullContent, ProjectTextureCatalogSnapshot? textures, ProjectTextureCatalog? textureCatalog,
        ProjectMeshCatalogSnapshot? meshes, ProjectMeshCatalog? meshCatalog)
    {
        _projectId = projectId;
        Collision = imports?.Collision;
        _hasFullContent = fullContent != null;
        if (fullContent != null && fullContent.Entries[0].Key.ProjectId != projectId)
            throw new InvalidOperationException("Ресурсы сборки принадлежат другому проекту.");
        _content = imports?._content ?? fullContent;
        _entries = imports?._entries ?? (fullContent == null ? new Dictionary<Guid, RenderAssetEntry>() :
            fullContent.Entries.ToDictionary(entry => entry.Key.AssetId));
        _defaults = imports?._defaults ?? [];
        _importIds = imports?._importIds;
        _slotNames = imports?._slotNames ?? [];
        _textureKeys = imports?._textureKeys ?? [];
        Meshes = imports?.Meshes ?? [];
        Textures = imports?.Textures ?? [];

        if (imports != null && fullContent != null)
        {
            Dictionary<Guid, RenderAssetEntry>? merged = null;
            foreach (var entry in fullContent.Entries)
            {
                if (_entries.TryGetValue(entry.Key.AssetId, out var prior))
                {
                    if (prior.Key != entry.Key) Collision ??= new(entry.Key.AssetId);
                }
                else
                    (merged ??= new(_entries)).Add(entry.Key.AssetId, entry);
            }
            if (merged != null)
            {
                _entries = merged;
                _rebuildBase = true;
            }
        }
        if (fullContent != null)
        {
            _fullSlotIds = [];
            foreach (var entry in fullContent.Entries)
                if (entry.Definition is MeshDefinition mesh)
                    foreach (var slot in mesh.Slots)
                        _fullSlotIds.Add(slot.SlotId);
        }

        if (textures is { Textures.Count: > 0 })
        {
            if (textures.ProjectId != projectId || textureCatalog == null)
                throw new InvalidOperationException("Каталог текстур не принадлежит проекту или недоступен.");
            _textureCatalog = textureCatalog;
            _projectTextures = textures.Textures.ToDictionary(row => row.Id);
            _textureKeys = new(_textureKeys);
            foreach (var texture in textures.Textures)
            {
                if (IsCatalogIdReserved(texture.Id) || !_textureKeys.TryAdd(texture.Id, texture.Key(projectId)))
                    Collision ??= new(texture.Id);
            }
            if (Collision == null)
                Textures = SortChoices(Textures.Concat(textures.Textures.Select(texture => new Choice(texture.Id, texture.Name))));
        }

        Materials = imports?.Materials ?? [];
        if (materials is { Materials.Count: > 0 })
        {
            foreach (var material in materials.Materials)
                if (HasReservedId(material.Id)) Collision ??= new(material.Id);
            _authored = new(materials.Materials);
            if (Collision == null)
                Materials = SortChoices(Materials.Concat(materials.Materials.Select(material => new Choice(material.Id, material.Name))));
        }

        if (meshes is { Meshes.Count: > 0 })
        {
            if (meshes.ProjectId != projectId || meshCatalog == null)
                throw new InvalidOperationException("Каталог мешей не принадлежит проекту или недоступен.");
            var headers = new AuthoredMeshes(projectId, meshCatalog, meshes.Meshes);
            foreach (var id in headers.Owners.Keys)
                if (HasReservedId(id)) Collision ??= new(id);
            Collision ??= headers.Collision;
            _meshes = headers;
            if (Collision == null)
            {
                Meshes = SortChoices(Meshes.Concat(meshes.Meshes.Select(mesh => new Choice(mesh.Id, mesh.Name))));
                if (headers.Textures.Count != 0)
                {
                    if (_projectTextures == null) _textureKeys = new(_textureKeys);
                    foreach (var (id, key) in headers.Textures) _textureKeys.Add(id, key);
                    Textures = SortChoices(Textures.Concat(headers.Textures.Keys.Select(id => new Choice(id, MeshDefaultName(headers.Owners[id])))));
                }
            }
        }
    }

    private static IReadOnlyList<Choice> SortChoices(IEnumerable<Choice> choices) => Array.AsReadOnly(choices
        .OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray());

    internal IReadOnlyList<Choice> Meshes { get; }
    internal IReadOnlyList<Choice> Materials { get; }
    internal IReadOnlyList<Choice> Textures { get; }
    internal ResourceCollision? Collision { get; }

    internal ProjectMaterial? ReadMaterial(Guid id)
    {
        if (_authored?.Find(id) is { } material) return material;
        if (_meshes?.Materials.TryGetValue(id, out var frozen) == true)
            return new(id, MeshDefaultName(_meshes.Owners[id]), frozen.BaseColorSrgb, frozen.BaseColorTextureId);
        if (_entries.GetValueOrDefault(id)?.Definition is not OpaqueUnlitMaterialDefinition imported) return null;
        var choice = Materials.FirstOrDefault(item => item.Id == id);
        return new(id, choice?.Name ?? id.ToString("D"), imported.BaseColorSrgb, imported.BaseColorTexture?.AssetId);
    }

    internal Guid? DefaultBinding(Guid mesh) => _meshes?.Rows.GetValueOrDefault(mesh)?.DefaultBindingId ??
        (_defaults.TryGetValue(mesh, out var binding) ? binding : null);

    internal MaterialBindingSetDefinition? DefaultDefinition(Guid mesh) =>
        DefaultBinding(mesh) is { } binding ? ReadBindingEntry(binding)?.Definition as MaterialBindingSetDefinition : null;

    internal string SlotName(Guid slot) => _meshes?.SlotNames.GetValueOrDefault(slot) ?? _slotNames[slot];

    internal Choice? AuthoredDefaultMaterialChoice(Guid mesh, Guid slot)
    {
        if (_meshes?.Rows.GetValueOrDefault(mesh) is not { } row) return null;
        foreach (var item in row.Slots)
            if (item.Id == slot) return new(item.DefaultMaterialId, MeshDefaultName(row));
        return null;
    }

    private static string MeshDefaultName(ProjectMesh row) => $"Из меша · {row.Name}";

    private RenderAssetEntry? ReadBindingEntry(Guid binding) => _entries.GetValueOrDefault(binding) ?? _meshes?.Bindings.GetValueOrDefault(binding);

    private bool TryGeometry(Guid mesh, out RenderAssetKey key, out bool hasUv0)
    {
        if (_entries.GetValueOrDefault(mesh) is { Definition: MeshDefinition geometry } entry)
        {
            key = entry.Key;
            hasUv0 = !geometry.Uv0.IsEmpty;
            return true;
        }
        if (_meshes?.Rows.GetValueOrDefault(mesh) is { } row)
        {
            key = row.MeshKey(_projectId);
            hasUv0 = row.HasUv0;
            return true;
        }
        key = default;
        hasUv0 = false;
        return false;
    }

    internal string? MaterialDiagnostic(Guid mesh, Guid slot, Guid material) =>
        ResolveMaterialSource(mesh, slot, material, out _, out _);

    private string? ResolveMaterialSource(Guid mesh, Guid slot, Guid material, out ProjectMaterial? authored, out RenderAssetKey frozenKey)
    {
        authored = null;
        frozenKey = default;
        if (Collision is { } collision) return collision.Message;
        if (!TryGeometry(mesh, out _, out var hasUv0) || DefaultDefinition(mesh) is not { } original)
            return InvalidSlot(mesh, slot);
        var index = SlotIndex(original, slot);
        if (index < 0)
            return InvalidSlot(mesh, slot);
        if (_authored?.Find(material) is { } row)
        {
            authored = row;
            if (authored.BaseColorTextureId is not { } texture) return null;
            if (!_textureKeys.ContainsKey(texture))
                return $"Текстура {texture:D} материала «{authored.Name}» ({material:D}) отсутствует в проекте и выбранных импортах сцены.";
            if (!hasUv0)
                return $"Материал «{authored.Name}» ({material:D}) требует UV0 у меша {mesh:D}.";
            return null;
        }
        if (_meshes?.Materials.ContainsKey(material) == true && original.Bindings[index].Material.AssetId == material)
        {
            frozenKey = original.Bindings[index].Material;
            return null;
        }
        if (_entries.GetValueOrDefault(material) is not { Definition: OpaqueUnlitMaterialDefinition definition } entry)
            return $"Материал {material:D} отсутствует.";
        frozenKey = entry.Key;
        return definition.BaseColorTexture.HasValue && !hasUv0
            ? $"Материал {material:D} требует UV0 у меша {mesh:D}." : null;
    }

    private RenderAssetKey? ResolveMaterial(Guid mesh, Guid slot, Guid id, out string? diagnostic)
    {
        diagnostic = ResolveMaterialSource(mesh, slot, id, out var authored, out var frozenKey);
        if (diagnostic != null) return null;
        return authored == null ? frozenKey : _authored!.Resolve(_projectId, authored,
            authored.BaseColorTextureId is { } texture ? _textureKeys[texture] : null).Key;
    }

    private static int SlotIndex(MaterialBindingSetDefinition set, Guid slot)
    {
        for (var i = 0; i < set.Bindings.Length; i++)
            if (set.Bindings[i].SlotId == slot) return i;
        return -1;
    }

    private static string InvalidSlot(Guid mesh, Guid slot) => $"Слот {slot:D} отсутствует у меша {mesh:D}.";
    private static bool IsBuiltinId(Guid id) => id == RenderContentProfile.CubeAssetId ||
        id == RenderContentProfile.CubeBindingSetAssetId || id == RenderContentProfile.WhiteMaterialAssetId ||
        id == RenderContentProfile.DefaultSlotId;

    private bool IsCatalogIdReserved(Guid id) => IsBuiltinId(id) || _entries.ContainsKey(id) || _importIds?.Contains(id) == true ||
        _fullSlotIds?.Contains(id) == true;

    private bool HasReservedId(Guid id) => IsCatalogIdReserved(id) || _projectTextures?.ContainsKey(id) == true ||
        _authored?.Find(id) != null || _meshes?.Owners.ContainsKey(id) == true;

    internal Guid? BindingForMaterial(Guid mesh, Guid slot, Guid material)
    {
        if (MaterialDiagnostic(mesh, slot, material) != null) return null;
        var original = DefaultDefinition(mesh)!;
        var imported = original.Bindings[SlotIndex(original, slot)];
        if (imported.Material.AssetId == material) return DefaultBinding(mesh);
        var id = MaterialBindingId(mesh, original.Bindings.Length == 1 ? null : slot, material);
        return HasReservedId(id) || id == Guid.Empty ? null : id;
    }

    internal string? BindingWithSlotMaterial(Guid mesh, string current, Guid slot, Guid material)
    {
        if (MaterialDiagnostic(mesh, slot, material) != null ||
            ResolveReferences(this, mesh.ToString("D"), current).Definition is not { } previous) return null;
        var original = DefaultDefinition(mesh)!;

        var overrides = previous.Bindings.Select(item =>
                (item.SlotId, MaterialId: item.SlotId == slot ? material : item.Material.AssetId))
            .Where(item => item.MaterialId != original.Bindings[SlotIndex(original, item.SlotId)].Material.AssetId).ToArray();
        return OverrideReference(mesh, overrides);
    }

    internal string? BindingWithTransferredMaterials(Guid source, MaterialBindingSetDefinition current, Guid target)
    {
        if (source == target || DefaultDefinition(source) is not { Bindings.Length: > 1 } original ||
            DefaultDefinition(target) is not { Bindings.Length: > 1 } destination) return null;

        var overrides = new List<(Guid SlotId, Guid MaterialId)>();
        var foundOverride = false;
        foreach (var slot in original.Bindings)
        {
            var index = SlotIndex(current, slot.SlotId);
            if (index < 0) return null;
            var material = current.Bindings[index].Material.AssetId;
            if (material == slot.Material.AssetId) continue;
            foundOverride = true;
            var label = SlotName(slot.SlotId);
            if (original.Bindings.Count(item => SlotName(item.SlotId) == label) != 1) return null;
            var matches = destination.Bindings.Where(item => SlotName(item.SlotId) == label).ToArray();
            if (matches.Length != 1) return null;
            var match = matches[0];
            if (match.Material.AssetId != material) overrides.Add((match.SlotId, material));
        }
        return foundOverride ? OverrideReference(target, overrides) : null;
    }

    private string? OverrideReference(Guid mesh, IReadOnlyList<(Guid SlotId, Guid MaterialId)> overrides)
    {
        if (Collision != null) return null;
        if (overrides.Count == 0) return DefaultBinding(mesh)?.ToString("D");
        if (overrides.Count == 1)
            return BindingForMaterial(mesh, overrides[0].SlotId, overrides[0].MaterialId)?.ToString("D");
        foreach (var (slot, material) in overrides)
            if (MaterialDiagnostic(mesh, slot, material) != null) return null;
        var value = SceneMaterialBindingReference.Format(overrides);
        return SceneMaterialBindingReference.TryParse(mesh, value, out var binding, out _) && !HasReservedId(binding) ? value : null;
    }

    internal static References ResolveReferences(SceneRenderAssets? assets, string meshReference, string bindingReference,
        bool allowMissingMesh = false, bool preview = false)
    {
        if (assets?.Collision is { } collision) return new(null, default, null, collision.Message);
        if (!Guid.TryParseExact(meshReference, "D", out var mesh) || mesh == Guid.Empty)
            return new(null, default, null, "Ссылка на меш должна содержать непустой ID.");
        if (preview && mesh == RenderContentProfile.CubeAssetId)
            return ResolveParsedReferences(null, mesh, bindingReference, allowMissingMesh: false);
        if (assets == null || assets.DefaultBinding(mesh) == null)
            return ResolveParsedReferences(assets, mesh, bindingReference, allowMissingMesh);

        // Cache only the derived bindings this snapshot actually uses, not every possible mesh/slot/material tuple.
        var key = (mesh, bindingReference);
        lock (assets._variantGate)
        {
            if (assets._variants?.TryGetValue(key, out var cached) == true) return cached;
            var resolved = ResolveParsedReferences(assets, mesh, bindingReference, allowMissingMesh);
            if (resolved.Definition != null && assets.ReadBindingEntry(resolved.Binding) == null &&
                (assets._variants?.Count ?? 0) < RenderContentProfile.MaximumManifestEntries)
                (assets._variants ??= []).Add(key, resolved);
            return resolved;
        }
    }

    private static References ResolveParsedReferences(SceneRenderAssets? assets, Guid mesh, string bindingReference, bool allowMissingMesh)
    {
        var validBinding = SceneMaterialBindingReference.TryParse(mesh, bindingReference, out var binding, out var overrides);
        if (mesh == RenderContentProfile.CubeAssetId && assets?._entries.ContainsKey(mesh) != true)
        {
            if (!validBinding || overrides.Length != 0) return new(null, binding, null, InvalidBinding(mesh));
            return binding == RenderContentProfile.CubeBindingSetAssetId ? new(null, binding, null, null) : new(null, binding, null, MissingBinding(mesh, binding));
        }
        if (assets == null || !assets.TryGeometry(mesh, out var geometry, out _))
        {
            if (allowMissingMesh && validBinding) return default;
            return new(null, binding, null, assets?.HasReservedId(mesh) != true
                ? $"Меш {mesh:D} отсутствует. Выберите ассет проекта или его импорт в каталоге сцены."
                : $"По ссылке {mesh:D} ожидается меш.");
        }
        if (!validBinding)
            return new(mesh, binding, null, InvalidBinding(mesh));

        var entry = assets.ReadBindingEntry(binding);
        var allowFullContentFallback = allowMissingMesh && assets._hasFullContent && assets._meshes?.Rows.ContainsKey(mesh) != true;
        if (entry == null)
        {
            if (assets.HasReservedId(binding))
                return new(mesh, binding, null, MissingBinding(mesh, binding));
            string? error;
            var definition = overrides.Length == 0
                ? assets.FindOverrideBinding(mesh, binding, out error)
                : assets.ResolveCombinedBinding(mesh, overrides, out error);
            if (definition != null) return new(mesh, binding, definition, null);
            if (allowFullContentFallback && error == null && (overrides.Length == 0 || assets.DefaultDefinition(mesh) == null))
                return default;
            return new(mesh, binding, null, error ?? MissingBinding(mesh, binding));
        }
        if (entry.Definition is not MaterialBindingSetDefinition set)
            return new(mesh, binding, null, $"По ссылке {binding:D} ожидается привязка материала для меша {mesh:D}.");
        if (set.Mesh != geometry)
            return new(mesh, binding, null, $"Привязка материала {binding:D} относится к мешу {set.Mesh.AssetId:D}. Требуется привязка для меша {mesh:D}.");
        if (overrides.Length != 0 && !MatchesOverrides(set, assets.DefaultDefinition(mesh), overrides))
            return new(mesh, binding, null, MissingBinding(mesh, binding));
        return new(mesh, binding, set, null);
    }

    private static bool MatchesOverrides(MaterialBindingSetDefinition set, MaterialBindingSetDefinition? original,
        (Guid SlotId, Guid MaterialId)[] overrides)
    {
        var found = 0;
        for (var i = 0; i < set.Bindings.Length; i++)
        {
            var slot = set.Bindings[i];
            var index = original == null ? -1 : SlotIndex(original, slot.SlotId);
            var material = index < 0 ? (Guid?)null : original!.Bindings[index].Material.AssetId;
            foreach (var (slotId, materialId) in overrides)
            {
                if (slotId != slot.SlotId) continue;
                material = materialId;
                found++;
                break;
            }
            if (material.HasValue && slot.Material.AssetId != material.Value) return false;
        }
        return found == overrides.Length;
    }

    private static string InvalidBinding(Guid mesh) => $"Ссылка на привязку материала меша {mesh:D} должна содержать ID или список переопределений слотов.";

    private static string MissingBinding(Guid mesh, Guid binding) =>
        $"Привязка материала {binding:D} для меша {mesh:D} отсутствует. Выберите импорт материала или назначьте материал заново.";

    private MaterialBindingSetDefinition? ResolveCombinedBinding(Guid mesh, (Guid SlotId, Guid MaterialId)[] overrides, out string? diagnostic)
    {
        diagnostic = null;
        if (DefaultDefinition(mesh) is not { } original) return null;
        var bindings = original.Bindings.ToBuilder();
        foreach (var (slot, material) in overrides)
        {
            if (ResolveMaterial(mesh, slot, material, out diagnostic) is not { } key) return null;
            bindings[SlotIndex(original, slot)] = new(slot, key);
        }
        return new(original.Mesh, bindings);
    }

    private MaterialBindingSetDefinition? FindOverrideBinding(Guid mesh, Guid binding, out string? diagnostic)
    {
        diagnostic = null;
        if (DefaultDefinition(mesh) is not { } original) return null;
        var match = -1;
        Guid material = default;
        for (var i = 0; i < original.Bindings.Length; i++)
        {
            var slot = original.Bindings[i];
            Guid? identitySlot = original.Bindings.Length == 1 ? null : slot.SlotId;
            foreach (var choice in Materials)
            {
                if (choice.Id == slot.Material.AssetId || MaterialBindingId(mesh, identitySlot, choice.Id) != binding) continue;
                if (match >= 0) return null;
                match = i;
                material = choice.Id;
            }
        }
        if (match < 0) return null;
        var matchedSlot = original.Bindings[match];
        if (ResolveMaterial(mesh, matchedSlot.SlotId, material, out diagnostic) is not { } key) return null;
        return new(original.Mesh, original.Bindings.SetItem(match, matchedSlot with { Material = key }));
    }

    internal static async Task<GameBuildRenderContent?> PrepareBuildAsync(SceneRenderAssets? assets, ResolvedSceneSnapshot scene,
        bool requireAll, CancellationToken token)
    {
        if (assets == null && !requireAll) return null;
        if (assets?.Collision is { } collision) throw new InvalidOperationException(collision.Message);
        var closure = assets == null ? null : new ExactClosure(assets, keepBase: true);
        var builtinCube = false;
        foreach (var entity in scene.Entities.Values)
        {
            token.ThrowIfCancellationRequested();
            foreach (var renderer in entity.Components.Values)
            {
                if (renderer.TypeId != MeshRenderingComponents.MeshRendererId) continue;
                var references = await MaterializeRendererAsync(assets, closure, entity, renderer, requireAll, preview: false, token)
                    .ConfigureAwait(false);
                builtinCube |= references.Definition == null && references.Binding == RenderContentProfile.CubeBindingSetAssetId;
            }
        }
        if (closure == null) return null;
        if (builtinCube && closure.HasEntries) closure.AddBuiltinCube();
        return closure.Freeze();
    }

    private static async ValueTask<References> MaterializeRendererAsync(SceneRenderAssets? assets, ExactClosure? closure,
        ResolvedEntity entity, ResolvedComponent renderer, bool requireAll, bool preview, CancellationToken token)
    {
        var references = ResolveReferences(assets, renderer.Properties["mesh"].Value.GetString(),
            renderer.Properties["bindings"].Value.GetString(), allowMissingMesh: !requireAll, preview);
        if (references.Diagnostic is { } error)
            throw new InvalidOperationException($"Сущность {entity.Id.Value:D}. {error}");
        if (references.Definition is { } binding)
            await closure!.AddBindingAsync(references.Binding, binding, token).ConfigureAwait(false);
        return references;
    }

    // A single exact walk keeps old selected/full payloads in Build, never whole authored mesh blobs.
    private sealed class ExactClosure(SceneRenderAssets assets, bool keepBase)
    {
        private readonly Dictionary<Guid, RenderAssetEntry> _added = [];
        private Dictionary<Guid, RenderContentManifest>? _loadedMeshes;
        private bool _builtinCubeAdded;
        internal bool HasEntries => _added.Count != 0 || keepBase && assets._entries.Count != 0;

        internal async ValueTask AddBindingAsync(Guid id, MaterialBindingSetDefinition binding, CancellationToken token)
        {
            var entry = assets.ReadBindingEntry(id);
            var key = entry?.Key ?? binding.CreateKey(assets._projectId, id);
            if (Existing(key) != null) return;
            AddNew(entry ?? new(key, binding));
            await AddResourceAsync(binding.Mesh, token).ConfigureAwait(false);
            foreach (var slot in binding.Bindings)
            {
                var material = await AddResourceAsync(slot.Material, token).ConfigureAwait(false);
                if (material.Definition is OpaqueUnlitMaterialDefinition { BaseColorTexture: { } texture })
                    await AddResourceAsync(texture, token).ConfigureAwait(false);
            }
        }

        internal void AddBuiltinCube()
        {
            if (_builtinCubeAdded) return;
            var slotId = RenderContentProfile.DefaultSlotId;
            if (assets._entries.ContainsKey(slotId) || assets._importIds?.Contains(slotId) == true)
                throw new InvalidOperationException(new ResourceCollision(slotId).Message);
            var builtin = BuiltinRenderContent.Create(assets._projectId, RenderContentHash.Compute("TRC1"u8), Guid.NewGuid());
            foreach (var entry in builtin.Entries)
            {
                var id = entry.Key.AssetId;
                var occupying = assets._entries.GetValueOrDefault(id);
                if (occupying is { } other && other.Key != entry.Key || assets._fullSlotIds?.Contains(id) == true ||
                    occupying == null && assets._importIds?.Contains(id) == true)
                    throw new InvalidOperationException(new ResourceCollision(id).Message);
                if (Existing(entry.Key) == null) AddNew(entry);
            }
            _builtinCubeAdded = true;
        }

        internal GameBuildRenderContent? Freeze()
        {
            if (!HasEntries) return null;
            if (keepBase && _added.Count == 0 && !assets._rebuildBase) return assets._content;
            return new(assets._projectId, keepBase ? [.. assets._entries.Values, .. _added.Values] : [.. _added.Values]);
        }

        private async ValueTask<RenderAssetEntry> AddResourceAsync(RenderAssetKey key, CancellationToken token)
        {
            if (Existing(key) is { } existing) return existing;
            var entry = assets._entries.GetValueOrDefault(key.AssetId) ?? assets._authored?.FindResolved(key.AssetId);
            if (entry == null && key.KindId == RenderContentProfile.BaseColorTextureKindId &&
                assets._projectTextures?.GetValueOrDefault(key.AssetId) is { } texture)
            {
                try { entry = new(key, await assets._textureCatalog!.ReadDefinitionAsync(texture, token).ConfigureAwait(false)); }
                catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or RenderContentException)
                {
                    throw new InvalidOperationException($"Не удалось прочитать текстуру «{texture.Name}» ({texture.Id:D}).", failure);
                }
            }
            if (entry == null && assets._meshes?.Owners.GetValueOrDefault(key.AssetId) is { } mesh)
            {
                _loadedMeshes ??= [];
                if (!_loadedMeshes.TryGetValue(mesh.Id, out var manifest))
                {
                    try { manifest = await assets._meshes.Catalog.ReadDefinitionAsync(mesh, token).ConfigureAwait(false); }
                    catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or RenderContentException)
                    {
                        throw new InvalidOperationException($"Не удалось прочитать меш «{mesh.Name}» ({mesh.Id:D}).", failure);
                    }
                    _loadedMeshes.Add(mesh.Id, manifest);
                }
                entry = manifest.Resolve(key);
            }
            if (entry == null || entry.Key != key)
                throw new InvalidOperationException($"Ревизия ресурса {key.AssetId:D} не разрешена для сборки.");
            AddNew(entry);
            return entry;
        }

        private RenderAssetEntry? Existing(RenderAssetKey key)
        {
            var entry = _added.GetValueOrDefault(key.AssetId) ?? (keepBase ? assets._entries.GetValueOrDefault(key.AssetId) : null);
            if (entry != null && entry.Key != key) throw new InvalidOperationException(new ResourceCollision(key.AssetId).Message);
            return entry;
        }

        private void AddNew(RenderAssetEntry entry)
        {
            if ((keepBase ? assets._entries.Count : 0) + _added.Count >= RenderContentProfile.MaximumManifestEntries)
                throw new InvalidOperationException("Превышен лимит ресурсов в сборке сцены.");
            _added.Add(entry.Key.AssetId, entry);
        }
    }

    private sealed class AuthoredMeshes
    {
        internal ProjectMeshCatalog Catalog { get; }
        internal Dictionary<Guid, ProjectMesh> Rows { get; } = [];
        internal Dictionary<Guid, ProjectMesh> Owners { get; } = [];
        internal Dictionary<Guid, RenderAssetEntry> Bindings { get; } = [];
        internal Dictionary<Guid, ProjectMeshDefaultMaterial> Materials { get; } = [];
        internal Dictionary<Guid, RenderAssetKey> Textures { get; } = [];
        internal Dictionary<Guid, string> SlotNames { get; } = [];
        internal ResourceCollision? Collision { get; }

        internal AuthoredMeshes(Guid project, ProjectMeshCatalog catalog, IReadOnlyList<ProjectMesh> rows)
        {
            Catalog = catalog;
            ResourceCollision? collision = null;
            foreach (var row in rows)
            {
                Reserve(row.Id, row);
                Reserve(row.DefaultBindingId, row);
                Rows.TryAdd(row.Id, row);
                foreach (var texture in row.DefaultTextures)
                {
                    Reserve(texture.Id, row);
                    Textures.TryAdd(texture.Id, texture.Key(project));
                }
                foreach (var material in row.DefaultMaterials)
                {
                    Reserve(material.Id, row);
                    Materials.TryAdd(material.Id, material);
                }
                var bindings = new MaterialSlotBinding[row.Slots.Length];
                for (var index = 0; index < bindings.Length; index++)
                {
                    var slot = row.Slots[index];
                    Reserve(slot.Id, row);
                    SlotNames.TryAdd(slot.Id, slot.Name);
                    var material = Materials[slot.DefaultMaterialId];
                    bindings[index] = new(slot.Id, material.Key(project));
                }
                var set = new MaterialBindingSetDefinition(row.MeshKey(project), bindings);
                Bindings.TryAdd(row.DefaultBindingId, new(row.DefaultBindingKey(project), set));
            }
            Collision = collision;

            void Reserve(Guid id, ProjectMesh row)
            {
                if (!Owners.TryAdd(id, row)) collision ??= new(id);
            }
        }
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

    private sealed class AuthoredMaterials(IReadOnlyList<ProjectMaterial> materials)
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<Guid, ProjectMaterial> _materials = materials.ToDictionary(material => material.Id);
        private Dictionary<Guid, RenderAssetEntry>? _resolved;
        internal ProjectMaterial? Find(Guid id) => _materials.GetValueOrDefault(id);

        internal RenderAssetEntry? FindResolved(Guid id)
        {
            lock (_gate) return _resolved?.GetValueOrDefault(id);
        }

        internal RenderAssetEntry Resolve(Guid project, ProjectMaterial material, RenderAssetKey? texture)
        {
            lock (_gate)
            {
                if (_resolved?.TryGetValue(material.Id, out var entry) == true) return entry;
                var definition = texture is { } key
                    ? new OpaqueUnlitMaterialDefinition(material.BaseColorSrgb, key, RenderTextureSampler.NearestClamp)
                    : new OpaqueUnlitMaterialDefinition(material.BaseColorSrgb);
                entry = new(definition.CreateKey(project, material.Id), definition);
                (_resolved ??= []).Add(material.Id, entry);
                return entry;
            }
        }
    }
}
