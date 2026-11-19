using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

// This authoring file is keyed by scene identity. The ordinary scene file and published artifacts keep their formats.
internal sealed class SceneModelSelectionStore(ResolvedProject project,
    IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>? reimport)
{
    private const int MaximumLedgers = 16 * 1024 * 1024;
    private const int MaximumBytes = 24 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    internal async Task<Checkpoint?> ReadAsync(Guid sceneId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FileStream input;
        try { input = new(FilePath(sceneId), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous); }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException) { return null; }
        byte[] bytes;
        await using (input)
            bytes = await JsonSceneDocumentStore.ReadBytesAsync(input, MaximumBytes, token).ConfigureAwait(false);
        try
        {
            var file = JsonSerializer.Deserialize<SelectionFile>(bytes, Json);
            if (file is not { FormatVersion: 1, Models.Length: <= 8 } ||
                file.SceneId != sceneId || file.ProjectId != project.Description.Id)
                throw new InvalidDataException("Saved model imports belong to a different scene or project, or have an unsupported format.");
            var models = new List<SceneModelImportSelection>(file.Models.Length);
            var size = 0;
            foreach (var row in file.Models)
            {
                token.ThrowIfCancellationRequested();
                if (row is not { Name.Length: > 0 and <= 256, Published.Length: > 0 } || row.SourceId == Guid.Empty ||
                    (size += row.Published.Length) > MaximumLedgers)
                    throw new InvalidDataException("Invalid saved model import.");
                var published = ModelImportLedgerCodec.Decode(row.Published, row.SourceId);
                var source = new ModelSourceCaptureArguments(row.SourceId,
                    ProjectModelPaths.Resolve(project.RootDirectory, row.SourceDirectory, allowRoot: true), row.RootName, row.Limits);
                var watch = new ModelSourceWatchArguments(source, published, row.GeometryScale)
                    { UseFileSystemNotifications = row.Notifications, PollInterval = TimeSpan.FromTicks(row.PollTicks) };
                var ledger = row.Ledger == null ? null : new ModelImportLedgerArguments(row.SourceId, ProjectModelPaths.Resolve(project.RootDirectory, row.Ledger));
                var settings = ledger == null ? null : reimport?.Create(new(project, ledger));
                models.Add(new(row.Name, watch) { Reimport = settings == null ? null : settings with { Ledger = ledger! }, SavedLedger = ledger });
            }
            token.ThrowIfCancellationRequested();
            return new(models.ToArray(), JsonSceneDocumentStore.Stamp(bytes));
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid saved model import selection.", error); }
    }

    internal Task<SceneFileStamp> WriteAsync(Guid sceneId, IReadOnlyList<SceneModelImportSelection> models,
        SceneFileStamp? expected, CancellationToken token)
    {
        var rows = new List<ModelRow>(models.Count);
        var size = 0;
        foreach (var model in models)
        {
            token.ThrowIfCancellationRequested();
            if (model.Name.Length > 256) throw new InvalidDataException("The saved model label exceeds its size limit.");
            var watch = model.Watch;
            var data = ModelImportLedgerCodec.Encode(watch.Published);
            if ((size += data.Length) > MaximumLedgers) throw new InvalidDataException("Saved model imports exceed their byte limit.");
            var ledger = model.Reimport?.Ledger ?? model.SavedLedger;
            rows.Add(new(watch.Source.SourceId, model.Name, ProjectModelPaths.Relative(project.RootDirectory, watch.Source.Directory, allowRoot: true), watch.Source.RootName,
                watch.Source.Limits, watch.GeometryScale, watch.UseFileSystemNotifications, watch.PollInterval.Ticks,
                ledger == null ? null : ProjectModelPaths.Relative(project.RootDirectory, ledger.Path), data));
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SelectionFile(1, project.Description.Id, sceneId, rows.ToArray()), Json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Saved model imports exceed their byte limit.");
        token.ThrowIfCancellationRequested();
        var path = FilePath(sceneId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return JsonSceneDocumentStore.WriteAtomicAsync(path, bytes, expected, MaximumBytes, token);
    }

    private string FilePath(Guid sceneId) => Path.Combine(project.ContentDirectory, ".aether-model-imports", sceneId.ToString("N") + ".tmimports");

    internal sealed record Checkpoint(SceneModelImportSelection[] Models, SceneFileStamp Stamp);
    private sealed record SelectionFile(int FormatVersion, Guid ProjectId, Guid SceneId, ModelRow[] Models);
    private sealed record ModelRow(Guid SourceId, string Name, string SourceDirectory, string RootName, ModelSourceCaptureLimits? Limits,
        float GeometryScale, bool Notifications, long PollTicks, string? Ledger, byte[] Published);
}
