using System.ComponentModel;
using TrueMoon.Argentis.Views;
using TrueMoon.Services;

namespace TrueMoon.Alloy.Hosting.Generation;

/// <summary>Supplies caller-owned child views to a generated view constructor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ViewConstructor
{
    [ThreadStatic] private static List<Type>? _activationPath;
    private readonly IServiceResolver _resolver;
    private readonly ViewDependencyOwnership _ownership;

    private ViewConstructor(IServiceResolver resolver, ViewDependencyOwnership ownership)
    {
        _resolver = resolver;
        _ownership = ownership;
    }

    /// <summary>Creates a required child through its registered factory.</summary>
    /// <typeparam name="TView">The concrete child view.</typeparam>
    /// <returns>A new, unparented view.</returns>
    public TView Create<TView>() where TView : View => Track(_resolver.Resolve<IFactory<TView>>());

    /// <summary>Creates an optional child when its factory is registered.</summary>
    /// <typeparam name="TView">The concrete child view.</typeparam>
    /// <returns>A new, unparented view, or null if its factory is missing.</returns>
    public TView? TryCreate<TView>() where TView : View =>
        _resolver.TryResolve<IFactory<TView>>() is { } factory ? Track(factory) : null;

    private TView Track<TView>(IFactory<TView> factory) where TView : View => _ownership.Track(factory.Create()
        ?? throw new InvalidOperationException($"The factory for '{typeof(TView)}' returned null."));

    /// <summary>Constructs a view and tracks its children until they are adopted by the tree.</summary>
    /// <typeparam name="TView">The concrete parent view.</typeparam>
    /// <param name="resolver">The application service resolver.</param>
    /// <param name="childCount">The maximum number of child arguments.</param>
    /// <param name="create">The generated constructor call.</param>
    /// <returns>The constructed parent view.</returns>
    public static TView Construct<TView>(IServiceResolver resolver, int childCount, Func<IServiceResolver, ViewConstructor, TView> create)
        where TView : View
        => Construct<TView, Func<IServiceResolver, ViewConstructor, TView>>(resolver, childCount, create,
            static (context, children, make) => make(context, children));

    /// <summary>Constructs a view with a runtime argument and tracks its children until tree adoption.</summary>
    /// <typeparam name="TView">The concrete parent view.</typeparam>
    /// <typeparam name="TArguments">The runtime creation argument.</typeparam>
    /// <param name="resolver">The application service resolver.</param>
    /// <param name="childCount">The maximum number of child arguments.</param>
    /// <param name="argument">The value supplied by the caller.</param>
    /// <param name="create">The generated constructor call.</param>
    /// <returns>The constructed parent view.</returns>
    public static TView Construct<TView, TArguments>(IServiceResolver resolver, int childCount, TArguments argument,
        Func<IServiceResolver, ViewConstructor, TArguments, TView> create) where TView : View
    {
        var path = _activationPath ??= [];
        var type = typeof(TView);
        if (path.Contains(type)) throw new InvalidOperationException($"Circular View activation: '{type}' is already being constructed.");
        var ownership = new ViewDependencyOwnership(childCount);
        path.Add(type);
        try
        {
            var view = create(resolver, new(resolver, ownership), argument);
            ownership.TransferTo(view);
            return view;
        }
        catch (Exception original)
        {
            try { ownership.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
        finally { path.RemoveAt(path.Count - 1); }
    }
}
