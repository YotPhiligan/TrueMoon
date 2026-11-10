using TrueMoon.Diagnostics;

namespace TrueMoon.Configuration;

internal sealed class ConfigurationNotifications
{
    private static readonly IEventsSource Events = new EventsSource("TrueMoon.Configuration");
    private readonly object _sync = new();
    // A retiring callback retains its capacity slot until it returns.
    private readonly HashSet<Subscription> _subscriptions = [];
    private bool _closed;

    internal IDisposable Subscribe(Action<ConfigurationUpdate> observer)
    {
        lock (_sync)
        {
            if (_closed) throw new InvalidOperationException("Configuration notifications have stopped.");
            if (_subscriptions.Count >= 64) throw new InvalidOperationException("Configuration subscription capacity reached.");
            var subscription = new Subscription(this, observer);
            _subscriptions.Add(subscription);
            return subscription;
        }
    }

    internal void Publish(ConfigurationUpdate update)
    {
        lock (_sync)
        {
            if (_closed) return;
            foreach (var subscription in _subscriptions)
            {
                if (!subscription.Active) continue;
                subscription.Pending = subscription.Pending is { } prior
                    ? new(update.Generation, true, false, prior.ChangedKeys.Concat(update.ChangedKeys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())
                    : update;
                subscription.Worker ??= Task.Run(() => Deliver(subscription));
            }
        }
    }

    internal Task Close()
    {
        lock (_sync)
        {
            _closed = true;
            foreach (var subscription in _subscriptions) { subscription.Active = false; subscription.Pending = null; }
            _subscriptions.RemoveWhere(subscription => subscription.Worker is null);
            return Task.WhenAll(_subscriptions.Select(subscription => subscription.Worker ?? Task.CompletedTask));
        }
    }

    private void Deliver(Subscription subscription)
    {
        while (true)
        {
            ConfigurationUpdate update;
            lock (_sync)
            {
                if (!subscription.Active || subscription.Pending is null)
                {
                    subscription.Worker = null;
                    if (!subscription.Active) _subscriptions.Remove(subscription);
                    return;
                }
                update = subscription.Pending;
                subscription.Pending = null;
            }
            try { subscription.Observer(update); }
            catch (Exception)
            {
                try { Events.Exception(ConfigurationMetadata.Error("NotificationCallback")); }
                catch (Exception) { /* Diagnostics cannot roll back a publication or fault its delivery worker. */ }
            }
        }
    }

    private sealed class Subscription(ConfigurationNotifications owner, Action<ConfigurationUpdate> observer) : IDisposable
    {
        internal readonly Action<ConfigurationUpdate> Observer = observer;
        internal bool Active = true;
        internal ConfigurationUpdate? Pending;
        internal Task? Worker;
        public void Dispose()
        {
            lock (owner._sync)
            {
                Active = false; Pending = null;
                if (Worker is null) owner._subscriptions.Remove(this);
            }
        }
    }
}
