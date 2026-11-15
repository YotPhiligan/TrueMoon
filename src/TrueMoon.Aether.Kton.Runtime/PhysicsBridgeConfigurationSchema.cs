using TrueMoon.Configuration;

namespace TrueMoon.Aether.Kton.Runtime;

/// <summary>Common configuration schema for the optional Aether bridge, separate from Kton DefaultWorld options.</summary>
public static class PhysicsBridgeConfigurationSchema
{
    /// <summary>Register this exact schema instance in the App profile.</summary>
    public static IConfigurationSchema<PhysicsBridgeOptions> Default { get; } = ConfigurationSchemaDefinitions.Create("Kton:Bridge",
        () => new PhysicsBridgeOptions(), value => value.Snapshot(), value => { _ = value.Snapshot(); },
        fields => fields.Field("MaximumContactEvents", value => value.MaximumContactEvents, (value, setting) => value.MaximumContactEvents = setting));
}
