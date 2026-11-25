using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis.Menus;

/// <summary>Immutable menu action description; contains no controls or native resources.</summary>
/// <param name="Id">Stable action identity.</param><param name="Text">Visible action name.</param><param name="Command">Borrowed shared command.</param>
/// <param name="Icon">Optional geometric symbol.</param><param name="Shortcut">Visible keyboard hint.</param>
public sealed record MenuAction(string Id, string Text, IUiCommand Command, UiSymbol? Icon = null, string Shortcut = "");