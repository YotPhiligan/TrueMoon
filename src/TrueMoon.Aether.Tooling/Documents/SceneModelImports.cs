using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Tooling.Documents;

// Mutations run through the document's exclusive operation and close gates.
internal sealed class SceneModelImports(Guid projectId, IFactory<ModelSourceCapture, ModelSourceCaptureArguments> captures,
    IFactory<ModelSourceWatch, ModelSourceWatchArguments> watches)
{
    private readonly List<ModelSourceWatch> _retired = [];
    private volatile Selection? _current;

    internal async Task SelectAsync(Guid openingId, IReadOnlyList<SceneModelImportSelection> imports,
        GameBuildRenderContent content, CancellationToken token)
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
            foreach (var material in published.Materials)
                Require(Key(material.AssetId, material.Definition));
            foreach (var mesh in published.Meshes)
            {
                var geometry = Key(mesh.AssetId, mesh.Geometry);
                var material = published.Materials.Single(item => item.AssetId == mesh.MaterialId);
                var bindings = new MaterialBindingSetDefinition(geometry, [new(mesh.SlotId, Key(material.AssetId, material.Definition))]);
                Require(geometry);
                Require(Key(mesh.BindingSetId, bindings));
            }
        }
        if (keys.Count != 0) throw new InvalidDataException("Frozen render content contains resources from outside the selected imports.");

        token.ThrowIfCancellationRequested();
        // Reuse the App-wide watch budget on reselection. The old baseline remains authoritative on failure.
        var previous = _current;
        if (previous?.Watches.Length > 0)
        {
            _retired.AddRange(previous.Watches);
            _current = previous with { Watches = [] };
        }
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
            _current = new(openingId, selected, pending.ToArray(), content);
        }
        catch (Exception failure)
        {
            _retired.AddRange(pending);
            try { await DrainRetiredAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }

        RenderAssetKey Key(Guid id, RenderAssetDefinition definition) =>
            new(projectId, id, definition.KindId, definition.SchemaVersion, definition.Revision);
        void Require(RenderAssetKey key)
        {
            if (!keys.Remove(key)) throw new InvalidDataException("Frozen render content does not match the exact selected imports.");
        }
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
        return DrainRetiredAsync();
    }

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
                    new SceneModelImportStatus(model.Watch.Source.SourceId, model.Name, GetStatus(i))).ToArray());
                return _statuses;
            }
        }

        private ModelImportFreshnessStatus? GetStatus(int index) =>
            Watches.Length == 0 ? null : Watches[index].Read()?.Freshness.Status;
    }
}
