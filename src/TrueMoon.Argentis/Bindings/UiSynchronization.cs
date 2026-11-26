namespace TrueMoon.Argentis.Bindings;

/// <summary>Controls when an attachment observer synchronizes its initial state.</summary>
public enum UiSynchronization
{
    /// <summary>Synchronize inline after subscribing.</summary>
    Immediate,

    /// <summary>Queue synchronization on the current attachment so it can safely change the tree.</summary>
    AfterTreeChange,
}
