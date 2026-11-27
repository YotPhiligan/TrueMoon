using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

internal sealed record ProjectTexture(Guid Id, string Name, string SourcePath, RenderContentHash Revision, int Width, int Height)
{
    internal RenderAssetKey Key(Guid projectId) => new(projectId, Id, RenderContentProfile.BaseColorTextureKindId,
        RenderContentProfile.SchemaVersion, Revision);
}

internal sealed class ProjectTextureCatalogSnapshot(Guid projectId, ProjectTexture[] textures, SceneFileStamp? stamp)
{
    internal Guid ProjectId { get; } = projectId;
    internal IReadOnlyList<ProjectTexture> Textures { get; } = ImmutableArray.CreateRange(textures);
    internal SceneFileStamp? Stamp { get; } = stamp;

    internal ProjectTexture? Find(Guid id)
    {
        foreach (var texture in Textures)
            if (texture.Id == id) return texture;
        return null;
    }
}

internal sealed class ProjectTextureCatalog(ResolvedProject project, Func<ReadOnlyMemory<byte>, BaseColorTextureDefinition>? decode = null)
{
    internal const int MaxNameLength = 256;
    private const int MaximumTextures = 256;
    private const int MaximumCatalogBytes = 1024 * 1024;
    private const int MaximumSourceBytes = 16 * 1024 * 1024;
    private const int MaximumBlobBytes = 20 + RenderContentProfile.MaximumTextureDimension * RenderContentProfile.MaximumTextureDimension * 4;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 8
    };
    private readonly Guid _projectId = project.Description.Id;
    private readonly string _path = Path.Combine(project.ContentDirectory, ".aether-texture-catalog.json");
    private readonly string _dataDirectory = Path.Combine(project.ContentDirectory, ".aether-texture-data");

    internal bool CanEdit => decode != null && OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    internal async Task<ProjectTextureCatalogSnapshot> ReadAsync(CancellationToken token)
    {
        var bytes = await ReadBytesAsync(token).ConfigureAwait(false);
        if (bytes == null) return new(_projectId, [], null);
        try
        {
            var file = JsonSerializer.Deserialize<CatalogFile>(bytes, Json);
            if (file is not { FormatVersion: 1, ProjectId.Length: 36, Textures.Length: <= MaximumTextures } ||
                !Guid.TryParseExact(file.ProjectId, "D", out var id) || id != _projectId)
                throw new InvalidDataException("Неподдерживаемый каталог текстур или другой проект.");

            var textures = new ProjectTexture[file.Textures.Length];
            var ids = new HashSet<Guid>();
            for (var i = 0; i < textures.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var row = file.Textures[i];
                if (row is not { Name: { }, SourcePath: { }, Revision.Length: 64 } ||
                    row.Id?.Length != 36 || !Guid.TryParseExact(row.Id, "D", out var textureId) || textureId == Guid.Empty || !ids.Add(textureId) ||
                    NormalizeName(row.Name) != row.Name || NormalizeSourcePath(row.SourcePath) != row.SourcePath ||
                    !IsDimension(row.Width) || !IsDimension(row.Height))
                    throw new InvalidDataException("Некорректная или повторная запись текстуры.");
                var revision = RenderContentHash.Parse(row.Revision);
                if (revision.IsEmpty || HashText(revision) != row.Revision)
                    throw new InvalidDataException("Некорректная ревизия текстуры.");
                textures[i] = new(textureId, row.Name, row.SourcePath, revision, row.Width, row.Height);
            }
            Array.Sort(textures, static (a, b) => a.Id.CompareTo(b.Id));
            return new(_projectId, textures, JsonSceneDocumentStore.Stamp(bytes));
        }
        catch (Exception error) when (error is JsonException or FormatException)
        {
            throw new InvalidDataException("Некорректный каталог текстур проекта.", error);
        }
    }

    internal async Task<ProjectTextureCatalogSnapshot> ImportAsync(ProjectTextureCatalogSnapshot expected, string name,
        string sourcePath, CancellationToken token)
    {
        CheckProject(expected, token);
        if (expected.Textures.Count >= MaximumTextures) throw new InvalidDataException("Достигнут предел числа текстур проекта.");
        var value = await CaptureAsync(new(Guid.NewGuid(), NormalizeName(name), NormalizeSourcePath(sourcePath), default, 0, 0), token)
            .ConfigureAwait(false);
        return await WriteAsync(expected, value, add: true, token).ConfigureAwait(false);
    }

    internal async Task<ProjectTextureCatalogSnapshot> ReplaceSourceAsync(ProjectTextureCatalogSnapshot expected, Guid id,
        string sourcePath, CancellationToken token)
    {
        CheckProject(expected, token);
        var current = RequireRow(expected, id);
        var value = await CaptureAsync(current with { SourcePath = NormalizeSourcePath(sourcePath) }, token).ConfigureAwait(false);
        return value == current ? await CheckStampAsync(expected, token).ConfigureAwait(false) :
            await WriteAsync(expected, value, add: false, token).ConfigureAwait(false);
    }

    internal async Task<ProjectTextureCatalogSnapshot> RenameAsync(ProjectTextureCatalogSnapshot expected, Guid id,
        string name, CancellationToken token)
    {
        CheckProject(expected, token);
        var current = RequireRow(expected, id);
        name = NormalizeName(name);
        return name == current.Name ? await CheckStampAsync(expected, token).ConfigureAwait(false) :
            await WriteAsync(expected, current with { Name = name }, add: false, token).ConfigureAwait(false);
    }

    internal async Task<BaseColorTextureDefinition> ReadDefinitionAsync(ProjectTexture row, CancellationToken token)
    {
        try
        {
            var bytes = await ImmutableAssetBlob.ReadAsync(BlobPath(row.Revision), MaximumBlobBytes, token).ConfigureAwait(false);
            if (bytes.Length != 20 + row.Width * row.Height * 4 || RenderContentHash.Compute(bytes) != row.Revision ||
                RenderContentCodec.Decode(bytes) is not BaseColorTextureDefinition definition ||
                definition.Width != row.Width || definition.Height != row.Height)
                throw new InvalidDataException("Данные текстуры не соответствуют каталогу.");
            return definition;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or RenderContentException)
        {
            throw new InvalidDataException($"Не удалось прочитать текстуру «{row.Name}» ({row.Id:D}).", error);
        }
    }

    internal static string NormalizeSourcePath(string sourcePath)
    {
        try
        {
            var path = ModelSourcePath.Normalize(sourcePath);
            if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Источником текстуры должен быть файл PNG.");
            return path;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            throw new InvalidDataException("Путь к PNG должен быть относительным путём внутри проекта.", error);
        }
    }

    private async Task<ProjectTexture> CaptureAsync(ProjectTexture row, CancellationToken token)
    {
        if (!CanEdit) throw new NotSupportedException("Импорт текстур требует декодера PNG и Windows x64.");
        _ = ProjectModelPaths.Resolve(project.RootDirectory, row.SourcePath);
        ReadOnlyMemory<byte> bytes;
        using (var files = new ModelSourceFiles(project.RootDirectory, static _ => { }))
        {
            var source = await files.ReadAsync(row.SourcePath, MaximumSourceBytes, token).ConfigureAwait(false);
            await files.RevalidateAsync(token).ConfigureAwait(false);
            bytes = source.Bytes;
        }
        token.ThrowIfCancellationRequested();
        var definition = decode!(bytes);
        var canonical = RenderContentCodec.Encode(definition);
        var revision = definition.Revision;
        var path = BlobPath(revision);
        if (row.Revision == revision)
            await ImmutableAssetBlob.RequireMatchAsync(path, canonical, MaximumBlobBytes, token).ConfigureAwait(false);
        else
            await ImmutableAssetBlob.PublishAsync(path, canonical, MaximumBlobBytes, token).ConfigureAwait(false);
        return row with { Revision = revision, Width = definition.Width, Height = definition.Height };
    }

    private async Task<ProjectTextureCatalogSnapshot> WriteAsync(ProjectTextureCatalogSnapshot expected, ProjectTexture value,
        bool add, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var textures = new ProjectTexture[expected.Textures.Count + (add ? 1 : 0)];
        for (var i = 0; i < expected.Textures.Count; i++)
        {
            var row = expected.Textures[i];
            textures[i] = row.Id == value.Id ? value : row;
        }
        if (add) textures[^1] = value;
        Array.Sort(textures, static (a, b) => a.Id.CompareTo(b.Id));
        var rows = Array.ConvertAll(textures, static row => new TextureRow(row.Id.ToString("D"), row.Name, row.SourcePath,
            HashText(row.Revision), row.Width, row.Height));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new CatalogFile(1, _projectId.ToString("D"), rows), Json);
        if (bytes.Length > MaximumCatalogBytes) throw new InvalidDataException("Каталог текстур превышает предел размера.");
        SceneFileStamp stamp;
        try
        {
            stamp = await JsonSceneDocumentStore.WriteAtomicAsync(_path, bytes, expected.Stamp, MaximumCatalogBytes, token).ConfigureAwait(false);
        }
        catch (InvalidDataException error) { throw new SceneFileConflictException(_path, error); }
        return new(_projectId, textures, stamp);
    }

    private async Task<ProjectTextureCatalogSnapshot> CheckStampAsync(ProjectTextureCatalogSnapshot expected, CancellationToken token)
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

    private void CheckProject(ProjectTextureCatalogSnapshot expected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (expected.ProjectId != _projectId) throw new SceneFileConflictException(_path);
    }

    private static ProjectTexture RequireRow(ProjectTextureCatalogSnapshot expected, Guid id) =>
        expected.Find(id) ?? throw new InvalidDataException("Текстура отсутствует в каталоге.");

    private static string NormalizeName(string name) => name?.Trim() is { Length: > 0 and <= MaxNameLength } valid ? valid :
        throw new InvalidDataException("Имя текстуры должно содержать от 1 до 256 символов.");

    private static bool IsDimension(int value) => value is > 0 and <= RenderContentProfile.MaximumTextureDimension;
    private string BlobPath(RenderContentHash revision) => Path.Combine(_dataDirectory, HashText(revision) + ".tmra");
    private static string HashText(RenderContentHash revision)
    {
        Span<byte> hash = stackalloc byte[32];
        revision.WriteBytes(hash);
        return Convert.ToHexString(hash);
    }

    private sealed record CatalogFile([property: JsonRequired] int FormatVersion, [property: JsonRequired] string ProjectId,
        [property: JsonRequired] TextureRow[] Textures);
    private sealed record TextureRow([property: JsonRequired] string Id, [property: JsonRequired] string Name,
        [property: JsonRequired] string SourcePath, [property: JsonRequired] string Revision,
        [property: JsonRequired] int Width, [property: JsonRequired] int Height);
}
