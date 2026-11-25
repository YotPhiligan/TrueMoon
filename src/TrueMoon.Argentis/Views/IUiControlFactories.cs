namespace TrueMoon.Argentis.Views;

/// <summary>Provides control factories to the view builder without a dependency on a host or container.</summary>
public interface IUiControlFactories
{
    /// <summary>Returns a live, unowned control that the content builder can adopt.</summary>
    TControl Create<TControl>() where TControl : Element;

    /// <summary>Creates a control whose factory requires typed arguments.</summary>
    TControl Create<TControl, TArguments>(TArguments arguments) where TControl : Element;
}
