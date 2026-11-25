using System.Collections.Frozen;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Shared factory descriptions only. Bound presenters belong to the session or the standalone renderer.</summary>
internal static partial class StockPresenters
{
    private static readonly FrozenDictionary<Type, ElementPresentation> Catalog;

    static StockPresenters()
    {
        Dictionary<Type, ElementPresentation> entries = [];
        AddFields(entries);
        AddWidgets(entries);
        AddChrome(entries);
        Catalog = entries.ToFrozenDictionary();
    }

    // A subclass keeps stock painting unless it has its own external registration.
    // Application overrides, unlike stock painting, never descend to a derived type.
    internal static ElementPresentation? Find(Type type)
    {
        for (Type? current = type; current != null && current != typeof(Element); current = current.BaseType)
            if (Catalog.TryGetValue(current, out var descriptor)) return descriptor;
        return null;
    }
}
