using TrueMoon.Nom.Hosting;
using TrueMoon.Configuration;

namespace TrueMoon.Nom;

/// <summary>Standard startup schema for Nom; resources and operation arguments remain separate.</summary>
public static class NomConfigurationSchema
{
    /// <summary>The exact schema instance used by configured App attachment.</summary>
    public static IConfigurationSchema<NomOptions> Default { get; } = ConfigurationSchemaDefinitions.Create("Nom", () => new NomOptions(),
        value => new NomOptions { MaximumOpenStores = value.MaximumOpenStores, QueueCount = value.QueueCount, QueueBytes = value.QueueBytes, BatchRows = value.BatchRows, BatchBytes = value.BatchBytes, DocumentBytes = value.DocumentBytes, DomainIndexBytes = value.DomainIndexBytes, DrainTimeout = value.DrainTimeout }, value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaximumOpenStores", value => value.MaximumOpenStores, (value, next) => value.MaximumOpenStores = next);
            fields.Field("QueueCount", value => value.QueueCount, (value, next) => value.QueueCount = next);
            fields.Field("QueueBytes", value => value.QueueBytes, (value, next) => value.QueueBytes = next);
            fields.Field("BatchRows", value => value.BatchRows, (value, next) => value.BatchRows = next);
            fields.Field("BatchBytes", value => value.BatchBytes, (value, next) => value.BatchBytes = next);
            fields.Field("DocumentBytes", value => value.DocumentBytes, (value, next) => value.DocumentBytes = next);
            fields.Field("DomainIndexBytes", value => value.DomainIndexBytes, (value, next) => value.DomainIndexBytes = next);
            fields.Field("DrainTimeout", value => value.DrainTimeout, (value, next) => value.DrainTimeout = next);
        });
}
