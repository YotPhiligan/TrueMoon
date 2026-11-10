namespace TrueMoon.Configuration;

/// <summary>Bounded asynchronous notifications of successful publications.</summary>
public interface IConfigurationChanges
{
    /// <summary>Subscribes outside the publication lock. At most 64 subscriptions are admitted.</summary>
    /// <param name="observer">Short callback. May reload or unsubscribe; must not wait for App shutdown.</param>
    /// <returns>A subscription handle, owning only its subscription.</returns>
    IDisposable Subscribe(Action<ConfigurationUpdate> observer);
}
