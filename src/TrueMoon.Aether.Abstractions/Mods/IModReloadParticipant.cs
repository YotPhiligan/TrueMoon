using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Mods;

/// <summary>Opt-in reversible lifecycle for entries/services used in system replacement.</summary>
/// <remarks>Start prepares private resources; externally retained callbacks must use IModGeneration.Guard.
/// Quiesce drains private tasks without destroying state. Resume must repair partial/failed quiesce and support retry.
/// Persistent gameplay state belongs to explicitly captured systems, not transient entry/service resources.</remarks>
public interface IModReloadParticipant
{
    /// <summary>Stops private activity without terminal disposal; a failed/cancelled attempt must be resumable.</summary><param name="cancellationToken">Cooperative preparation cancellation.</param>
    ValueTask QuiesceAsync(CancellationToken cancellationToken);
    /// <summary>Restores activity after an abandoned replacement; called with admission still closed.</summary><param name="cancellationToken">Rollback completion token.</param>
    ValueTask ResumeAsync(CancellationToken cancellationToken);
}

/// <summary>Explicit system-state contract for replacement on an unchanged stable-component World.</summary>
public interface IModReloadableSystem : ISystem
{
    /// <summary>Captures complete persistent system state without modifying World or the running instance.</summary><returns>Detached versioned bytes.</returns>
    ModSystemState CaptureState();
    /// <summary>Restores a private candidate, rejecting incompatible state instead of silently resetting it.</summary><param name="state">Host-copied neutral data keyed by exact system ID.</param>
    void RestoreState(ModSystemState state);
}

/// <summary>Per-generation callback admission. Obtain once through IModHostServices during composition.</summary>
public interface IModGeneration
{
    /// <summary>Whether this generation is published and accepts callbacks.</summary>
    bool IsActive { get; }
    /// <summary>Creates a stable host-owned callback. Subscribe this exact returned delegate, not a module closure wrapping it.</summary>
    /// <param name="callback">Synchronous non-reentrant callback. Do not start detached async work inside it.</param>
    /// <returns>Host delegate: staged/quiesced/retired invocations do nothing; retirement clears module references.</returns>
    Action Guard(Action callback);
}
