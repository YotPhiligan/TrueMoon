using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Project-local registry of published model sources, independent of per-scene selection.</summary>
public sealed class ProjectModelCatalog
{
    private const int MaximumModels = 256;
    private const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };
    private readonly ResolvedProject _project;
    private readonly IFactory<ModelImportLedgerSession, ModelImportLedgerArguments> _ledgers;
    private readonly IFactory<ModelSourceCapture, ModelSourceCaptureArguments> _captures;
    private readonly IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>? _reimport;
    private readonly IFactory<ModelImportOperation, ModelImportArguments>? _imports;
    private readonly string _path;

    internal ProjectModelCatalog(ResolvedProject project, IFactory<ModelImportLedgerSession, ModelImportLedgerArguments> ledgers,
        IFactory<ModelSourceCapture, ModelSourceCaptureArguments> captures,
        IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>? reimport,
        IFactory<ModelImportOperation, ModelImportArguments>? imports)
    {
        _project = project; _ledgers = ledgers; _captures = captures; _reimport = reimport; _imports = imports;
        _path = Path.Combine(project.ContentDirectory, ".aether-model-catalog.json");
    }

    /// <summary>Whether this host supplies an import worker and project content publisher.</summary>
    public bool CanImport => _imports != null && _reimport != null;

    /// <summary>Imports a project-local model and registers it after its render content is published.</summary>
    /// <param name="path">Project-relative path to a glTF or GLB source.</param>
    /// <param name="cancellationToken">Cancellation before the catalog commit.</param>
    /// <remarks>Retrying a source reuses its pending ledger. A registered path is already imported.</remarks>
    public async Task ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!CanImport) throw new InvalidOperationException("Первичный импорт моделей не настроен.");
        var relative = ModelSourcePath.Normalize(path);
        if (Path.GetExtension(relative).ToLowerInvariant() is not (".gltf" or ".glb"))
            throw new InvalidDataException("Выберите файл .gltf или .glb внутри проекта.");
        var catalog = await ReadFileAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.Rows.Any(row => SamePath(row, relative))) return;
        if (catalog.Rows.Length >= MaximumModels) throw new InvalidOperationException("Достигнут предел каталога моделей проекта.");

        // The path supplies identity only for the initial import. The ledger owns every subasset ID.
        var name = Path.GetFileName(relative);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"Aether.ProjectModel/1/{_project.Description.Id:N}/{relative.ToUpperInvariant()}"));
        var sourceId = new Guid(hash.AsSpan(0, 16));
        var fullPath = ProjectModelPaths.Resolve(_project.RootDirectory, relative);
        var directory = Path.GetDirectoryName(fullPath)!;
        var ledgerPath = Path.Combine(_project.ContentDirectory, ".aether-model-ledgers", $"{sourceId:N}.tmledger");
        var source = new ModelSourceCaptureArguments(sourceId, directory, name);
        var ledger = new ModelImportLedgerArguments(sourceId, ledgerPath);
        var row = CreateRow(name, source, ledger);
        var ledgerDirectory = Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!);
        if ((ledgerDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Каталог ledger модели должен находиться внутри проекта.");
        // Keep the lease through the catalog commit so a concurrent retry cannot move the pending ledger generation.
        using var lease = new FileStream(ledgerPath + ".import-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if ((await ReadFileAsync(cancellationToken).ConfigureAwait(false)).Rows.Any(value => SamePath(value, relative))) return;
        var settings = _reimport!.Create(new(_project, ledger));
        if (settings?.Worker == null || settings.Publisher?.ProjectId != _project.Description.Id)
            throw new InvalidOperationException("Worker и хранилище импорта должны быть настроены для активного проекта.");
        CapturedModelSource captured;
        await using (var capture = _captures.Create(source) ?? throw new InvalidOperationException("Model capture factory returned null."))
            captured = await capture.CaptureAsync(cancellationToken).ConfigureAwait(false);
        PublishedModelImport published;
        await using (var session = _ledgers.Create(ledger) ?? throw new InvalidOperationException("Model ledger factory returned null."))
        {
            var previous = await session.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (previous?.Candidate.InputRevision == captured.InputRevision)
                published = previous;
            else
            {
                await using var operation = _imports!.Create(new(captured, settings.Worker, settings.ScratchDirectory, settings.Deadline))
                    ?? throw new InvalidOperationException("Model import factory returned null.");
                var candidate = await operation.ImportAsync(cancellationToken).ConfigureAwait(false);
                published = await session.PublishAsync(candidate, previous?.Generation ?? 0, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        var content = await settings.Publisher.PublishAsync(published, cancellationToken).ConfigureAwait(false);
        var exact = SceneModelImports.GetRenderEntries(_project.Description.Id, published).Select(entry => entry.Key);
        if (!content.AssetKeys.ToHashSet().SetEquals(exact))
            throw new InvalidDataException("Хранилище не подтвердило точные ресурсы импорта.");
        await RegisterRowAsync(row, relative, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists registered project imports without opening their ledgers or sources.</summary>
    /// <param name="cancellationToken">Read cancellation.</param>
    /// <returns>Detached model list, sorted by display name and stable identity.</returns>
    public async Task<IReadOnlyList<ProjectModelCatalogItem>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await ReadFileAsync(cancellationToken).ConfigureAwait(false);
        return Array.AsReadOnly(catalog.Rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.SourceId)
            .Select(row => new ProjectModelCatalogItem(row.SourceId, row.Name, row.Directory == "." ? row.RootName : row.Directory + "/" + row.RootName)).ToArray());
    }

    /// <summary>Registers or updates a project model after verifying its committed ledger.</summary>
    /// <param name="name">Display label for the catalog.</param>
    /// <param name="source">Project-local source and capture settings.</param>
    /// <param name="ledger">Project-local ledger for this source.</param>
    /// <param name="cancellationToken">Cancellation before the registry commit.</param>
    /// <returns>Completion of the atomic registry update. A concurrent change can be retried.</returns>
    public async Task RegisterAsync(string name, ModelSourceCaptureArguments source, ModelImportLedgerArguments ledger,
        CancellationToken cancellationToken = default)
    {
        var row = CreateRow(name, source, ledger);
        await ReadPublishedAsync(ledger, cancellationToken).ConfigureAwait(false);
        await RegisterRowAsync(row, null, cancellationToken).ConfigureAwait(false);
    }

    private ModelRow CreateRow(string name, ModelSourceCaptureArguments source, ModelImportLedgerArguments ledger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ledger);
        if (name.Length > 256 || source.SourceId != ledger.SourceId)
            throw new ArgumentException("A catalog model requires a bounded name and matching source and ledger identities.");
        using var capture = _captures.Create(source) ?? throw new InvalidOperationException("Model capture factory returned null.");
        return new ModelRow(source.SourceId, name, ProjectModelPaths.Relative(_project.RootDirectory, source.Directory, allowRoot: true),
            capture.Arguments.RootName, ProjectModelPaths.Relative(_project.RootDirectory, ledger.Path), source.Limits);
    }

    private async Task RegisterRowAsync(ModelRow row, string? importedPath, CancellationToken cancellationToken)
    {
        var catalog = await ReadFileAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.Rows.Any(value => value == row)) return;
        if (importedPath != null && catalog.Rows.Any(value => SamePath(value, importedPath) || value.SourceId == row.SourceId))
            throw new InvalidOperationException("Каталог модели изменился во время импорта. Обновите список и повторите действие.");
        var rows = catalog.Rows.Where(item => item.SourceId != row.SourceId).Append(row).OrderBy(item => item.SourceId).ToArray();
        if (rows.Length > MaximumModels) throw new InvalidOperationException("Достигнут предел каталога моделей проекта.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new CatalogFile(1, _project.Description.Id, rows), Json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("The model catalog exceeds its byte limit.");
        await JsonSceneDocumentStore.WriteAtomicAsync(_path, bytes, catalog.Stamp, MaximumBytes, cancellationToken).ConfigureAwait(false);
    }

    private static bool SamePath(ModelRow row, string path) =>
        string.Equals(row.Directory == "." ? row.RootName : row.Directory + "/" + row.RootName, path, StringComparison.OrdinalIgnoreCase);

    internal async Task<SceneModelImportSelection> LoadAsync(Guid sourceId, CancellationToken token)
    {
        var file = await ReadFileAsync(token).ConfigureAwait(false);
        var row = file.Rows.SingleOrDefault(item => item.SourceId == sourceId)
            ?? throw new InvalidOperationException("Модель отсутствует в каталоге проекта. Обновите список.");
        var source = new ModelSourceCaptureArguments(sourceId,
            ProjectModelPaths.Resolve(_project.RootDirectory, row.Directory, allowRoot: true), row.RootName, row.Limits);
        var ledger = new ModelImportLedgerArguments(sourceId, ProjectModelPaths.Resolve(_project.RootDirectory, row.Ledger));
        var published = await ReadPublishedAsync(ledger, token).ConfigureAwait(false);
        var settings = _reimport?.Create(new(_project, ledger));
        return new(row.Name, new(source, published, published.Candidate.GeometryScale))
            { SavedLedger = ledger, Reimport = settings == null ? null : settings with { Ledger = ledger } };
    }

    private async Task<PublishedModelImport> ReadPublishedAsync(ModelImportLedgerArguments ledger, CancellationToken token)
    {
        await using var session = _ledgers.Create(ledger) ?? throw new InvalidOperationException("Model ledger factory returned null.");
        return await session.OpenAsync(token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Опубликованный импорт модели отсутствует.");
    }

    private async Task<(ModelRow[] Rows, SceneFileStamp? Stamp)> ReadFileAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FileStream input;
        try { input = new(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous); }
        catch (FileNotFoundException) { return ([], null); }
        byte[] bytes;
        await using (input)
            bytes = await JsonSceneDocumentStore.ReadBytesAsync(input, MaximumBytes, token).ConfigureAwait(false);
        try
        {
            var file = JsonSerializer.Deserialize<CatalogFile>(bytes, Json);
            if (file is not { FormatVersion: 1, Models.Length: <= MaximumModels } || file.ProjectId != _project.Description.Id)
                throw new InvalidDataException("Unsupported model catalog or different project identity.");
            var sources = new HashSet<Guid>();
            foreach (var row in file.Models)
            {
                token.ThrowIfCancellationRequested();
                if (row == null || row.SourceId == Guid.Empty || !sources.Add(row.SourceId) || row.Name is not { Length: <= 256 } ||
                    string.IsNullOrWhiteSpace(row.Name) || ModelSourcePath.Normalize(row.RootName) != row.RootName)
                    throw new InvalidDataException("Invalid project model registration.");
                _ = ProjectModelPaths.Resolve(_project.RootDirectory, row.Directory, allowRoot: true);
                _ = ProjectModelPaths.Resolve(_project.RootDirectory, row.Ledger);
                _ = row.Limits?.Snapshot();
            }
            return (file.Models, JsonSceneDocumentStore.Stamp(bytes));
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NotSupportedException)
        { throw new InvalidDataException("Invalid project model catalog.", error); }
    }

    private sealed record CatalogFile(int FormatVersion, Guid ProjectId, ModelRow[] Models);
    private sealed record ModelRow(Guid SourceId, string Name, string Directory, string RootName, string Ledger, ModelSourceCaptureLimits? Limits);
}
