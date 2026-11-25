using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Views;

namespace TrueMoon.Argentis;

/// <summary>Declarative content installed when the view joins a UI session.</summary>
public interface IContent
{
    /// <summary>The installed content root, if one exists.</summary>
    Element? Child { get; }
    /// <summary>Declares the untyped layout factory for the first attachment.</summary>
    void Content(Func<IUiBuildContext<object>, Element> factory);
}

/// <summary>Declarative content with access to a view's current typed data.</summary>
public interface IContent<TData> : IDataContext<TData>, IContent
{
    /// <summary>Sets initial data and declares the typed layout factory for the first attachment.</summary>
    void Content(TData? data, Func<IUiBuildContext<TData>, Element> factory);
}
