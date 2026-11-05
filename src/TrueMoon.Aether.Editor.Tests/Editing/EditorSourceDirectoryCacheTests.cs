using System.Security;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceDirectoryCacheTests
{
    [Fact]
    public void Validate_ReusesSharedAncestorsCaseInsensitivelyUntilFinalRevalidation()
    {
        var ledger = new Ledger(); var path = Local("cache", "project", "one.cs");
        using var cache = ledger.Cache();
        cache.ValidatePath(path);
        var expected = Ancestors(path).Reverse().ToArray();
        Assert.Equal(expected, ledger.Pins.Select(pin => pin.Path));
        Assert.Equal(expected.Length, cache.Count);
        Assert.All(expected, directory => Assert.Equal(1, ledger.Reads[directory]));
        cache.ValidatePath(Local("cache", "project", "two.cs").ToUpperInvariant());
        Assert.Equal(expected.Length, ledger.Pins.Count);
        Assert.All(expected, directory => Assert.Equal(1, ledger.Reads[directory]));
        cache.Revalidate();
        Assert.All(expected, directory => Assert.Equal(2, ledger.Reads[directory]));
        Assert.All(ledger.Pins, pin => Assert.Equal(0, pin.Disposals));
        cache.Dispose(); cache.Dispose();
        Assert.Equal(0, cache.Count);
        Assert.Equal(expected.Reverse(), ledger.Released);
        Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
        Assert.Throws<ObjectDisposedException>(() => cache.ValidatePath(path));
        Assert.Throws<ObjectDisposedException>(cache.Revalidate);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(255)] [InlineData(256)]
    public void Validate_CapBoundaryFallsBackWithoutRetainingAdditionalPins(int capacity)
    {
        var ledger = new Ledger(); using var cache = ledger.Cache(capacity: capacity);
        for (var index = 0; index <= EditorSourceDirectoryCache.MaximumCachedDirectories; index++)
            cache.ValidatePath(Local("cap", "d" + index, "file.cs"));
        Assert.Equal(capacity, cache.Count); Assert.Equal(capacity, ledger.Pins.Count);
        var lastDirectory = Local("cap", "d256");
        Assert.Equal(1, ledger.Reads[lastDirectory]);
        cache.ValidatePath(Path.Combine(lastDirectory, "other.cs"));
        Assert.Equal(2, ledger.Reads[lastDirectory]); // Capacity fallback still performs a fresh coverage check.
        Assert.Equal(capacity, ledger.Pins.Count);
        var beforeRevalidation = ledger.Reads.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        cache.Revalidate();
        foreach (var pin in ledger.Pins) Assert.Equal(beforeRevalidation[pin.Path] + 1, ledger.Reads[pin.Path]);
        cache.Dispose();
        Assert.Equal(0, cache.Count); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Theory]
    [InlineData(0)] [InlineData(256)]
    public void Validate_OverflowFallbackFreshlyRejectsAnAncestorThatBecomesReparse(int capacity)
    {
        var ledger = new Ledger(); var overflow = Local("cap", "d256"); var changed = false;
        ledger.Attributes = directory => changed && directory == overflow ? FileAttributes.Directory | FileAttributes.ReparsePoint : FileAttributes.Directory;
        using var cache = ledger.Cache(capacity: capacity);
        for (var index = 0; index <= EditorSourceDirectoryCache.MaximumCachedDirectories; index++)
            cache.ValidatePath(Local("cap", "d" + index, "first.cs"));
        Assert.Equal(capacity, cache.Count); Assert.Equal(capacity, ledger.Pins.Count); Assert.Equal(1, ledger.Reads[overflow]);
        changed = true;
        Assert.Throws<NotSupportedException>(() => cache.ValidatePath(Path.Combine(overflow, "next.cs")));
        Assert.Equal(2, ledger.Reads[overflow]); Assert.Equal(capacity, cache.Count); Assert.Equal(capacity, ledger.Pins.Count);
        cache.Dispose(); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Theory]
    [InlineData(-1)] [InlineData(257)] [InlineData(258)]
    public void Constructor_InvalidCapacityRejectsBeforeOpeningAnything(int capacity)
    {
        var ledger = new Ledger();
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Cache(capacity: capacity));
        Assert.Equal("maximumDirectories", error.ParamName);
        Assert.Empty(ledger.Pins); Assert.Empty(ledger.Reads);
    }

    [Theory]
    [InlineData("relative.cs")] [InlineData(@"\rooted\file.cs")] [InlineData(@"\\host\share\file.cs")]
    public void Validate_UnrepresentablePathDoesNotOpenOrCacheDirectories(string path)
    {
        var ledger = new Ledger(); using var cache = ledger.Cache();
        Assert.Throws<NotSupportedException>(() => cache.ValidatePath(path));
        Assert.Empty(ledger.Pins); Assert.Empty(ledger.Reads); Assert.Equal(0, cache.Count);
    }

    [Theory]
    [InlineData("missing")] [InlineData("file")] [InlineData("unleased")]
    public void Validate_MissingNonDirectoryAndUnpinnableResultsAreNeverMemoized(string kind)
    {
        var ledger = new Ledger(); var weak = Local("cache", "weak"); var ready = false;
        ledger.PinOverride = path => !ready && path == weak && kind == "unleased" ? null : ledger.Create(path);
        ledger.Attributes = path => !ready && path == weak ? kind switch
        { "missing" => null, "file" => FileAttributes.Normal, _ => FileAttributes.Directory } : FileAttributes.Directory;
        using var cache = ledger.Cache();
        cache.ValidatePath(Path.Combine(weak, "one.cs")); cache.ValidatePath(Path.Combine(weak, "two.cs"));
        Assert.Equal(2, ledger.PinAttempts[weak]); Assert.Equal(2, ledger.Reads[weak]);
        Assert.Equal(Ancestors(Path.Combine(weak, "one.cs")).Count() - 1, cache.Count);
        Assert.All(ledger.Pins.Where(pin => pin.Path == weak), pin => Assert.Equal(1, pin.Disposals));
        ready = true; cache.ValidatePath(Path.Combine(weak, "three.cs"));
        Assert.Equal(3, ledger.PinAttempts[weak]);
        Assert.Equal(Ancestors(Path.Combine(weak, "one.cs")).Count(), cache.Count);
        cache.ValidatePath(Path.Combine(weak, "four.cs"));
        Assert.Equal(3, ledger.PinAttempts[weak]); Assert.Equal(3, ledger.Reads[weak]);
        cache.Dispose(); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Validate_UnpinnedAncestorDoesNotPermitCachingItsDescendants()
    {
        var ledger = new Ledger(); var absent = Local("absent"); var child = Path.Combine(absent, "nested");
        ledger.PinOverride = path => path == absent ? null : ledger.Create(path);
        using var cache = ledger.Cache();
        cache.ValidatePath(Path.Combine(child, "one.cs")); cache.ValidatePath(Path.Combine(child, "two.cs"));
        Assert.Equal(1, cache.Count); Assert.Single(ledger.Pins);
        Assert.Equal(2, ledger.PinAttempts[absent]); Assert.False(ledger.PinAttempts.ContainsKey(child));
        Assert.Equal(2, ledger.Reads[absent]); Assert.Equal(2, ledger.Reads[child]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Validate_PinAndAdmissionErrorsDoNotCacheFailureAndCleanRejectedProduct(bool admission)
    {
        var ledger = new Ledger(); var directory = Local("cache", "fault"); var fail = true;
        var expected = new UnauthorizedAccessException("coverage denied");
        ledger.PinOverride = path => fail && !admission && path == directory ? throw expected : ledger.Create(path);
        ledger.Attributes = path => fail && admission && path == directory ? throw expected : FileAttributes.Directory;
        using var cache = ledger.Cache();
        Assert.Same(expected, Assert.Throws<UnauthorizedAccessException>(() => cache.ValidatePath(Path.Combine(directory, "a.cs"))));
        Assert.Equal(2, cache.Count);
        Assert.All(ledger.Pins.Where(pin => pin.Path == directory), pin => Assert.Equal(1, pin.Disposals));
        Assert.All(ledger.Pins.Where(pin => pin.Path != directory), pin => Assert.Equal(0, pin.Disposals));
        fail = false; cache.ValidatePath(Path.Combine(directory, "b.cs"));
        Assert.Equal(3, cache.Count); Assert.Equal(2, ledger.PinAttempts[directory]);
        cache.Dispose(); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Theory]
    [InlineData("missing")] [InlineData("file")] [InlineData("reparse")]
    [InlineData("denied")] [InlineData("io")] [InlineData("security")]
    public void Revalidate_RejectsPersistentDirectoryChangesAndDoesNotConsumeOwnership(string kind)
    {
        var ledger = new Ledger(); var directory = Local("cache", "project"); var changed = false;
        Exception? expected = kind switch { "denied" => new UnauthorizedAccessException("new ACL"), "io" => new IOException("coverage"), "security" => new SecurityException("security"), _ => null };
        ledger.Attributes = path => changed && path == directory ? kind switch
        {
            "missing" => null, "file" => FileAttributes.Normal, "reparse" => FileAttributes.Directory | FileAttributes.ReparsePoint,
            _ => throw expected!
        } : FileAttributes.Directory;
        using var cache = ledger.Cache(); var path = Path.Combine(directory, "a.cs"); cache.ValidatePath(path);
        changed = true;
        cache.ValidatePath(path); // Hits reuse a protected chain; the final fresh check must still reject it.
        var error = Record.Exception(cache.Revalidate);
        if (expected != null) Assert.Same(expected, error);
        else if (kind == "reparse") Assert.IsType<NotSupportedException>(error);
        else Assert.IsType<IOException>(error);
        Assert.Equal(3, cache.Count); Assert.All(ledger.Pins, pin => Assert.Equal(0, pin.Disposals));
        cache.Dispose(); Assert.Equal(0, cache.Count); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Validate_FreshCacheDoesNotReusePriorAttemptOrBaselineState()
    {
        var ledger = new Ledger(); var directory = Local("cache", "project"); var path = Path.Combine(directory, "a.cs");
        using (var first = ledger.Cache()) { first.ValidatePath(path); first.Revalidate(); }
        Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
        ledger.Attributes = value => value == directory ? FileAttributes.Directory | FileAttributes.ReparsePoint : FileAttributes.Directory;
        using var second = ledger.Cache();
        Assert.Throws<NotSupportedException>(() => second.ValidatePath(path));
        Assert.Equal(2, ledger.PinAttempts[directory]); Assert.Equal(2, second.Count);
        second.Dispose(); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Cancellation_OnCachedHitAndRevalidationStillReleasesAllPins()
    {
        var ledger = new Ledger(); using var cancellation = new CancellationTokenSource();
        using var cache = ledger.Cache(cancellation.Token); var path = Local("cache", "project", "a.cs"); cache.ValidatePath(path);
        var reads = ledger.Reads.Values.Sum(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => cache.ValidatePath(path));
        Assert.ThrowsAny<OperationCanceledException>(cache.Revalidate);
        Assert.Equal(reads, ledger.Reads.Values.Sum());
        cache.Dispose(); Assert.Equal(0, cache.Count); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Cancellation_AfterPinAcquisitionDoesNotLeakUnacceptedProduct()
    {
        var ledger = new Ledger(); using var cancellation = new CancellationTokenSource();
        ledger.PinOverride = path => { var pin = ledger.Create(path); cancellation.Cancel(); return pin; };
        using var cache = ledger.Cache(cancellation.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => cache.ValidatePath(Local("cache", "a.cs")));
        var rejected = Assert.Single(ledger.Pins); Assert.Equal(1, rejected.Disposals);
        Assert.Equal(0, cache.Count); Assert.Empty(ledger.Reads);
    }

    [Fact]
    public void Cancellation_DuringFinalWalkCannotReturnSuccessfulValidation()
    {
        var ledger = new Ledger(); using var cancellation = new CancellationTokenSource(); var finishing = false;
        ledger.Attributes = _ => { if (finishing) cancellation.Cancel(); return FileAttributes.Directory; };
        using var cache = ledger.Cache(cancellation.Token); cache.ValidatePath(Local("cache", "project", "a.cs"));
        finishing = true;
        Assert.ThrowsAny<OperationCanceledException>(cache.Revalidate);
        cache.Dispose(); Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Dispose_AggregatesFailuresReleasesOtherPinsAndRetriesOnlyStillOwnedProducts()
    {
        var ledger = new Ledger(); var cache = ledger.Cache(); cache.ValidatePath(Local("cache", "project", "a.cs"));
        var expected = new IOException("release failed"); var failed = ledger.Pins[1]; var once = true;
        failed.OnDispose = () => { if (once) { once = false; throw expected; } };
        var aggregate = Assert.Throws<AggregateException>(cache.Dispose);
        Assert.Same(expected, Assert.Single(aggregate.InnerExceptions)); Assert.Equal(1, cache.Count);
        Assert.All(ledger.Pins, pin => Assert.Equal(1, pin.Disposals));
        Assert.Throws<ObjectDisposedException>(() => cache.ValidatePath(Local("cache", "other.cs")));
        cache.Dispose(); cache.Dispose();
        Assert.Equal(0, cache.Count); Assert.Equal(2, failed.Disposals);
        Assert.All(ledger.Pins.Where(pin => pin != failed), pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Validate_RejectedNonDirectoryCleanupRetainsOwnerForOneLaterRetry()
    {
        var ledger = new Ledger(); var directory = Local("cache", "file"); var expected = new IOException("close rejected product");
        ledger.Attributes = path => path == directory ? FileAttributes.Normal : FileAttributes.Directory;
        var once = true;
        ledger.PinOverride = path => { var pin = ledger.Create(path); if (path == directory) pin.OnDispose = () => { if (once) { once = false; throw expected; } }; return pin; };
        using var cache = ledger.Cache();
        Assert.Same(expected, Record.Exception(() => cache.ValidatePath(Path.Combine(directory, "a.cs"))));
        var rejected = Assert.Single(ledger.Pins, pin => pin.Path == directory); Assert.Equal(1, rejected.Disposals);
        Assert.Equal(2, cache.Count);
        cache.Dispose(); Assert.Equal(2, rejected.Disposals);
        Assert.All(ledger.Pins.Where(pin => pin != rejected), pin => Assert.Equal(1, pin.Disposals));
    }

    [Fact]
    public void Validate_AdmissionAndCleanupFailuresPreserveBothErrorsAndRetryRejectedOwnership()
    {
        var ledger = new Ledger(); var directory = Local("cache", "rejected"); var deny = true;
        var admission = new UnauthorizedAccessException("admission denied"); var cleanup = new IOException("cleanup failed"); var failCleanup = true;
        ledger.Attributes = path => deny && path == directory ? throw admission : FileAttributes.Directory;
        ledger.PinOverride = path =>
        {
            var pin = ledger.Create(path);
            if (path == directory && deny) pin.OnDispose = () => { if (failCleanup) { failCleanup = false; throw cleanup; } };
            return pin;
        };
        using var cache = ledger.Cache();
        var aggregate = Assert.Throws<AggregateException>(() => cache.ValidatePath(Path.Combine(directory, "first.cs")));
        Assert.Equal(2, aggregate.InnerExceptions.Count); Assert.Same(admission, aggregate.InnerExceptions[0]); Assert.Same(cleanup, aggregate.InnerExceptions[1]);
        var rejected = Assert.Single(ledger.Pins, pin => pin.Path == directory);
        Assert.Equal(1, rejected.Disposals); Assert.Equal(2, cache.Count);
        Assert.All(ledger.Pins.Where(pin => pin != rejected), pin => Assert.Equal(0, pin.Disposals));
        deny = false; cache.ValidatePath(Path.Combine(directory, "next.cs"));
        Assert.Equal(2, ledger.PinAttempts[directory]); Assert.Equal(3, cache.Count); Assert.Equal(1, rejected.Disposals);
        cache.Dispose(); Assert.Equal(0, cache.Count); Assert.Equal(2, rejected.Disposals);
        Assert.All(ledger.Pins.Where(pin => pin != rejected), pin => Assert.Equal(1, pin.Disposals));
    }

    private static string Local(params string[] names) => names.Aggregate(Path.GetPathRoot(Path.GetTempPath())!, Path.Combine);
    private static IEnumerable<string> Ancestors(string path)
    { for (var parent = Path.GetDirectoryName(path); parent != null; parent = Path.GetDirectoryName(parent)) yield return parent; }
    private sealed class Ledger
    {
        internal List<Pin> Pins { get; } = [];
        internal List<string> Released { get; } = [];
        internal Dictionary<string, int> Reads { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, int> PinAttempts { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Func<string, IDisposable?>? PinOverride { get; set; }
        internal Func<string, FileAttributes?> Attributes { get; set; } = _ => FileAttributes.Directory;
        internal Pin Create(string path) { var pin = new Pin(path, Released); Pins.Add(pin); return pin; }
        internal EditorSourceDirectoryCache Cache(CancellationToken token = default, int capacity = EditorSourceDirectoryCache.MaximumCachedDirectories) =>
            new(token, path => { PinAttempts[path] = PinAttempts.GetValueOrDefault(path) + 1; return PinOverride == null ? Create(path) : PinOverride(path); },
                path => { Reads[path] = Reads.GetValueOrDefault(path) + 1; return Attributes(path); }, capacity);
    }
    private sealed class Pin(string path, List<string> released) : IDisposable
    {
        internal string Path { get; } = path;
        internal int Disposals { get; private set; }
        internal Action? OnDispose { get; set; }
        public void Dispose() { Disposals++; released.Add(Path); OnDispose?.Invoke(); }
    }
}
