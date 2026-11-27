using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

internal sealed record ProjectMaterial(Guid Id, string Name, Vector3 BaseColorSrgb, Guid? BaseColorTextureId);

internal sealed class ProjectMaterialCatalogSnapshot(Guid projectId, ProjectMaterial[] materials, SceneFileStamp? stamp)
{
    internal Guid ProjectId { get; } = projectId;
    internal IReadOnlyList<ProjectMaterial> Materials { get; } = Array.AsReadOnly(materials);
    internal SceneFileStamp? Stamp { get; } = stamp;

    internal ProjectMaterial? Find(Guid id)
    {
        foreach (var material in Materials)
            if (material.Id == id) return material;
        return null;
    }
}

// Authored material rows are project content, not scene state or imported render entries.
internal sealed class ProjectMaterialCatalog(ResolvedProject project, Func<CancellationToken, Task>? beforeCommit = null)
{
    internal const int MaxNameLength = 256;
    private const int MaximumMaterials = 256;
    private const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 8
    };
    private readonly Guid _projectId = project.Description.Id;
    private readonly string _path = Path.Combine(project.ContentDirectory, ".aether-material-catalog.json");

    internal async Task<ProjectMaterialCatalogSnapshot> ReadAsync(CancellationToken token)
    {
        var bytes = await ReadBytesAsync(token).ConfigureAwait(false);
        if (bytes == null) return new(_projectId, [], null);
        try
        {
            var file = JsonSerializer.Deserialize<CatalogFile>(bytes, Json);
            if (file is not { FormatVersion: 1, Materials.Length: <= MaximumMaterials } ||
                !Guid.TryParseExact(file.ProjectId, "D", out var id) || id != _projectId)
                throw new InvalidDataException("Неподдерживаемый каталог материалов или другой проект.");

            var materials = new ProjectMaterial[file.Materials.Length];
            var ids = new HashSet<Guid>();
            for (var i = 0; i < materials.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var row = file.Materials[i];
                if (row is not { Name: { }, BaseColorSrgb: { } color } || !Guid.TryParseExact(row.Id, "D", out var materialId) ||
                    !ids.Add(materialId) || row.Name != row.Name.Trim())
                    throw new InvalidDataException("Некорректная или повторная запись материала.");
                Guid? texture = null;
                if (row.BaseColorTextureId != null)
                {
                    if (!Guid.TryParseExact(row.BaseColorTextureId, "D", out var textureId))
                        throw new InvalidDataException("Некорректный ID текстуры материала.");
                    texture = textureId;
                }
                materials[i] = Validate(new(materialId, row.Name, new(color.R, color.G, color.B), texture));
            }
            return new(_projectId, materials, JsonSceneDocumentStore.Stamp(bytes));
        }
        catch (Exception error) when (error is JsonException or RenderContentException)
        {
            throw new InvalidDataException("Некорректный каталог материалов проекта.", error);
        }
    }

    internal async Task<ProjectMaterialCatalogSnapshot> WriteAsync(ProjectMaterialCatalogSnapshot expected,
        ProjectMaterial value, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (expected.ProjectId != _projectId) throw new SceneFileConflictException(_path);
        value = Validate(value);
        var existing = expected.Find(value.Id);
        if (value == existing)
        {
            byte[]? bytes;
            try { bytes = await ReadBytesAsync(token).ConfigureAwait(false); }
            catch (InvalidDataException error) { throw new SceneFileConflictException(_path, error); }
            var observed = bytes == null ? null : JsonSceneDocumentStore.Stamp(bytes);
            if (observed != expected.Stamp) throw new SceneFileConflictException(_path);
            token.ThrowIfCancellationRequested();
            return expected;
        }
        var count = expected.Materials.Count + (existing == null ? 1 : 0);
        if (count > MaximumMaterials) throw new InvalidDataException("Достигнут предел числа материалов проекта.");
        var materials = new ProjectMaterial[count];
        for (var i = 0; i < expected.Materials.Count; i++)
        {
            var row = expected.Materials[i];
            materials[i] = row.Id == value.Id ? value : row;
        }
        if (existing == null) materials[^1] = value;
        Array.Sort(materials, static (a, b) => a.Id.CompareTo(b.Id));
        var rows = Array.ConvertAll(materials, static row => new MaterialRow(row.Id.ToString("D"), row.Name,
            new(row.BaseColorSrgb.X, row.BaseColorSrgb.Y, row.BaseColorSrgb.Z), row.BaseColorTextureId?.ToString("D")));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CatalogFile(1, _projectId.ToString("D"), rows), Json);
        if (payload.Length > MaximumBytes) throw new InvalidDataException("Каталог материалов превышает предел размера.");
        if (beforeCommit != null) await beforeCommit(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        SceneFileStamp stamp;
        try { stamp = await JsonSceneDocumentStore.WriteAtomicAsync(_path, payload, expected.Stamp, MaximumBytes, token).ConfigureAwait(false); }
        catch (InvalidDataException error) { throw new SceneFileConflictException(_path, error); }
        return new(_projectId, materials, stamp);
    }

    private async Task<byte[]?> ReadBytesAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FileStream input;
        try { input = new(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous); }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        await using (input)
            return await JsonSceneDocumentStore.ReadBytesAsync(input, MaximumBytes, token).ConfigureAwait(false);
    }

    private static ProjectMaterial Validate(ProjectMaterial value)
    {
        var name = value.Name?.Trim();
        if (value.Id == Guid.Empty || value.BaseColorTextureId == Guid.Empty || name is not { Length: > 0 and <= MaxNameLength })
            throw new InvalidDataException("Требуются корректные ID и имя материала длиной от 1 до 256 символов.");
        RenderColor.Validate(value.BaseColorSrgb);
        return name == value.Name ? value : value with { Name = name };
    }

    private sealed record CatalogFile([property: JsonRequired] int FormatVersion, [property: JsonRequired] string ProjectId,
        [property: JsonRequired] MaterialRow[] Materials);
    private sealed record MaterialRow([property: JsonRequired] string Id, [property: JsonRequired] string Name,
        [property: JsonRequired] ColorRow BaseColorSrgb, [property: JsonRequired] string? BaseColorTextureId);
    private sealed record ColorRow([property: JsonRequired] float R, [property: JsonRequired] float G, [property: JsonRequired] float B);
}
