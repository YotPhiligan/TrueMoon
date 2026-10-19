namespace TrueMoon.Alloy.Hosting;

/// <summary>Origin of an asynchronous window close attempt.</summary>
public enum UiCloseReason
{
    /// <summary>A cancellable API request.</summary>
    Application,
    /// <summary>A cancellable native title bar or system close request.</summary>
    Native,
    /// <summary>Shutdown requested by Close, StopAsync or the application dispatcher; vetoes are ignored.</summary>
    Shutdown
}
