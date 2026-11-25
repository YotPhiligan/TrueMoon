using TrueMoon.Alloy.Hosting.Generation;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Registers caller-owned views using provider-neutral, generated construction.</summary>
public static class ViewRegistrationExtensions
{
    /// <summary>Registers a factory that creates a fresh view and prepares it with an optional DI style.</summary>
    /// <typeparam name="TView">The concrete view to create.</typeparam>
    /// <param name="services">The application service registrations.</param>
    /// <returns>The same registration context.</returns>
    /// <remarks>A closed call requires the Argentis analyzer. Call the factory on the UI owner thread.
    /// Concrete View constructor parameters are created through their registered factories.
    /// The caller owns the view; application services and styles are borrowed from DI.</remarks>
    public static IServicesRegistrationContext View<TView>(this IServicesRegistrationContext services) where TView : View
    {
        ArgumentNullException.ThrowIfNull(services);
        return Register(services, ViewActivation<TView>.Required());
    }

    /// <summary>Registers a view factory and its default application-owned style.</summary>
    /// <typeparam name="TView">The concrete view to create.</typeparam>
    /// <typeparam name="TViewStyle">The concrete style, constructed through DI.</typeparam>
    /// <param name="services">The application service registrations.</param>
    /// <returns>The same registration context.</returns>
    /// <remarks>An existing <see cref="IViewStyle{TView}"/> registration takes precedence.</remarks>
    public static IServicesRegistrationContext View<TView, TViewStyle>(this IServicesRegistrationContext services)
        where TView : View where TViewStyle : class, IViewStyle<TView>
    {
        ArgumentNullException.ThrowIfNull(services);
        var create = ViewActivation<TView>.Required();
        if (!services.Exist<IViewStyle<TView>>())
            services.Singleton<IViewStyle<TView>>(ViewActivation<TViewStyle>.Required());
        return Register(services, create);
    }

    /// <summary>Registers a view factory that takes a runtime argument and applies an optional DI style.</summary>
    /// <typeparam name="TView">The concrete view to create.</typeparam>
    /// <typeparam name="TArguments">The type of exactly one constructor parameter.</typeparam>
    /// <param name="services">The application service registrations.</param>
    /// <returns>The same registration context.</returns>
    /// <remarks>A closed call requires the Argentis analyzer. Other parameters are supplied as in
    /// <see cref="View{TView}(IServicesRegistrationContext)"/>. Call the factory on the UI owner thread.
    /// The caller owns the view; the runtime argument is not resolved from DI.</remarks>
    public static IServicesRegistrationContext ViewWithArgs<TView, TArguments>(this IServicesRegistrationContext services)
        where TView : View
    {
        ArgumentNullException.ThrowIfNull(services);
        var create = ViewActivation<TView, TArguments>.Required();
        return services.Singleton<IFactory<TView, TArguments>>(resolver => new Factory<TView, TArguments>(argument =>
        {
            var style = resolver.TryResolve<IViewStyle<TView>>();
            return ViewCreation.Prepare(create(resolver, argument), style);
        }));
    }

    private static IServicesRegistrationContext Register<TView>(IServicesRegistrationContext services, Func<IServiceResolver, TView> create)
        where TView : View => services.Singleton<IFactory<TView>>(resolver => new Factory<TView>(() =>
    {
        var style = resolver.TryResolve<IViewStyle<TView>>();
        return ViewCreation.Prepare(create(resolver), style);
    }));
}
