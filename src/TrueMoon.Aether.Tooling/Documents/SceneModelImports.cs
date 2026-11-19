using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

// Mutations run through the document's exclusive operation and close gates.
internal sealed class SceneModelImports(Guid projectId, IFactory<ModelSourceCapture, ModelSourceCaptureArguments> captures,
    IFactory<ModelSourceWatch, ModelSourceWatchArguments> watches,
    IFactory<ModelImportOperation, ModelImportArguments>? importFactory,
    IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>? ledgerFactory,
    SceneModelSelectionStore? store)
{
    private readonly List<ModelSourceWatch> _retired = [];
    private volatile Selection? _current;
    private Guid _sceneId;
    private SceneFileStamp? _stamp;

    internal async Task SelectAsync(Guid openingId, IReadOnlyList<SceneModelImportSelection> imports,
        GameBuildRenderContent content, CancellationToken token)
    {
        var selected = Validate(imports, content);
        token.ThrowIfCancellationRequested();
        // Reuse the App-wide watch budget on reselection. The old baseline remains authoritative on failure.
        var previous = _current;
        if (previous?.Watches.Length > 0)
        {
            _retired.AddRange(previous.Watches);
            _current = new(previous.OpeningId, previous.Imports, [], previous.Content);
        }
        ModelSourceWatch[] pending = [];
        try
        {
            pending = await StartWatchesAsync(selected, token).ConfigureAwait(false);
            await PersistAsync(selected, token).ConfigureAwait(false);
            _current = new(openingId, selected, pending, content);
        }
        catch (Exception failure)
        {
            _retired.AddRange(pending);
            try { await StartOpenedWatchesAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    private SceneModelImportSelection[] Validate(IReadOnlyList<SceneModelImportSelection> imports, GameBuildRenderContent content)
    {
        ArgumentNullException.ThrowIfNull(imports);
        ArgumentNullException.ThrowIfNull(content);
        if (imports.Count is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(imports));
        var selected = imports.ToArray();
        var sources = new HashSet<Guid>();
        var keys = content.AssetKeys.ToHashSet();
        foreach (var model in selected)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentException.ThrowIfNullOrWhiteSpace(model.Name);
            var watch = model.Watch ?? throw new ArgumentException("Specify a model source and published generation.", nameof(imports));
            var published = watch.Published;
            if (published == null || watch.Source == null || watch.Source.SourceId != published.Candidate.SourceId ||
                !sources.Add(watch.Source.SourceId))
                throw new ArgumentException("Each selected import must name a distinct matching model source.", nameof(imports));
            if (model.Reimport is { } reimport)
            {
                if (importFactory == null || ledgerFactory == null)
                    throw new InvalidOperationException("Register model import before configuring document reimport.");
                if (reimport.Ledger?.SourceId != watch.Source.SourceId || reimport.Worker == null || reimport.Publisher?.ProjectId != projectId)
                    throw new ArgumentException("Reimport setup must match the selected source and project and specify a worker.", nameof(imports));
            }
            foreach (var key in Keys(published))
                if (!keys.Remove(key)) throw new InvalidDataException("Frozen render content does not match the exact selected imports.");
        }
        if (keys.Count != 0) throw new InvalidDataException("Frozen render content contains resources from outside the selected imports.");
        return selected;
    }

    internal async Task<RestoredSelection?> RestoreAsync(Guid sceneId, CancellationToken token)
    {
        if (store == null || await store.ReadAsync(sceneId, token).ConfigureAwait(false) is not { } checkpoint) return null;
        if (checkpoint.Models.Length == 0) return new([], null, checkpoint.Stamp);
        var content = new GameBuildRenderContent(projectId, checkpoint.Models.SelectMany(model => Entries(model.Watch.Published)).ToArray());
        var selected = Validate(checkpoint.Models, content);
        foreach (var model in selected)
        {
            token.ThrowIfCancellationRequested();
            ModelSourceWatchHost.ValidateArguments(model.Watch);
            using var capture = captures.Create(model.Watch.Source) ?? throw new InvalidOperationException("Model capture factory returned null.");
        }
        return new(selected, content, checkpoint.Stamp);
    }

    // Called with the document's publication gate held after all candidate reads and validation.
    internal void Open(Guid sceneId, Guid openingId, RestoredSelection? restored)
    {
        if (_current is { } previous) _retired.AddRange(previous.Watches);
        _sceneId = sceneId;
        _stamp = restored?.Stamp;
        _current = restored?.Content is not { } content ? null : new(openingId, restored.Imports, [], content);
    }

    internal async Task SetCatalogSelectionAsync(Guid openingId, Guid sourceId, bool selected, ProjectModelCatalog catalog, CancellationToken token)
    {
        var current = _current?.Imports ?? [];
        var contains = current.Any(model => model.Watch.Source.SourceId == sourceId);
        if (contains == selected) return;
        SceneModelImportSelection[] next;
        if (selected)
        {
            if (current.Length == 8) throw new InvalidOperationException("Для сцены можно выбрать до восьми моделей.");
            next = [.. current, await catalog.LoadAsync(sourceId, token).ConfigureAwait(false)];
        }
        else next = current.Where(model => model.Watch.Source.SourceId != sourceId).ToArray();

        if (next.Length != 0)
        {
            var content = new GameBuildRenderContent(projectId, next.SelectMany(model => Entries(model.Watch.Published)).ToArray());
            await SelectAsync(openingId, next, content, token).ConfigureAwait(false);
            return;
        }
        await PersistAsync([], token).ConfigureAwait(false);
        if (_current is { } previous) _retired.AddRange(previous.Watches);
        _current = null;
        await DrainRetiredAsync().ConfigureAwait(false);
    }

    internal async Task StartOpenedWatchesAsync()
    {
        if (_current is not { Watches.Length: 0 } opened)
        {
            await DrainRetiredAsync().ConfigureAwait(false);
            return;
        }
        var active = await StartWatchesAsync(opened.Imports, CancellationToken.None).ConfigureAwait(false);
        _current = new(opened.OpeningId, opened.Imports, active, opened.Content);
    }

    private async Task<ModelSourceWatch[]> StartWatchesAsync(SceneModelImportSelection[] selected, CancellationToken token)
    {
        await DrainRetiredAsync().ConfigureAwait(false);
        var pending = new List<ModelSourceWatch>(selected.Length);
        try
        {
            foreach (var model in selected)
            {
                token.ThrowIfCancellationRequested();
                pending.Add(watches.Create(model.Watch) ?? throw new InvalidOperationException("Model watch factory returned null."));
            }
            token.ThrowIfCancellationRequested();
            return pending.ToArray();
        }
        catch (Exception failure)
        {
            _retired.AddRange(pending);
            try { await DrainRetiredAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    private async Task PersistAsync(SceneModelImportSelection[] selected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (store != null) _stamp = await store.WriteAsync(_sceneId, selected, _stamp, token).ConfigureAwait(false);
    }

    internal async Task ReimportAsync(Guid openingId, Guid sourceId, long generation, CancellationToken token)
    {
        if (_current is not { } selection || selection.OpeningId != openingId)
            throw new InvalidOperationException("The reimport targets a different document opening.");
        var index = Array.FindIndex(selection.Imports, model => model.Watch.Source.SourceId == sourceId);
        if (index < 0 || selection.Imports[index] is not { Reimport: { } setup } model || model.Watch.Published.Generation != generation)
            throw new InvalidOperationException("The selected model generation changed or cannot be reimported.");
        var watch = model.Watch;
        var previous = watch.Published;
        CapturedModelSource source;
        await using (var capture = captures.Create(watch.Source) ?? throw new InvalidOperationException("Model capture factory returned null."))
            source = await capture.CaptureAsync(token).ConfigureAwait(false);

        await using var ledger = ledgerFactory!.Create(setup.Ledger)
            ?? throw new InvalidOperationException("Model ledger factory returned null.");
        var published = await ledger.OpenAsync(token).ConfigureAwait(false)
            ?? throw new InvalidDataException("The selected model ledger is missing.");
        if (published.Generation == previous.Generation && published.Candidate.Revision == previous.Candidate.Revision)
        {
            if (MatchesInput(published)) return;
            ModelImportCandidate candidate;
            await using (var operation = importFactory!.Create(new(source, setup.Worker, setup.ScratchDirectory, setup.Deadline)
                { GeometryScale = watch.GeometryScale }) ?? throw new InvalidOperationException("Model import factory returned null."))
                candidate = await operation.ImportAsync(token).ConfigureAwait(false);
            RequireCompatible(previous, PublishedModelImport.Prepare(candidate, previous, generation, null));
            published = await ledger.PublishAsync(candidate, generation, cancellationToken: token).ConfigureAwait(false);
        }
        else if (published.Generation != checked(previous.Generation + 1) || !MatchesInput(published))
            throw new InvalidOperationException("Поколение импорта изменилось. Выберите актуальный импорт модели.");

        // A retry after the ledger commit can finish publishing precisely the same captured input.
        RequireCompatible(previous, published);
        var updated = await setup.Publisher.PublishAsync(published, token).ConfigureAwait(false);
        if (!Keys(published).ToHashSet().SetEquals(updated.AssetKeys))
            throw new InvalidDataException("The content publisher returned resources from a different import.");
        var head = await ledger.OpenAsync(token).ConfigureAwait(false);
        if (head?.Generation != published.Generation || head.Candidate.Revision != published.Candidate.Revision)
            throw new InvalidOperationException("Поколение импорта изменилось во время публикации ресурсов.");
        var content = selection.Content.WithReplacedAssets(Keys(previous), updated);
        var selected = selection.Imports.ToArray();
        selected[index] = model with { Watch = watch with { Published = published } };
        await ReplaceWatchAsync(selection, selected, index, content, token).ConfigureAwait(false);

        bool MatchesInput(PublishedModelImport value) => value.Candidate.InputRevision == source.InputRevision &&
            value.Candidate.GeometryScale == watch.GeometryScale;
    }

    private async Task ReplaceWatchAsync(Selection previous, SceneModelImportSelection[] selected, int index,
        GameBuildRenderContent content, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (previous.Watches.Length == 0)
        {
            await SelectAsync(previous.OpeningId, selected, content, token).ConfigureAwait(false);
            return;
        }
        var active = previous.Watches.ToArray();
        await active[index].DisposeAsync().ConfigureAwait(false);
        ModelSourceWatch? replacement = null;
        try
        {
            token.ThrowIfCancellationRequested();
            replacement = watches.Create(selected[index].Watch) ?? throw new InvalidOperationException("Model watch factory returned null.");
            await PersistAsync(selected, token).ConfigureAwait(false);
            active[index] = replacement;
            _current = new(previous.OpeningId, selected, active, content);
        }
        catch (Exception failure)
        {
            try
            {
                if (replacement != null)
                {
                    _retired.Add(replacement);
                    await DrainRetiredAsync().ConfigureAwait(false);
                }
                active[index] = watches.Create(previous.Imports[index].Watch)
                    ?? throw new InvalidOperationException("Model watch factory returned null.");
                _current = new(previous.OpeningId, previous.Imports, active, previous.Content);
            }
            catch (Exception recovery) { throw new AggregateException(failure, recovery); }
            throw;
        }
    }

    private static void RequireCompatible(PublishedModelImport previous, PublishedModelImport next)
    {
        if (!previous.Bindings.Select(value => (value.Key, value.Id)).ToHashSet()
                .SetEquals(next.Bindings.Select(value => (value.Key, value.Id))) ||
            !previous.Instances.ToHashSet().SetEquals(next.Instances) ||
            !previous.Meshes.Select(mesh => (mesh.AssetId, mesh.BindingSetId, mesh.SlotId, mesh.MaterialId)).ToHashSet()
                .SetEquals(next.Meshes.Select(mesh => (mesh.AssetId, mesh.BindingSetId, mesh.SlotId, mesh.MaterialId))))
            throw new InvalidDataException("Реимпорт изменяет состав, привязки или размещение модели в сцене.");
    }

    private IEnumerable<RenderAssetKey> Keys(PublishedModelImport published) => Entries(published).Select(entry => entry.Key);

    private IEnumerable<RenderAssetEntry> Entries(PublishedModelImport published)
    {
        foreach (var material in published.Materials) yield return new(Key(material.AssetId, material.Definition), material.Definition);
        foreach (var mesh in published.Meshes)
        {
            var geometry = Key(mesh.AssetId, mesh.Geometry);
            var material = published.Materials.Single(item => item.AssetId == mesh.MaterialId);
            var bindings = new MaterialBindingSetDefinition(geometry, [new(mesh.SlotId, Key(material.AssetId, material.Definition))]);
            yield return new(geometry, mesh.Geometry);
            yield return new(Key(mesh.BindingSetId, bindings), bindings);
        }

        RenderAssetKey Key(Guid id, RenderAssetDefinition definition) =>
            new(projectId, id, definition.KindId, definition.SchemaVersion, definition.Revision);
    }

    internal IReadOnlyList<SceneModelImportStatus> Read(Guid openingId) =>
        _current is { } selection && selection.OpeningId == openingId ? selection.Read() : [];

    internal async Task<GameBuildRenderContent?> CheckAsync(Guid openingId, CancellationToken token)
    {
        var selection = _current;
        if (selection == null || selection.OpeningId != openingId) return null;
        foreach (var model in selection.Imports)
        {
            await using var capture = captures.Create(model.Watch.Source)
                ?? throw new InvalidOperationException("Model capture factory returned null.");
            var result = await capture.CheckFreshnessAsync(model.Watch.Published, model.Watch.GeometryScale, token).ConfigureAwait(false);
            if (result.Status == ModelImportFreshnessStatus.Stale)
                throw new InvalidOperationException($"Модель «{model.Name}» устарела. Обновите импорт перед сборкой.");
            if (result.Status == ModelImportFreshnessStatus.Unavailable)
                throw new InvalidOperationException($"Исходники модели «{model.Name}» недоступны.", result.CaptureError);
        }
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(selection, _current)) throw new OperationCanceledException("Model import selection changed during capture.", token);
        return selection.Content;
    }

    internal Task ClearAsync()
    {
        if (_current is { } selection) _retired.AddRange(selection.Watches);
        _current = null;
        _sceneId = Guid.Empty;
        _stamp = null;
        return DrainRetiredAsync();
    }

    internal sealed record RestoredSelection(SceneModelImportSelection[] Imports, GameBuildRenderContent? Content, SceneFileStamp Stamp);

    private async Task DrainRetiredAsync()
    {
        // Remove only after actual cleanup. The next owner operation can retry a failed disposal.
        foreach (var watch in _retired.ToArray())
        {
            await watch.DisposeAsync().ConfigureAwait(false);
            _retired.Remove(watch);
        }
    }

    private sealed record Selection(Guid OpeningId, SceneModelImportSelection[] Imports, ModelSourceWatch[] Watches,
        GameBuildRenderContent Content)
    {
        private readonly Lock _gate = new();
        private IReadOnlyList<SceneModelImportStatus> _statuses = [];

        internal IReadOnlyList<SceneModelImportStatus> Read()
        {
            lock (_gate)
            {
                if (_statuses.Count == Imports.Length)
                {
                    var matches = true;
                    for (var i = 0; i < _statuses.Count; i++)
                        matches &= _statuses[i].Status == GetStatus(i);
                    if (matches) return _statuses;
                }
                _statuses = Array.AsReadOnly(Imports.Select((model, i) =>
                    new SceneModelImportStatus(model.Watch.Source.SourceId, model.Name, GetStatus(i))
                    { CanReimport = model.Reimport != null, Generation = model.Watch.Published.Generation }).ToArray());
                return _statuses;
            }
        }

        private ModelImportFreshnessStatus? GetStatus(int index) =>
            Watches.Length == 0 ? null : Watches[index].Read()?.Freshness.Status;
    }
}
