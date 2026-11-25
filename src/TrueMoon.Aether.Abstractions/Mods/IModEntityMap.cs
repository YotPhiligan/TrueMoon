using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Mods;

/// <summary>Generation-local immutable persistent-ID/handle map. Retained old maps return old, stale handles, never current handles.</summary>
public interface IModEntityMap
{
    /// <summary>Resolves a live entity in this generation, including forward/self references during import.</summary><param name="id">Persistent identity.</param><returns>Generation-local handle.</returns>
    EntityHandle Resolve(EntityId id);
    /// <summary>Exports a handle as a persistent identity; foreign/stale/dangling targets reject.</summary><param name="handle">Generation-local live handle.</param><returns>Persistent identity.</returns>
    EntityId Persistent(EntityHandle handle);
}

/// <summary>Neutral effective values and candidate generation map supplied to a local import factory.</summary>
public sealed record ModComponentReadArguments(ComponentData Data, IModEntityMap Entities);
/// <summary>Copied typed value and required persistent occurrence identity for a local export factory.</summary>
public sealed record ModComponentWriteArguments<T>(ComponentId Id, T Value, IModEntityMap Entities) where T : struct;
