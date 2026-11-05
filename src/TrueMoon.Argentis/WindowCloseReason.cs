namespace TrueMoon.Argentis;

/// <summary>Reason for a host-coordinated close attempt.</summary>
public enum WindowCloseReason
{
    /// <summary>An application or component close request; handlers may veto.</summary>
    Application,
    /// <summary>An operating-system close request; handlers may veto.</summary>
    Native,
    /// <summary>Terminal host/App shutdown; vetoes are ignored, all callbacks drain and errors remain observable.</summary>
    Shutdown
}
