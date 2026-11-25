using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Runtime.Ecs;

/// <summary>Reusable bounded FIFO commands. The caller owns disposal and serializes recording/playback.</summary>
/// <remarks>Validation is atomic with respect to World data; unexpected commit failure faults the World, without rollback.</remarks>
public sealed class EntityCommandBuffer : IDisposable
{
    private readonly World _world;
    private readonly int _maximumCommands;
    private readonly Action<int>? _beforeApply;
    private readonly List<RecordedCommand> _commands = [];
    private readonly List<EntityHandle> _createdHandles = [];
    private readonly Dictionary<Type, ValueAction> _valueActions = [];
    private readonly List<Type> _collectibleActions = [];
    private const int PayloadBlockSize = 16 * 1024;
    private readonly List<PayloadBlock> _payload = [];
    private int _payloadBlock;
    private EntityHandle[] _targets = [];
    private World.CommandValidation? _validation;
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
        _commands.Add(new(CommandKind.Create, token, null, 0, 0)); _createdCount++;
        return token;
    }
    /// <summary>Records destruction of a live or previously reserved entity.</summary><param name="entity">Target.</param>
    public void DestroyEntity(CommandTarget entity)
    {
        CheckRecording(null); ValidateTarget(entity);
        _commands.Add(new(CommandKind.Destroy, entity, null, 0, 0));
    }
    /// <summary>Records strict singleton addition.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void AddComponent<T>(CommandTarget entity, T value) where T : struct
    { RecordValue(CommandKind.Add, entity, value); }
    /// <summary>Records replacement of an existing component.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param><param name="value">Copied value.</param>
    public void SetComponent<T>(CommandTarget entity, T value) where T : struct
    { RecordValue(CommandKind.Set, entity, value); }
    /// <summary>Records idempotent component removal.</summary><typeparam name="T">Value type.</typeparam><param name="entity">Target.</param>
    public void RemoveComponent<T>(CommandTarget entity) where T : struct
    { RecordValue<T>(CommandKind.Remove, entity, default); }

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
        var targetCount = _commands.Count;
        try
        {
            try
            {
                _createdHandles.Clear(); _createdHandles.EnsureCapacity(_createdCount);
                if (_targets.Length < targetCount) Array.Resize(ref _targets, Grow(_targets.Length, targetCount, _maximumCommands));
                var validation = _validation ??= _world.BeginCommandValidation();
                validation.ResetFromWorld();
                for (var i = 0; i < _commands.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var command = _commands[i];
                    if (command.Kind == CommandKind.Create)
                    {
                        var created = validation.Create();
                        _createdHandles.Add(created); _targets[i] = created;
                    }
                    else
                    {
                        var target = command.Target.IsDeferred ? _createdHandles[command.Target.Deferred.Index] : command.Target.Live;
                        _targets[i] = target; validation.CheckAlive(target);
                        switch (command.Kind)
                        {
                            case CommandKind.Destroy: validation.Destroy(target); break;
                            case CommandKind.Add: validation.Add(target, command.Action!.ComponentType); break;
                            case CommandKind.Set: validation.Set(target, command.Action!.ComponentType); break;
                            case CommandKind.Remove: validation.Remove(target, command.Action!.ComponentType); break;
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { Fail(error); throw; }

            // Allocate the published result before commit, never after World has changed.
            // The public constructor copies this reusable list; published results never borrow scratch.
            var result = _createdHandles.Count == 0 ? CommandPlaybackResult.Empty : new CommandPlaybackResult(_id, _batchId, _createdHandles);
            var nextBatch = Guid.NewGuid();
            try
            {
                for (var i = 0; i < _commands.Count; i++)
                {
                    _beforeApply?.Invoke(i);
                    var command = _commands[i];
                    if (command.Kind == CommandKind.Create)
                    {
                        if (_world.CreateEntity() != _targets[i]) throw new InvalidOperationException("Allocator changed after command validation.");
                    }
                    else if (command.Kind == CommandKind.Destroy) _world.DestroyEntity(_targets[i]);
                    else command.Action!.Apply(command.Kind, _world, _targets[i], command.Kind == CommandKind.Remove
                        ? ReadOnlySpan<byte>.Empty : _payload[command.Block].Data.AsSpan(command.Offset));
                    AppliedCommandCount++;
                }
            }
            catch (Exception error) { _world.MarkFaulted(error); Fail(error); throw; }
            ReleaseRecorded(clearCache: false); _createdCount = 0; _batchId = nextBatch;
            return result;
        }
        finally
        {
            _validation?.Clear(); _createdHandles.Clear();
            if (_targets.Length >= targetCount) _targets.AsSpan(0, targetCount).Clear();
            _playing = false;
        }
    }

    /// <summary>Discards pending commands/failure and invalidates all recording tokens, without changing World.</summary>
    public void Reset()
    {
        CheckDisposed();
        if (_playing) throw new InvalidOperationException("Cannot reset during playback.");
        var nextBatch = Guid.NewGuid();
        ReleaseRecorded(clearCache: true); _createdCount = 0; _batchId = nextBatch;
        _validation?.Clear(); _createdHandles.Clear();
        Failure = null; IsFaulted = false; AppliedCommandCount = 0;
    }
    /// <summary>Discards commands once. Values and borrowed World are never disposed.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        if (_playing) throw new InvalidOperationException("Cannot dispose during playback.");
        IsDisposed = true; ReleaseRecorded(clearCache: true); _createdCount = 0;
        _commands.Capacity = 0; _createdHandles.Clear(); _createdHandles.Capacity = 0;
        _payload.Clear(); _payload.Capacity = 0; _targets = []; _validation?.Clear(); _validation = null;
    }

    private void ValidateTarget(CommandTarget entity)
    {
        if (entity.IsDeferred)
        {
            var token = entity.Deferred;
            if (token.BufferId != _id || token.BatchId != _batchId || (uint)token.Index >= (uint)_createdCount)
                throw new ArgumentException("The deferred entity does not belong to this recording batch.", "entity");
        }
        else if (!_world.IsAlive(entity.Live)) throw new ArgumentException("The entity is not alive in this World.", "entity");
    }
    private void RecordValue<T>(CommandKind kind, CommandTarget entity, T value) where T : struct
    {
        World.ValidateComponent<T>(); CheckRecording(typeof(T)); ValidateTarget(entity);
        var bytes = kind == CommandKind.Remove ? 0 : Unsafe.SizeOf<T>();
        _commands.EnsureCapacity(_commands.Count + 1);
        var action = Action<T>();
        var block = 0; var offset = 0;
        if (bytes != 0) MemoryMarshal.Write(ReservePayload(bytes, out block, out offset), in value);
        _commands.Add(new(kind, entity, action, block, offset));
    }
    private Span<byte> ReservePayload(int bytes, out int index, out int offset)
    {
        // Keep values contiguous within a block, without imposing one array's capacity on the
        // whole batch. Oversized individual components get a larger block of their own.
        while (true)
        {
            if (_payloadBlock == _payload.Count) _payload.Add(new(new byte[Math.Max(256, bytes)], 0));
            var block = _payload[_payloadBlock];
            var required = (long)block.Used + bytes;
            if (required > block.Data.Length)
            {
                if (block.Used != 0 && required > PayloadBlockSize) { _payloadBlock++; continue; }
                Array.Resize(ref block.Data, Grow(block.Data.Length, (int)required, Math.Max(PayloadBlockSize, bytes)));
            }
            index = _payloadBlock; offset = block.Used;
            block.Used += bytes; _payload[index] = block;
            return block.Data.AsSpan(offset, bytes);
        }
    }
    private ValueAction Action<T>() where T : struct
    {
        var type = typeof(T);
        if (_valueActions.TryGetValue(type, out var action)) return action;
        action = new ValueAction<T>();
        // The cache's type count is bounded by the existing command budget. Collectible handlers
        // live only as long as their pending batch, including closed generics from shared assemblies.
        if (_valueActions.Count < _maximumCommands)
        {
            if (type.IsCollectible) _collectibleActions.EnsureCapacity(_collectibleActions.Count + 1);
            _valueActions.Add(type, action);
            if (type.IsCollectible) _collectibleActions.Add(type);
        }
        return action;
    }
    private void ReleaseRecorded(bool clearCache)
    {
        _commands.Clear();
        for (var i = 0; i < _payload.Count; i++)
        {
            var block = _payload[i]; block.Data.AsSpan(0, block.Used).Clear();
            block.Used = 0; _payload[i] = block;
        }
        _payloadBlock = 0;
        if (clearCache) _valueActions.Clear();
        else foreach (var type in _collectibleActions) _valueActions.Remove(type);
        _collectibleActions.Clear();
    }
    private static int Grow(int current, int required, int maximum)
    {
        if (required > maximum) throw new InvalidOperationException("The command payload exceeds managed array capacity.");
        return (int)Math.Min(maximum, Math.Max(required, Math.Max(4L, current * 2L)));
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
    private struct PayloadBlock(byte[] data, int used)
    {
        internal byte[] Data = data;
        internal int Used = used;
    }
    private readonly record struct RecordedCommand(CommandKind Kind, CommandTarget Target, ValueAction? Action, int Block, int Offset);
    private abstract class ValueAction
    {
        internal abstract Type ComponentType { get; }
        internal abstract void Apply(CommandKind kind, World world, EntityHandle target, ReadOnlySpan<byte> payload);
    }
    private sealed class ValueAction<T> : ValueAction where T : struct
    {
        internal override Type ComponentType => typeof(T);
        internal override void Apply(CommandKind kind, World world, EntityHandle target, ReadOnlySpan<byte> payload)
        {
            switch (kind)
            {
                case CommandKind.Add: world.AddComponent(target, MemoryMarshal.Read<T>(payload)); break;
                case CommandKind.Set: world.SetComponent(target, MemoryMarshal.Read<T>(payload)); break;
                case CommandKind.Remove: world.RemoveComponent<T>(target); break;
            }
        }
    }
}
