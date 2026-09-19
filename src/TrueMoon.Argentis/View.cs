namespace TrueMoon.Argentis;

/// <summary>A reusable view authored in a C# class.</summary>
public abstract class View : ContentControl, IView
{
    /// <summary>Creates content once. Use SetContent to replace it dynamically.</summary>
    public void Content(Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        SetContent(factory() as Element ?? throw new ArgumentException("View content must be an Element.", nameof(factory)));
    }
    /// <inheritdoc />
    public virtual object? GetContent() => Child;
}
/// <summary>A view with a typed data context.</summary>
public abstract class View<TData> : View, IView<TData>
{
    /// <inheritdoc />
    public TData? DataContext { get; set; }
    /// <inheritdoc />
    public void Content(TData? data, Func<TData?, object> factory) { DataContext = data; Content(() => factory(DataContext)); }
}
