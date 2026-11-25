using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Kul;

/// <summary>Complete audio attachment/detachment at phase boundaries; no native resource creation.</summary>
public static class AudioEntityCommands
{
    /// <summary>Declares component writes used by the helpers.</summary><param name="description">System metadata.</param><returns>Same description.</returns>
    public static EcsSystemDescription DeclareWrites(EcsSystemDescription description) => description.Write<AudioSource>().Write<AudioSourceState>().Write<AudioPlaybackCommand>();
    /// <summary>Creates a nonspatial source.</summary><param name="commands">Active writer.</param><param name="source">Source.</param>
    /// <param name="command">Optional initial command.</param><returns>Batch-local token.</returns>
    public static DeferredEntity CreateSource(EcsCommandWriter commands, AudioSource source, AudioPlaybackCommand? command = null)
    { var entity = commands.CreateEntity(); AddSource(commands, entity, source, command); return entity; }
    /// <summary>Attaches audio roles to an existing entity, including physics entities.</summary><param name="commands">Writer.</param>
    /// <param name="entity">Live/batch-local entity.</param><param name="source">Source.</param><param name="command">Optional command.</param>
    public static void AddSource(EcsCommandWriter commands, CommandTarget entity, AudioSource source, AudioPlaybackCommand? command = null)
    { commands.AddComponent(entity, source); commands.AddComponent(entity, default(AudioSourceState)); if (command is { } value) commands.AddComponent(entity, value); }
    /// <summary>Removes all audio roles; preserves other entity components.</summary><param name="commands">Writer.</param><param name="entity">Entity.</param>
    public static void RemoveSource(EcsCommandWriter commands, CommandTarget entity)
    { commands.RemoveComponent<AudioSource>(entity); commands.RemoveComponent<AudioSourceState>(entity); commands.RemoveComponent<AudioPlaybackCommand>(entity); }
}
