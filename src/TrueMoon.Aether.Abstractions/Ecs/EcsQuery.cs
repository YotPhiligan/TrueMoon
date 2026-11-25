namespace TrueMoon.Aether.Ecs;

/// <summary>Invocation-stamped query. Structural mutation is deferred; refs exist only inside synchronous callbacks.</summary>
/// <typeparam name="T">Required reference-free value component.</typeparam>
public readonly struct EcsQuery<T> where T : struct
{
    private readonly IEcsSystemAccess? _access;
    private readonly ulong _invocation;
    private readonly Type[]? _excluded;
    internal EcsQuery(IEcsSystemAccess access, ulong invocation, Type[]? excluded) { _access = access; _invocation = invocation; _excluded = excluded; }
    private IEcsSystemAccess Access => EcsAccess.Require(_access, _invocation);
    /// <summary>Returns an independent exclusion query, at most eight distinct declared exclusions.</summary><typeparam name="TExcluded">Excluded type.</typeparam><returns>New query; required types cannot be excluded.</returns>
    public EcsQuery<T> Without<TExcluded>() where TExcluded : struct => new(Access, _invocation, EcsAccess.Exclude<TExcluded>(_excluded, typeof(T), null));
    /// <summary>Observes matched values.</summary><param name="action">Scoped readonly callback.</param>
    public void ForEach(QueryReadAction<T> action) { ArgumentNullException.ThrowIfNull(action); Access.ForEach(_invocation, _excluded, action); }
    /// <summary>Writes matched values; earlier writes survive a callback fault.</summary><param name="action">Scoped writable callback.</param>
    public void ForEachWrite(QueryWriteAction<T> action) { ArgumentNullException.ThrowIfNull(action); Access.ForEachWrite(_invocation, _excluded, action); }
}

/// <summary>Invocation query for two distinct required types.</summary><typeparam name="T1">First component.</typeparam><typeparam name="T2">Second component.</typeparam>
public readonly struct EcsQuery<T1, T2> where T1 : struct where T2 : struct
{
    private readonly IEcsSystemAccess? _access;
    private readonly ulong _invocation;
    private readonly Type[]? _excluded;
    internal EcsQuery(IEcsSystemAccess access, ulong invocation, Type[]? excluded) { _access = access; _invocation = invocation; _excluded = excluded; }
    private IEcsSystemAccess Access => EcsAccess.Require(_access, _invocation);
    /// <summary>Adds one independent exclusion, preserving both required types.</summary><typeparam name="TExcluded">Excluded type.</typeparam><returns>New query.</returns>
    public EcsQuery<T1, T2> Without<TExcluded>() where TExcluded : struct => new(Access, _invocation, EcsAccess.Exclude<TExcluded>(_excluded, typeof(T1), typeof(T2)));
    /// <summary>Observes both values.</summary><param name="action">Scoped callback.</param>
    public void ForEach(QueryReadAction<T1, T2> action) { ArgumentNullException.ThrowIfNull(action); Access.ForEach(_invocation, _excluded, action); }
    /// <summary>Writes first and observes second.</summary><param name="action">Scoped callback.</param>
    public void ForEachWrite(QueryWriteAction<T1, T2> action) { ArgumentNullException.ThrowIfNull(action); Access.ForEachWrite(_invocation, _excluded, action); }
    /// <summary>Writes both values.</summary><param name="action">Scoped callback.</param>
    public void ForEachWriteBoth(QueryWriteBothAction<T1, T2> action) { ArgumentNullException.ThrowIfNull(action); Access.ForEachWriteBoth(_invocation, _excluded, action); }
}
