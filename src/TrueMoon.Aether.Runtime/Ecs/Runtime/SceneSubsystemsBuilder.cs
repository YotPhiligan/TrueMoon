using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime.Ecs.Runtime;

/// <summary>Composition-time selection of optional scene subsystems using borrowed DI factories.</summary>
public sealed class SceneSubsystemsBuilder
{
    private readonly List<(string Id, Func<IServiceResolver, IFactory<ISceneSubsystem, SceneSubsystemArguments>> Resolve)> _entries = [];
    /// <summary>Adds one subsystem. The factory is resolved once when the session factory is activated, never in a tick.</summary>
    /// <typeparam name="T">Resource owner, distinct from its scheduler-owned systems.</typeparam>
    /// <param name="id">Nonempty ordinal unique identifier, at most128characters.</param><returns>This builder.</returns>
    public SceneSubsystemsBuilder Add<T>(string id) where T : class, ISceneSubsystem
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentException("A subsystem ID must contain1..128characters.", nameof(id));
        if (_entries.Count == 64) throw new InvalidOperationException("At most64scene subsystems are supported.");
        if (_entries.Any(e => e.Id == id)) throw new ArgumentException("Duplicate subsystem ID.", nameof(id));
        if (typeof(IEcsSystem).IsAssignableFrom(typeof(T)) || typeof(Abstractions.Ecs.ISystem).IsAssignableFrom(typeof(T)))
            throw new ArgumentException("Subsystem resource owners must be distinct from scheduler-owned systems.", nameof(T));
        _entries.Add((id, resolver =>
        {
            var factory = resolver.Resolve<IFactory<T, SceneSubsystemArguments>>() ?? throw new InvalidOperationException($"Register IFactory<{typeof(T).Name}, SceneSubsystemArguments>.");
            return new Factory<ISceneSubsystem, SceneSubsystemArguments>(arguments => factory.Create(arguments)!);
        }));
        return this;
    }
    internal Func<IServiceResolver, IFactory<ISceneSubsystem, SceneSubsystemArguments>>[] Snapshot() => _entries.Select(e => e.Resolve).ToArray();
}
