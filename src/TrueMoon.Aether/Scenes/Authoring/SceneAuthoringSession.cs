using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>One serialized owner of committed document, bounded history and non-evicting operation receipts.</summary>
internal sealed class SceneAuthoringSession : ISceneAuthoringSession
{
    private readonly object _gate = new();
    private readonly SceneResolutionService _resolution;
    private readonly SceneAuthoringOptions _options;
    private SessionData? _data;
    private bool _executing;

    internal SceneAuthoringSession(SceneResolutionService resolution, SceneDocument document, SceneAuthoringOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        _resolution = resolution; _options = options;
        var resolved = Resolve(document, default);
        _data = new(new(document, resolved, document.Revision, 0, 0), [], [], ImmutableDictionary<Guid, Receipt>.Empty);
    }

    public SceneAuthoringState Read() { lock (_gate) return Current.State; }

    public SceneAuthoringResult Apply(TransformEditBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return Execute(Command.Apply, batch.Operation, batch.Edits, cancellationToken);
    }

    public SceneAuthoringResult Undo(AuthoringOperation operation, CancellationToken cancellationToken = default)
        => Execute(Command.Undo, operation, [], cancellationToken);

    public SceneAuthoringResult Redo(AuthoringOperation operation, CancellationToken cancellationToken = default)
        => Execute(Command.Redo, operation, [], cancellationToken);

    private SceneAuthoringResult Execute(Command command, AuthoringOperation operation, ImmutableArray<TransformEdit> edits, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            var data = Current;
            if (_executing) throw new InvalidOperationException("Reentrant authoring mutations are not supported.");
            if (data.Receipts.TryGetValue(operation.Id, out var receipt))
                return receipt.Command == command && receipt.Operation == operation && receipt.Edits.SequenceEqual(edits)
                    ? receipt.Result : Result(operation, SceneAuthoringError.OperationIdConflict, data.State);
            token.ThrowIfCancellationRequested();
            if (data.Receipts.Count == _options.MaxOperations)
                return Result(operation, SceneAuthoringError.OperationLimitReached, data.State);

            SceneAuthoringResult Reject(SceneAuthoringError error, string path = "", string message = "")
            {
                var result = Result(operation, error, data.State, path, message);
                _data = data with { Receipts = data.Receipts.Add(operation.Id, new(command, operation, edits, result)) };
                return result;
            }

            if (operation.ExpectedRevision != data.State.Document.Revision) return Reject(SceneAuthoringError.RevisionConflict);
            if (data.State.Document.Revision == long.MaxValue) return Reject(SceneAuthoringError.RevisionExhausted);
            if (command == Command.Undo && data.Undo.IsEmpty) return Reject(SceneAuthoringError.NoUndo);
            if (command == Command.Redo && data.Redo.IsEmpty) return Reject(SceneAuthoringError.NoRedo);
            if (edits.Length > _options.MaxBatchEdits) return Reject(SceneAuthoringError.BatchLimitExceeded);
            if (command == Command.Apply && ValidateEdits(data.State.Resolved, edits) is { } invalid)
                return Reject(invalid.Error, invalid.Path, invalid.Message);

            _executing = true;
            try
            {
                var revision = data.State.Document.Revision + 1;
                var entry = command == Command.Undo ? data.Undo[^1] : command == Command.Redo ? data.Redo[^1] : null;
                var content = command == Command.Apply ? ApplyEdits(data.State.Document, edits) :
                    command == Command.Undo ? entry!.Before : entry!.After;
                var document = new SceneDocument(content.Id, revision, content.Entities, content.Definitions, content.Schemas);
                ResolvedSceneSnapshot resolved;
                try { resolved = Resolve(document, token); }
                catch (SceneResolutionException error) { token.ThrowIfCancellationRequested(); return Reject(SceneAuthoringError.InvalidScene, error.Path, error.Message); }
                catch (InvalidDataException error) { token.ThrowIfCancellationRequested(); return Reject(SceneAuthoringError.InvalidScene, SceneResolver.ScenePath(document.Id), error.Message); }
                var contentRevision = command == Command.Apply ? revision :
                    command == Command.Undo ? entry!.BeforeContentRevision : entry!.AfterContentRevision;
                var undo = data.Undo; var redo = data.Redo;
                switch (command)
                {
                    case Command.Apply:
                        redo = [];
                        if (_options.MaxHistoryEntries > 0)
                        {
                            undo = undo.Add(new(data.State.Document, document, data.State.ContentRevision, contentRevision, operation));
                            if (undo.Count > _options.MaxHistoryEntries) undo = undo.RemoveAt(0);
                        }
                        break;
                    case Command.Undo: undo = undo.RemoveAt(undo.Count - 1); redo = redo.Add(entry!); break;
                    case Command.Redo: redo = redo.RemoveAt(redo.Count - 1); undo = undo.Add(entry!); break;
                }
                var state = new SceneAuthoringState(document, resolved, contentRevision, undo.Count, redo.Count);
                var result = Result(operation, SceneAuthoringError.None, state);
                var next = new SessionData(state, undo, redo, data.Receipts.Add(operation.Id, new(command, operation, edits, result)));
                token.ThrowIfCancellationRequested();
                // The single publication is the commit point. Cancellation after this cannot change the returned outcome.
                _data = next;
                return result;
            }
            finally { _executing = false; }
        }
    }

    private static (SceneAuthoringError Error, string Path, string Message)? ValidateEdits(ResolvedSceneSnapshot scene, ImmutableArray<TransformEdit> edits)
    {
        var targets = new HashSet<(EntityId, ComponentId)>();
        foreach (var edit in edits)
        {
            var path = $"{SceneResolver.ScenePath(scene.Id)}/entities/{edit.EntityId.Value:D}/components/{edit.ComponentId.Value:D}";
            if (!targets.Add((edit.EntityId, edit.ComponentId))) return (SceneAuthoringError.DuplicateTarget, path, "A batch can address each Transform once.");
            if (!scene.Entities.TryGetValue(edit.EntityId, out var entity)) return (SceneAuthoringError.MissingEntity, path, "The entity does not exist.");
            if (!entity.Components.TryGetValue(edit.ComponentId, out var component)) return (SceneAuthoringError.MissingComponent, path, "The component is not visible.");
            if (component.TypeId != BuiltInComponents.TransformId || component.SchemaVersion != 1)
                return (SceneAuthoringError.UnsupportedComponent, path, "Only built-in v1 Transform replacements are supported.");
            if (!edit.Value.IsValid()) return (SceneAuthoringError.InvalidTransform, path, "Transform values must be representable, with positive scale.");
        }
        return null;
    }

    private static SceneDocument ApplyEdits(SceneDocument source, ImmutableArray<TransformEdit> edits)
    {
        var byEntity = edits.ToLookup(e => e.EntityId);
        var entities = source.Entities.Select(entity =>
        {
            var updates = byEntity[entity.Id].ToArray();
            if (updates.Length == 0) return entity;
            var targets = updates.Select(e => e.ComponentId).ToHashSet();
            var patch = entity.Changes;
            var overrides = patch.PropertyOverrides.Where(p => !targets.Contains(p.ComponentId))
                .Concat(updates.SelectMany(edit => edit.Value.Properties().Select(p =>
                    new ComponentPropertyOverride(edit.ComponentId, p.Key, PropertyValue.From(p.Value)))));
            return new EntityInstance(entity.Id, entity.DefinitionId,
                new EntityPatch(patch.AddedComponents, patch.RemovedComponents, overrides, patch.RestoredComponents), entity.ParentId);
        });
        return new(source.Id, source.Revision, entities, source.Definitions, source.Schemas);
    }

    private ResolvedSceneSnapshot Resolve(SceneDocument document, CancellationToken token)
    {
        var result = _resolution.Resolve(document, token);
        foreach (var schema in result.ComponentSchemas)
            if (schema.TypeId == BuiltInComponents.TransformId) BuiltInComponents.ValidateSchema(schema);
        // Validate resolved local and parent-composed transforms without imposing the preview's entity limit or material policy.
        var worlds = new Dictionary<EntityId, Matrix4x4>();
        foreach (var id in result.HierarchyOrder)
        {
            token.ThrowIfCancellationRequested();
            var entity = result.Entities[id];
            var component = entity.Components.Values.SingleOrDefault(c => c.TypeId == BuiltInComponents.TransformId);
            var local = Matrix4x4.Identity;
            if (component != null)
            {
                double Number(string key) => component.Properties[key].Value.GetNumber();
                var value = new TransformValue(Number("x"), Number("y"), Number("z"), Number("pitch"), Number("yaw"),
                    Number("roll"), Number("sx"), Number("sy"), Number("sz"));
                if (!value.IsValid()) throw new InvalidDataException($"Invalid Transform on entity {id.Value:D}.");
                local = Matrix4x4.CreateScale((float)value.ScaleX, (float)value.ScaleY, (float)value.ScaleZ) *
                    Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll((float)value.Yaw, (float)value.Pitch, (float)value.Roll)) *
                    Matrix4x4.CreateTranslation((float)value.X, (float)value.Y, (float)value.Z);
            }
            var world = entity.ParentId is { } parent ? local * worlds[parent] : local;
            RenderPrimitive.ValidateMatrix(world);
            var determinant = world.GetDeterminant();
            if (!float.IsFinite(determinant) || determinant == 0) throw new InvalidDataException($"Singular/unrepresentable Transform on entity {id.Value:D}.");
            worlds.Add(id, world);
        }
        return result;
    }

    private SessionData Current => _data ?? throw new ObjectDisposedException(nameof(ISceneAuthoringSession));

    /// <summary>Closes the session and releases its history/receipts. Borrowed resolution services are not disposed.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_executing) throw new InvalidOperationException("Cannot dispose a session from its resolution callback.");
            _data = null;
        }
    }

    private static SceneAuthoringResult Result(AuthoringOperation operation, SceneAuthoringError error, SceneAuthoringState state,
        string path = "", string message = "") => new(operation, error, state.Document.Revision, state.ContentRevision, path, message);
    private enum Command { Apply, Undo, Redo }
    private sealed record HistoryEntry(SceneDocument Before, SceneDocument After, long BeforeContentRevision, long AfterContentRevision, AuthoringOperation Operation);
    private sealed record Receipt(Command Command, AuthoringOperation Operation, ImmutableArray<TransformEdit> Edits, SceneAuthoringResult Result);
    private sealed record SessionData(SceneAuthoringState State, ImmutableList<HistoryEntry> Undo, ImmutableList<HistoryEntry> Redo,
        ImmutableDictionary<Guid, Receipt> Receipts);
}
