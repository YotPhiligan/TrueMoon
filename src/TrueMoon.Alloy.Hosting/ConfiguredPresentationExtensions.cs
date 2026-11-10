using TrueMoon.Configuration;
using TrueMoon.Argentis;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Applies prepared window data before the existing owner-thread presentation registration.</summary>
public static class ConfiguredPresentationExtensions
{
    /// <summary>Registers a configured window without creating graphics resources or a root view.</summary>
    /// <param name="context">App context.</param><param name="rootFactory">Borrowed root factory.</param>
    /// <param name="compose">Explicit renderer, appearance and factory composition.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredPresentation(this IAppConfigurationContext context,
        Func<IServiceResolver, Element> rootFactory, Action<AlloyOptions> compose, Action<WindowSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(compose); ArgumentNullException.ThrowIfNull(rootFactory);
        return ConfigurationModuleBinding.Configure(context, WindowConfigurationSchema.Default, (app, settings) => app.UsePresentation(rootFactory, options =>
        {
            compose(options);
            if (options.Window is null) options.UseSilkWindow();
            settings.ApplyTo(options.Window!);
        }), configure);
    }
}
