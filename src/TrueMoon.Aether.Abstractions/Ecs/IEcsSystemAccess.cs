namespace TrueMoon.Aether.Abstractions.Ecs;

/// <summary>Host binding for typed invocation access. The concrete implementation belongs to Runtime.</summary>
/// <remarks>Every method must reject an inactive/previous invocation, wrong thread, disposed/faulted host,
/// undeclared access and invalid/stale/wrong-World handles before callbacks or recording. Queries hold the
/// host's structural lease; writes before a callback fault survive, then the scheduler becomes terminally
/// faulted and discards pending commands. No implementation may store component values boxed as object/IComponent.
/// Exclusions are borrowed only for the call. Playback/reset/dispose are deliberately absent.</remarks>
public interface IEcsSystemAccess
{
    /// <summary>Validates the exact invocation and synchronous owner. The stamp must never wrap/revive.</summary><param name="invocation">Nonzero invocation stamp.</param>
    void ValidateInvocation(ulong invocation);
    /// <summary>Observes the issuing World identity without exposing its implementation.</summary><param name="invocation">Exact invocation.</param><returns>Nonempty transient World ID.</returns>
    Guid GetWorldId(ulong invocation);
    /// <summary>Checks a transient handle without changing data.</summary><param name="invocation">Invocation.</param><param name="entity">Handle.</param><returns>Current liveness.</returns>
    bool IsAlive(ulong invocation, EntityHandle entity);
    /// <summary>Checks declared membership.</summary><typeparam name="T">Value component.</typeparam><param name="invocation">Invocation.</param><param name="entity">Handle.</param><returns>Membership.</returns>
    bool HasComponent<T>(ulong invocation, EntityHandle entity) where T : struct;
    /// <summary>Copies a declared component; exposes no borrowed reference.</summary><typeparam name="T">Value component.</typeparam><param name="invocation">Invocation.</param><param name="entity">Handle.</param><param name="value">Detached copy.</param><returns>Whether present.</returns>
    bool TryGetComponent<T>(ulong invocation, EntityHandle entity, out T value) where T : struct;
    /// <summary>Visits one readonly component under a structural lease.</summary><typeparam name="T">Required component.</typeparam><param name="invocation">Invocation.</param><param name="excluded">Declared exclusions, at most eight.</param><param name="action">Synchronous scoped callback.</param>
    void ForEach<T>(ulong invocation, ReadOnlySpan<Type> excluded, QueryReadAction<T> action) where T : struct;
    /// <summary>Visits one writable component.</summary><typeparam name="T">Required component.</typeparam><param name="invocation">Invocation.</param><param name="excluded">Declared exclusions.</param><param name="action">Scoped callback.</param>
    void ForEachWrite<T>(ulong invocation, ReadOnlySpan<Type> excluded, QueryWriteAction<T> action) where T : struct;
    /// <summary>Visits two readonly components.</summary><typeparam name="T1">First component.</typeparam><typeparam name="T2">Second component.</typeparam><param name="invocation">Invocation.</param><param name="excluded">Declared exclusions.</param><param name="action">Scoped callback.</param>
    void ForEach<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, QueryReadAction<T1, T2> action) where T1 : struct where T2 : struct;
    /// <summary>Writes first and observes second.</summary><typeparam name="T1">Writable component.</typeparam><typeparam name="T2">Readonly component.</typeparam><param name="invocation">Invocation.</param><param name="excluded">Declared exclusions.</param><param name="action">Scoped callback.</param>
    void ForEachWrite<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, QueryWriteAction<T1, T2> action) where T1 : struct where T2 : struct;
    /// <summary>Writes both required components.</summary><typeparam name="T1">First component.</typeparam><typeparam name="T2">Second component.</typeparam><param name="invocation">Invocation.</param><param name="excluded">Declared exclusions.</param><param name="action">Scoped callback.</param>
    void ForEachWriteBoth<T1, T2>(ulong invocation, ReadOnlySpan<Type> excluded, QueryWriteBothAction<T1, T2> action) where T1 : struct where T2 : struct;
    /// <summary>Observes pending phase commands.</summary><param name="invocation">Invocation.</param><returns>Pending count.</returns>
    int CommandCount(ulong invocation);
    /// <summary>Records a reservation without structural mutation.</summary><param name="invocation">Invocation.</param><returns>Batch-local reservation.</returns>
    DeferredEntity CreateEntity(ulong invocation);
    /// <summary>Records destruction.</summary><param name="invocation">Invocation.</param><param name="entity">Live or batch-local target.</param>
    void DestroyEntity(ulong invocation, CommandTarget entity);
    /// <summary>Records declared typed addition.</summary><typeparam name="T">Value component.</typeparam><param name="invocation">Invocation.</param><param name="entity">Target.</param><param name="value">Copied value.</param>
    void AddComponent<T>(ulong invocation, CommandTarget entity, T value) where T : struct;
    /// <summary>Records declared typed replacement.</summary><typeparam name="T">Value component.</typeparam><param name="invocation">Invocation.</param><param name="entity">Target.</param><param name="value">Copied value.</param>
    void SetComponent<T>(ulong invocation, CommandTarget entity, T value) where T : struct;
    /// <summary>Records declared removal.</summary><typeparam name="T">Value component.</typeparam><param name="invocation">Invocation.</param><param name="entity">Target.</param>
    void RemoveComponent<T>(ulong invocation, CommandTarget entity) where T : struct;
    /// <summary>Requests terminal stop after the callback, discarding pending phase commands.</summary><param name="invocation">Invocation.</param>
    void RequestStop(ulong invocation);
}
