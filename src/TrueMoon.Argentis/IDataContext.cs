namespace TrueMoon.Argentis;

/// <summary>A replaceable source of borrowed data for a view or another binding owner.</summary>
public interface IDataContext<TData>
{
    /// <summary>The current source, or null to suspend contextual bindings.</summary>
    TData? DataContext { get; set; }

    /// <summary>Raised after the source changes, on the owner thread of any attached consumers.</summary>
    event Action? DataContextChanged;
}
