using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Abstractions.Mods;

/// <summary>Temporary group-local factory registration. Types/delegates never enter long-lived App registrations.</summary>
public interface IModRegistration
{
    /// <summary>Registers a fresh scheduler-owned system factory; metadata is copied.</summary>
    /// <param name="metadata">Access/order declarations.</param><param name="factory">Borrowed factory; entry owns factory resources.</param>
    void AddSystem(EcsSystemMetadata metadata, IFactory<ISystem> factory);
    /// <summary>Registers a scalar component value factory by exact neutral schema.</summary>
    /// <typeparam name="T">Reference-free value component.</typeparam><param name="schema">Frozen schema.</param><param name="factory">Borrowed scalar conversion factory.</param>
    void AddComponent<T>(ComponentSchema schema, IFactory<T, ComponentData> factory) where T : struct;
    /// <summary>Registers one group-owned service result. Factories must return distinct fresh instances.</summary>
    /// <param name="id">Group-local service ID.</param><param name="factory">Borrowed factory.</param>
    void AddService(string id, IFactory<IModService> factory);
}
