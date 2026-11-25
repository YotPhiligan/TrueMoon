namespace TrueMoon.Aether.Mods;

/// <summary>Static entry factory invoked by the host's injected factory, never by the root DI container.</summary>
/// <remarks>A failed Create must release its unpublished resources. Created entry results belong to the module group.</remarks>
public interface IModEntryPoint
{
    /// <summary>Creates an entry with constructor dependencies obtained once during composition.</summary>
    /// <param name="services">Temporary, stable-host-contract composition access. Invalid after registration.</param>
    /// <returns>Fresh, exclusively group-owned entry.</returns>
    static abstract IModEntry Create(IModHostServices services);
}

/// <summary>Module-owned composition and cooperative lifecycle. No callbacks may remain active after StopAsync succeeds.</summary>
public interface IModEntry : IAsyncDisposable
{
    /// <summary>Registers local factories before any product is activated.</summary>
    /// <param name="registration">Temporary group-local registration; unavailable after this callback.</param>
    void Register(IModRegistration registration);
    /// <summary>Starts private tasks/subscriptions after registration and service activation.</summary>
    /// <param name="cancellationToken">Cooperative admission/start cancellation.</param>
    ValueTask StartAsync(CancellationToken cancellationToken);
    /// <summary>Drains tasks/subscriptions, including partially failed startup. Must support retry after failure.</summary>
    /// <param name="cancellationToken">Cooperative shutdown token.</param>
    ValueTask StopAsync(CancellationToken cancellationToken);
}

/// <summary>Optional local service product. Successful Stop drains all callbacks; failed cleanup remains group-owned for retry.</summary>
public interface IModService : IAsyncDisposable
{
    /// <summary>Starts one exclusively owned factory result.</summary><param name="cancellationToken">Cooperative cancellation.</param>
    ValueTask StartAsync(CancellationToken cancellationToken);
    /// <summary>Stops a fully or partially started product.</summary><param name="cancellationToken">Cooperative cancellation.</param>
    ValueTask StopAsync(CancellationToken cancellationToken);
}

/// <summary>Composition-only view of explicitly allowed stable host contracts. Not a per-frame service locator.</summary>
public interface IModHostServices
{
    /// <summary>Gets a borrowed host dependency. Module types, including nested generic arguments, are forbidden.</summary>
    /// <typeparam name="T">Stable shared/framework contract.</typeparam><returns>Borrowed host service.</returns>
    T GetRequired<T>() where T : class;
}
