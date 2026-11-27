using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

internal sealed partial class ProjectMeshCatalog
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8
    };

    internal async Task<ProjectMeshCatalogSnapshot> ReadAsync(CancellationToken token)
    {
        var bytes = await ReadBytesAsync(token).ConfigureAwait(false);
        if (bytes == null) return new(_projectId, [], null);
        try
        {
            var file = JsonSerializer.Deserialize<CatalogFile>(bytes, Json);
            if (file is not { FormatVersion: 1, Meshes.Length: <= MaximumMeshes } || ReadId(file.ProjectId) != _projectId)
                throw new InvalidDataException("Неподдерживаемый каталог мешей или другой проект.");
            var ids = new HashSet<Guid>();
            var rows = new ProjectMesh[file.Meshes.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                rows[i] = ReadRow(file.Meshes[i], ids);
            }
            Array.Sort(rows, static (a, b) => a.Id.CompareTo(b.Id));
            return new(_projectId, rows, JsonSceneDocumentStore.Stamp(bytes));
        }
        catch (Exception error) when (error is JsonException or ArgumentException or FormatException or NotSupportedException or RenderContentException)
        {
            throw new InvalidDataException("Некорректный каталог мешей проекта.", error);
        }
    }

    private ProjectMesh ReadRow(MeshRow? row, HashSet<Guid> ids)
    {
        if (row is not { Slots.Length: >= 1 and <= 2, DefaultMaterials.Length: >= 1 and <= 2, DefaultTextures.Length: <= 1,
                Bounds: { Min: { }, Max: { } }, Source: { MeshLocator: { }, SourcePath: { }, Generation: > 0 } source } ||
            NormalizeName(row.Name) != row.Name || ModelSourcePath.Normalize(source.SourcePath) != source.SourcePath)
            throw BadHeader();
        var id = AddId(row.Id, ids);
        var bindingId = AddId(row.DefaultBindingId, ids);
        var bounds = new RenderBounds(row.Bounds.Min.ToVector(), row.Bounds.Max.ToVector());
        bounds.Validate();
        ModelSubassetValidation.Locator(source.MeshLocator);
        if (source.InputRevision is not { Length: 64 } || !source.InputRevision.All(Uri.IsHexDigit)) throw BadHeader();
        _ = ReadHash(source.CandidateRevision);
        var provenance = new ProjectMeshSource(ReadId(source.SourceId), source.SourcePath, ReadId(source.PublishedMeshId), source.MeshLocator,
            source.Generation, source.InputRevision, source.CandidateRevision, ReadHash(source.MeshRevision));

        var textures = ImmutableArray.CreateBuilder<ProjectMeshDefaultTexture>(row.DefaultTextures.Length);
        foreach (var texture in row.DefaultTextures)
        {
            if (texture is not { Width: > 0 and <= RenderContentProfile.MaximumTextureDimension,
                    Height: > 0 and <= RenderContentProfile.MaximumTextureDimension } || !row.HasUv0)
                throw BadHeader();
            textures.Add(new(AddId(texture.Id, ids), ReadHash(texture.Revision), texture.Width, texture.Height));
        }
        var defaultTextures = textures.MoveToImmutable();
        var materials = ImmutableArray.CreateBuilder<ProjectMeshDefaultMaterial>(row.DefaultMaterials.Length);
        foreach (var material in row.DefaultMaterials)
        {
            if (material?.BaseColorSrgb is not { } rgb) throw BadHeader();
            var color = rgb.ToVector();
            var textureId = material.BaseColorTextureId == null ? (Guid?)null : ReadId(material.BaseColorTextureId);
            var texture = textureId == null ? null : defaultTextures.FirstOrDefault(item => item.Id == textureId);
            if (textureId != null && texture == null) throw BadHeader();
            var definition = texture == null ? new OpaqueUnlitMaterialDefinition(color) : new OpaqueUnlitMaterialDefinition(color, texture.Key(_projectId));
            if (ReadHash(material.Revision) != definition.Revision) throw BadHeader();
            materials.Add(new(AddId(material.Id, ids), definition.Revision, color, textureId));
        }
        var defaultMaterials = materials.MoveToImmutable();
        foreach (var texture in defaultTextures)
            if (!defaultMaterials.Any(material => material.BaseColorTextureId == texture.Id)) throw BadHeader();

        var meshRevision = ReadHash(row.MeshRevision);
        var bindingRevision = ReadHash(row.DefaultBindingRevision);
        var slots = ImmutableArray.CreateBuilder<ProjectMeshSlot>(row.Slots.Length);
        var bindings = new MaterialSlotBinding[row.Slots.Length];
        for (var i = 0; i < row.Slots.Length; i++)
        {
            var slot = row.Slots[i];
            if (slot is not { Name.Length: > 0 and <= MaxNameLength } || string.IsNullOrWhiteSpace(slot.Name) ||
                (row.Slots.Length == 1 ? slot.Name != ImplicitSlot : i == 1 && StringComparer.Ordinal.Equals(slot.Name, slots[0].Name)))
                throw BadHeader();
            var materialId = ReadId(slot.DefaultMaterialId);
            var material = defaultMaterials.FirstOrDefault(item => item.Id == materialId) ?? throw BadHeader();
            var slotId = AddId(slot.Id, ids);
            slots.Add(new(slotId, slot.Name, materialId));
            bindings[i] = new(slotId, material.Key(_projectId));
        }
        var defaultSlots = slots.MoveToImmutable();
        foreach (var material in defaultMaterials)
            if (!defaultSlots.Any(slot => slot.DefaultMaterialId == material.Id)) throw BadHeader();
        var result = new ProjectMesh(id, row.Name, bindingId, ReadHash(row.PayloadHash), meshRevision, bindingRevision,
            row.HasUv0, bounds, defaultSlots, defaultMaterials, defaultTextures, provenance);
        if (new MaterialBindingSetDefinition(result.MeshKey(_projectId), bindings).Revision != bindingRevision) throw BadHeader();
        return result;
    }

    private async Task<ProjectMeshCatalogSnapshot> WriteAsync(ProjectMeshCatalogSnapshot expected, ProjectMesh value,
        bool add, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var meshes = new ProjectMesh[expected.Meshes.Count + (add ? 1 : 0)];
        for (var i = 0; i < expected.Meshes.Count; i++)
        {
            var row = expected.Meshes[i];
            meshes[i] = !add && row.Id == value.Id ? value : row;
        }
        if (add) meshes[^1] = value;
        Array.Sort(meshes, static (a, b) => a.Id.CompareTo(b.Id));
        var rows = Array.ConvertAll(meshes, ToRow);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new CatalogFile(1, _projectId.ToString("D"), rows), Json);
        if (bytes.Length > MaximumCatalogBytes) throw new InvalidDataException("Каталог мешей превышает предел размера.");
        SceneFileStamp stamp;
        try
        {
            stamp = await JsonSceneDocumentStore.WriteAtomicAsync(_path, bytes, expected.Stamp, MaximumCatalogBytes, token).ConfigureAwait(false);
        }
        catch (InvalidDataException error) { throw new SceneFileConflictException(_path, error); }
        return new(_projectId, meshes, stamp);
    }

    private static MeshRow ToRow(ProjectMesh mesh) => new(mesh.Id.ToString("D"), mesh.Name, mesh.DefaultBindingId.ToString("D"),
        HashText(mesh.PayloadHash), HashText(mesh.MeshRevision), HashText(mesh.DefaultBindingRevision), mesh.HasUv0,
        new(Position.From(mesh.Bounds.Minimum), Position.From(mesh.Bounds.Maximum)),
        mesh.Slots.Select(static slot => new SlotRow(slot.Id.ToString("D"), slot.Name, slot.DefaultMaterialId.ToString("D"))).ToArray(),
        mesh.DefaultMaterials.Select(static material => new MaterialRow(material.Id.ToString("D"), HashText(material.Revision),
            Color.From(material.BaseColorSrgb), material.BaseColorTextureId?.ToString("D"))).ToArray(),
        mesh.DefaultTextures.Select(static texture => new TextureRow(texture.Id.ToString("D"), HashText(texture.Revision), texture.Width, texture.Height)).ToArray(),
        new(mesh.Source.SourceId.ToString("D"), mesh.Source.SourcePath, mesh.Source.PublishedMeshId.ToString("D"), mesh.Source.MeshLocator,
            mesh.Source.Generation, mesh.Source.InputRevision, mesh.Source.CandidateRevision, HashText(mesh.Source.MeshRevision)));

    private async Task<ProjectMeshCatalogSnapshot> CheckStampAsync(ProjectMeshCatalogSnapshot expected, CancellationToken token)
    {
        byte[]? bytes;
        try { bytes = await ReadBytesAsync(token).ConfigureAwait(false); }
        catch (InvalidDataException error) { throw new SceneFileConflictException(_path, error); }
        if ((bytes == null ? null : JsonSceneDocumentStore.Stamp(bytes)) != expected.Stamp)
            throw new SceneFileConflictException(_path);
        token.ThrowIfCancellationRequested();
        return expected;
    }

    private async Task<byte[]?> ReadBytesAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FileStream input;
        try { input = new(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous); }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        await using (input)
            return await JsonSceneDocumentStore.ReadBytesAsync(input, MaximumCatalogBytes, token).ConfigureAwait(false);
    }

    private void CheckProject(ProjectMeshCatalogSnapshot expected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (expected.ProjectId != _projectId) throw new SceneFileConflictException(_path);
    }

    private static string NormalizeName(string name) => name?.Trim() is { Length: > 0 and <= MaxNameLength } valid ? valid :
        throw new InvalidDataException("Имя меша должно содержать от 1 до 256 символов.");
    private static Guid ReadId(string? text) => text is { Length: 36 } && Guid.TryParseExact(text, "D", out var id) && id != Guid.Empty
        ? id : throw BadHeader();
    private static Guid AddId(string text, HashSet<Guid> ids)
    {
        var id = ReadId(text);
        return ids.Add(id) ? id : throw new InvalidDataException("Каталог мешей повторяет идентификатор.");
    }
    private static RenderContentHash ReadHash(string? text)
    {
        if (text is not { Length: 64 } || text.Any(static c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F'))) throw BadHeader();
        var hash = RenderContentHash.Parse(text);
        return !hash.IsEmpty ? hash : throw BadHeader();
    }
    private string BlobPath(RenderContentHash hash) => Path.Combine(_dataDirectory, HashText(hash) + ".trc");
    private static string HashText(RenderContentHash hash)
    {
        Span<byte> bytes = stackalloc byte[32];
        hash.WriteBytes(bytes);
        return Convert.ToHexString(bytes);
    }
    private static InvalidDataException BadHeader() => new("Некорректная запись меша в каталоге проекта.");

    private sealed record CatalogFile(int FormatVersion, string ProjectId, MeshRow[] Meshes);
    private sealed record MeshRow(string Id, string Name, string DefaultBindingId, string PayloadHash, string MeshRevision,
        string DefaultBindingRevision, bool HasUv0, BoundsRow Bounds, SlotRow[] Slots, MaterialRow[] DefaultMaterials,
        TextureRow[] DefaultTextures, SourceRow Source);
    private sealed record BoundsRow(Position Min, Position Max);
    private sealed record Position(float X, float Y, float Z)
    {
        internal Vector3 ToVector() => new(X, Y, Z);
        internal static Position From(Vector3 value) => new(value.X, value.Y, value.Z);
    }
    private sealed record Color(float R, float G, float B)
    {
        internal Vector3 ToVector() => new(R, G, B);
        internal static Color From(Vector3 value) => new(value.X, value.Y, value.Z);
    }
    private sealed record SlotRow(string Id, string Name, string DefaultMaterialId);
    private sealed record MaterialRow(string Id, string Revision, Color BaseColorSrgb, string? BaseColorTextureId);
    private sealed record TextureRow(string Id, string Revision, int Width, int Height);
    private sealed record SourceRow(string SourceId, string SourcePath, string PublishedMeshId, string MeshLocator, long Generation,
        string InputRevision, string CandidateRevision, string MeshRevision);
}
