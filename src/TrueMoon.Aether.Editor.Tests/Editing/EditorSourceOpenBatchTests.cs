using System.Collections.Concurrent;
using System.Security.Cryptography;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceOpenBatchTests
{
    [Fact]
    public async Task Create_CompletionMayDisposeAndReadmitWithoutRetainingAnyEarlierNativeOwner()
    {
        using var files = new Inputs(4); using var release = new ManualResetEventSlim();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var entered = 0;
        var firstOwners = new ConcurrentBag<EditorSourceFile>();
        var creating = EditorSourceOpenBatch.CreateAsync(files.Files, 0, _ => { }, default, path =>
        {
            var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path); firstOwners.Add(reader);
            if (Interlocked.Increment(ref entered) == 4) ready.TrySetResult();
            try { if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Reentrant completion gate not released."); return reader; }
            catch { reader.Dispose(); throw; }
        });
        var readmitted = DisposeAndReadmit();
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(readmitted.IsCompleted);
            release.Set(); using var next = await readmitted.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(4, next.Count); Assert.All(firstOwners, owner => Assert.False(owner.CanRead));
            for (var index = 0; index < 4; index++)
            {
                using var reader = Assert.IsType<EditorSourceFile>(next.Take(index));
                Assert.Equal(files.Files[index].Length, reader.Before.Length);
            }
        }
        finally
        {
            release.Set(); using var next = await readmitted.WaitAsync(TimeSpan.FromSeconds(10));
            next.Dispose(); foreach (var reader in firstOwners) reader.Dispose();
            foreach (var file in files.Files) Exclusive(file.Path);
        }

        async Task<EditorSourceOpenBatch> DisposeAndReadmit()
        {
            using var first = await creating.ConfigureAwait(false);
            first.Dispose(); foreach (var file in files.Files) Exclusive(file.Path);
            return await EditorSourceOpenBatch.CreateAsync(files.Files, 0, _ => { }, default).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Create_WorkersRetainEachAdmissionExecutionContextWithoutLeakingChangesToTheCoordinator()
    {
        using var files = new Inputs(4); var ambient = new AsyncLocal<string?> { Value = "coordinator" };
        var seen = new ConcurrentDictionary<string, string?>();
        using var batch = await EditorSourceOpenBatch.CreateAsync(files.Files, 0, path => ambient.Value = path, default, path =>
        {
            seen[path] = ambient.Value;
            ambient.Value = "worker-only";
            return EditorSourceFile.Open(path);
        });
        Assert.Equal("coordinator", ambient.Value); Assert.Equal(4, batch.Count); Assert.Equal(4, seen.Count);
        for (var index = 0; index < 4; index++)
        {
            Assert.Equal(files.Files[index].Path, seen[files.Files[index].Path]);
            using var reader = Assert.IsType<EditorSourceFile>(batch.Take(index));
            Assert.Equal(files.Files[index].Length, reader.Before.Length);
        }
        batch.Dispose(); foreach (var file in files.Files) Exclusive(file.Path);
    }

    [Fact]
    public async Task Create_ImmediatelyCompletedWorkersCannotLoseSlotsOrPublishBeforeAllAdmissions()
    {
        using var files = new Inputs(4);
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var validated = new List<string>(); var opened = new ConcurrentDictionary<string, int>();
            using var batch = await EditorSourceOpenBatch.CreateAsync(files.Files, 0, validated.Add, default, path =>
            { opened.AddOrUpdate(path, 1, static (_, count) => count + 1); return null; });
            Assert.Equal(4, batch.Count); Assert.Equal(files.Files.Select(file => file.Path), validated);
            Assert.Equal(4, opened.Count); Assert.All(opened.Values, count => Assert.Equal(1, count));
            for (var index = 0; index < batch.Count; index++)
            {
                Assert.Null(batch.Take(index));
                Assert.Throws<InvalidOperationException>(() => batch.Take(index));
            }
        }
        foreach (var file in files.Files) Exclusive(file.Path);
    }

    [Fact]
    public async Task Create_FourNativeOpensOverlapButHandOffWaitsForEveryOwnerAndPreservesOrder()
    {
        using var files = new Inputs(5); using var release = new ManualResetEventSlim();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owners = new ConcurrentDictionary<string, EditorSourceFile>(); var validations = new List<string>();
        var validationThreads = new List<int>(); var coordinator = Environment.CurrentManagedThreadId; var entered = 0;
        var creating = EditorSourceOpenBatch.CreateAsync(files.Files, 0, path =>
        { validations.Add(path); validationThreads.Add(Environment.CurrentManagedThreadId); }, default, path =>
        {
            var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path);
            owners[path] = reader;
            if (Interlocked.Increment(ref entered) == 4) ready.TrySetResult();
            try { if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Native open gate not released."); return reader; }
            catch { reader.Dispose(); throw; }
        });
        EditorSourceOpenBatch? batch = null; EditorSourceFile? first = null, last = null;
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(creating.IsCompleted); Assert.Equal(4, owners.Count); Assert.Equal(4, entered);
            Assert.Equal(files.Files.Take(4).Select(file => file.Path), validations);
            Assert.All(validationThreads, thread => Assert.Equal(coordinator, thread));
            Assert.DoesNotContain(files.Files[4].Path, owners.Keys);
            release.Set(); batch = await creating.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(4, batch.Count);
            first = Assert.IsType<EditorSourceFile>(batch.Take(0)); last = Assert.IsType<EditorSourceFile>(batch.Take(3));
            Assert.Same(owners[files.Files[0].Path], first); Assert.Same(owners[files.Files[3].Path], last);
            Assert.Throws<InvalidOperationException>(() => batch.Take(0));
            batch.Dispose(); batch.Dispose();
            Assert.True(first.CanRead); Assert.True(last.CanRead);
            Assert.False(owners[files.Files[1].Path].CanRead); Assert.False(owners[files.Files[2].Path].CanRead);
            Assert.Throws<ObjectDisposedException>(() => batch.Take(1));
            Exclusive(files.Files[1].Path); Exclusive(files.Files[2].Path);
            var bytes = new byte[2]; Assert.Equal(2, first.Read(bytes, 0, bytes.Length)); Assert.Equal(new byte[] { 11, 31 }, bytes);
            first.Dispose(); last.Dispose(); Assert.All(owners.Values, reader => Assert.False(reader.CanRead));
            Exclusive(files.Files[0].Path); Exclusive(files.Files[3].Path);
        }
        finally
        {
            release.Set(); batch ??= await creating.WaitAsync(TimeSpan.FromSeconds(10)); batch.Dispose(); first?.Dispose(); last?.Dispose();
            foreach (var reader in owners.Values) reader.Dispose();
        }
    }

    [Theory]
    [InlineData(0, 4)] [InlineData(4, 1)] [InlineData(5, 0)]
    public async Task Create_OffsetsExposeOnlyTheBoundedSuffixAndEveryTakeTransfersExactlyOnce(int offset, int expected)
    {
        using var files = new Inputs(5); var validations = new List<string>(); var calls = 0;
        using var batch = await EditorSourceOpenBatch.CreateAsync(files.Files, offset, validations.Add, default, path =>
        { Interlocked.Increment(ref calls); return EditorSourceFile.Open(path); });
        Assert.Equal(expected, batch.Count); Assert.Equal(expected, calls);
        Assert.Equal(files.Files.Skip(offset).Take(expected).Select(file => file.Path), validations);
        for (var index = 0; index < expected; index++)
        {
            using var reader = Assert.IsType<EditorSourceFile>(batch.Take(index));
            Assert.Equal(files.Files[offset + index].Length, reader.Before.Length);
            var bytes = new byte[2]; Assert.Equal(2, reader.Read(bytes, 0, bytes.Length));
            Assert.Equal(new byte[] { (byte)(11 + offset + index), 31 }, bytes);
            Assert.Throws<InvalidOperationException>(() => batch.Take(index));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Take(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Take(expected));
        batch.Dispose(); foreach (var file in files.Files) Exclusive(file.Path);
    }

    [Theory]
    [InlineData(-1)] [InlineData(6)]
    public async Task Create_InvalidOffsetRejectsBeforeValidationOrNativeAdmission(int offset)
    {
        using var files = new Inputs(5); var validated = 0; var opened = 0;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => EditorSourceOpenBatch.CreateAsync(files.Files, offset,
            _ => validated++, default, path => { opened++; return EditorSourceFile.Open(path); }));
        Assert.Equal(0, validated); Assert.Equal(0, opened); foreach (var file in files.Files) Exclusive(file.Path);
    }

    [Theory]
    [InlineData("files")] [InlineData("validate")]
    public async Task Create_MissingRequiredDependencyRejectsBeforeOpeningAnything(string missing)
    {
        using var files = new Inputs(1); var opened = 0;
        var error = await Assert.ThrowsAsync<ArgumentNullException>(() => EditorSourceOpenBatch.CreateAsync(
            missing == "files" ? null! : files.Files, 0, missing == "validate" ? null! : _ => { }, default,
            path => { opened++; return EditorSourceFile.Open(path); }));
        Assert.Equal(missing, error.ParamName); Assert.Equal(0, opened); Exclusive(files.Files[0].Path);
    }

    [Fact]
    public async Task Take_MissingAndFailedLaterOpensDoNotMaskEarlierOwnershipAndDisposeClosesRemainder()
    {
        using var files = new Inputs(4); var sentinel = new IOException("later native failure");
        var owners = new ConcurrentDictionary<string, EditorSourceFile>();
        using var batch = await EditorSourceOpenBatch.CreateAsync(files.Files, 0, _ => { }, default, path =>
        {
            if (path == files.Files[1].Path) return null;
            if (path == files.Files[2].Path) throw sentinel;
            var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path); owners[path] = reader; return reader;
        });
        Assert.Equal(4, batch.Count); Assert.Equal(2, owners.Count);
        using var first = Assert.IsType<EditorSourceFile>(batch.Take(0)); Assert.Same(owners[files.Files[0].Path], first);
        Assert.Null(batch.Take(1)); Assert.Throws<InvalidOperationException>(() => batch.Take(1));
        Assert.Same(sentinel, Assert.Throws<IOException>(() => batch.Take(2)));
        batch.Dispose(); Assert.True(first.CanRead); Assert.False(owners[files.Files[3].Path].CanRead);
        first.Dispose(); Assert.All(owners.Values, reader => Assert.False(reader.CanRead));
        foreach (var file in files.Files) Exclusive(file.Path);
    }

    [Fact]
    public async Task Create_ValidationFailureStopsFutureAdmissionButWaitsForEarlierNativeJobs()
    {
        using var files = new Inputs(5); using var release = new ManualResetEventSlim();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sentinel = new NotSupportedException("unsupported ancestor"); var validated = new List<string>();
        var owners = new ConcurrentDictionary<string, EditorSourceFile>(); var entered = 0;
        var creating = EditorSourceOpenBatch.CreateAsync(files.Files, 0, path =>
        { validated.Add(path); if (path == files.Files[2].Path) throw sentinel; }, default, path =>
        {
            var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path); owners[path] = reader;
            if (Interlocked.Increment(ref entered) == 2) ready.TrySetResult();
            try { if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Earlier native jobs not released."); return reader; }
            catch { reader.Dispose(); throw; }
        });
        EditorSourceOpenBatch? batch = null;
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(creating.IsCompleted);
            Assert.Equal(files.Files.Take(3).Select(file => file.Path), validated); Assert.Equal(2, entered);
            release.Set(); batch = await creating.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(3, batch.Count);
            using (var first = Assert.IsType<EditorSourceFile>(batch.Take(0))) Assert.Equal(2, first.Before.Length);
            using (var second = Assert.IsType<EditorSourceFile>(batch.Take(1))) Assert.Equal(2, second.Before.Length);
            Assert.Same(sentinel, Assert.Throws<NotSupportedException>(() => batch.Take(2)));
            Assert.DoesNotContain(files.Files[3].Path, owners.Keys); Assert.DoesNotContain(files.Files[4].Path, owners.Keys);
            batch.Dispose(); Assert.All(owners.Values, reader => Assert.False(reader.CanRead));
            foreach (var file in files.Files) Exclusive(file.Path);
        }
        finally
        {
            release.Set(); batch ??= await creating.WaitAsync(TimeSpan.FromSeconds(10)); batch.Dispose();
            foreach (var reader in owners.Values) reader.Dispose();
        }
    }

    [Fact]
    public async Task Create_PreCancelledTokenAdmitsNoPathAndRetainsTheCancellationIdentity()
    {
        using var files = new Inputs(4); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var validated = 0; var opened = 0;
        using var batch = await EditorSourceOpenBatch.CreateAsync(files.Files, 0, _ => validated++, cancellation.Token,
            path => { opened++; return EditorSourceFile.Open(path); });
        Assert.Equal(1, batch.Count); Assert.Equal(0, validated); Assert.Equal(0, opened);
        Assert.Equal(cancellation.Token, Assert.ThrowsAny<OperationCanceledException>(() => batch.Take(0)).CancellationToken);
        foreach (var file in files.Files) Exclusive(file.Path);
    }

    [Fact]
    public async Task Create_CancellationAfterOpenWaitsForNativeFactoriesAndDisposesEveryReturnedProduct()
    {
        using var files = new Inputs(4); using var cancellation = new CancellationTokenSource(); using var release = new ManualResetEventSlim();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owners = new ConcurrentDictionary<string, EditorSourceFile>(); var entered = 0;
        var creating = EditorSourceOpenBatch.CreateAsync(files.Files, 0, _ => { }, cancellation.Token, path =>
        {
            var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path); owners[path] = reader;
            if (Interlocked.Increment(ref entered) == 4) ready.TrySetResult();
            try { if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Cancelled native factories not released."); return reader; }
            catch { reader.Dispose(); throw; }
        });
        EditorSourceOpenBatch? batch = null;
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
            Assert.False(creating.IsCompleted); Assert.Equal(4, owners.Count); Assert.All(owners.Values, reader => Assert.True(reader.CanRead));
            release.Set(); batch = await creating.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(4, batch.Count);
            for (var index = 0; index < 4; index++)
                Assert.Equal(cancellation.Token, Assert.ThrowsAny<OperationCanceledException>(() => batch.Take(index)).CancellationToken);
            Assert.All(owners.Values, reader => Assert.False(reader.CanRead)); batch.Dispose();
            foreach (var file in files.Files) Exclusive(file.Path);
        }
        finally
        {
            release.Set(); batch ??= await creating.WaitAsync(TimeSpan.FromSeconds(10)); batch.Dispose();
            foreach (var reader in owners.Values) reader.Dispose();
        }
    }

    private static void Exclusive(string path)
    { using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(2, stream.Length); }

    private sealed class Inputs : IDisposable
    {
        private readonly SourceWatchFiles _files = new();
        internal GameBuildInputFile[] Files { get; }
        internal Inputs(int count)
        {
            var directory = Path.Combine(_files.Root, "batch-inputs"); Directory.CreateDirectory(directory);
            Files = Enumerable.Range(0, count).Select(index =>
            {
                var path = Path.Combine(directory, index.ToString("D4") + ".bin"); var bytes = new byte[] { (byte)(11 + index), 31 };
                File.WriteAllBytes(path, bytes); return new GameBuildInputFile(path, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            }).ToArray();
        }
        public void Dispose() => _files.Dispose();
    }
}
