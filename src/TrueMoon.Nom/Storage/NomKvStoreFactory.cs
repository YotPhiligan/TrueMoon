namespace TrueMoon.Nom.Storage;

// Low-level owner/factory; its App registration and resource-catalog adapter belong to NOM2.
internal sealed class NomKvStoreFactory : IDisposable
{
    private readonly object gate = new();
    private readonly HashSet<NomKvStore> owners = [];
    private readonly NomKvFileOperations files;
    private readonly int maximumOwners;
    private bool stopping;
    internal int ActiveStoreCount { get { lock (gate) return owners.Count; } }
    internal bool IsReleased { get { lock (gate) return stopping && owners.Count == 0; } }

    internal NomKvStoreFactory(NomKvFileOperations? files = null, int maximumOwners = 4)
    {
        if (maximumOwners is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(maximumOwners));
        this.files = files ?? new(); this.maximumOwners = maximumOwners;
    }
    internal NomKvStore Create(NomKvOpenArguments arguments) => OpenCore(arguments, create: true);
    internal NomKvStore Open(NomKvOpenArguments arguments) => OpenCore(arguments, create: false);
    private NomKvStore OpenCore(NomKvOpenArguments arguments, bool create)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments.Root);
        if (arguments.ProjectId == Guid.Empty || arguments.StoreId == Guid.Empty) throw new ArgumentException("Nonempty Nom identities are required.", nameof(arguments));
        if (!Path.IsPathFullyQualified(arguments.Root) || arguments.Root.StartsWith(@"\\", StringComparison.Ordinal) || arguments.Root.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("A full local Nom root is required.", nameof(arguments));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(arguments.Root));
        if (root == Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!)) throw new ArgumentException("A project root cannot be a volume root.", nameof(arguments));
        var limits = (arguments.Limits ?? new()) with { }; limits.Validate(); arguments.Token.ThrowIfCancellationRequested();
        NomKvStore store;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(stopping, this);
            if (owners.Count >= maximumOwners) throw new InvalidOperationException("Nom KV owner capacity reached.");
            store = new(arguments with { Root = root, Limits = limits }, files, Closed); owners.Add(store);
        }
        try
        {
            store.Initialize(create);
            lock (gate) ObjectDisposedException.ThrowIf(stopping, this);
            return store;
        }
        catch (Exception creationError)
        {
            try { store.Dispose(); }
            catch (Exception cleanupError) { throw new AggregateException("Nom KV open and cleanup failed; live owner retained by factory.", creationError, cleanupError); }
            throw;
        }
    }
    private void Closed(NomKvStore owner) { lock (gate) owners.Remove(owner); }
    public void Dispose()
    {
        NomKvStore[] snapshot;
        lock (gate) { stopping = true; snapshot = owners.ToArray(); }
        foreach (var owner in snapshot) owner.RequestStop();
        List<Exception>? errors = null;
        foreach (var owner in snapshot)
        {
            try { owner.Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors != null) throw new AggregateException("Nom KV factory drain failed; retry retains live resources.", errors);
    }
}
