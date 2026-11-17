namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Persistent source identity and explicit sidecar location. Rename/move the sidecar with its source; copy gets a new SourceId.</summary>
/// <param name="SourceId">Persistent source identity.</param><param name="Path">Absolute caller-owned sidecar path.</param>
public sealed record ModelImportLedgerArguments(Guid SourceId,string Path);

/// <summary>Caller-owned serial session. App Stop cancels/drains active IO; snapshots remain caller-owned.</summary>
public sealed class ModelImportLedgerSession : IAsyncDisposable
{
    private readonly object _gate=new();
    private readonly ModelImportHost _host;
    private readonly IModelImportLedgerStore _store;
    private readonly ModelImportLedgerArguments _arguments;
    private readonly CancellationTokenSource _stop=new();
    private Task? _operation,_dispose;
    private bool _disposed;
    private PublishedModelImport? _current;
    internal ModelImportLedgerSession(ModelImportHost host,IModelImportLedgerStore store,ModelImportLedgerArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); _host=host; _store=store;
        _arguments=arguments with { Path=FileModelImportLedgerStore.Validate(arguments.Path,arguments.SourceId) };
    }
    /// <summary>Last successfully opened/committed snapshot. Stale/failed/cancelled publication does not replace it.</summary>
    public PublishedModelImport? Current { get { lock (_gate) return _current; } }
    /// <summary>Read-only open. An absent sidecar returns null and performs no writes or implicit migration.</summary>
    /// <param name="cancellationToken">Read cancellation.</param><returns>The complete current generation or null.</returns>
    public Task<PublishedModelImport?> OpenAsync(CancellationToken cancellationToken=default) => Start(token=>_store.ReadAsync(_arguments.Path,_arguments.SourceId,token),cancellationToken);
    /// <summary>Atomically publishes a complete candidate and its reconciled permanent identities.</summary>
    /// <param name="candidate">Validated CPU result.</param><param name="expectedGeneration">Exact base generation, zero if absent.</param><param name="edits">Explicit identity decisions.</param><param name="cancellationToken">Cancellation before the store commit point.</param><returns>The committed immutable snapshot.</returns>
    public async Task<PublishedModelImport> PublishAsync(ModelImportCandidate candidate,long expectedGeneration,ModelImportIdentityEdits? edits=null,CancellationToken cancellationToken=default) =>
        (await Start(async token=>await _store.PublishAsync(_arguments.Path,_arguments.SourceId,expectedGeneration,candidate,edits,token).ConfigureAwait(false),cancellationToken).ConfigureAwait(false))!;
    private Task<PublishedModelImport?> Start(Func<CancellationToken,Task<PublishedModelImport?>> action,CancellationToken caller)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if (_operation is { IsCompleted:false }) throw new InvalidOperationException("Model ledger session is already operating.");
            caller.ThrowIfCancellationRequested(); _host.Enter(this); var task=RunAsync(action,caller); _operation=task; return task;
        }
    }
    private async Task<PublishedModelImport?> RunAsync(Func<CancellationToken,Task<PublishedModelImport?>> action,CancellationToken caller)
    {
        await Task.Yield(); using var linked=CancellationTokenSource.CreateLinkedTokenSource(caller,_stop.Token);
        try { var result=await action(linked.Token).ConfigureAwait(false); lock (_gate) _current=result; return result; }
        finally { _host.Exit(this); }
    }
    /// <summary>Cancels/drains active IO; import/store errors remain observable on their original tasks.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed=true; if(_dispose==null || _dispose.IsFaulted) _dispose=DisposeCoreAsync(); return new(_dispose);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await Task.Yield(); _stop.Cancel(); Task? operation; lock (_gate) operation=_operation;
        if (operation!=null) { try { await operation.ConfigureAwait(false); } catch { } }
        lock (_gate) _operation=null; _stop.Dispose();
    }
}
