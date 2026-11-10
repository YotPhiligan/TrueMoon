namespace TrueMoon.Configuration;

/// <summary>Connects standard Core settings through the deferred App configuration stage.</summary>
public static class CoreConfigurationExtensions
{
    /// <summary>Registers validated startup App settings and an owned path resolver.</summary>
    /// <param name="context">App composition context.</param><param name="configureApp">Pure explicit name overrides.</param>
    /// <param name="configurePaths">Pure explicit path overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseCoreConfiguration(this IAppConfigurationContext context,
        Action<AppSettings>? configureApp = null, Action<PathSettings>? configurePaths = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ConfigureModule(CoreConfigurationSchemas.App, (app, value) =>
            app.Services(services => services.RemoveAll<AppSettings>().Instance(value)
                .RemoveAll<AppNameSnapshot>().Instance(new AppNameSnapshot(value.Name))), configureApp);
        context.ConfigureModule(CoreConfigurationSchemas.Paths, (app, value) =>
        {
            var frozen = value with { };
            app.Services(services => services.RemoveAll<PathSettings>().Instance(value)
                .RemoveAll<IPathResolver>().Singleton<IPathResolver>(_ => new PathResolver(frozen)));
        }, configurePaths);
        return context;
    }
}
