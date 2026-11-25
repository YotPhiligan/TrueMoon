using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Layout;

/// <summary>A content container with a theme-colored outline.</summary>
public class Border : ContentControl
{
    /// <inheritdoc />
    protected override float ThemeBorderWidth => Theme.BorderWidth;
}