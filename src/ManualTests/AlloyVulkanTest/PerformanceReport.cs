using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Rendering.Skia;

namespace AlloyVulkanTest;

internal readonly record struct PerformanceMeasurement(long Ticks, long AllocatedBytes, int Calls);
internal readonly record struct PerformanceStart(long Ticks, long AllocatedBytes);
internal readonly record struct HostSample(PerformanceMeasurement ExportOrCompletion, PerformanceMeasurement Return, PerformanceMeasurement Composition, PerformanceMeasurement Presentation, ulong UiImage = 0);
internal readonly record struct FrameSample(PerformanceMeasurement Mutation, PerformanceMeasurement Update, PerformanceMeasurement Measure, PerformanceMeasurement Arrange, PerformanceMeasurement Draw, HostSample Host, bool Rendered);
internal sealed record PerformanceCalls(long Measure, long Arrange, long Draw, long Resize, long SurfaceCreates);
internal sealed record PerformanceCase(string Backend, string Mode, int Rows, int MinimumElements, int MaximumElements, int WarmupFrames, double WarmupMilliseconds, int Samples,
    long BatchTicks, int[] Collections, PerformanceCalls Calls, FrameSample[] Frames);
internal sealed record PerformanceStatistics(int Samples, int Calls, double MeanMicroseconds, double MedianMicroseconds, double P95Microseconds, double MinimumMicroseconds,
    double MaximumMicroseconds, double StandardDeviationMicroseconds, double MeanAllocatedBytes, long TotalAllocatedBytes);

internal static class PerformanceClock
{
    internal static PerformanceStart Begin()
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        return new PerformanceStart(Stopwatch.GetTimestamp(), allocated);
    }
    internal static PerformanceMeasurement End(PerformanceStart start)
    {
        var ticks = Stopwatch.GetTimestamp() - start.Ticks;
        return new PerformanceMeasurement(ticks, GC.GetAllocatedBytesForCurrentThread() - start.AllocatedBytes, 1);
    }
    internal static PerformanceMeasurement Measure(Action action)
    { var start = Begin(); action(); return End(start); }
    internal static PerformanceMeasurement[] Calibrate(int samples)
    {
        for (var i = 0; i < 1000; i++) Measure(Noop);
        var results = new PerformanceMeasurement[samples];
        for (var i = 0; i < results.Length; i++) results[i] = Measure(Noop);
        return results;
    }
    private static void Noop() { }
}

internal static class PerformanceReport
{
    private static readonly (string Name, Func<FrameSample, PerformanceMeasurement> Select)[] Phases =
    [
        ("model-mutation", frame => frame.Mutation), ("update-total", frame => frame.Update),
        ("measure-core", frame => frame.Measure), ("arrange-core", frame => frame.Arrange), ("surface-render", frame => frame.Draw),
        ("host-export-or-completion", frame => frame.Host.ExportOrCompletion), ("host-return", frame => frame.Host.Return),
        ("host-composition", frame => frame.Host.Composition), ("host-presentation", frame => frame.Host.Presentation)
    ];

    internal static PerformanceStatistics Summarize(IEnumerable<PerformanceMeasurement> values)
    {
        var samples = values.ToArray(); var times = samples.Select(value => value.Ticks * (1_000_000d / Stopwatch.Frequency)).Order().ToArray();
        var mean = times.Average(); var allocated = samples.Sum(value => value.AllocatedBytes);
        return new PerformanceStatistics(samples.Length, samples.Sum(value => value.Calls), mean, times[(int)Math.Ceiling(times.Length * .5) - 1], times[(int)Math.Ceiling(times.Length * .95) - 1],
            times[0], times[^1], Math.Sqrt(times.Sum(value => (value - mean) * (value - mean)) / times.Length), allocated / (double)samples.Length, allocated);
    }

    internal static void Write(SettingsPerformanceProbe.Options options, UiViewport viewport, PerformanceMeasurement[] calibration, Dictionary<string, object> hardware, List<PerformanceCase> cases)
    {
        var summaries = cases.Select(@case => new { @case.Backend, @case.Mode, Phases = Phases.ToDictionary(phase => phase.Name, phase => Summarize(@case.Frames.Select(phase.Select))) }).ToArray();
        var assembly = Assembly.GetExecutingAssembly(); var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        var root = FindRepository();
        var sources = root == null ? [] : Fingerprints(root, SourceFiles(root));
        var binaries = Fingerprints(AppContext.BaseDirectory, Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(path => Path.GetFileName(path).StartsWith("TrueMoon.", StringComparison.Ordinal) || Path.GetFileName(path).StartsWith("Silk.NET.", StringComparison.Ordinal) || Path.GetFileName(path) == "SkiaSharp.dll")
            .Append(assembly.Location).Append(SkiaNativeLibrary.LoadedPath!));
        var fonts = OperatingSystem.IsWindows() ? Fingerprints(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            new[] { "segoeui.ttf", "seguisym.ttf", "seguiemj.ttf" }.Select(name => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", name)).Where(File.Exists)) : [];
        using var environment = options.EnvironmentFile == null ? null : JsonDocument.Parse(File.ReadAllText(options.EnvironmentFile));
        using var nativeBuild = root == null ? null : JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.build.json")));
        var report = new
        {
            Schema = "truemoon-alloy-settings-baseline-v1", CreatedUtc = DateTime.UtcNow, Configuration = configuration,
            Method = "Sequential instrumented frame baseline; raw samples retained; no outlier removal or overhead subtraction; no statistical confidence claim.",
            Scene = new { View = "AlloyTest.View1", ViewWidth = 720, ViewHeight = 1000, viewport.Width, viewport.Height, viewport.Scale, options.Rows, options.Samples,
                MinimumWarmupFrames = options.Warmup, MinimumWarmupMilliseconds = options.WarmupMilliseconds, Theme = "Dark/default density", LoadState = "Ready", Focused = false,
                PropertyEdit = "Name alternates Замер А 👩‍💻 / Замер Б 👩‍💻", ListEdit = "append new fixed-name row / remove last row; count N/N+1; unchanged rows preserve identity",
                Font = "Segoe UI and system fallback; font file hashes identify this machine, not delivered fixed-font golden fixtures" },
            Boundaries = new
            {
                UpdateTotal = "UiSession.Update including posted bindings, layout and renderer; contains measure/arrange/render subphases. Do not add nested time/allocations to total.",
                MeasureCore = "Transparent profiling Panel.MeasureCore: full View1 subtree Measure; excludes outer Panel Measure margin/padding bookkeeping.",
                ArrangeCore = "Transparent profiling Panel.ArrangeCore: full View1 subtree Arrange; excludes outer Panel Arrange slot bookkeeping.",
                Render = "CPU wall time in IUiRenderSurface.Render; raster executes on CPU, GL includes Flush, Vulkan records Skia work. Not GPU timestamp duration.",
                Host = "GL glFinish completion; Vulkan export+Submit(true)+WaitIdle, Return+WaitIdle. HUD composition and presentation measured separately; each includes sequential host waits.",
                Allocation = "GC.GetAllocatedBytesForCurrentThread delta; managed allocations on owner thread only. Excludes native/driver/VRAM and other threads; not live heap size.",
                Excluded = "Session/tree/device/compositor/swapchain setup, initial frame, warmup, report construction, snapshots/readback and disposal. BatchTicks includes harness overhead.",
                Counters = "UI factory creates/resize/measure/arrange/render and retained identity assertions; do not measure native GPU allocation count or VRAM.",
                Calibration = "Empty delegate with same Begin/End pair; no subtraction. Sub-microsecond static Update is near the clock/instrumentation floor."
            },
            Runtime = new { Framework = RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture, RuntimeInformation.ProcessArchitecture,
                Environment.ProcessorCount, Stopwatch.Frequency, Stopwatch.IsHighResolution, GCSettings.IsServerGC, LatencyMode = GCSettings.LatencyMode.ToString(), Debugger.IsAttached,
                TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"), TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") },
            ExternalEnvironment = environment?.RootElement.Clone(), Hardware = hardware, NativeBuild = nativeBuild?.RootElement.Clone(),
            SourceFiles = sources, SourceFingerprint = Hash(Encoding.UTF8.GetBytes(string.Join('\n', sources.Select(file => file.Path + "=" + file.Sha256)))),
            BinaryFiles = binaries, FontFiles = fonts, Asset = Fingerprints(AppContext.BaseDirectory, [Path.Combine(AppContext.BaseDirectory, "Assets", "alloy-mark.png")]),
            Calibration = Summarize(calibration), CalibrationSamples = calibration, Cases = cases, Summaries = summaries
        };
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        var output = Path.GetFullPath(options.Output); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, jsonOptions) + "\n");
        var csv = new StringBuilder("backend,mode,phase,samples,calls,mean_us,median_us,p95_us,min_us,max_us,stddev_us,managed_bytes_mean,managed_bytes_total\n");
        var markdown = new StringBuilder($"# Alloy settings baseline ({configuration})\n\n32-row default scene; actual configured rows: {options.Rows}; viewport900×1100 scale1; samples{options.Samples}; warmup≥{options.Warmup} frames/≥{options.WarmupMilliseconds}ms.\n\nTime is CPU wall time; allocations are managed owner-thread bytes. Nested phases are included in Update. All samples/outliers retained. See JSON for boundaries, counters, fingerprints, hardware and raw data.\n\n| Backend | Mode | Update median µs | Update p95 µs | Update B/frame | Measure median µs | Arrange median µs | Render median µs |\n| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |\n");
        foreach (var summary in summaries)
        {
            foreach (var (phase, value) in summary.Phases)
                csv.Append(CultureInfo.InvariantCulture, $"{summary.Backend},{summary.Mode},{phase},{value.Samples},{value.Calls},{value.MeanMicroseconds:F6},{value.MedianMicroseconds:F6},{value.P95Microseconds:F6},{value.MinimumMicroseconds:F6},{value.MaximumMicroseconds:F6},{value.StandardDeviationMicroseconds:F6},{value.MeanAllocatedBytes:F3},{value.TotalAllocatedBytes}\n");
            var update = summary.Phases["update-total"];
            markdown.Append(CultureInfo.InvariantCulture, $"| {summary.Backend} | {summary.Mode} | {update.MedianMicroseconds:F3} | {update.P95Microseconds:F3} | {update.MeanAllocatedBytes:F1} | {summary.Phases["measure-core"].MedianMicroseconds:F3} | {summary.Phases["arrange-core"].MedianMicroseconds:F3} | {summary.Phases["surface-render"].MedianMicroseconds:F3} |\n");
        }
        markdown.Append("\n| Backend | Mode | Export/completion median µs | Return median µs | Composition median µs | Presentation median µs |\n| --- | --- | ---: | ---: | ---: | ---: |\n");
        foreach (var summary in summaries)
            markdown.Append(CultureInfo.InvariantCulture, $"| {summary.Backend} | {summary.Mode} | {summary.Phases["host-export-or-completion"].MedianMicroseconds:F3} | {summary.Phases["host-return"].MedianMicroseconds:F3} | {summary.Phases["host-composition"].MedianMicroseconds:F3} | {summary.Phases["host-presentation"].MedianMicroseconds:F3} |\n");
        File.WriteAllText(Path.ChangeExtension(output, ".csv"), csv.ToString()); File.WriteAllText(Path.ChangeExtension(output, ".md"), markdown.ToString());
        Console.WriteLine($"Baseline JSON/CSV/Markdown: {output}; Release:{configuration == "Release"}; calibration median{report.Calibration.MedianMicroseconds:F3}us.");
    }

    private sealed record Fingerprint(string Path, string Sha256);
    private static Fingerprint[] Fingerprints(string root, IEnumerable<string> paths) => paths.Order(StringComparer.Ordinal).Select(path => new Fingerprint(Path.GetRelativePath(root, path).Replace('\\', '/'), Hash(File.ReadAllBytes(path)))).ToArray();
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string? FindRepository()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && File.Exists(Path.Combine(directory.FullName, "TrueMoon.slnx"))) return directory.FullName;
        return null;
    }
    private static IEnumerable<string> SourceFiles(string root)
    {
        foreach (var folder in new[] { "TrueMoon.Argentis", "TrueMoon.Alloy/Runtime", "TrueMoon.Alloy.Rendering.Skia", "TrueMoon.Alloy.Platform.Silk", "TrueMoon.Alloy.Hosting", "ManualTests/AlloyTest", "ManualTests/AlloyVulkanTest" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
                if (Path.GetExtension(file) is ".cs" or ".csproj" && !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")) yield return file;
        yield return Path.Combine(root, "ManualTests/AlloyVulkanTest/MeasureSettings.ps1");
        foreach (var name in new[] { "Directory.Build.props", "Directory.Packages.props", "global.json", "TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.build.json" })
            if (File.Exists(Path.Combine(root, name))) yield return Path.Combine(root, name);
    }
}
