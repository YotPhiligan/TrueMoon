using TrueMoon.Configuration;
using TrueMoon.Nom.Hosting;

namespace TrueMoon.Nom;

/// <summary>Connects prepared admission settings to Nom's existing lifecycle.</summary>
public static class ConfiguredNomExtensions
{
    /// <summary>Registers configured Nom without opening a catalog or creating a factory product.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure explicit overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredNom(this IAppConfigurationContext context, Action<NomOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, NomConfigurationSchema.Default, (app, value) => app.UseNom(options =>
        {
            options.MaximumOpenStores = value.MaximumOpenStores;
            options.QueueCount = value.QueueCount;
            options.QueueBytes = value.QueueBytes;
            options.BatchRows = value.BatchRows;
            options.BatchBytes = value.BatchBytes;
            options.DocumentBytes = value.DocumentBytes;
            options.DomainIndexBytes = value.DomainIndexBytes;
            options.DrainTimeout = value.DrainTimeout;
        }), configure);
}
