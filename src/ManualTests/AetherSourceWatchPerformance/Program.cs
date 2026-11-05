using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Dependencies;

namespace AetherSourceWatchPerformance;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private const int SampleMilliseconds = 5;
    internal static async Task<int> Main(string[] args)
    {
        var output = Path.GetFullPath(Value("--output"));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This source profile and IO counters require Windows.");
        Directory.CreateDirectory(output);
        if (File.Exists(Path.Combine(output, "report.json"))) throw new IOException("Choose a new output directory; existing evidence is retained.");
        var report = new Dictionary<string, object?> { ["Completed"] = false, ["StartedUtc"] = DateTimeOffset.UtcNow,
            ["Profile"] = Value("--profile"), ["Notifications"] = bool.Parse(Value("--notifications")), ["SamplerMilliseconds"] = SampleMilliseconds,
            ["Environment"] = new { OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(), Environment.ProcessorCount, Environment.ProcessId, IsServerGC = System.Runtime.GCSettings.IsServerGC } };
        try
        {
            await Run(output, report);
            report["Completed"] = true;
            return 0;
        }
        catch (Exception error) { report["Failure"] = error.ToString(); Console.Error.WriteLine(error); return 1; }
        finally { report["FinishedUtc"] = DateTimeOffset.UtcNow; await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, Json)); }

        string Value(string name)
        { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("Missing " + name); }
    }

    private static async Task Run(string output, Dictionary<string, object?> report)
    {
        Progress("Generating fixture");
        // Keep generated projects outside the checkout's Directory.Build.props hierarchy and short enough for SDK tools.
        var runDirectory = Path.Combine(Path.GetTempPath(), "tmob-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(runDirectory); report["RunDirectory"] = runDirectory;
        var fixture = SourceGraph.Write(Path.Combine(runDirectory, "s"), (string)report["Profile"]!);
        var options = new EditorSourceWatchOptions { UseFileSystemNotifications = (bool)report["Notifications"]! };
        report["Options"] = options;
        await using var app = App.Builder(_ => { }).Setup(context => context.Services(services =>
        {
            services.Singleton<GameBuildCompiler>(_ => new(new string('A', 64), Path.Combine(runDirectory, "a")));
            services.Singleton<IFactory<EditorSourceWatch, ResolvedProject>>(_ => new Factory<EditorSourceWatch, ResolvedProject>(project => new(project, options, TimeProvider.System)));
        })).Build();
        var compiler = app.Services.Resolve<GameBuildCompiler>()!;
        Progress("Frozen Build (excluded from observation windows)");
        var buildStart = Stopwatch.GetTimestamp();
        var artifact = await compiler.BuildAsync(fixture.Request);
        var descriptor = artifact.Inputs?.SourceWatch ?? throw new InvalidOperationException("No captured descriptor: " + artifact.Inputs?.Preparation?.SourceWatchUnavailableReason);
        Check(compiler.ProcessId == null, "Compiler child remains live.");
        report["Build"] = new { Milliseconds = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds, artifact.BuildId,
            Inputs = artifact.Inputs!.Fingerprint, descriptor.SourceFingerprint, descriptor.Version, Trees = descriptor.SourceTrees.Count,
            Files = descriptor.Files.Count, TreeFiles = descriptor.TreeFiles.Count, Probes = descriptor.Probes.Count,
            Bytes = descriptor.Files.Sum(file => file.Length), fixture.GeneratedFiles, fixture.GeneratedBytes,
            CompilerDiagnostics = compiler.ReadDiagnostics(16, 0) };
        Check(descriptor.SourceTrees.Count == 4 && descriptor.TreeFiles.Count >= fixture.GeneratedFiles, "The real descriptor lost the large graph.");
        report["Descriptor"] = descriptor;
        // Setup/build allocations are excluded; no induced collections during the measured windows.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        using var metrics = new ProcessMetrics();
        var factory = app.Services.Resolve<IFactory<EditorSourceWatch, ResolvedProject>>()!;
        await using var watch = factory.Create(fixture.Project)!;
        Progress("NoBuild idle");
        var initial = watch.Read();
        Check(initial is { State: EditorSourceState.NoBuild, WatcherCount: 0, IsScanning: false }, "Open activated tracking.");
        report["NoBuildIdle"] = await Idle(watch, metrics, TimeSpan.FromSeconds(2));
        Check(watch.Read() == initial, "An inert observer published work.");
        Progress("Initial observation");
        var passes = new List<PassResult>(); report["Passes"] = passes;
        var beforeBind = metrics.Capture(); watch.Bind(artifact);
        passes.Add(await Pass(watch, metrics, 0, "initial", beforeBind));
        for (var pass = 0; pass < 3; pass++)
        {
            Progress("Steady polling pass " + (pass + 1));
            var previous = watch.Read();
            passes.Add(await Pass(watch, metrics, previous.ObservationVersion, "steady", metrics.Capture()));
        }
        var observed = watch.Read();
        report["StableSnapshot"] = observed;
        report["BudgetExceededPasses"] = passes.Count(pass => pass.Snapshot.Reason?.Contains("time budget", StringComparison.Ordinal) == true);
        if (observed.State != EditorSourceState.ObservedUnchanged)
        {
            // A budget failure is a measured outcome, not a reason to raise the production budget or claim Same.
            report["ContentChangeSkipped"] = "Baseline observation is " + observed.State + ": " + observed.Reason;
        }
        else
        {
            var target = descriptor.Files.Last(file => (Path.GetExtension(file.Path) is ".cs" or ".dat") && descriptor.TreeFiles.Contains(file.Path, StringComparer.OrdinalIgnoreCase) && Path.GetFileName(file.Path).StartsWith('C'));
            var stamp = File.GetLastWriteTimeUtc(target.Path);
            var offset = target.Length - 4; // Last 'x' in the generated comment; valid C# before and after.
            var beforeChange = metrics.Capture(); var changedAt = Stopwatch.GetTimestamp();
            try
            {
                Mutate(target.Path, offset, (byte)'y'); File.SetLastWriteTimeUtc(target.Path, stamp);
                Progress("Same-length/same-mtime content change");
                var changed = await Pass(watch, metrics, observed.ObservationVersion, "content-change", beforeChange);
                Check(changed.Snapshot.State == EditorSourceState.Changed && changed.Snapshot.ChangedPaths.Contains(target.Path), "Content change was not proved.");
                report["ContentChange"] = new { Target = target.Path, target.Length, SameLength = new FileInfo(target.Path).Length == target.Length,
                    SameTimestamp = File.GetLastWriteTimeUtc(target.Path) == stamp,
                    PublicationMilliseconds = Stopwatch.GetElapsedTime(changedAt).TotalMilliseconds,
                    ExpectedHashBytesToDifference = descriptor.Files.TakeWhile(file => file.Path != target.Path).Sum(file => file.Length) + target.Length,
                    Result = changed };
            }
            finally { Mutate(target.Path, offset, (byte)'x'); File.SetLastWriteTimeUtc(target.Path, stamp); }
            var latched = watch.Read().ObservationVersion;
            report["ChangedIdle"] = await Idle(watch, metrics, TimeSpan.FromSeconds(2));
            Check(watch.Read().State == EditorSourceState.Changed && watch.Read().ObservationVersion == latched, "Changed did not remain latched without rescanning.");
        }
        Progress("Stop/drain");
        var closing = Stopwatch.GetTimestamp(); await watch.DisposeAsync(); await watch.DisposeAsync();
        Check(watch.Read() is { State: EditorSourceState.Stopped, WatcherCount: 0, IsScanning: false }, "Observer did not drain.");
        report["CloseMilliseconds"] = Stopwatch.GetElapsedTime(closing).TotalMilliseconds;
        report["StoppedSnapshot"] = watch.Read();
        var sourceDrift = new List<string>();
        foreach (var file in descriptor.Files)
        {
            using var stream = File.OpenRead(file.Path);
            if (new FileInfo(file.Path).Length != file.Length || Convert.ToHexString(SHA256.HashData(stream)) != file.Sha256) sourceDrift.Add(file.Path);
        }
        report["SourceDrift"] = sourceDrift; Check(sourceDrift.Count == 0, "Fixture not restored.");

        void Progress(string stage) => Program.Progress(stage);
    }

    private static async Task<PassResult> Pass(EditorSourceWatch watch, ProcessMetrics metrics, long version, string kind, MetricPoint before)
    {
        var previousAt = before.Timestamp; long? startBefore = null, startAfter = null;
        var peakPrivate = before.PrivateBytes; var peakWorking = before.WorkingSetBytes;
        var lastMetric = before.Timestamp; var maxGap = 0d; var reads = 0;
        while (true)
        {
            var snapshot = watch.Read(); var now = Stopwatch.GetTimestamp(); reads++;
            maxGap = Math.Max(maxGap, Stopwatch.GetElapsedTime(previousAt, now).TotalMilliseconds);
            if (snapshot.IsScanning && startAfter == null) { startBefore = previousAt; startAfter = now; }
            if (snapshot.ObservationVersion > version)
            {
                var after = metrics.Capture();
                var lower = startAfter.HasValue ? Math.Max(0, Stopwatch.GetElapsedTime(startAfter.Value, previousAt).TotalMilliseconds) : 0;
                var upper = Stopwatch.GetElapsedTime(startBefore ?? previousAt, now).TotalMilliseconds;
                return new(kind, snapshot, MetricWindow.Between(before, after, peakPrivate, peakWorking), lower, upper, maxGap, reads);
            }
            if (Stopwatch.GetElapsedTime(before.Timestamp, now) > TimeSpan.FromSeconds(25)) throw new TimeoutException("No observation: " + snapshot);
            if (Stopwatch.GetElapsedTime(lastMetric, now) >= TimeSpan.FromMilliseconds(100))
            { metrics.SamplePeak(metrics.Capture(), ref peakPrivate, ref peakWorking); lastMetric = now; }
            previousAt = now; await Task.Delay(SampleMilliseconds);
        }
    }
    private static async Task<MetricWindow> Idle(EditorSourceWatch watch, ProcessMetrics metrics, TimeSpan duration)
    {
        var before = metrics.Capture(); var peakPrivate = before.PrivateBytes; var peakWorking = before.WorkingSetBytes;
        var lastMetric = before.Timestamp;
        while (Stopwatch.GetElapsedTime(before.Timestamp) < duration)
        {
            Check(!watch.Read().IsScanning, "Unexpected scan in idle window.");
            if (Stopwatch.GetElapsedTime(lastMetric) >= TimeSpan.FromMilliseconds(100))
            { metrics.SamplePeak(metrics.Capture(), ref peakPrivate, ref peakWorking); lastMetric = Stopwatch.GetTimestamp(); }
            await Task.Delay(SampleMilliseconds);
        }
        return MetricWindow.Between(before, metrics.Capture(), peakPrivate, peakWorking);
    }
    private static void Mutate(string path, long offset, byte value)
    { using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); stream.Position = offset; stream.WriteByte(value); }
    private static void Progress(string stage) => Console.WriteLine(DateTimeOffset.UtcNow.ToString("O") + " " + stage);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record PassResult(string Kind, EditorSourceSnapshot Snapshot, MetricWindow Metrics,
        double ScanLowerMilliseconds, double ScanUpperMilliseconds, double SamplerMaxGapMilliseconds, int SnapshotReads);
}
