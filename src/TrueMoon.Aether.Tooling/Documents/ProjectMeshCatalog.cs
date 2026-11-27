using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

internal sealed record MeshSourceChoice(Guid SourceId, long Generation, string CandidateRevision, Guid PublishedMeshId);
internal sealed record ProjectMeshGeometryChoice(MeshSourceChoice Selector, string Name, string MeshLocator, RenderAssetKey Key,
    RenderBounds Bounds, bool HasUv0, ImmutableArray<string> Slots);
internal sealed record ProjectMeshSource(Guid SourceId, string SourcePath, Guid PublishedMeshId, string MeshLocator,
    long Generation, string InputRevision, string CandidateRevision, RenderContentHash MeshRevision);
internal sealed record ProjectMeshSlot(Guid Id, string Name, Guid DefaultMaterialId);
internal sealed record ProjectMeshDefaultMaterial(Guid Id, RenderContentHash Revision, Vector3 BaseColorSrgb, Guid? BaseColorTextureId)
{
    internal RenderAssetKey Key(Guid projectId) => new(projectId, Id, RenderContentProfile.MaterialKindId,
        RenderContentProfile.SchemaVersion, Revision);
}
internal sealed record ProjectMeshDefaultTexture(Guid Id, RenderContentHash Revision, int Width, int Height)
{
    internal RenderAssetKey Key(Guid projectId) => new(projectId, Id, RenderContentProfile.BaseColorTextureKindId,
        RenderContentProfile.SchemaVersion, Revision);
}
internal sealed record ProjectMesh(Guid Id, string Name, Guid DefaultBindingId, RenderContentHash PayloadHash,
    RenderContentHash MeshRevision, RenderContentHash DefaultBindingRevision, bool HasUv0, RenderBounds Bounds,
    ImmutableArray<ProjectMeshSlot> Slots, ImmutableArray<ProjectMeshDefaultMaterial> DefaultMaterials,
    ImmutableArray<ProjectMeshDefaultTexture> DefaultTextures, ProjectMeshSource Source)
{
    internal RenderAssetKey MeshKey(Guid projectId) => new(projectId, Id, RenderContentProfile.MeshKindId,
        RenderContentProfile.SchemaVersion, MeshRevision);
    internal RenderAssetKey DefaultBindingKey(Guid projectId) => new(projectId, DefaultBindingId, RenderContentProfile.BindingSetKindId,
        RenderContentProfile.SchemaVersion, DefaultBindingRevision);
}

internal sealed class ProjectMeshCatalogSnapshot(Guid projectId, ProjectMesh[] meshes, SceneFileStamp? stamp)
{
    internal Guid ProjectId { get; } = projectId;
    internal IReadOnlyList<ProjectMesh> Meshes { get; } = ImmutableArray.CreateRange(meshes);
    internal SceneFileStamp? Stamp { get; } = stamp;

    internal ProjectMesh? Find(Guid id)
    {
        foreach (var mesh in Meshes)
            if (mesh.Id == id) return mesh;
        return null;
    }
}

internal sealed partial class ProjectMeshCatalog(ResolvedProject project, ProjectModelCatalog? sources = null)
{
    internal const int MaxNameLength = 256;
    private const int MaximumMeshes = 256;
    private const int MaximumCatalogBytes = 1024 * 1024;
    private const int MaximumBlobBytes = 4 * 1024 * 1024;
    private const string ImplicitSlot = "Основной";
    private const string StaleSource = "Источник меша изменился. Обновите список.";
    private readonly Guid _projectId = project.Description.Id;
    private readonly string _path = Path.Combine(project.ContentDirectory, ".aether-mesh-catalog.json");
    private readonly string _dataDirectory = Path.Combine(project.ContentDirectory, ".aether-mesh-data");

    internal bool CanEdit => sources != null;

    internal Task<IReadOnlyList<ProjectModelCatalogItem>> ReadSourcesAsync(CancellationToken token) =>
        RequireSources().ReadAsync(token);

    internal async Task<IReadOnlyList<ProjectMeshGeometryChoice>> ReadSourceAsync(Guid sourceId, CancellationToken token)
    {
        var selection = await RequireSources().LoadAsync(sourceId, token).ConfigureAwait(false);
        var published = selection.Watch.Published;
        var choices = new ProjectMeshGeometryChoice[published.Meshes.Count];
        for (var i = 0; i < choices.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var mesh = published.Meshes[i];
            var locator = Locator(published, ModelSubassetKind.Mesh, mesh.AssetId);
            choices[i] = new(new(sourceId, published.Generation, published.Candidate.Revision, mesh.AssetId),
                Leaf(locator), locator, mesh.Geometry.CreateKey(_projectId, mesh.AssetId), mesh.Geometry.Bounds,
                !mesh.Geometry.Uv0.IsEmpty, SlotLabels(published, mesh));
        }
        return Array.AsReadOnly(choices);
    }

    internal async Task<ProjectMeshCatalogSnapshot> CreateAsync(ProjectMeshCatalogSnapshot expected, MeshSourceChoice selector,
        string name, CancellationToken token)
    {
        CheckProject(expected, token);
        if (expected.Meshes.Count >= MaximumMeshes) throw new InvalidDataException("Достигнут предел числа мешей проекта.");
        name = NormalizeName(name);
        var (published, mesh, source) = await CaptureSourceAsync(selector, token).ConfigureAwait(false);
        var labels = SlotLabels(published, mesh);
        var entries = new List<RenderAssetEntry>(5);
        var materials = ImmutableArray.CreateBuilder<ProjectMeshDefaultMaterial>();
        var textures = ImmutableArray.CreateBuilder<ProjectMeshDefaultTexture>();
        var sourceSlots = mesh.Slots;
        var firstMaterial = sourceSlots[0].MaterialId;
        var lastMaterial = sourceSlots[^1].MaterialId;
        var sourceEntries = published.CreateRenderEntries(_projectId)
            .TakeWhile(static entry => entry.Definition is not MeshDefinition)
            .Where(entry => entry.Definition is BaseColorTextureDefinition || entry.Key.AssetId == firstMaterial || entry.Key.AssetId == lastMaterial)
            .ToDictionary(static entry => entry.Key.AssetId);
        var copiedMaterials = new Dictionary<Guid, Guid>();
        var copiedTextures = new Dictionary<Guid, RenderAssetKey>();
        var slots = ImmutableArray.CreateBuilder<ProjectMeshSlot>(sourceSlots.Length);
        for (var i = 0; i < sourceSlots.Length; i++)
        {
            var materialId = sourceSlots[i].MaterialId;
            if (!copiedMaterials.TryGetValue(materialId, out var copyId))
            {
                var material = (OpaqueUnlitMaterialDefinition)sourceEntries[materialId].Definition;
                Guid? textureId = null;
                if (material.BaseColorTexture is { } texture)
                {
                    if (!copiedTextures.TryGetValue(texture.AssetId, out var copyKey))
                    {
                        var definition = (BaseColorTextureDefinition)sourceEntries[texture.AssetId].Definition;
                        var copy = Entry(Guid.NewGuid(), definition);
                        entries.Add(copy);
                        copyKey = copy.Key;
                        copiedTextures.Add(texture.AssetId, copyKey);
                        textures.Add(new(copyKey.AssetId, copyKey.Revision, definition.Width, definition.Height));
                    }
                    textureId = copyKey.AssetId;
                    material = new(material.BaseColorSrgb, copyKey);
                }
                copyId = Guid.NewGuid();
                entries.Add(Entry(copyId, material));
                materials.Add(new(copyId, material.Revision, material.BaseColorSrgb, textureId));
                copiedMaterials.Add(materialId, copyId);
            }
            slots.Add(new(Guid.NewGuid(), labels[i], copyId));
        }
        var row = new ProjectMesh(Guid.NewGuid(), name, Guid.NewGuid(), default, default, default, false, default,
            slots.MoveToImmutable(), materials.ToImmutable(), textures.ToImmutable(), source);
        row = await PublishGeometryAsync(row, mesh.Geometry, entries, token).ConfigureAwait(false);
        return await WriteAsync(expected, row, add: true, token).ConfigureAwait(false);
    }

    internal async Task<ProjectMeshCatalogSnapshot> ReplaceGeometryAsync(ProjectMeshCatalogSnapshot expected, Guid id,
        MeshSourceChoice selector, CancellationToken token)
    {
        CheckProject(expected, token);
        var current = RequireRow(expected, id);
        var (published, mesh, source) = await CaptureSourceAsync(selector, token).ConfigureAwait(false);
        var labels = SlotLabels(published, mesh);
        if (labels.Length != current.Slots.Length)
            throw new InvalidDataException("Новая геометрия должна сохранять набор слотов меша.");
        if (current.HasUv0 && mesh.Geometry.Uv0.IsEmpty)
            throw new InvalidDataException("Новая геометрия должна сохранять UV0 меша.");
        var slots = ImmutableArray.CreateBuilder<ProjectMeshSlot>(labels.Length);
        foreach (var label in labels)
            slots.Add(current.Slots.FirstOrDefault(slot => StringComparer.Ordinal.Equals(slot.Name, label)) ??
                throw new InvalidDataException("Новая геометрия должна сохранять имена слотов меша."));
        var previous = await ReadDefinitionAsync(current, token).ConfigureAwait(false);
        var entries = previous.Entries.Where(static entry => entry.Definition is OpaqueUnlitMaterialDefinition or BaseColorTextureDefinition).ToList();
        var row = current with { Slots = slots.MoveToImmutable(), Source = source };
        row = await PublishGeometryAsync(row, mesh.Geometry, entries, token).ConfigureAwait(false);
        return row.PayloadHash == current.PayloadHash && row.Source == current.Source
            ? await CheckStampAsync(expected, token).ConfigureAwait(false)
            : await WriteAsync(expected, row, add: false, token).ConfigureAwait(false);
    }

    internal async Task<ProjectMeshCatalogSnapshot> RenameAsync(ProjectMeshCatalogSnapshot expected, Guid id,
        string name, CancellationToken token)
    {
        CheckProject(expected, token);
        var current = RequireRow(expected, id);
        name = NormalizeName(name);
        return name == current.Name ? await CheckStampAsync(expected, token).ConfigureAwait(false) :
            await WriteAsync(expected, current with { Name = name }, add: false, token).ConfigureAwait(false);
    }

    internal async Task<RenderContentManifest> ReadDefinitionAsync(ProjectMesh row, CancellationToken token)
    {
        try
        {
            var bytes = await ImmutableAssetBlob.ReadAsync(BlobPath(row.PayloadHash), MaximumBlobBytes, token).ConfigureAwait(false);
            if (RenderContentHash.Compute(bytes) != row.PayloadHash) throw BadBlob();
            var manifest = GameBuildRenderContent.Read(bytes, row.PayloadHash);
            if (manifest.ProjectId != _projectId || manifest.Entries.Length != 2 + row.DefaultMaterials.Length + row.DefaultTextures.Length)
                throw BadBlob();
            var geometry = (MeshDefinition)manifest.Resolve(row.MeshKey(_projectId)).Definition;
            var bindings = (MaterialBindingSetDefinition)manifest.Resolve(row.DefaultBindingKey(_projectId)).Definition;
            if (geometry.Bounds != row.Bounds || (!geometry.Uv0.IsEmpty) != row.HasUv0 || geometry.Slots.Length != row.Slots.Length ||
                bindings.Mesh != row.MeshKey(_projectId)) throw BadBlob();
            for (var i = 0; i < row.Slots.Length; i++)
            {
                var slot = row.Slots[i];
                if (geometry.Slots[i].SlotId != slot.Id || bindings.Bindings[i].Material.AssetId != slot.DefaultMaterialId) throw BadBlob();
            }
            foreach (var header in row.DefaultMaterials)
            {
                var material = (OpaqueUnlitMaterialDefinition)manifest.Resolve(header.Key(_projectId)).Definition;
                if (material.BaseColorSrgb != header.BaseColorSrgb || material.BaseColorTexture?.AssetId != header.BaseColorTextureId) throw BadBlob();
            }
            foreach (var header in row.DefaultTextures)
            {
                var texture = (BaseColorTextureDefinition)manifest.Resolve(header.Key(_projectId)).Definition;
                if (texture.Width != header.Width || texture.Height != header.Height) throw BadBlob();
            }
            token.ThrowIfCancellationRequested();
            return manifest;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or RenderContentException)
        {
            throw new InvalidDataException($"Не удалось прочитать меш «{row.Name}» ({row.Id:D}).", error);
        }
    }

    private async Task<ProjectMesh> PublishGeometryAsync(ProjectMesh row, MeshDefinition source, List<RenderAssetEntry> entries,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var ranges = new MeshSlot[source.Slots.Length];
        for (var i = 0; i < ranges.Length; i++) ranges[i] = source.Slots[i] with { SlotId = row.Slots[i].Id };
        var geometry = source.Uv0.IsEmpty
            ? new MeshDefinition(source.Vertices, source.Indices, ranges, source.Bounds)
            : new MeshDefinition(source.Vertices, source.Uv0, source.Indices, ranges, source.Bounds);
        // A replacement already verified the previous blob; unchanged authored geometry preserves the complete closure.
        if (geometry.Revision == row.MeshRevision) return row;
        var mesh = Entry(row.Id, geometry);
        var slots = new MaterialSlotBinding[ranges.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = row.Slots[i];
            slots[i] = new(slot.Id, row.DefaultMaterials.First(material => material.Id == slot.DefaultMaterialId).Key(_projectId));
        }
        var binding = Entry(row.DefaultBindingId, new MaterialBindingSetDefinition(mesh.Key, slots));
        entries.Add(mesh);
        entries.Add(binding);
        var payload = new GameBuildRenderContent(_projectId, entries);
        using var stream = new MemoryStream();
        payload.WriteTo(stream);
        if (stream.Length > MaximumBlobBytes) throw new InvalidDataException("Данные меша превышают предел размера.");
        var bytes = stream.ToArray();
        var hash = RenderContentHash.Compute(bytes);
        await ImmutableAssetBlob.PublishAsync(BlobPath(hash), bytes, MaximumBlobBytes, token).ConfigureAwait(false);
        return row with { PayloadHash = hash, MeshRevision = mesh.Key.Revision, DefaultBindingRevision = binding.Key.Revision,
            Bounds = geometry.Bounds, HasUv0 = !geometry.Uv0.IsEmpty };
    }

    private async Task<(PublishedModelImport Published, PublishedModelMesh Mesh, ProjectMeshSource Source)> CaptureSourceAsync(
        MeshSourceChoice selector, CancellationToken token)
    {
        var catalog = RequireSources();
        SceneModelImportSelection selection;
        try { selection = await catalog.LoadAsync(selector.SourceId, token).ConfigureAwait(false); }
        catch (InvalidOperationException error) { throw new InvalidDataException(StaleSource, error); }
        var published = selection.Watch.Published;
        var candidate = published.Candidate;
        if (candidate.SourceId != selector.SourceId || published.Generation != selector.Generation || candidate.Revision != selector.CandidateRevision ||
            published.Meshes.FirstOrDefault(mesh => mesh.AssetId == selector.PublishedMeshId) is not { } mesh)
            throw new InvalidDataException(StaleSource);
        var capture = selection.Watch.Source;
        var sourcePath = ProjectModelPaths.Relative(project.RootDirectory, Path.Combine(capture.Directory, capture.RootName));
        var source = new ProjectMeshSource(candidate.SourceId, sourcePath, mesh.AssetId, Locator(published, ModelSubassetKind.Mesh, mesh.AssetId),
            published.Generation, candidate.InputRevision, candidate.Revision, mesh.Geometry.Revision);
        return (published, mesh, source);
    }

    private static ImmutableArray<string> SlotLabels(PublishedModelImport published, PublishedModelMesh mesh)
    {
        var slots = mesh.Slots;
        if (slots.Length == 1) return [ImplicitSlot];
        var first = Leaf(Locator(published, ModelSubassetKind.Slot, slots[0].SlotId));
        var second = Leaf(Locator(published, ModelSubassetKind.Slot, slots[1].SlotId));
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second) || StringComparer.Ordinal.Equals(first, second))
            throw new InvalidDataException("Имена слотов меша должны быть непустыми и различными.");
        return [first, second];
    }

    private static string Locator(PublishedModelImport published, ModelSubassetKind kind, Guid id) =>
        published.Bindings.First(binding => binding.Kind == kind && binding.Id == id).Key.Locator;
    private static string Leaf(string locator) => Uri.UnescapeDataString(locator[(locator.LastIndexOf('/') + 1)..]);
    private ProjectModelCatalog RequireSources() => sources ?? throw new NotSupportedException("Каталог источников мешей недоступен.");
    private RenderAssetEntry Entry(Guid id, RenderAssetDefinition definition) => new(definition.CreateKey(_projectId, id), definition);
    private static ProjectMesh RequireRow(ProjectMeshCatalogSnapshot expected, Guid id) =>
        expected.Find(id) ?? throw new InvalidDataException("Меш отсутствует в каталоге.");
    private static InvalidDataException BadBlob() => new("Данные меша не соответствуют каталогу.");
}
