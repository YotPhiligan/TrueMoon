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
}
