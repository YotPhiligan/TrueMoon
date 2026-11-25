using TrueMoon.Argentis.Bindings;

namespace TrueMoon.Argentis.Views;

/// <summary>A control being configured with the current data context of its view.</summary>
public readonly struct UiControl<TControl, TData> where TControl : Element
{
    private readonly IDataContext<TData>? _context;

    internal UiControl(TControl element, IDataContext<TData>? context)
    {
        Element = element;
        _context = context;
    }

    /// <summary>The concrete element that will be installed in the tree.</summary>
    public TControl Element { get; }

    internal IDataContext<TData> Context => _context
        ?? throw new InvalidOperationException("A contextual binding requires typed view content.");

    /// <summary>Unwraps the element for normal tree composition.</summary>
    public static implicit operator TControl(UiControl<TControl, TData> control) => control.Element;
}
