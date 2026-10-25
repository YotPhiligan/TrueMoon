namespace TrueMoon.Argentis;

/// <summary>Optional activity signal from custom header content whose menus may receive focus outside the title bar.</summary>
public interface IWindowHeaderActivity
{
    /// <summary>Whether window chrome should retain its active background while header interaction is in progress.</summary>
    bool KeepsTitleBarActive { get; }
}
