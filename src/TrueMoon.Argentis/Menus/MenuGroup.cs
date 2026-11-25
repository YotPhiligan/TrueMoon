namespace TrueMoon.Argentis.Menus;

/// <summary>A named top-level menu with a frozen action list.</summary>
public sealed class MenuGroup
{
    /// <summary>Creates a nonempty menu.</summary>
    /// <param name="id">Stable identity.</param><param name="label">Visible heading.</param><param name="actions">Action declarations.</param>
    public MenuGroup(string id, string label, IEnumerable<MenuAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(label); ArgumentNullException.ThrowIfNull(actions);
        var values = actions.ToArray();
        if (values.Length == 0 || values.Any(a => a == null || string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.Text) || a.Command == null || a.Shortcut == null) ||
            values.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Menu actions must be nonempty and have distinct valid identities.", nameof(actions));
        Id = id; Label = label; Actions = Array.AsReadOnly(values);
    }
    /// <summary>Gets stable menu identity.</summary>
    public string Id { get; }
    /// <summary>Gets its visible heading.</summary>
    public string Label { get; }
    /// <summary>Gets frozen action declarations.</summary>
    public IReadOnlyList<MenuAction> Actions { get; }
}