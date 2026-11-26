using TrueMoon.Argentis.Bindings;

namespace TrueMoon.Argentis.Views;

/// <summary>Creates controls for one invocation of a view's content factory.</summary>
public interface IUiBuildContext<TData>
{
    /// <summary>The borrowed view context, when the layout declares a data type.</summary>
    IDataContext<TData>? DataContext { get; }

    /// <summary>Creates a fresh control with access to the view's current data context.</summary>
    UiControl<TControl, TData> Create<TControl>() where TControl : Element;

    /// <summary>Creates a control through a factory with typed arguments.</summary>
    UiControl<TControl, TData> Create<TControl, TArguments>(TArguments arguments) where TControl : Element;

    /// <summary>Creates a fresh control through a local factory instead of the session registry.</summary>
    /// <param name="factory">Borrowed factory. The content builder owns its returned draft, not the factory.</param>
    /// <returns>The draft coupled to the current build context.</returns>
    UiControl<TControl, TData> Create<TControl>(IFactory<TControl> factory) where TControl : Element;

    /// <summary>Creates a fresh control through a local factory with typed arguments.</summary>
    /// <param name="factory">Borrowed factory. The content builder owns its returned draft, not the factory.</param>
    /// <param name="arguments">Arguments supplied only to this creation.</param>
    /// <returns>The draft coupled to the current build context.</returns>
    UiControl<TControl, TData> Create<TControl, TArguments>(IFactory<TControl, TArguments> factory, TArguments arguments)
        where TControl : Element;
}
