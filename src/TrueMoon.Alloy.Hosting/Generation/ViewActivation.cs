using System.ComponentModel;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting.Generation;

/// <summary>Connects generated constructor delegates to closed view and style registrations.</summary>
/// <typeparam name="T">The concrete view or style.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ViewActivation<T> where T : class
{
    private static Func<IServiceResolver, T>? _create;

    /// <summary>Publishes the generated constructor delegate without capturing any application services.</summary>
    /// <param name="create">The generated constructor delegate.</param>
    public static void Register(Func<IServiceResolver, T> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        // More than one consumer assembly can request the same closed type.
        Interlocked.CompareExchange(ref _create, create, null);
    }

    internal static Func<IServiceResolver, T> Required() => Volatile.Read(ref _create)
        ?? throw new InvalidOperationException($"No generated view activation for '{typeof(T)}'. Attach the Argentis analyzer in the project containing the View registration, or register an explicit factory.");
}

/// <summary>Connects generated constructor delegates to view registrations with a runtime argument.</summary>
/// <typeparam name="TView">The concrete view.</typeparam>
/// <typeparam name="TArguments">The runtime creation argument.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ViewActivation<TView, TArguments> where TView : class
{
    private static Func<IServiceResolver, TArguments, TView>? _create;

    /// <summary>Publishes the generated constructor delegate without capturing any application services.</summary>
    /// <param name="create">The generated constructor delegate.</param>
    public static void Register(Func<IServiceResolver, TArguments, TView> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        Interlocked.CompareExchange(ref _create, create, null);
    }

    internal static Func<IServiceResolver, TArguments, TView> Required() => Volatile.Read(ref _create)
        ?? throw new InvalidOperationException($"No generated view activation for '{typeof(TView)}' with argument '{typeof(TArguments)}'. Enable the Argentis analyzer in the project containing the ViewWithArgs registration, or register an explicit factory.");
}
