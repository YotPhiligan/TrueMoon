using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Tooling.Documents;

/// <summary>Owns one authoring product, selected file and saved checkpoint. The project/caller owns this factory product.</summary>
/// <remarks>File operations are serialized with fail-fast admission. Save allows edits; candidate loading and close freeze edits.
/// Dispose is terminal forced discard/drain; normal user transitions use CloseAsync with an explicit choice first.</remarks>
public sealed class SceneDocumentSession : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly string _root;
    private readonly ISceneDocumentStore _store;
    private readonly IFactory<ISceneAuthoringSession, SceneDocument> _authoring;
    private readonly List<ISceneAuthoringSession> _retired = [];
    private ISceneAuthoringSession? _current;
    private string? _path;
    private SceneFileStamp? _stamp;
    private long? _saved;
    private long _generation;
    private Guid _openingId;
    private DocumentOperation _operation;
    private CancellationTokenSource? _pending;
    private Task _work = Task.CompletedTask;
    private Task<bool>? _close;
    private bool _closing, _discarding, _closed;
    private Exception? _error;
    internal SceneModelImports? ModelImports { get; init; }
    internal ProjectModelCatalog? ModelCatalog { get; init; }

    /// <summary>Whether this project exposes a catalog of its imported models.</summary>
    public bool HasModelCatalog => ModelCatalog != null;

    /// <summary>Whether the project host enables initial model import.</summary>
    public bool CanImportModel => ModelCatalog?.CanImport == true;

    /// <summary>Imports a project-relative model into the project catalog independently of scene selection.</summary>
    /// <param name="path">Path to the model relative to the project root.</param>
    /// <param name="cancellationToken">Cancellation before catalog publication.</param>
    public Task ImportModelAsync(string path, CancellationToken cancellationToken = default)
    {
        var catalog = ModelCatalog ?? throw new InvalidOperationException("The project model catalog is not configured.");
        return Begin(DocumentOperation.Loading, async token =>
        {
            await catalog.ImportAsync(path, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>Reads the project catalog without selecting models or editing a scene.</summary>
    /// <param name="cancellationToken">Read cancellation.</param>
    /// <returns>Detached project model list.</returns>
    public Task<IReadOnlyList<ProjectModelCatalogItem>> ReadModelCatalogAsync(CancellationToken cancellationToken = default) =>
        (ModelCatalog ?? throw new InvalidOperationException("The project model catalog is not configured.")).ReadAsync(cancellationToken);

    /// <summary>Adds or removes a registered model in the resource selection for one document opening.</summary>
    /// <param name="openingId">Exact current document opening.</param>
    /// <param name="sourceId">Catalog model source identity.</param>
    /// <param name="selected">Whether this model should belong to the scene selection.</param>
    /// <param name="cancellationToken">Cancellation before selection commit.</param>
    /// <returns>Completion of the scene resource selection change.</returns>
    public Task SetCatalogModelSelectedAsync(Guid openingId, Guid sourceId, bool selected, CancellationToken cancellationToken = default)
    {
        var catalog = ModelCatalog ?? throw new InvalidOperationException("The project model catalog is not configured.");
        var models = ModelImports ?? throw new InvalidOperationException("The project model selection is not configured.");
        if (sourceId == Guid.Empty) throw new ArgumentException("A model source identity is required.", nameof(sourceId));
        return Begin(DocumentOperation.Loading, async token =>
        {
            if (openingId == Guid.Empty || Read().OpeningId != openingId)
                throw new InvalidOperationException("The model selection targets a different document opening.");
            await models.SetCatalogSelectionAsync(openingId, sourceId, selected, catalog, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>Creates an initially empty owner borrowing App services.</summary>
    /// <param name="project">Project whose scenes directory bounds all file paths.</param>
    /// <param name="store">Borrowed persistence boundary.</param><param name="authoring">Borrowed factory for owned engine sessions.</param>
    public SceneDocumentSession(ResolvedProject project, ISceneDocumentStore store, IFactory<ISceneAuthoringSession, SceneDocument> authoring)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(authoring);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project.ScenesDirectory));
        _store = store; _authoring = authoring;
    }

    /// <summary>Reads coherent state. Previously returned immutable content remains valid after replacement/close.</summary>
    /// <returns>Current document metadata and engine authoring state.</returns>
    public SceneDocumentState Read()
    {
        lock (_gate)
        {
            var content = _closed ? null : _current?.Read();
            return new(content, _path, _stamp, _saved, _closing ? DocumentOperation.Closing : _operation,
                content != null && (_saved == null || content.ContentRevision != _saved), _error, _closed) { OpeningId = content == null ? Guid.Empty : _openingId };
        }
    }

    /// <summary>Selects exact frozen model resources for the current document opening and starts observing their sources.</summary>
    /// <remarks>Register model source capture before document services. The caller supplies the exact content for these imports.</remarks>
    public Task SelectModelImportsAsync(Guid openingId, IReadOnlyList<SceneModelImportSelection> imports,
        GameBuildRenderContent content, CancellationToken cancellationToken = default)
    {
        var models = ModelImports ?? throw new InvalidOperationException("Model source capture is not configured for this document.");
        return Begin(DocumentOperation.Loading, async token =>
        {
            if (openingId == Guid.Empty || Read().OpeningId != openingId)
                throw new InvalidOperationException("The model selection targets a different document opening.");
            await models.SelectAsync(openingId, imports, content, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>Reads the latest source status of the selected models for the given opening without I/O.</summary>
    public IReadOnlyList<SceneModelImportStatus> ReadModelImports(Guid openingId) => ModelImports?.Read(openingId) ?? [];

    /// <summary>Reimports one selected model under the document operation gate and swaps its frozen resources on success.</summary>
    /// <param name="openingId">Exact current document opening.</param>
    /// <param name="sourceId">Persistent identity of the selected model.</param>
    /// <param name="generation">The expected selected ledger generation.</param>
    /// <param name="cancellationToken">Cancellation before the document selection changes.</param>
    /// <returns>Completion of import, publication and document selection.</returns>
    public Task ReimportModelAsync(Guid openingId, Guid sourceId, long generation, CancellationToken cancellationToken = default)
    {
        var models = ModelImports ?? throw new InvalidOperationException("Model source capture is not configured for this document.");
        return Begin(DocumentOperation.Loading, async token =>
        {
            if (openingId == Guid.Empty || Read().OpeningId != openingId)
                throw new InvalidOperationException("The reimport targets a different document opening.");
            await models.ReimportAsync(openingId, sourceId, generation, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>Rechecks the selected sources before Build and returns their frozen resources, when selected.</summary>
    public async Task<GameBuildRenderContent?> CheckModelImportsAsync(Guid openingId, CancellationToken cancellationToken = default)
    {
        var selection = ModelImports == null ? null : await ModelImports.CheckAsync(openingId, cancellationToken).ConfigureAwait(false);
        return Read() is { Content: { } scene } current && current.OpeningId == openingId
            ? SceneRenderAssets.PrepareBuild(selection?.Assets, scene.Resolved, HasModelCatalog, cancellationToken)
            : selection?.Content;
    }

    internal SceneRenderAssets? ReadRenderAssets(Guid openingId) => ModelImports?.ReadRenderAssets(openingId);

    internal SceneAuthoringResult EditRenderComponents(SceneRenderAssets expected, ComponentEditBatch batch, CancellationToken token)
    {
        lock (_gate)
        {
            VerifyEditable();
            if (!ReferenceEquals(ReadRenderAssets(_openingId), expected))
                throw new InvalidOperationException("Ресурсы сцены изменились. Отмените черновик и повторите выбор.");
            return Current.EditComponents(batch, token);
        }
    }

    /// <summary>Selects a validated new unsaved document after applying the current document's close policy.</summary>
    /// <param name="document">Immutable new content.</param><param name="choice">Decision for existing unsaved content.</param>
    /// <param name="saveCurrentAs">Optional path for saving the previous untitled document.</param><param name="cancellationToken">Preparation cancellation.</param>
    /// <returns>False when unsaved content vetoes the transition.</returns>
    public Task<bool> NewAsync(SceneDocument document, DocumentCloseChoice choice = DocumentCloseChoice.Cancel,
        string? saveCurrentAs = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document); Choice(choice);
        return Begin(DocumentOperation.Loading, token => SwapAsync(document, null, null, choice, saveCurrentAs, token), cancellationToken);
    }

    /// <summary>Prepares a complete file without changing the active document on read/validation/cancellation failure.</summary>
    /// <param name="path">Absolute or scenes-root-relative .aetherscene path.</param><param name="choice">Decision for existing unsaved content.</param>
    /// <param name="saveCurrentAs">Optional path for saving the previous untitled document.</param><param name="cancellationToken">Preparation cancellation.</param>
    /// <returns>False when unsaved content vetoes the transition.</returns>
    public Task<bool> LoadAsync(string path, DocumentCloseChoice choice = DocumentCloseChoice.Cancel,
        string? saveCurrentAs = null, CancellationToken cancellationToken = default)
    {
        path = ScenePath(path); Choice(choice);
        return Begin(DocumentOperation.Loading, async token =>
        {
            var file = await _store.ReadAsync(path, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return await SwapAsync(file.Document, path, file.Stamp, choice, saveCurrentAs, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private async Task<bool> SwapAsync(SceneDocument document, string? path, SceneFileStamp? stamp,
        DocumentCloseChoice choice, string? saveCurrentAs, CancellationToken token)
    {
        ISceneAuthoringSession? candidate = null;
        Exception? failure = null;
        try
        {
            token.ThrowIfCancellationRequested();
            candidate = _authoring.Create(document) ?? throw new InvalidOperationException("Authoring factory returned null.");
            var state = candidate.Read();
            var imports = ModelImports == null ? null : await ModelImports.RestoreAsync(document.Id.Value, token).ConfigureAwait(false);
            if (!await AllowTransitionAsync(choice, saveCurrentAs, token).ConfigureAwait(false)) return false;
            ISceneAuthoringSession? previous;
            lock (_gate)
            {
                token.ThrowIfCancellationRequested(); VerifyOpen();
                if (_closing) throw new OperationCanceledException(token);
                previous = _current; _current = candidate; candidate = null; _generation++; _openingId = Guid.NewGuid();
                _path = path; _stamp = stamp; _saved = path == null ? null : state.ContentRevision;
                ModelImports?.Open(document.Id.Value, _openingId, imports);
            }
            if (previous != null) Release(previous);
            if (ModelImports != null) await ModelImports.StartOpenedWatchesAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            if (candidate != null)
            {
                try { Release(candidate); }
                catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
            }
        }
    }

    /// <summary>Writes the selected file using its last observed fingerprint.</summary>
    /// <param name="cancellationToken">Cancellation before file commit.</param><returns>Exact persisted snapshot receipt.</returns>
    public Task<DocumentSaveResult> SaveAsync(CancellationToken cancellationToken = default)
        => Begin(DocumentOperation.Saving, token => SaveCapturedAsync(null, token), cancellationToken);

    /// <summary>Writes a new path without overwriting an unobserved existing file; failure preserves the current path/checkpoint.</summary>
    /// <param name="path">New .aetherscene path within scenes root.</param><param name="cancellationToken">Cancellation before commit.</param>
    /// <returns>Exact persisted snapshot receipt.</returns>
    public Task<DocumentSaveResult> SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        path = ScenePath(path);
        return Begin(DocumentOperation.Saving, token => SaveCapturedAsync(path, token), cancellationToken);
    }

    private async Task<DocumentSaveResult> SaveCapturedAsync(string? target, CancellationToken token)
    {
        SceneAuthoringState state; SceneFileStamp? expected; long generation;
        lock (_gate)
        {
            token.ThrowIfCancellationRequested();
            state = Current.Read(); generation = _generation;
            target = target == null ? _path ?? throw new InvalidOperationException("Choose SaveAs for an untitled document.") : ScenePath(target);
            expected = string.Equals(target, _path, StringComparison.OrdinalIgnoreCase) ? _stamp : null;
        }
        var stamp = await _store.WriteAsync(target, state.Document, expected, token).ConfigureAwait(false);
        // The store's result is a file-commit receipt; cancellation after that must not invent a failed write.
        lock (_gate)
        {
            if (!_closed && !_discarding && generation == _generation)
            { _path = target; _stamp = stamp; _saved = state.ContentRevision; }
        }
        return new(target, state.Document.Revision, state.ContentRevision, stamp);
    }

    /// <summary>Commits an engine Transform batch; edits remain permitted during a captured snapshot save.</summary>
    /// <param name="batch">Engine operation.</param><param name="cancellationToken">Before-commit cancellation.</param>
    /// <returns>The original engine receipt.</returns>
    public SceneAuthoringResult Apply(TransformEditBatch batch, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.Apply(batch, cancellationToken); } }
    /// <summary>Changes scene parents through the same engine session and history as Transform edits.</summary>
    /// <param name="batch">Atomic parent changes.</param><param name="cancellationToken">Before-commit cancellation.</param>
    /// <returns>Engine receipt.</returns>
    public SceneAuthoringResult Reparent(ReparentBatch batch, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.Reparent(batch, cancellationToken); } }
    /// <summary>Edits component values and presence through the shared engine history.</summary>
    /// <param name="batch">Atomic scalar and presence changes.</param><param name="cancellationToken">Before-commit cancellation.</param>
    /// <returns>The engine receipt.</returns>
    public SceneAuthoringResult EditComponents(ComponentEditBatch batch, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.EditComponents(batch, cancellationToken); } }
    /// <summary>Commits names/ordered placements through the shared engine history.</summary>
    /// <param name="batch">Atomic hierarchy intent.</param><param name="cancellationToken">Before-commit cancellation.</param><returns>Engine receipt.</returns>
    public SceneAuthoringResult EditHierarchy(HierarchyEditBatch batch, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.EditHierarchy(batch, cancellationToken); } }
    /// <summary>Creates, deletes or duplicates instances through the existing owned engine session.</summary>
    /// <param name="batch">Frozen entity request.</param><param name="cancellationToken">Before-commit cancellation.</param><returns>Shared engine receipt.</returns>
    public SceneAuthoringResult EditEntities(EntityEditBatch batch, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.EditEntities(batch, cancellationToken); } }

    internal SceneAuthoringResult PlaceSelectedModel(SceneDocument expected, Guid openingId, Guid sourceId,
        long generation, CancellationToken cancellationToken) => Edit(expected, _ =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var imports = ModelImports ?? throw new InvalidOperationException("The project model catalog is not configured.");
        var model = imports.RequirePlacementSelection(openingId, sourceId, generation);
        var placed = ImportedModelScene.Place(expected, model.Watch.Published);
        var name = EntityInstance.IsValidName(model.Name) ? model.Name : null;
        var entities = placed.Entities.Skip(expected.Entities.Length).Select(entity =>
            new EntityInstance(entity.Id, entity.DefinitionId, entity.Changes, entity.ParentId, name, entity.SiblingOrder));
        var batch = new CreateEntitiesBatch(new(Guid.NewGuid(), expected.Revision, "editor"), entities,
            placed.Definitions.Skip(expected.Definitions.Length), placed.Schemas.Skip(expected.Schemas.Length));
        return Current.EditEntities(batch, cancellationToken);
    });

    /// <summary>Restores prior content using the existing engine history.</summary>
    /// <param name="operation">Engine operation identity.</param><param name="cancellationToken">Before-commit cancellation.</param>
    /// <returns>Engine receipt.</returns>
    public SceneAuthoringResult Undo(AuthoringOperation operation, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.Undo(operation, cancellationToken); } }
    /// <summary>Restores undone content using the existing engine history.</summary>
    /// <param name="operation">Engine operation identity.</param><param name="cancellationToken">Before-commit cancellation.</param>
    /// <returns>Engine receipt.</returns>
    public SceneAuthoringResult Redo(AuthoringOperation operation, CancellationToken cancellationToken = default)
    { lock (_gate) { VerifyEditable(); return Current.Redo(operation, cancellationToken); } }

    // Keep document identity validation and the UI mutation under the same owner gate:
    // a direct/headless replacement must not slip between the check and Apply/Undo/Redo.
    internal SceneAuthoringResult Edit(SceneDocument expected, Func<SceneDocumentSession, SceneAuthoringResult> action)
    {
        lock (_gate)
        {
            VerifyEditable();
            if (!ReferenceEquals(Current.Read().Document, expected))
                throw new InvalidOperationException("The document changed; cancel the draft and reload it.");
            return action(this);
        }
    }

    internal Task RunCapturedAsync(SceneAuthoringState expected, Func<CancellationToken, Task> action, CancellationToken caller)
    {
        lock (_gate)
        {
            VerifyOpen();
            if (_pending != null || _closing || _retired.Count != 0 || !ReferenceEquals(Current.Read(), expected))
                throw new OperationCanceledException("Scene startup was superseded by document work.");
            caller.ThrowIfCancellationRequested();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = cancellation; _operation = DocumentOperation.Runtime; _error = null; _work = completion.Task;
            Task work;
            try { work = action(cancellation.Token); }
            catch (Exception error)
            {
                _pending = null; _operation = DocumentOperation.None; _error = error; cancellation.Dispose();
                completion.TrySetResult(); throw; // No runtime admission occurred; preserve synchronous rejection.
            }
            _ = CompleteAsync(); return completion.Task;
            async Task CompleteAsync()
            {
                Exception? failure = null;
                try { await work.ConfigureAwait(false); }
                catch (Exception error) { failure = error; }
                finally { lock (_gate) { _pending = null; _operation = DocumentOperation.None; _error = failure; cancellation.Dispose(); } }
                if (failure is OperationCanceledException cancelled) completion.TrySetCanceled(cancelled.CancellationToken);
                else if (failure != null) completion.TrySetException(failure); else completion.TrySetResult();
            }
        }
    }

    private async Task<bool> AllowTransitionAsync(DocumentCloseChoice choice, string? saveAs, CancellationToken token)
    {
        if (!Read().IsDirty) return true;
        if (choice == DocumentCloseChoice.Cancel) return false;
        if (choice == DocumentCloseChoice.Save)
        {
            await SaveCapturedAsync(saveAs, token).ConfigureAwait(false);
            return !Read().IsDirty;
        }
        return true;
    }

    private Task<T> Begin<T>(DocumentOperation operation, Func<CancellationToken, Task<T>> action, CancellationToken caller)
    {
        TaskCompletionSource<T> completion; CancellationTokenSource pending;
        lock (_gate)
        {
            VerifyOpen(); caller.ThrowIfCancellationRequested();
            if (_pending != null || _closing || _retired.Count != 0) throw new InvalidOperationException("Document IO/cleanup is already active; finish or retry Close first.");
            pending = CancellationTokenSource.CreateLinkedTokenSource(caller);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _work = completion.Task;
            _pending = pending; _operation = operation; _error = null;
        }
        _ = Task.Run(async () =>
        {
            T result = default!; Exception? failure = null; var token = pending.Token;
            try { token.ThrowIfCancellationRequested(); result = await action(token).ConfigureAwait(false); }
            catch (Exception error) { failure = error; }
            finally { lock (_gate) { _pending = null; _operation = DocumentOperation.None; _error = failure; pending.Dispose(); } }
            if (failure is OperationCanceledException) completion.TrySetCanceled(token);
            else if (failure != null) completion.TrySetException(failure); else completion.TrySetResult(result);
        });
        return completion.Task;
    }

    /// <summary>Applies Save/Discard/Cancel, fences edits/IO, drains accepted work, then releases the current authoring product.</summary>
    /// <param name="choice">Unsaved content policy.</param><param name="saveAsPath">Target for an untitled document when saving.</param>
    /// <param name="cancellationToken">May veto final close; accepted IO is always drained before returning.</param>
    /// <returns>False for an unsaved-content veto; failure preserves the active document for recovery.</returns>
    public Task<bool> CloseAsync(DocumentCloseChoice choice = DocumentCloseChoice.Cancel, string? saveAsPath = null,
        CancellationToken cancellationToken = default)
    {
        Choice(choice);
        TaskCompletionSource<bool> completion; Task work; Exception? cancellationFailure = null;
        lock (_gate)
        {
            if (_closing) return _close!;
            cancellationToken.ThrowIfCancellationRequested();
            if (!_closed && Read().IsDirty && choice == DocumentCloseChoice.Cancel) return Task.FromResult(false);
            if (_closed) choice = DocumentCloseChoice.Discard;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _close = completion.Task;
            _closing = true; _discarding = choice != DocumentCloseChoice.Save; work = _work;
            if (_discarding || _operation is DocumentOperation.Loading or DocumentOperation.Runtime)
                try { _pending?.Cancel(); } catch (Exception error) { cancellationFailure = error; }
        }
        _ = CompleteCloseAsync(work, choice, saveAsPath, cancellationToken, cancellationFailure, completion);
        return completion.Task;
    }

    private async Task CompleteCloseAsync(Task work, DocumentCloseChoice choice, string? saveAs, CancellationToken token,
        Exception? failure, TaskCompletionSource<bool> completion)
    {
        var allowed = false;
        try
        {
            try { await work.ConfigureAwait(false); } catch { /* The IO caller owns its operation error. */ }
            token.ThrowIfCancellationRequested();
            if (failure != null) throw failure;
            allowed = _closed || await AllowTransitionAsync(choice, saveAs, token).ConfigureAwait(false);
            if (allowed)
            {
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    foreach (var retired in _retired.ToArray()) { retired.Dispose(); _retired.Remove(retired); }
                    _current?.Dispose(); _current = null; _path = null; _stamp = null; _saved = null; _generation++; _openingId = Guid.Empty;
                }
                if (ModelImports != null) await ModelImports.ClearAsync().ConfigureAwait(false);
            }
        }
        catch (Exception error) { failure = error; }
        finally { lock (_gate) { _closing = false; _discarding = false; _error = failure; } }
        if (failure is OperationCanceledException) completion.TrySetCanceled(token);
        else if (failure != null) completion.TrySetException(failure); else completion.TrySetResult(allowed);
    }

    /// <summary>Terminal owner cleanup: closes admission and discards memory after actual IO drain. Normal UI transitions use CloseAsync first.</summary>
    /// <returns>Shared cleanup completion, retryable after a cleanup fault.</returns>
    public ValueTask DisposeAsync()
    { lock (_gate) _closed = true; return new(CloseAsync(DocumentCloseChoice.Discard)); }
    /// <summary>Completes the same terminal owned cleanup synchronously.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    // Project commit calls this under its own gate, immediately before publishing the new project.
    // The same gate as Apply/Undo/Redo prevents a new unsaved edit between the cleanliness check and disposal.
    internal bool TryFenceClean()
        => TryFence(false, false, null);

    // An explicit project decision may discard only the captured document, never a later edit/replacement.
    internal bool TryFenceForProject(SceneDocument? expected, bool discard)
        => TryFence(discard, true, expected);

    private bool TryFence(bool discard, bool checkExpected, SceneDocument? expected)
    {
        lock (_gate)
        {
            if (_pending != null || _closing || _retired.Count != 0) return false;
            var state = Read();
            if (!discard && state.IsDirty ||
                checkExpected && !ReferenceEquals(state.Content?.Document, expected)) return false;
            _closed = true;
            return true;
        }
    }
    private ISceneAuthoringSession Current => _current ?? throw new InvalidOperationException("No scene document is open.");
    private void VerifyOpen() => ObjectDisposedException.ThrowIf(_closed, this);
    private void VerifyEditable()
    { VerifyOpen(); if (_closing || _operation is DocumentOperation.Loading or DocumentOperation.Runtime || _retired.Count != 0) throw new InvalidOperationException("The document is transitioning."); }
    private void Release(ISceneAuthoringSession product)
    { try { product.Dispose(); } catch { lock (_gate) _retired.Add(product); throw; } }
    private static void Choice(DocumentCloseChoice choice)
    { if (!Enum.IsDefined(choice)) throw new ArgumentOutOfRangeException(nameof(choice)); }
    private string ScenePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var result = Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar), _root);
        var prefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !result.EndsWith(".aetherscene", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Scene path must be an .aetherscene file within the project's scenes directory.", nameof(path));
        return result;
    }
}
