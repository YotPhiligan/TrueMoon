namespace TrueMoon.Argentis.Menus;

/// <summary>Disclosure/menu capability independent of a concrete renderer.</summary>
public interface IMenu
{
    /// <summary>Gets whether top-level headings are being revealed.</summary>
    bool IsExpanded { get; }
    /// <summary>Gets whether a dropdown is open.</summary>
    bool IsMenuOpen { get; }
    /// <summary>Closes the dropdown and collapses its disclosure.</summary>
    void CloseMenu();
}