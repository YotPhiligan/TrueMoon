namespace TrueMoon.Aether.Editor.Editing;

// Per-attempt, positive-directory-only reuse. Every cached path has a pinned chain
// to its drive root. Missing paths and over-capacity chains keep the original fresh checks.
internal sealed class EditorSourceDirectoryCache : IDisposable
{
    internal const int MaximumCachedDirectories = 256;
    private readonly CancellationToken _token;
    private readonly Func<string, IDisposable?> _pin;
    private readonly Func<string, FileAttributes?> _attributes;
    private readonly int _maximum;
    private readonly Dictionary<string, IDisposable> _pins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDisposable> _retired = [];
    private bool _disposed;

    internal EditorSourceDirectoryCache(CancellationToken token, Func<string, IDisposable?>? pin = null,
        Func<string, FileAttributes?>? attributes = null, int maximumDirectories = MaximumCachedDirectories)
    {
        if (maximumDirectories is < 0 or > MaximumCachedDirectories) throw new ArgumentOutOfRangeException(nameof(maximumDirectories));
        _token = token; _pin = pin ?? OptionalPin; _attributes = attributes ?? ReadAttributes; _maximum = maximumDirectories;
    }

    internal int Count => _pins.Count;

    internal void ValidatePath(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); _token.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new NotSupportedException("Source observation requires representable local drive paths.");
        var parent = Path.GetDirectoryName(path);
        if (parent != null && !_pins.ContainsKey(parent)) ValidateAncestors(parent);
    }

    private void ValidateAncestors(string directory)
    {
        // Iterative root-first acquisition also handles deep explicit paths without recursive stack growth.
        var pending = new List<string>();
        for (var current = directory; current != null && !_pins.ContainsKey(current); current = Path.GetDirectoryName(current))
        { _token.ThrowIfCancellationRequested(); pending.Add(current); }
        var pinnedAncestors = true;
        for (var index = pending.Count - 1; index >= 0; index--)
            pinnedAncestors = ValidateDirectory(pending[index], pinnedAncestors);
    }

    private bool ValidateDirectory(string directory, bool pinnedAncestors)
    {
        _token.ThrowIfCancellationRequested();
        if (_pins.ContainsKey(directory)) return true;
        if (!pinnedAncestors || _pins.Count >= _maximum) { Check(directory); return false; }
        var pin = _pin(directory);
        if (pin == null) { Check(directory); return false; }
        FileAttributes? attributes;
        try
        {
            _token.ThrowIfCancellationRequested();
            attributes = Check(directory);
        }
        catch (Exception original)
        {
            ReleaseRejected(pin, original);
            throw;
        }
        if (attributes == null || (attributes.Value & FileAttributes.Directory) == 0) { ReleaseRejected(pin); return false; }
        try { _pins.Add(directory, pin); }
        catch (Exception original) { ReleaseRejected(pin, original); throw; }
        return true;
    }

    private void ReleaseRejected(IDisposable pin, Exception? original = null)
    {
        try { pin.Dispose(); }
        catch (Exception cleanup)
        {
            // Failed cleanup is never a successful validation/cache hit; retain its owner for Dispose retry.
            _retired.Add(pin);
            if (original != null) throw new AggregateException(original, cleanup);
            throw;
        }
    }

    internal void Revalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var directory in _pins.Keys)
        {
            _token.ThrowIfCancellationRequested();
            // Re-authorize by path, not a stale handle grant; detect persistent in-place
            // reparse/ACL changes even while rename/delete are fenced by the pin.
            var attributes = Check(directory);
            if (attributes == null || (attributes.Value & FileAttributes.Directory) == 0)
                throw new IOException("Source ancestor directory changed during observation: " + directory);
        }
        _token.ThrowIfCancellationRequested();
    }

    private FileAttributes? Check(string directory)
    {
        var attributes = _attributes(directory);
        if ((attributes.GetValueOrDefault() & FileAttributes.ReparsePoint) != 0)
            throw new NotSupportedException("Source observation does not traverse reparse points.");
        return attributes;
    }
    private static FileAttributes? ReadAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static IDisposable? OptionalPin(string path)
    {
        try { return EditorSourceDirectoryPin.Open(path); }
        // Pins require directory-list access to establish sharing. Metadata-only access
        // remains supported via uncached checks; the following Check still propagates
        // real metadata access/reparse errors. No unpinned chain is memoized.
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Dispose()
    {
        _disposed = true;
        List<Exception>? errors = null;
        foreach (var pin in _retired.AsEnumerable().Reverse().ToArray())
        {
            try { pin.Dispose(); _retired.Remove(pin); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        foreach (var pair in _pins.Reverse().ToArray())
        {
            try { pair.Value.Dispose(); _pins.Remove(pair.Key); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors != null) throw new AggregateException(errors);
    }
}
