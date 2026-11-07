namespace TrueMoon.Aether.Ecs;

/// <summary>Reusable bounded FIFO commands. The caller owns disposal and serializes recording/playback.</summary>
/// <remarks>Validation is atomic with respect to World data; unexpected commit failure faults the World, without rollback.</remarks>
public sealed class EntityCommandBuffer : IDisposable
{
    private readonly World _world;
    private readonly int _maximumCommands;
    private readonly Action<int>? _beforeApply;
    private readonly List<RecordedCommand> _commands = [];
    private readonly Guid _id = Guid.NewGuid();
    private Guid _batchId = Guid.NewGuid();
    private int _createdCount;
    private bool _playing;

    /// <summary>Creates an empty caller-owned command buffer without changing World.</summary>
    /// <param name="world">Borrowed World.</param><param name="maximumCommands">Command count limit, 1..1048576.</param>
    public EntityCommandBuffer(World world, int maximumCommands = 4096) : this(world, maximumCommands, null) { }
    internal EntityCommandBuffer(World world, int maximumCommands, Action<int>? beforeApply)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (maximumCommands is < 1 or > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumCommands));
        world.CheckCommandRecording(null);
        _world = world; _maximumCommands = maximumCommands; _beforeApply = beforeApply;
    }
    /// <summary>Gets pending command count; readable when faulted.</summary>
    public int Count { get { CheckDisposed(); return _commands.Count; } }
    /// <summary>Gets whether Reset is required before recording/playback can continue.</summary>
    public bool IsFaulted { get; private set; }
    /// <summary>Gets whether this buffer has been disposed.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Gets fully completed commands in the last playback attempt.</summary>
    public int AppliedCommandCount { get; private set; }
    /// <summary>Gets the last validation/commit failure, cleared by Reset.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>Records an empty entity reservation.</summary><returns>A token for subsequent commands in this batch.</returns>
    public DeferredEntity CreateEntity()
    {
        CheckRecording(null);
        var token = new DeferredEntity(_id, _batchId, _createdCount);
        _commands.Add(new SimpleCommand(CommandKind.Create, token)); _createdCount++;
        return token;
    }
    /// <summary>Records destruction of a live or previously reserved entity.</summary><param name="entity">Target.</param>
    public void DestroyEntity(CommandTarget entity) => Record(new SimpleCommand(CommandKind.Destroy, entity));
    /// <summary>Records strict singleton addition.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void AddComponent<T>(CommandTarget entity, T value) where T : struct
    { World.ValidateComponent<T>(); Record(new ValueCommand<T>(CommandKind.Add, entity, value)); }
    /// <summary>Records replacement of an existing component.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void SetComponent<T>(CommandTarget entity, T value) where T : struct
    { World.ValidateComponent<T>(); Record(new ValueCommand<T>(CommandKind.Set, entity, value)); }
    /// <summary>Records idempotent component removal.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param>
    public void RemoveComponent<T>(CommandTarget entity) where T : struct
    { World.ValidateComponent<T>(); Record(new ValueCommand<T>(CommandKind.Remove, entity, default)); }

    /// <summary>Validates the whole batch, then commits without cancellation callbacks or rollback.</summary>
    /// <param name="cancellationToken">Observed during preflight and immediately before commit.</param>
    /// <returns>Independent reservation mapping. Empty playback reuses an empty result.</returns>
    public CommandPlaybackResult Playback(CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        if (_playing || IsFaulted) throw new InvalidOperationException("Playback requires a recording, nonfaulted buffer.");
        _world.CheckPlayback(); AppliedCommandCount = 0;
        cancellationToken.ThrowIfCancellationRequested();
        if (_commands.Count == 0) return CommandPlaybackResult.Empty;
        _playing = true;
        try
        {
            EntityHandle[] created;
            EntityHandle[] targets;
            try
            {
                created = new EntityHandle[_createdCount]; targets = new EntityHandle[_commands.Count];
                var validation = _world.BeginCommandValidation();
                for (var i = 0; i < _commands.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var command = _commands[i];
                    if (command.Kind == CommandKind.Create)
                        targets[i] = created[command.Target.Deferred.Index] = validation.Create();
                    else
                    {
                        var target = command.Target.IsDeferred ? created[command.Target.Deferred.Index] : command.Target.Live;
                        targets[i] = target; validation.CheckAlive(target);
                        switch (command.Kind)
                        {
                            case CommandKind.Destroy: validation.Destroy(target); break;
                            case CommandKind.Add: validation.Add(target, command.ComponentType!); break;
                            case CommandKind.Set: validation.Set(target, command.ComponentType!); break;
                            case CommandKind.Remove: validation.Remove(target, command.ComponentType!); break;
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { Fail(error); throw; }

            // Allocate the published result before commit, never after World has changed.
            var result = created.Length == 0 ? CommandPlaybackResult.Empty : new CommandPlaybackResult(_id, _batchId, created);
            var nextBatch = Guid.NewGuid();
            try
            {
                for (var i = 0; i < _commands.Count; i++)
                {
                    _beforeApply?.Invoke(i);
                    _commands[i].Apply(_world, targets[i]); AppliedCommandCount++;
                }
            }
            catch (Exception error) { _world.MarkFaulted(error); Fail(error); throw; }
            _commands.Clear(); _createdCount = 0; _batchId = nextBatch;
            return result;
        }
        finally { _playing = false; }
    }

    /// <summary>Discards pending commands/failure and invalidates all recording tokens, without changing World.</summary>
    public void Reset()
    {
        CheckDisposed();
        if (_playing) throw new InvalidOperationException("Cannot reset during playback.");
        var nextBatch = Guid.NewGuid();
        _commands.Clear(); _createdCount = 0; _batchId = nextBatch;
        Failure = null; IsFaulted = false; AppliedCommandCount = 0;
    }
    /// <summary>Discards commands once. Values and borrowed World are never disposed.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        if (_playing) throw new InvalidOperationException("Cannot dispose during playback.");
        IsDisposed = true; _commands.Clear(); _createdCount = 0;
    }

    private void Record(RecordedCommand command)
    {
        CheckRecording(command.ComponentType);
        if (command.Target.IsDeferred)
        {
            var token = command.Target.Deferred;
            if (token.BufferId != _id || token.BatchId != _batchId || (uint)token.Index >= (uint)_createdCount)
                throw new ArgumentException("The deferred entity does not belong to this recording batch.", "entity");
        }
        else if (!_world.IsAlive(command.Target.Live)) throw new ArgumentException("The entity is not alive in this World.", "entity");
        _commands.Add(command);
    }
    private void CheckRecording(Type? type)
    {
        CheckDisposed();
        if (_playing || IsFaulted) throw new InvalidOperationException("Reset a faulted buffer before recording again.");
        _world.CheckCommandRecording(type);
        if (_commands.Count == _maximumCommands) throw new InvalidOperationException("The phase command budget is exhausted.");
    }
    private void CheckDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
    private void Fail(Exception error) { Failure = error; IsFaulted = true; }

    private enum CommandKind { Create, Destroy, Add, Set, Remove }
    private abstract class RecordedCommand(CommandKind kind, CommandTarget target, Type? type)
    {
        internal CommandKind Kind { get; } = kind;
        internal CommandTarget Target { get; } = target;
        internal Type? ComponentType { get; } = type;
        internal abstract void Apply(World world, EntityHandle target);
    }
    private sealed class SimpleCommand(CommandKind kind, CommandTarget target) : RecordedCommand(kind, target, null)
    {
        internal override void Apply(World world, EntityHandle target)
        {
            if (Kind == CommandKind.Destroy) world.DestroyEntity(target);
            else if (world.CreateEntity() != target) throw new InvalidOperationException("Allocator changed after command validation.");
        }
    }
    private sealed class ValueCommand<T>(CommandKind kind, CommandTarget target, T value) : RecordedCommand(kind, target, typeof(T)) where T : struct
    {
        internal override void Apply(World world, EntityHandle target)
        {
            switch (Kind)
            {
                case CommandKind.Add: world.AddComponent(target, value); break;
                case CommandKind.Set: world.SetComponent(target, value); break;
                case CommandKind.Remove: world.RemoveComponent<T>(target); break;
            }
        }
    }
}
