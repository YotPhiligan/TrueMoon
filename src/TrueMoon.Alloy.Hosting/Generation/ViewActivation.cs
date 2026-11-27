using System.ComponentModel;
using System.Reflection;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting.Generation;

/// <summary>Connects generated constructor delegates to closed view and style registrations.</summary>
/// <typeparam name="T">The concrete view or style.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ViewActivation<T> where T : class
{
    /// <summary>Publishes the generated constructor delegate under the name of the contributing assembly.</summary>
    /// <param name="create">The generated constructor delegate.</param>
    public static void Register(Func<IServiceResolver, T> create)
    {
        var name = Assembly.GetCallingAssembly().GetName().Name;
        ViewActivations.Add(typeof(T), null, string.IsNullOrWhiteSpace(name) ? "<anonymous>" : name, create);
    }

    /// <summary>Publishes a constructor delegate under an explicit contribution name.</summary>
    /// <param name="create">The constructor delegate.</param>
    /// <param name="contribution">The name a registration has to repeat to select this contribution.</param>
    public static void Register(Func<IServiceResolver, T> create, string contribution) =>
        ViewActivations.Add(typeof(T), null, contribution, create);

    internal static Func<IServiceResolver, T> Required() => Require(null);

    internal static Func<IServiceResolver, T> Required(string contribution) => Require(contribution);

    private static Func<IServiceResolver, T> Require(string? contribution)
    {
        if (ViewActivations.TryGet(typeof(T), null, contribution, out var entry)) return (Func<IServiceResolver, T>)entry!;
        var known = ViewActivations.Contributions(typeof(T), null);
        if (contribution is not null)
            throw new InvalidOperationException(
                $"No view activation for '{typeof(T)}' with contribution '{contribution}'. " +
                $"Registered contributions: {string.Join(", ", known)}.");
        throw new InvalidOperationException(known.Length == 0
            ? $"No generated view activation for '{typeof(T)}'. Attach the Argentis analyzer in the project containing the View registration, or register an explicit factory."
            : $"More than one generated view activation for '{typeof(T)}': {string.Join(", ", known)}. Call the registration overload that names the contribution.");
    }
}

/// <summary>Connects generated constructor delegates to view registrations with a runtime argument.</summary>
/// <typeparam name="TView">The concrete view.</typeparam>
/// <typeparam name="TArguments">The runtime creation argument.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ViewActivation<TView, TArguments> where TView : class
{
    /// <summary>Publishes the generated constructor delegate under the name of the contributing assembly.</summary>
    /// <param name="create">The generated constructor delegate.</param>
    public static void Register(Func<IServiceResolver, TArguments, TView> create)
    {
        var name = Assembly.GetCallingAssembly().GetName().Name;
        ViewActivations.Add(typeof(TView), typeof(TArguments), string.IsNullOrWhiteSpace(name) ? "<anonymous>" : name, create);
    }

    /// <summary>Publishes a constructor delegate under an explicit contribution name.</summary>
    /// <param name="create">The constructor delegate.</param>
    /// <param name="contribution">The name a registration has to repeat to select this contribution.</param>
    public static void Register(Func<IServiceResolver, TArguments, TView> create, string contribution) =>
        ViewActivations.Add(typeof(TView), typeof(TArguments), contribution, create);

    internal static Func<IServiceResolver, TArguments, TView> Required() => Require(null);

    internal static Func<IServiceResolver, TArguments, TView> Required(string contribution) => Require(contribution);

    private static Func<IServiceResolver, TArguments, TView> Require(string? contribution)
    {
        if (ViewActivations.TryGet(typeof(TView), typeof(TArguments), contribution, out var entry))
            return (Func<IServiceResolver, TArguments, TView>)entry!;
        var known = ViewActivations.Contributions(typeof(TView), typeof(TArguments));
        if (contribution is not null)
            throw new InvalidOperationException(
                $"No view activation for '{typeof(TView)}' with argument '{typeof(TArguments)}' with contribution '{contribution}'. " +
                $"Registered contributions: {string.Join(", ", known)}.");
        throw new InvalidOperationException(known.Length == 0
            ? $"No generated view activation for '{typeof(TView)}' with argument '{typeof(TArguments)}'. Enable the Argentis analyzer in the project containing the ViewWithArgs registration, or register an explicit factory."
            : $"More than one generated view activation for '{typeof(TView)}' with argument '{typeof(TArguments)}': {string.Join(", ", known)}. Call the ViewWithArgs overload that names the contribution.");
    }
}
