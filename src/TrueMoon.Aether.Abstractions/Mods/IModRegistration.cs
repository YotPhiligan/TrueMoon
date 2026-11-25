using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Mods;

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

    /// <summary>Registers bidirectional private component values for new-World migration. Factories remain local, never root DI-owned.</summary>
    /// <typeparam name="T">Reference-free component value.</typeparam><param name="schema">Exact current schema.</param>
    /// <param name="read">Borrowed import factory.</param><param name="write">Borrowed export factory.</param><param name="references">Copied entity-reference declarations.</param>
    void AddMigratingComponent<T>(ComponentSchema schema, IFactory<T, ModComponentReadArguments> read,
        IFactory<ComponentData, ModComponentWriteArguments<T>> write, IEnumerable<ComponentReference>? references = null) where T : struct
        => throw new NotSupportedException("This registration backend does not support World migration.");
    /// <summary>Declares one exact version step and every intentionally removed property. Host must separately admit field removal.</summary>
    /// <param name="typeId">Stable component type identity.</param><param name="sourceVersion">Exact source schema version.</param><param name="targetVersion">Exact target schema version.</param>
    /// <param name="factory">Fresh exclusively owned pure migrator.</param><param name="removedProperties">All removed source fields, copied now.</param>
    void AddComponentMigration(ComponentTypeId typeId, int sourceVersion, int targetVersion, IFactory<IModComponentMigrator> factory, IEnumerable<string>? removedProperties = null)
        => throw new NotSupportedException("This registration backend does not support World migration.");
}
