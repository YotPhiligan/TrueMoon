using System.Diagnostics;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticRunRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sweep_EmptyStoreDoesNotCreateArchiveOrReportLoss()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var status = archive.Store(new()).Sweep(Now);
        Assert.Equal(new DiagnosticRetentionStatus(0, 0, 0, 0, 0, 0, null, false, false), status);
        Assert.False(Directory.Exists(archive.Root));
    }

    [Fact]
    public void Sweep_CountLimitDeletesOldestByCreationInsteadOfDirectoryMtime()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxRuns = 2 });
        var oldest = archive.Closed(store, Now.AddHours(-3));
        var older = archive.Closed(store, Now.AddHours(-2));
        var newer = archive.Closed(store, Now.AddHours(-1));
        var newest = archive.Closed(store, Now);
        Directory.SetLastWriteTimeUtc(oldest, Now.AddDays(5).UtcDateTime);
        Directory.SetLastWriteTimeUtc(newest, Now.AddDays(-5).UtcDateTime);

        var status = store.Sweep(Now);
        Assert.False(Directory.Exists(oldest)); Assert.False(Directory.Exists(older));
        Assert.True(Directory.Exists(newer)); Assert.True(Directory.Exists(newest));
        Assert.Equal(2, status.RetainedRuns); Assert.Equal(2, status.DeletedRuns);
        Assert.Equal(DiagnosticRunTestDirectory.Size(newer) + DiagnosticRunTestDirectory.Size(newest), status.RetainedBytes);
        Assert.False(status.IsOverBudget); Assert.Equal(0, status.Failures);
        var unchanged = store.Sweep(Now);
        Assert.Equal(2, unchanged.RetainedRuns); Assert.Equal(0, unchanged.DeletedRuns);
    }

    [Fact]
    public void Sweep_EqualCreationTimesUseOrdinalDirectoryTieBreaker()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxRuns = 1 });
        var jobs = Enumerable.Range(0, 3).Select(_ => archive.Closed(store, Now)).Order(StringComparer.Ordinal).ToArray();
        var status = store.Sweep(Now);
        Assert.False(Directory.Exists(jobs[0])); Assert.False(Directory.Exists(jobs[1]));
        Assert.True(Directory.Exists(jobs[2]));
        Assert.Equal(2, status.DeletedRuns); Assert.Equal(1, status.RetainedRuns);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    public void Sweep_ByteBudgetIncludesMarkerAndRetainsExactEquality(int adjustment, int expectedJobs)
    {
        using var archive = new DiagnosticRunTestDirectory();
        var writer = archive.Store(new());
        var old = archive.Closed(writer, Now.AddMinutes(-1), 25);
        var newest = archive.Closed(writer, Now, 45);
        var total = DiagnosticRunTestDirectory.Size(old) + DiagnosticRunTestDirectory.Size(newest);
        var store = archive.Store(new() { MaxRuns = 10, MaxTotalBytes = total + adjustment });

        var status = store.Sweep(Now);
        Assert.Equal(expectedJobs, status.RetainedRuns);
        Assert.Equal(2 - expectedJobs, status.DeletedRuns);
        Assert.Equal(expectedJobs == 2, Directory.Exists(old));
        Assert.True(Directory.Exists(newest));
        Assert.Equal(expectedJobs == 2 ? total : DiagnosticRunTestDirectory.Size(newest), status.RetainedBytes);
        Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Sweep_AgeExpiresOnlyStrictlyAfterCreationLimit(int ticksInsideLimit, bool retained)
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxAge = TimeSpan.FromHours(2) });
        var boundary = archive.Closed(store, Now.AddHours(-2).AddTicks(ticksInsideLimit).ToOffset(TimeSpan.FromHours(5)));
        var newest = archive.Closed(store, Now);
        Assert.EndsWith("+00:00", File.ReadAllText(Path.Combine(boundary, DiagnosticRunStore.MarkerFile)));

        var status = store.Sweep(Now);
        Assert.Equal(retained, Directory.Exists(boundary));
        Assert.True(Directory.Exists(newest));
        Assert.Equal(retained ? 2 : 1, status.RetainedRuns);
        Assert.Equal(retained ? 0 : 1, status.DeletedRuns);
        Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("bytes")]
    [InlineData("age")]
    public void Sweep_ProtectsNewestAndExplicitJobAndReportsUnsatisfiedBudget(string budget)
    {
        using var archive = new DiagnosticRunTestDirectory();
        var options = new DiagnosticRunRetentionOptions();
        if (budget == "count") options.MaxRuns = 1;
        if (budget == "bytes") options.MaxTotalBytes = 1;
        if (budget == "age") options.MaxAge = TimeSpan.FromMinutes(1);
        var store = archive.Store(options);
        var protectedOld = archive.Closed(store, Now.AddDays(-2));
        var newest = archive.Closed(store, Now.AddDays(-1));

        var status = store.Sweep(Now, protectedOld + Path.DirectorySeparatorChar);
        Assert.True(Directory.Exists(protectedOld)); Assert.True(Directory.Exists(newest));
        Assert.Equal(2, status.RetainedRuns); Assert.Equal(0, status.DeletedRuns);
        Assert.True(status.IsOverBudget); Assert.Equal(0, status.Failures);
    }

    [Fact]
    public void Sweep_ProtectsActiveRunAcrossStoreInstancesUntilItsLeaseCloses()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var first = archive.Store(new() { MaxRuns = 1 });
        var second = archive.Store(new() { MaxRuns = 1 });
        using var active = first.Create(Now);
        File.WriteAllText(Path.Combine(active.Directory, "events-0.jsonl"), "active-output");
        var old = archive.Closed(second, Now.AddMinutes(-2));
        var newest = archive.Closed(second, Now.AddMinutes(-1));

        var status = second.Sweep(Now);
        Assert.Equal(1, status.ActiveRuns); Assert.Equal(1, status.RetainedRuns);
        Assert.Equal(DiagnosticRunTestDirectory.Size(newest), status.RetainedBytes);
        Assert.True(Directory.Exists(active.Directory)); Assert.False(Directory.Exists(old));
        Assert.False(status.IsOverBudget);
        active.Dispose();
        var finished = second.Sweep(Now);
        Assert.Equal(0, finished.ActiveRuns); Assert.Equal(1, finished.RetainedRuns);
        Assert.False(Directory.Exists(newest));
        Assert.Equal("active-output", File.ReadAllText(Path.Combine(active.Directory, "events-0.jsonl")));
    }

    [Fact]
    public async Task Sweep_MaintenanceContentionPreservesJobsAndRecoversWithoutBlocking()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxRuns = 1 });
        var old = archive.Closed(store, Now.AddMinutes(-1));
        var newest = archive.Closed(store, Now);
        using (var held = new FileStream(Path.Combine(archive.Root, ".retention.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var busy = await Task.Run(() => store.Sweep(Now)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(busy.MaintenanceBusy); Assert.Equal(0, busy.Failures); Assert.Equal(0, busy.DeletedRuns);
            Assert.True(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
        }
        var recovered = store.Sweep(Now);
        Assert.False(recovered.MaintenanceBusy); Assert.Equal(1, recovered.DeletedRuns);
        Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
    }

    [Theory]
    [InlineData("unmarked")]
    [InlineData("invalid-name")]
    [InlineData("invalid-magic")]
    [InlineData("invalid-schema")]
    [InlineData("invalid-time")]
    [InlineData("oversized-marker")]
    [InlineData("empty-marker")]
    [InlineData("nested-staging")]
    [InlineData("artifact-file")]
    [InlineData("negative-slot")]
    [InlineData("leading-zero-slot")]
    [InlineData("foreign-prefix")]
    [InlineData("directory-with-file-name")]
    public void Sweep_PreservesUnknownIncompleteAndNonflatDirectoriesWhilePruningOwnedJobs(string kind)
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxRuns = 1 });
        string preserved;
        if (kind == "empty-marker") { using var job = store.Create(Now.AddHours(-2)); preserved = job.Directory; }
        else preserved = archive.Closed(store, Now.AddHours(-2));
        var marker = Path.Combine(preserved, DiagnosticRunStore.MarkerFile);
        switch (kind)
        {
            case "unmarked": File.Delete(marker); break;
            case "empty-marker": File.WriteAllText(marker, ""); break;
            case "invalid-name":
                var renamed = Path.Combine(archive.Root, "legacy-job"); Directory.Move(preserved, renamed); preserved = renamed; break;
            case "invalid-magic": File.WriteAllText(marker, "Unowned/1\n" + Now.ToString("O")); break;
            case "invalid-schema": File.WriteAllText(marker, File.ReadAllText(marker).Replace("/1\n", "/2\n", StringComparison.Ordinal)); break;
            case "invalid-time": File.WriteAllText(marker, "TrueMoon.Diagnostics.Run/1\nnot-a-completion-time"); break;
            case "oversized-marker": File.WriteAllBytes(marker, new byte[129]); break;
            case "nested-staging": Directory.CreateDirectory(Path.Combine(preserved, "output")); File.WriteAllText(Path.Combine(preserved, "output", "game.dll"), "artifact"); break;
            case "artifact-file": File.WriteAllText(Path.Combine(preserved, "game.dll"), "artifact"); break;
            case "negative-slot": File.WriteAllText(Path.Combine(preserved, "events--1.jsonl"), "unknown"); break;
            case "leading-zero-slot": File.WriteAllText(Path.Combine(preserved, "events-01.jsonl"), "unknown"); break;
            case "foreign-prefix": File.WriteAllText(Path.Combine(preserved, "custom-0.jsonl"), "unknown"); break;
            case "directory-with-file-name": File.Delete(Path.Combine(preserved, "events-0.jsonl")); Directory.CreateDirectory(Path.Combine(preserved, "events-0.jsonl")); break;
        }
        var before = DiagnosticRunTestDirectory.Snapshot(preserved);
        var removable = archive.Closed(store, Now.AddHours(-1));
        var newest = archive.Closed(store, Now);

        var status = store.Sweep(Now);
        Assert.Equal(before, DiagnosticRunTestDirectory.Snapshot(preserved));
        if (kind == "directory-with-file-name") Assert.True(Directory.Exists(Path.Combine(preserved, "events-0.jsonl")));
        Assert.False(Directory.Exists(removable)); Assert.True(Directory.Exists(newest));
        Assert.Equal(1, status.SkippedDirectories); Assert.Equal(1, status.RetainedRuns);
        Assert.Equal(1, status.DeletedRuns); Assert.Equal(0, status.Failures);
    }

    [Fact]
    public void Sweep_PreservesLegacyFlatFilesAndUnrelatedDirectories()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var artifact = Path.Combine(archive.TempRoot, "artifacts", new string('A', 64)); Directory.CreateDirectory(artifact);
        var dll = Path.Combine(artifact, "Game.dll"); File.WriteAllText(dll, "published-artifact");
        var store = archive.Store(new() { MaxRuns = 1 });
        var old = archive.Closed(store, Now.AddMinutes(-1)); var newest = archive.Closed(store, Now);
        var legacy = Path.Combine(archive.Root, "events-0.jsonl"); File.WriteAllText(legacy, "legacy-flat");
        var status = store.Sweep(Now);
        Assert.Equal("legacy-flat", File.ReadAllText(legacy));
        Assert.Equal("published-artifact", File.ReadAllText(dll));
        Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
        Assert.Equal(1, status.DeletedRuns); Assert.Equal(0, status.SkippedDirectories);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("ancestor")]
    [InlineData("job")]
    [InlineData("output-entry")]
    public void Sweep_RejectsReparseAncestryAndEntriesWithoutTouchingTargets(string kind)
    {
        using var archive = new DiagnosticRunTestDirectory();
        var externalRoot = Path.Combine(archive.TempRoot, "external");
        var externalStore = new DiagnosticRunStore(externalRoot, new());
        var externalOld = archive.Closed(externalStore, Now.AddHours(-2));
        var externalNewest = archive.Closed(externalStore, Now.AddHours(-1));
        var sentinel = Path.Combine(externalOld, "events-0.jsonl"); File.WriteAllText(sentinel, "outside-owned-root");
        var beforeOld = DiagnosticRunTestDirectory.Snapshot(externalOld);
        var beforeNewest = DiagnosticRunTestDirectory.Snapshot(externalNewest);
        var store = archive.Store(new() { MaxRuns = 1 });
        string link, target;
        if (kind == "root") { link = archive.Root; target = externalRoot; }
        else if (kind == "ancestor")
        {
            var parent = Path.Combine(archive.TempRoot, "redirect"); link = parent; target = archive.TempRoot;
            store = new DiagnosticRunStore(Path.Combine(parent, "external"), new() { MaxRuns = 1 });
        }
        else if (kind == "job")
        {
            archive.Closed(store, Now); link = Path.Combine(archive.Root, Guid.NewGuid().ToString("N")); target = externalOld;
        }
        else
        {
            var candidate = archive.Closed(store, Now.AddHours(-1));
            link = Path.Combine(candidate, "events-0.jsonl"); File.Delete(link);
            archive.Closed(store, Now); target = externalOld;
        }
        archive.Junction(link, target);
        var status = store.Sweep(Now);
        Assert.Equal("outside-owned-root", File.ReadAllText(sentinel));
        Assert.Equal(beforeOld, DiagnosticRunTestDirectory.Snapshot(externalOld));
        Assert.Equal(beforeNewest, DiagnosticRunTestDirectory.Snapshot(externalNewest));
        Assert.True(Directory.Exists(link)); Assert.Equal(0, status.DeletedRuns);
        if (kind is "root" or "ancestor") Assert.Equal(1, status.Failures);
        else Assert.Equal(1, status.SkippedDirectories);
        if (kind is "root" or "ancestor") Assert.Throws<IOException>(() => store.Create(Now));
    }

    [Fact]
    public void Sweep_DeleteFailurePreservesOwnershipContinuesAndRetriesAfterHandleRelease()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxRuns = 1 });
        var blocked = archive.Closed(store, Now.AddHours(-2));
        var removable = archive.Closed(store, Now.AddHours(-1));
        var newest = archive.Closed(store, Now);
        using (var held = new FileStream(Path.Combine(blocked, "events-0.jsonl"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = store.Sweep(Now);
            Assert.Equal(1, failed.Failures); Assert.Equal(1, failed.DeletedRuns);
            Assert.Equal(2, failed.RetainedRuns); Assert.True(failed.IsOverBudget);
            Assert.InRange(failed.LastError!.Length, 1, 1024);
            Assert.True(File.Exists(Path.Combine(blocked, DiagnosticRunStore.MarkerFile)));
            Assert.False(Directory.Exists(removable)); Assert.True(Directory.Exists(newest));
        }
        var recovered = store.Sweep(Now);
        Assert.Equal(0, recovered.Failures); Assert.Null(recovered.LastError);
        Assert.Equal(1, recovered.DeletedRuns); Assert.Equal(1, recovered.RetainedRuns);
        Assert.False(recovered.IsOverBudget); Assert.False(Directory.Exists(blocked));
    }

    [Fact]
    public void Sweep_MaintenanceFailureIsObservableAndCanBeRetried()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var store = archive.Store(new() { MaxRuns = 1 });
        var old = archive.Closed(store, Now.AddMinutes(-1)); var newest = archive.Closed(store, Now);
        var lockPath = Path.Combine(archive.Root, ".retention.lock"); Directory.CreateDirectory(lockPath);
        var failed = store.Sweep(Now);
        Assert.Equal(1, failed.Failures); Assert.NotNull(failed.LastError); Assert.False(failed.MaintenanceBusy);
        Assert.True(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
        Directory.Delete(lockPath);
        var recovered = store.Sweep(Now);
        Assert.Equal(0, recovered.Failures); Assert.Equal(1, recovered.DeletedRuns);
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public void Store_CopiesAllRetentionLimitsBeforeCallerMutatesOptions()
    {
        using var archive = new DiagnosticRunTestDirectory();
        var options = new DiagnosticRunRetentionOptions { MaxRuns = 2, MaxTotalBytes = 100_000, MaxAge = TimeSpan.FromDays(7) };
        var store = archive.Store(options);
        options.MaxRuns = 1; options.MaxTotalBytes = 1; options.MaxAge = TimeSpan.FromTicks(1);
        var old = archive.Closed(store, Now.AddHours(-3));
        var middle = archive.Closed(store, Now.AddHours(-2)); var newest = archive.Closed(store, Now.AddHours(-1));
        var status = store.Sweep(Now);
        Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(middle)); Assert.True(Directory.Exists(newest));
        Assert.Equal(2, status.RetainedRuns); Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData("runs", 0)] [InlineData("runs", -1)]
    [InlineData("bytes", 0)] [InlineData("bytes", -1)]
    [InlineData("age", 0)] [InlineData("age", -1)]
    public void InvalidRetentionLimits_AreRejectedBeforeCreatingDirectories(string field, int value)
    {
        using var archive = new DiagnosticRunTestDirectory();
        var options = new DiagnosticRunRetentionOptions();
        if (field == "runs") options.MaxRuns = value;
        if (field == "bytes") options.MaxTotalBytes = value;
        if (field == "age") options.MaxAge = TimeSpan.FromTicks(value);
        Assert.Throws<ArgumentOutOfRangeException>(() => archive.Store(options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonDiagnosticSink(archive.Root, 1024, 2, null, options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticPipeline(new() { JsonRetention = options, Emergency = new() { Enabled = false } }));
        Assert.False(Directory.Exists(archive.Root));
    }
}

internal sealed class DiagnosticRunTestDirectory : IDisposable
{
    private readonly List<string> _links = [];
    internal string TempRoot { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-RunRetentionTests-" + Guid.NewGuid().ToString("N"));
    internal string Root => Path.Combine(TempRoot, "runs");
    internal DiagnosticRunTestDirectory() => Directory.CreateDirectory(TempRoot);
    internal DiagnosticRunStore Store(DiagnosticRunRetentionOptions options) => new(Root, options);
    internal string Closed(DiagnosticRunStore store, DateTimeOffset time, int outputBytes = 16)
    {
        using var job = store.Create(time);
        File.WriteAllBytes(Path.Combine(job.Directory, "events-0.jsonl"), Enumerable.Repeat((byte)'x', outputBytes).ToArray());
        File.WriteAllText(Path.Combine(job.Directory, "events.lock"), "lock-metadata");
        return job.Directory;
    }
    internal static long Size(string directory) => Directory.EnumerateFiles(directory).Sum(path => new FileInfo(path).Length);
    internal static string[] Snapshot(string directory) => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(directory, path) + ":" + Convert.ToHexString(File.ReadAllBytes(path))).ToArray();
    internal void Junction(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
            Assert.True(process.ExitCode == 0, output + error);
        }
        else Directory.CreateSymbolicLink(link, target);
        _links.Add(link);
    }
    public void Dispose()
    {
        foreach (var link in _links.AsEnumerable().Reverse()) if (Directory.Exists(link)) Directory.Delete(link);
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(TempRoot);
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test cleanup escaped OS temp.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
