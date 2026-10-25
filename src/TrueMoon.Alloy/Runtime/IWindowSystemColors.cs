using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Optional cached system colors, read on the window owner thread without native queries per frame.</summary>
public interface IWindowSystemColors
{
    /// <summary>Current user accent, or null when the platform cannot obtain it. The window owns its refresh lifecycle.</summary>
    Color? AccentColor { get; }
}
