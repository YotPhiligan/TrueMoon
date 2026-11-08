using System.Collections.Concurrent;
using System.Globalization;

using TrueMoon;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

internal sealed record OpenArguments(string Root, Guid ProjectId, Guid StoreId, bool Create,
    CatalogLimits? Limits = null, bool FailAfterOpen = false, CancellationToken Token = default,
    bool FailBeforeThreadStart = false, OwnerGate? StartupGate = null, TimeSpan? DrainTimeout = null, NomFormatLimits? StorageLimits = null,
    RestoreAssetStoreArguments? Restore = null);
internal sealed record CatalogHeader(Guid ProjectId, Guid StoreId, long Generation);
internal sealed record ThreadEvent(string Step, int Thread);
internal sealed class OwnerGate
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource StopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly ManualResetEventSlim Release = new();
}

// Research composition root: ordinary construction is confined to this typed factory.
internal sealed class CatalogFactory(int maximumOwners = 4, NomFileOperations? fileOperations = null) : IFactory<CatalogOwner, OpenArguments>, IDisposable, IAsyncDisposable
{
    private readonly object gate = new();
    private readonly HashSet<CatalogOwner> owners = [];
    private bool disposed;
    private readonly NomFileOperations files = fileOperations ?? new();
    private readonly int maximumOwners = maximumOwners is >= 1 and <= 4 ? maximumOwners : throw new ArgumentOutOfRangeException(nameof(maximumOwners));
    internal int Count { get { lock (gate) return owners.Count; } }
    public CatalogOwner Create(OpenArguments arguments)
    {
        arguments.Token.ThrowIfCancellationRequested();
        (arguments.Limits ?? new()).Validate();
        if(arguments.DrainTimeout is { } timeout && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(10))) throw new InvalidDataException("Drain timeout budget.");
        if (arguments.ProjectId == Guid.Empty || arguments.StoreId == Guid.Empty || !Path.IsPathFullyQualified(arguments.Root) || arguments.Root.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidDataException("Explicit local root/project/store identity required.");
        var canonicalRoot = Path.GetFullPath(arguments.Root);
        if (canonicalRoot.StartsWith(@"\\", StringComparison.Ordinal)) throw new InvalidDataException("A local store root is required.");
        arguments = arguments with { Root = canonicalRoot };
        CatalogOwner? owner = null;
        try
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (owners.Count >= maximumOwners) throw new InvalidOperationException("Owner capacity.");
                owner = new CatalogOwner(arguments, Closed, files); owners.Add(owner);
                owner.StartThread(); // Registry startup and stop are serialized; ready wait never holds this lock.
            }
            owner.WaitReady();
            if(!owner.RestorePublished) arguments.Token.ThrowIfCancellationRequested();
            lock (gate) ObjectDisposedException.ThrowIf(disposed, this);
            return owner;
        }
        catch (Exception creationError)
        {
            try { owner?.DisposeAfterFailedCreateAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception cleanupError) { throw new AggregateException("Store creation failed and cleanup retained an owner.", creationError, cleanupError); }
            throw;
        }
    }
    private void Closed(CatalogOwner owner) { lock (gate) owners.Remove(owner); }
    internal void CloseAdmission() { lock (gate) disposed = true; }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        CatalogOwner[] values; lock (gate) { disposed = true; values = owners.ToArray(); }
        var failures = new List<Exception>();
        // Starting these async drains requests every stop before awaiting the group. No implicit second retry.
        var draining = values.Select(async owner =>
        {
            try { await owner.DisposeAsync().ConfigureAwait(false); return (Exception?)null; }
            catch (Exception error) { return error; }
        }).ToArray();
        foreach (var error in await Task.WhenAll(draining).ConfigureAwait(false)) if (error != null) failures.Add(error);
        if (failures.Count != 0) throw new AggregateException("Catalog owners failed to drain.", failures);
    }
}

internal sealed partial class CatalogOwner : IDisposable, IAsyncDisposable
{
    private interface ICommand { int Bytes { get; } bool Control { get; } void Execute(CatalogOwner owner); }
    private sealed class Command<T>(Func<CatalogOwner, T> body, int bytes, CancellationToken token, bool control) : ICommand
    {
        internal readonly TaskCompletionSource<T> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Bytes => bytes;
        public bool Control => control;
        public void Execute(CatalogOwner owner)
        {
            try { token.ThrowIfCancellationRequested(); Completion.SetResult(body(owner)); }
            catch (OperationCanceledException ex) { Completion.SetCanceled(ex.CancellationToken); }
            catch (Exception ex) { Completion.SetException(ex); }
        }
    }
    private readonly OpenArguments arguments;
    private readonly CatalogLimits limits;
    private readonly Action<CatalogOwner> closed;
    private readonly NomFileOperations files;
    private readonly object admission = new();
    private readonly List<ThreadEvent> trace = [];
    private readonly BlockingCollection<ICommand> queue;
    private readonly Thread thread;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource drain = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AutoResetEvent retryClose = new(false);
    private bool closing;
    private NomCatalog? catalog;
    private NomKvStoreFactory? kvFactory;
    private NomKvStore? kvStore;


    private bool stopping;
    private bool started;
    private int queuedBytes;
    private int queuedData;
    private int queuedControl;
    internal int OwnerThread { get; private set; }
    internal bool IsClosed { get; private set; }
    internal int Admitted { get; private set; }
    internal bool RestorePublished { get; private set; }
    internal ThreadEvent[] Trace { get { lock (trace) return trace.ToArray(); } }
    internal string Root => Path.GetFullPath(arguments.Root);
    internal CatalogHeader Header { get; private set; } = null!;
    private NomCatalog Catalog { get { Mark("catalog-access"); return catalog ?? throw new ObjectDisposedException(nameof(CatalogOwner)); } }

    internal CatalogOwner(OpenArguments arguments, Action<CatalogOwner> closed, NomFileOperations? files = null)
    {
        this.arguments = arguments; limits = arguments.Limits ?? new(); this.closed = closed; this.files = files ?? new();
        queue = new(limits.QueueCount+1); thread = new(Run) { IsBackground = true, Name = "Nom catalog owner" };
    }
    internal void StartThread()
    {
        if (arguments.FailBeforeThreadStart) throw new InvalidDataException("Injected failed thread start.");
        thread.Start(); started = true;
    }
    internal void WaitReady() => ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    private void Mark(string step)
    {
        if (Environment.CurrentManagedThreadId != OwnerThread) throw new InvalidOperationException("Database owner thread violated.");
        lock (trace) { if(trace.Count < 4096) trace.Add(new(step, Environment.CurrentManagedThreadId)); }
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    private void CheckPath(string path)
    {
        for (FileSystemInfo? item = new DirectoryInfo(path); item != null; item = ((DirectoryInfo)item).Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reparse root unsupported.");
    }
    private void Initialize()
    {
        if (arguments.StartupGate is { } startup)
        { startup.Entered.TrySetResult(); if (!startup.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Startup proof gate."); }
        lock (admission) ObjectDisposedException.ThrowIf(stopping, this);
        arguments.Token.ThrowIfCancellationRequested(); CheckPath(Root);
        if(arguments.Restore is { } restore) RestoreBackup(restore);
        kvFactory = new NomKvStoreFactory(files, maximumOwners: 1);
        Mark("kv-open");
        var options = new NomKvOpenArguments(Root, arguments.ProjectId, arguments.StoreId, Limits: arguments.StorageLimits, Token: RestorePublished ? CancellationToken.None : arguments.Token, RepairIncompleteTail: false);
        kvStore = arguments.Create ? kvFactory.Create(options) : kvFactory.Open(options);
        catalog = new NomCatalog(kvStore,limits.DomainIndexBytes);
        if (arguments.Create) Catalog.Create(arguments.ProjectId, arguments.StoreId);
        Header = ReadHeader(); Catalog.ValidateAll(); RecoverResources();
        if (arguments.FailAfterOpen) throw new InvalidDataException("Injected failed startup after catalog initialization.");
    }
    private CatalogHeader ReadHeader()
    {
        Mark("read-header"); var header = Catalog.Header;
        Require(header.ProjectId == arguments.ProjectId && header.StoreId == arguments.StoreId && header.Generation == kvStore!.Generation, "Catalog header/authority mismatch.");
        return header;
    }
    private void Begin() { Mark("begin"); Catalog.Begin(); }
    private void Commit() { Mark("commit"); Catalog.Commit(); }
    private void Rollback(Exception? cause = null) { Mark("rollback"); Catalog.Rollback(); }
    private void Run()
    {
        OwnerThread = Environment.CurrentManagedThreadId; Exception? failure = null;
        try
        {
            Initialize();
            lock (admission) { ObjectDisposedException.ThrowIf(stopping, this); ready.SetResult(); }
            foreach (var command in queue.GetConsumingEnumerable())
            {
                lock (admission) { queuedBytes -= command.Bytes; if(command.Control) queuedControl--; else queuedData--; }
                Mark("command"); command.Execute(this);
            }
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            lock (admission) { stopping = true; closing = true; }
            ready.TrySetException(failure ?? new ObjectDisposedException(nameof(CatalogOwner)));
            while (!IsClosed)
            {
                try
                {
                    if(backupVerification!=null) { backupVerification.Dispose(); backupVerification=null; }
                    if(backupTransfer!=null) { backupTransfer.CloseAll(); backupTransfer=null; }
                    if (kvFactory != null) { Mark("kv-close"); kvFactory.Dispose(); kvFactory = null; kvStore = null; catalog = null; }

                    Closed();
                }
                catch (Exception error)
                {
                    ReportDrainFailure(error);
                    // Keep the same owner thread and handles alive; only an explicit later drain requests a retry.
                    if (kvFactory?.IsReleased == true) Closed(); else retryClose.WaitOne();
                }
            }
        }
    }
    private void ReportDrainFailure(Exception error)
    {
        lock (admission)
        {
            drain.TrySetException(error);
            _ = drain.Task.Exception;
        }
    }
    private void Closed()
    {
        lock (admission) { IsClosed = true; retryClose.Dispose(); }
        closed(this); exited.TrySetResult(); drain.TrySetResult();
    }
    private Task<T> Enqueue<T>(Func<CatalogOwner, T> body, int bytes = 0, CancellationToken token = default, bool control = false)
    {
        token.ThrowIfCancellationRequested(); var command = new Command<T>(body, bytes, token, control);
        lock (admission)
        {
            if ((stopping && !control) || IsClosed) throw new ObjectDisposedException(nameof(CatalogOwner));
            if ((control ? queuedControl >= 1 : queuedData >= limits.QueueCount) || bytes > limits.QueueBytes - queuedBytes || !queue.TryAdd(command)) throw new InvalidOperationException("Bounded queue full.");
            if(control) queuedControl++; else queuedData++;
            queuedBytes += bytes; Admitted++;
        }
        return command.Completion.Task;
    }
    internal Task<long> AddAsync(long expectedGeneration, IReadOnlyList<AssetRecord> rows, bool failAfterFirst = false, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (rows.Count is < 1 || rows.Count > limits.BatchRows) throw new InvalidDataException("Batch row budget.");
        var encoded = new List<byte[]>(); var count = 0;
        foreach (var row in rows)
        {
            var bytes = CatalogCodec.Encode(row, limits.DocumentBytes); count = checked(count + bytes.Length);
            if (count > limits.BatchBytes) throw new InvalidDataException("Batch serialized byte budget.");
            encoded.Add(bytes);
        }
        return Enqueue(owner =>
        {
            owner.RequireResourceReady();
            owner.RequireNoGarbage();
            lock(owner.admission) { if(owner.activeResourceWrite != null) throw new InvalidOperationException("Resource write is active."); }
            Require(owner.Header.Generation == expectedGeneration, "Stale generation.");
            var next = checked(expectedGeneration + 1); owner.Begin();
            try
            {
                for (var i = 0; i < encoded.Count; i++)
                {
                    token.ThrowIfCancellationRequested(); owner.Mark("insert");
                    owner.Catalog.PutAsset(CatalogCodec.Decode(encoded[i]), insertOnly: true);
                    if (i == 0 && failAfterFirst) throw new InvalidDataException("Injected failure after first insert.");
                }
                token.ThrowIfCancellationRequested();
                var header = owner.Header with { Generation = next };
                owner.Catalog.SetHeader(header);
                owner.Commit(); owner.Header = owner.Header with { Generation = next }; return next;
            }
            catch (Exception cause) { owner.Rollback(cause); throw; }
        }, count, token);
    }
    internal Task<AssetRecord[]> ReadAsync() => Enqueue(owner =>
    {
        owner.RequireResourceReady();
        return owner.Catalog.Assets();
    });
    internal Task HoldAsync(OwnerGate gate) => Enqueue(owner =>
    { gate.Entered.SetResult(); if (!gate.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Proof gate."); return true; });
    private Task BeginStop(bool retry)
    {
        var request = false;
        Task attempt;
        lock (admission)
        {
            if (IsClosed) return exited.Task;
            if (retry && drain.Task.IsFaulted)
            {
                drain = new(TaskCreationOptions.RunContinuationsAsynchronously);
                if (closing) retryClose.Set();
            }
            if (!stopping) { stopping = true; request = true; CompleteAfterResourceDrain(); arguments.StartupGate?.StopRequested.TrySetResult(); }
            attempt = drain.Task;
        }
        try { if(request) resourceShutdown.Cancel(); }
        catch (Exception error) { ReportDrainFailure(error); }
        try { if (!closing) StopReservedResource(); }
        catch (Exception error) { ReportDrainFailure(error); }
        if (!started) Closed();
        return attempt;
    }
    internal void RequestStop() => _ = BeginStop(true);
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => WaitForDrainAsync(BeginStop(true));
    internal ValueTask DisposeAfterFailedCreateAsync() => WaitForDrainAsync(BeginStop(false));
    private async ValueTask WaitForDrainAsync(Task attempt)
    {
        await attempt.WaitAsync(arguments.DrainTimeout ?? TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await exited.Task.ConfigureAwait(false);
        if (!started) return;
        if (!thread.Join(TimeSpan.FromSeconds(1))) throw new TimeoutException("Owner thread exit not confirmed.");
    }
}
