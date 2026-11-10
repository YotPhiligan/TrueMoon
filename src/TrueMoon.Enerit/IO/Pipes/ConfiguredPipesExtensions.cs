using TrueMoon.Configuration;

namespace TrueMoon.Enerit.IO.Pipes;

/// <summary>Connects prepared data limits to the existing transport registration and ownership.</summary>
public static class ConfiguredPipesExtensions
{
    /// <summary>Registers configured pipes without opening a connection or changing endpoint ownership.</summary>
    /// <param name="context">App context.</param><param name="endpoint">Borrowed endpoint resolver.</param><param name="configure">Pure explicit option overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredPipesServiceTransport(this IAppConfigurationContext context,
        Func<Type, string>? endpoint = null, Action<PipesTransportOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, PipesConfigurationSchema.Default, (app, options) =>
        {
            if (endpoint is null) app.PipesServiceTransport(options);
            else app.PipesServiceTransport(endpoint, options);
        }, configure);
}
