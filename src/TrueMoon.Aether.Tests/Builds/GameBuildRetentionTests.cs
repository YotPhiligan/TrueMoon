using System.Diagnostics;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sweep_EmptyStoreDoesNotCreateArchiveOrReportLoss()
    {
        using var archive = new BuildRetentionTestArchive();
        var status = archive.Store(new()).Sweep(Now);
        Assert.Equal(new GameBuildRetentionStatus(0, 0, 0, 0, 0, 0, null, false, false), status);
        Assert.False(Directory.Exists(archive.Jobs));
    }

    [Fact]
    public void Sweep_CountLimitDeletesOldestByCompletionInsteadOfDirectoryMtime()
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxJobs = 2 });
        var oldest = archive.Completed(store, Now.AddHours(-3));
        var older = archive.Completed(store, Now.AddHours(-2));
        var newer = archive.Completed(store, Now.AddHours(-1));
        var newest = archive.Completed(store, Now);
        Directory.SetLastWriteTimeUtc(oldest, Now.AddDays(5).UtcDateTime);
        Directory.SetLastWriteTimeUtc(newest, Now.AddDays(-5).UtcDateTime);

        var status = store.Sweep(Now);
        Assert.False(Directory.Exists(oldest)); Assert.False(Directory.Exists(older));
        Assert.True(Directory.Exists(newer)); Assert.True(Directory.Exists(newest));
        Assert.Equal(2, status.RetainedJobs); Assert.Equal(2, status.DeletedJobs);
        Assert.Equal(BuildRetentionTestArchive.Size(newer) + BuildRetentionTestArchive.Size(newest), status.RetainedBytes);
        Assert.False(status.IsOverBudget); Assert.Equal(0, status.Failures);
        var unchanged = store.Sweep(Now);
        Assert.Equal(2, unchanged.RetainedJobs); Assert.Equal(0, unchanged.DeletedJobs);
    }

    [Fact]
    public void Sweep_EqualCompletionTimesUseOrdinalDirectoryTieBreaker()
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxJobs = 1 });
        var jobs = Enumerable.Range(0, 3).Select(_ => archive.Completed(store, Now)).Order(StringComparer.Ordinal).ToArray();
        var status = store.Sweep(Now);
        Assert.False(Directory.Exists(jobs[0])); Assert.False(Directory.Exists(jobs[1]));
        Assert.True(Directory.Exists(jobs[2]));
        Assert.Equal(2, status.DeletedJobs); Assert.Equal(1, status.RetainedJobs);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    public void Sweep_ByteBudgetIncludesMarkerAndRetainsExactEquality(int adjustment, int expectedJobs)
    {
        using var archive = new BuildRetentionTestArchive();
        var writer = archive.Store(new());
        var old = archive.Completed(writer, Now.AddMinutes(-1), 25);
        var newest = archive.Completed(writer, Now, 45);
        var total = BuildRetentionTestArchive.Size(old) + BuildRetentionTestArchive.Size(newest);
        var store = archive.Store(new() { MaxJobs = 10, MaxTotalBytes = total + adjustment });

        var status = store.Sweep(Now);
        Assert.Equal(expectedJobs, status.RetainedJobs);
        Assert.Equal(2 - expectedJobs, status.DeletedJobs);
        Assert.Equal(expectedJobs == 2, Directory.Exists(old));
        Assert.True(Directory.Exists(newest));
        Assert.Equal(expectedJobs == 2 ? total : BuildRetentionTestArchive.Size(newest), status.RetainedBytes);
        Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Sweep_AgeExpiresOnlyStrictlyAfterCompletionLimit(int ticksInsideLimit, bool retained)
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxAge = TimeSpan.FromHours(2) });
        var boundary = archive.Completed(store, Now.AddHours(-2).AddTicks(ticksInsideLimit).ToOffset(TimeSpan.FromHours(5)));
        var newest = archive.Completed(store, Now);
        Assert.EndsWith("+00:00", File.ReadAllText(Path.Combine(boundary, BuildDiagnosticStore.MarkerFile)));

        var status = store.Sweep(Now);
        Assert.Equal(retained, Directory.Exists(boundary));
        Assert.True(Directory.Exists(newest));
        Assert.Equal(retained ? 2 : 1, status.RetainedJobs);
        Assert.Equal(retained ? 0 : 1, status.DeletedJobs);
        Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("bytes")]
    [InlineData("age")]
    public void Sweep_ProtectsNewestAndExplicitJobAndReportsUnsatisfiedBudget(string budget)
    {
        using var archive = new BuildRetentionTestArchive();
        var options = new GameBuildRetentionOptions();
        if (budget == "count") options.MaxJobs = 1;
        if (budget == "bytes") options.MaxTotalBytes = 1;
        if (budget == "age") options.MaxAge = TimeSpan.FromMinutes(1);
        var store = archive.Store(options);
        var protectedOld = archive.Completed(store, Now.AddDays(-2));
        var newest = archive.Completed(store, Now.AddDays(-1));

        var status = store.Sweep(Now, protectedOld + Path.DirectorySeparatorChar);
        Assert.True(Directory.Exists(protectedOld)); Assert.True(Directory.Exists(newest));
        Assert.Equal(2, status.RetainedJobs); Assert.Equal(0, status.DeletedJobs);
        Assert.True(status.IsOverBudget); Assert.Equal(0, status.Failures);
    }

    [Fact]
    public void Sweep_ProtectsActiveJobAcrossStoreInstancesUntilItCompletes()
    {
        using var archive = new BuildRetentionTestArchive();
        var first = archive.Store(new() { MaxJobs = 1 });
        var second = archive.Store(new() { MaxJobs = 1 });
        using var active = first.Begin();
        File.WriteAllText(Path.Combine(active.Directory, "build.stdout.log"), "active-output");
        var old = archive.Completed(second, Now.AddMinutes(-2));
        var newest = archive.Completed(second, Now.AddMinutes(-1));

        var status = second.Sweep(Now);
        Assert.Equal(1, status.ActiveJobs); Assert.Equal(1, status.RetainedJobs);
        Assert.Equal(BuildRetentionTestArchive.Size(newest), status.RetainedBytes);
        Assert.True(Directory.Exists(active.Directory)); Assert.False(Directory.Exists(old));
        Assert.False(status.IsOverBudget);
        active.Complete(Now);
        var finished = second.Sweep(Now);
        Assert.Equal(0, finished.ActiveJobs); Assert.Equal(1, finished.RetainedJobs);
        Assert.False(Directory.Exists(newest));
        Assert.Equal("active-output", File.ReadAllText(Path.Combine(active.Directory, "build.stdout.log")));
    }

    [Fact]
    public async Task Sweep_MaintenanceContentionPreservesJobsAndRecoversWithoutBlocking()
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxJobs = 1 });
        var old = archive.Completed(store, Now.AddMinutes(-1));
        var newest = archive.Completed(store, Now);
        using (var held = new FileStream(Path.Combine(archive.Jobs, ".retention.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var busy = await Task.Run(() => store.Sweep(Now)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(busy.MaintenanceBusy); Assert.Equal(0, busy.Failures); Assert.Equal(0, busy.DeletedJobs);
            Assert.True(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
        }
        var recovered = store.Sweep(Now);
        Assert.False(recovered.MaintenanceBusy); Assert.Equal(1, recovered.DeletedJobs);
        Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
    }

    [Theory]
    [InlineData("unmarked")]
    [InlineData("invalid-name")]
    [InlineData("invalid-magic")]
    [InlineData("invalid-schema")]
    [InlineData("invalid-time")]
    [InlineData("oversized-marker")]
    [InlineData("incomplete")]
    [InlineData("nested-staging")]
    [InlineData("artifact-file")]
    [InlineData("directory-with-file-name")]
    public void Sweep_PreservesUnknownIncompleteAndNonflatDirectoriesWhilePruningOwnedJobs(string kind)
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxJobs = 1 });
        string preserved;
        if (kind == "incomplete") { using var job = store.Begin(); preserved = job.Directory; }
        else preserved = archive.Completed(store, Now.AddHours(-2));
        var marker = Path.Combine(preserved, BuildDiagnosticStore.MarkerFile);
        switch (kind)
        {
            case "unmarked": File.Delete(marker); break;
            case "invalid-name":
                var renamed = Path.Combine(archive.Jobs, "legacy-job"); Directory.Move(preserved, renamed); preserved = renamed; break;
            case "invalid-magic": File.WriteAllText(marker, "Unowned/1\n" + Now.ToString("O")); break;
            case "invalid-schema": File.WriteAllText(marker, File.ReadAllText(marker).Replace("/1\n", "/2\n", StringComparison.Ordinal)); break;
            case "invalid-time": File.WriteAllText(marker, "TrueMoon.Aether.BuildDiagnostics/1\nnot-a-completion-time"); break;
            case "oversized-marker": File.WriteAllBytes(marker, new byte[129]); break;
            case "nested-staging": Directory.CreateDirectory(Path.Combine(preserved, "output")); File.WriteAllText(Path.Combine(preserved, "output", "game.dll"), "artifact"); break;
            case "artifact-file": File.WriteAllText(Path.Combine(preserved, "game.dll"), "artifact"); break;
            case "directory-with-file-name": File.Delete(Path.Combine(preserved, "build.stdout.log")); Directory.CreateDirectory(Path.Combine(preserved, "build.stdout.log")); break;
        }
        var before = BuildRetentionTestArchive.Snapshot(preserved);
        var removable = archive.Completed(store, Now.AddHours(-1));
        var newest = archive.Completed(store, Now);

        var status = store.Sweep(Now);
        Assert.Equal(before, BuildRetentionTestArchive.Snapshot(preserved));
        if (kind == "directory-with-file-name") Assert.True(Directory.Exists(Path.Combine(preserved, "build.stdout.log")));
        Assert.False(Directory.Exists(removable)); Assert.True(Directory.Exists(newest));
        Assert.Equal(1, status.SkippedDirectories); Assert.Equal(1, status.RetainedJobs);
        Assert.Equal(1, status.DeletedJobs); Assert.Equal(0, status.Failures);
    }

    [Fact]
    public void Sweep_NeverInspectsOrDeletesPublishedArtifactsOutsideJobs()
    {
        using var archive = new BuildRetentionTestArchive();
        var artifact = Path.Combine(archive.Root, new string('A', 64)); Directory.CreateDirectory(artifact);
        var dll = Path.Combine(artifact, "Game.dll"); File.WriteAllText(dll, "published-artifact");
        var store = archive.Store(new() { MaxJobs = 1 });
        var old = archive.Completed(store, Now.AddMinutes(-1)); var newest = archive.Completed(store, Now);
        var status = store.Sweep(Now);
        Assert.Equal("published-artifact", File.ReadAllText(dll));
        Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
        Assert.Equal(1, status.DeletedJobs); Assert.Equal(0, status.SkippedDirectories);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("jobs")]
    [InlineData("job")]
    [InlineData("output-entry")]
    public void Sweep_RejectsReparseAncestryAndEntriesWithoutTouchingTargets(string kind)
    {
        using var archive = new BuildRetentionTestArchive();
        var externalRoot = Path.Combine(archive.TempRoot, "external");
        var externalStore = new BuildDiagnosticStore(externalRoot, new());
        var externalOld = archive.Completed(externalStore, Now.AddHours(-2));
        var externalNewest = archive.Completed(externalStore, Now.AddHours(-1));
        var sentinel = Path.Combine(externalOld, "build.stdout.log"); File.WriteAllText(sentinel, "outside-owned-root");
        var beforeOld = BuildRetentionTestArchive.Snapshot(externalOld);
        var beforeNewest = BuildRetentionTestArchive.Snapshot(externalNewest);
        var store = archive.Store(new() { MaxJobs = 1 });
        string link, target;
        if (kind == "root") { link = archive.Root; target = externalRoot; }
        else if (kind == "jobs") { Directory.CreateDirectory(archive.Root); link = archive.Jobs; target = Path.Combine(externalRoot, "jobs"); }
        else if (kind == "job")
        {
            archive.Completed(store, Now); link = Path.Combine(archive.Jobs, Guid.NewGuid().ToString("N")); target = externalOld;
        }
        else
        {
            var candidate = archive.Completed(store, Now.AddHours(-1));
            link = Path.Combine(candidate, "build.stdout.log"); File.Delete(link);
            archive.Completed(store, Now); target = externalOld;
        }
        archive.Junction(link, target);
        var status = store.Sweep(Now);
        Assert.Equal("outside-owned-root", File.ReadAllText(sentinel));
        Assert.Equal(beforeOld, BuildRetentionTestArchive.Snapshot(externalOld));
        Assert.Equal(beforeNewest, BuildRetentionTestArchive.Snapshot(externalNewest));
        Assert.True(Directory.Exists(link)); Assert.Equal(0, status.DeletedJobs);
        if (kind is "root" or "jobs") Assert.Equal(1, status.Failures);
        else Assert.Equal(1, status.SkippedDirectories);
        if (kind is "root" or "jobs") Assert.Throws<IOException>(() => store.Begin());
    }

    [Fact]
    public void Sweep_DeleteFailurePreservesOwnershipContinuesAndRetriesAfterHandleRelease()
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxJobs = 1 });
        var blocked = archive.Completed(store, Now.AddHours(-2));
        var removable = archive.Completed(store, Now.AddHours(-1));
        var newest = archive.Completed(store, Now);
        using (var held = new FileStream(Path.Combine(blocked, "build.stdout.log"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = store.Sweep(Now);
            Assert.Equal(1, failed.Failures); Assert.Equal(1, failed.DeletedJobs);
            Assert.Equal(2, failed.RetainedJobs); Assert.True(failed.IsOverBudget);
            Assert.InRange(failed.LastError!.Length, 1, 1024);
            Assert.True(File.Exists(Path.Combine(blocked, BuildDiagnosticStore.MarkerFile)));
            Assert.False(Directory.Exists(removable)); Assert.True(Directory.Exists(newest));
        }
        var recovered = store.Sweep(Now);
        Assert.Equal(0, recovered.Failures); Assert.Null(recovered.LastError);
        Assert.Equal(1, recovered.DeletedJobs); Assert.Equal(1, recovered.RetainedJobs);
        Assert.False(recovered.IsOverBudget); Assert.False(Directory.Exists(blocked));
    }

    [Fact]
    public void Sweep_MaintenanceFailureIsObservableAndCanBeRetried()
    {
        using var archive = new BuildRetentionTestArchive();
        var store = archive.Store(new() { MaxJobs = 1 });
        var old = archive.Completed(store, Now.AddMinutes(-1)); var newest = archive.Completed(store, Now);
        var lockPath = Path.Combine(archive.Jobs, ".retention.lock"); Directory.CreateDirectory(lockPath);
        var failed = store.Sweep(Now);
        Assert.Equal(1, failed.Failures); Assert.NotNull(failed.LastError); Assert.False(failed.MaintenanceBusy);
        Assert.True(Directory.Exists(old)); Assert.True(Directory.Exists(newest));
        Directory.Delete(lockPath);
        var recovered = store.Sweep(Now);
        Assert.Equal(0, recovered.Failures); Assert.Equal(1, recovered.DeletedJobs);
        Assert.False(Directory.Exists(old));
    }

    [Fact]
    public void Store_CopiesAllRetentionLimitsBeforeCallerMutatesOptions()
    {
        using var archive = new BuildRetentionTestArchive();
        var options = new GameBuildRetentionOptions { MaxJobs = 2, MaxTotalBytes = 100_000, MaxAge = TimeSpan.FromDays(7) };
        var store = archive.Store(options);
        options.MaxJobs = 1; options.MaxTotalBytes = 1; options.MaxAge = TimeSpan.FromTicks(1);
        var old = archive.Completed(store, Now.AddHours(-3));
        var middle = archive.Completed(store, Now.AddHours(-2)); var newest = archive.Completed(store, Now.AddHours(-1));
        var status = store.Sweep(Now);
        Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(middle)); Assert.True(Directory.Exists(newest));
        Assert.Equal(2, status.RetainedJobs); Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData("jobs", 0)] [InlineData("jobs", -1)]
    [InlineData("bytes", 0)] [InlineData("bytes", -1)]
    [InlineData("age", 0)] [InlineData("age", -1)]
    public void InvalidRetentionLimits_AreRejectedBeforeCreatingDirectories(string field, int value)
    {
        using var archive = new BuildRetentionTestArchive();
        var options = new GameBuildRetentionOptions();
        if (field == "jobs") options.MaxJobs = value;
        if (field == "bytes") options.MaxTotalBytes = value;
        if (field == "age") options.MaxAge = TimeSpan.FromTicks(value);
        Assert.Throws<ArgumentOutOfRangeException>(() => archive.Store(options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameBuildCompiler(GameBuildFixture.Hash, archive.Root, null, null, options));
        Assert.False(Directory.Exists(archive.Root));
    }
}

internal sealed class BuildRetentionTestArchive : IDisposable
{
    private readonly List<string> _links = [];
    internal string TempRoot { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-RetentionTests-" + Guid.NewGuid().ToString("N"));
    internal string Root => Path.Combine(TempRoot, "artifacts");
    internal string Jobs => Path.Combine(Root, "jobs");
    internal BuildRetentionTestArchive() => Directory.CreateDirectory(TempRoot);
    internal BuildDiagnosticStore Store(GameBuildRetentionOptions options) => new(Root, options);
    internal string Completed(BuildDiagnosticStore store, DateTimeOffset time, int outputBytes = 16)
    {
        using var job = store.Begin();
        File.WriteAllBytes(Path.Combine(job.Directory, "build.stdout.log"), Enumerable.Repeat((byte)'x', outputBytes).ToArray());
        File.WriteAllText(Path.Combine(job.Directory, "build.stderr.log"), "stderr");
        File.WriteAllText(Path.Combine(job.Directory, "build.output.json"), "{}");
        job.Complete(time); return job.Directory;
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
