using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Hosting;

/// <summary>App-owned store registry and typed factory. Borrow this service; dispose individual products when their project closes.</summary>
/// <remarks>The host retains live and failed store owners until actual drain. Stop closes admission before awaiting I/O.
/// Drain timeouts can be retried through StopAsync/DisposeAsync after returning leases; cancelling a stop wait never releases live catalog/root ownership.
/// Transient close IO failures retain remaining file handles and retry on the same owner thread.
/// Catalog IO uses the owned Nom KV journal; unknown commits require close/reopen and operation lookup.</remarks>
public sealed class NomStoreHost : IFactory<IProjectAssetStore, OpenAssetStoreArguments>, IStartable, IStoppable, IDisposable, IAsyncDisposable
{
    private readonly CatalogFactory factory;
    private readonly NomSettings settings;
    private readonly object cleanupGate = new();
    private Task? cleanup;
    private bool stopping;
    internal NomStoreHost(CatalogFactory factory, NomSettings settings)
    { this.factory = factory; this.settings = settings; }
    /// <summary>Gets the number of live or retained failed store owners in this App.</summary>
    public int ActiveStoreCount => factory.Count;
    /// <summary>Whether one cleanup attempt has confirmed all store owners released.</summary>
    public bool IsReleased { get { lock (cleanupGate) return cleanup?.IsCompletedSuccessfully == true; } }
    /// <summary>Opens an explicitly identified store without transferring sole disposal responsibility to DI.</summary>
    /// <param name="arguments">Root, identities, mode and opening cancellation.</param>
    /// <returns>A caller-disposable store also retained by this host for App fallback cleanup.</returns>
    public IProjectAssetStore Create(OpenAssetStoreArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); arguments.CancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Root);
        if (arguments.ProjectId == Guid.Empty || arguments.StoreId == Guid.Empty || !Path.IsPathFullyQualified(arguments.Root) || arguments.Root.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("Explicit local root/project/store identity required.", nameof(arguments));
        var root = Path.GetFullPath(arguments.Root);
        if (root.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("A local store root is required.", nameof(arguments));
        if (!Enum.IsDefined(arguments.Mode)) throw new ArgumentOutOfRangeException(nameof(arguments), "Unsupported store open mode.");
        var owner = factory.Create(new(root, arguments.ProjectId, arguments.StoreId, arguments.Mode == AssetStoreOpenMode.Create,
            settings.Limits, Token: arguments.CancellationToken, DrainTimeout: settings.DrainTimeout));
        return new ProjectAssetStore(owner);
    }
    /// <summary>Verifies an immutable backup into a new prepared root, publishes it, then opens a caller-owned store.</summary>
    /// <param name="arguments">Expected identities, immutable backup, absent destination and transfer limits.</param>
    /// <returns>A restored store retained by this host until actual cleanup.</returns>
    public IProjectAssetStore Create(RestoreAssetStoreArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); arguments.CancellationToken.ThrowIfCancellationRequested();
        if(arguments.ProjectId==Guid.Empty || arguments.StoreId==Guid.Empty) throw new ArgumentException("Nonempty restore identities required.",nameof(arguments));
        var captured=arguments with { Root=NomBackupTransfer.LocalRoot(arguments.Root),BackupRoot=NomBackupTransfer.LocalRoot(arguments.BackupRoot),Options=(arguments.Options??new()).Snapshot() };
        var owner=factory.Create(new(captured.Root,captured.ProjectId,captured.StoreId,false,settings.Limits,Token:captured.CancellationToken,DrainTimeout:settings.DrainTimeout,Restore:captured));
        return new ProjectAssetStore(owner);
    }
    /// <summary>Participates in startup without opening a catalog or creating a worker.</summary>
    /// <param name="cancellationToken">Startup cancellation.</param><returns>Immediate completion while admission remains open.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (cleanupGate) ObjectDisposedException.ThrowIf(stopping, this);
        return Task.CompletedTask;
    }
    /// <summary>Closes admission immediately and waits for shared cleanup. A cancelled waiter does not cancel cleanup.</summary>
    /// <param name="cancellationToken">Cancellation of this wait only.</param><returns>Confirmed drain, or a retained cleanup failure.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default) => BeginCleanup().WaitAsync(cancellationToken);
    private Task BeginCleanup()
    {
        TaskCompletionSource? attempt = null;
        Task shared;
        lock (cleanupGate)
        {
            stopping = true; factory.CloseAdmission();
            // Cache success/running work, but allow an explicit retry after a failed attempt.
            if (cleanup == null || cleanup.IsCompleted && !cleanup.IsCompletedSuccessfully)
            { attempt = new(TaskCreationOptions.RunContinuationsAsynchronously); cleanup = attempt.Task; }
            shared = cleanup;
        }
        // Do not hold the host lock while cancellation callbacks or store teardown execute.
        if (attempt != null) _ = DrainAsync(attempt);
        return shared;
    }
    private async Task DrainAsync(TaskCompletionSource attempt)
    {
        try { await factory.DisposeAsync().ConfigureAwait(false); attempt.TrySetResult(); }
        catch (Exception error)
        {
            attempt.TrySetException(error);
            _ = attempt.Task.Exception; // A cancelled waiter still leaves an observed, retryable failure.
        }
    }
    /// <summary>Stops and synchronously drains owned stores. Dispose products/streams first to avoid a retained timeout.</summary>
    public void Dispose() => BeginCleanup().GetAwaiter().GetResult();
    /// <summary>Stops and asynchronously drains owned stores; timed-out drain attempts remain retryable after returning leases.</summary>
    /// <returns>Completion after actual store closure.</returns>
    public ValueTask DisposeAsync() => new(BeginCleanup());
}
