using TrueMoon.Aether.Mods;

namespace TrueMoon.Aether.Runtime.Ecs;

public sealed partial class EcsScheduler
{
    // Detached candidates cannot execute, claim a World or poison its fault state on failed cleanup.
    internal static EcsScheduler PrepareDetached(World world, IReadOnlyList<EcsSystemRegistration> systems, Action<EcsScheduler> own, EcsScheduler? previous)
    {
        world.CheckPlayback(); var options = new EcsSchedulerOptions();
        if (systems.Count > options.MaximumSystems) throw new InvalidOperationException("System budget exceeded.");
        var registrations = systems.ToArray(); var order = SystemGraph.Order(registrations.Select(r => r.Plan).ToArray());
        var scheduler = new EcsScheduler(world, order.Select(i => registrations[i]).ToArray(), options);
        own(scheduler); // Publish ownership before any factory callback; failures retain this exact cleanup owner.
        var identities = new HashSet<object>(ReferenceEqualityComparer.Instance);
        using var preparation = world.BeginReloadPreparation();
        foreach (var entry in scheduler._entries)
        {
            var instance = entry.Registration.Factory.Create(world) ?? throw new InvalidOperationException("System factory returned null.");
            var identity = instance is EcsContractSystem contract ? contract.OwnershipIdentity : instance;
            if (previous != null && previous._entries.Any(e => ReferenceEquals(((EcsContractSystem)e.System!).OwnershipIdentity, identity)))
                throw new InvalidOperationException("Candidate returned a currently owned system.");
            if (!identities.Add(identity)) throw new InvalidOperationException("System results must not alias.");
            entry.System = instance;
            if (identity is not IModReloadableSystem) throw new Mods.ModFailure(Mods.ModLoadError.ReloadNotSupported, "Each replacement system must explicitly capture/restore its complete state.");
        }
        return scheduler;
    }
    internal void RejectAliases(EcsScheduler? previous)
    {
        if (previous == null) return;
        var old = previous._entries.Select(e => ((EcsContractSystem)e.System!).OwnershipIdentity).ToHashSet(ReferenceEqualityComparer.Instance);
        var alias = false;
        foreach (var entry in _entries)
            if (entry.System is EcsContractSystem contract && old.Contains(contract.OwnershipIdentity)) { entry.System = null; alias = true; }
        if (alias) throw new InvalidOperationException("A candidate factory returned a currently owned system.");
    }
    internal Dictionary<string, ModSystemState> CaptureReloadState()
    {
        if (!_attached || State != EcsSchedulerState.Ready || _ticking || Commands.Count != 0) throw new InvalidOperationException("Capture requires an idle completed tick.");
        var result = new Dictionary<string, ModSystemState>(StringComparer.Ordinal); long bytes = 0;
        using var preparation = _world.BeginReloadPreparation();
        foreach (var entry in _entries)
        {
            var state = ((IModReloadableSystem)((EcsContractSystem)entry.System!).OwnershipIdentity).CaptureState()
                ?? throw new InvalidOperationException("Capture returned null.");
            bytes += state.Bytes.Length;
            if (bytes > 4 * 1024 * 1024) throw new InvalidOperationException("Group state budget exceeded.");
            result.Add(entry.Registration.Plan.Id, new(state.Version, state.Bytes.Span));
        }
        return result;
    }
    internal void RestoreReloadState(IReadOnlyDictionary<string, ModSystemState> states)
    {
        using var preparation = _world.BeginReloadPreparation();
        if (states.Count != _entries.Length || _entries.Any(e => !states.ContainsKey(e.Registration.Plan.Id)))
            throw new InvalidOperationException("State system IDs must match exactly; implicit resets/drops are unsupported.");
        foreach (var entry in _entries)
        {
            var state = states[entry.Registration.Plan.Id];
            ((IModReloadableSystem)((EcsContractSystem)entry.System!).OwnershipIdentity).RestoreState(new(state.Version, state.Bytes.Span));
        }
    }
    internal void PublishReplacing(EcsScheduler? previous)
    {
        if (_attached || State != EcsSchedulerState.Ready || _ticking) throw new InvalidOperationException("Candidate is not private/ready.");
        if (previous == null)
        {
            _world.ClaimScheduler(this); _world.CompleteActivation(this); _attached = true; return;
        }
        if (!ReferenceEquals(_world, previous._world) || !previous._attached || previous._ticking ||
            previous.State != EcsSchedulerState.Ready || previous.Commands.Count != 0)
            throw new InvalidOperationException("Replacement requires the current idle healthy World owner.");
        _world.ReplaceScheduler(previous, this); // No plugin callback/allocation/cancellation after this assignment.
        previous._attached = false; _attached = true; TickNumber = previous.TickNumber;
    }
    internal void SetMigrationTick(ulong tick)
    { if (_attached || State != EcsSchedulerState.Ready) throw new InvalidOperationException("Tick state requires a private candidate."); TickNumber = tick; }
}
