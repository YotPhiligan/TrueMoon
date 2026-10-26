using System.Collections.Frozen;
using System.Text.Json;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Tests;

public sealed class ManagedDiagnosticPipelineTests
{
    [Fact]
    public async Task ManagedJsonSink_CreatesLazyIndependentRunsAndRetainsOnlyNewestClosedRun()
    {
        using var directory = new DiagnosticRunTestDirectory();
        var options = new DiagnosticRunRetentionOptions { MaxRuns = 1 };
        await using var first = new JsonDiagnosticSink(directory.Root, 4096, 2, null, options);
        await using var second = new JsonDiagnosticSink(directory.Root, 4096, 2, "events", options);
        Assert.Null(first.DirectoryPath); Assert.Null(first.RetentionStatus);
        Assert.False(Directory.Exists(directory.Root));

        await first.WriteAsync(Record(1));
        await second.WriteAsync(Record(2));
        var firstPath = Assert.IsType<string>(first.DirectoryPath);
        var secondPath = Assert.IsType<string>(second.DirectoryPath);
        Assert.NotEqual(firstPath, secondPath);
        Assert.True(Guid.TryParseExact(Path.GetFileName(firstPath), "N", out _));
        Assert.Equal(directory.Root, Path.GetDirectoryName(firstPath));
        var active = directory.Store(options).Sweep(DateTimeOffset.UtcNow);
        Assert.Equal(2, active.ActiveRuns); Assert.Equal(0, active.RetainedRuns);
        Assert.Equal(0, active.DeletedRuns); Assert.False(active.IsOverBudget);

        await first.DisposeAsync();
        Assert.Equal(new[] { 1 }, await ReadEventIds(firstPath));
        await second.DisposeAsync();
        Assert.False(Directory.Exists(firstPath)); Assert.True(Directory.Exists(secondPath));
        Assert.Equal(new[] { 2 }, await ReadEventIds(secondPath));
        var status = Assert.IsType<DiagnosticRetentionStatus>(second.RetentionStatus);
        Assert.Equal(1, status.RetainedRuns); Assert.Equal(1, status.DeletedRuns);
        Assert.Equal(0, status.ActiveRuns); Assert.Equal(0, status.Failures);
        Assert.False(status.IsOverBudget);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnusedJsonSink_DisposeDoesNotCreateOutput(bool managed)
    {
        using var directory = new DiagnosticRunTestDirectory();
        var sink = managed
            ? new JsonDiagnosticSink(directory.Root, 4096, 2, null, new())
            : new JsonDiagnosticSink(directory.Root, 4096, 2, null);
        await sink.DisposeAsync(); await sink.DisposeAsync();
        Assert.False(Directory.Exists(directory.Root)); Assert.Null(sink.RetentionStatus);
        Assert.Equal(managed ? null : directory.Root, sink.DirectoryPath);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("Events")]
    [InlineData("events-1")]
    public void ManagedJsonSink_RejectsPrefixesOutsideOwnedWhitelistBeforeIo(string prefix)
    {
        using var directory = new DiagnosticRunTestDirectory();
        Assert.Throws<ArgumentException>(() => new JsonDiagnosticSink(directory.Root, 4096, 2, prefix, new()));
        Assert.False(Directory.Exists(directory.Root));
    }

    [Fact]
    public async Task Pipeline_RetentionMaintenanceFailurePreservesRecordAndReportsEmergencyThenRecovers()
    {
        using var directory = new DiagnosticRunTestDirectory();
        Directory.CreateDirectory(directory.Root);
        var maintenanceLock = Path.Combine(directory.Root, ".retention.lock");
        Directory.CreateDirectory(maintenanceLock);
        var writer = new EmergencyTestWriter();
        await using var pipeline = new DiagnosticPipeline(Options(directory.Root, writer));
        pipeline.Publish("Managed.Tests", new(DiagnosticLevel.Information, 55, "Proof", "retained record"));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var failed = Assert.IsType<DiagnosticRetentionStatus>(pipeline.JsonRetentionStatus);
        Assert.Equal(1, failed.Failures); Assert.NotNull(failed.LastError);
        Assert.InRange(failed.LastError.Length, 1, 512);
        var jsonHealth = Assert.Single(pipeline.Health());
        Assert.Equal("jsonl", jsonHealth.Name); Assert.Equal(1, jsonHealth.Written);
        Assert.Equal(0, jsonHealth.Failures); Assert.Equal(0, jsonHealth.Dropped);
        Assert.Equal(new[] { 55 }, await ReadEventIds(pipeline.JsonDirectory!));
        Assert.Contains(writer.Lines, line => line.Contains("Stage=Retention Sink=jsonl", StringComparison.Ordinal));
        Assert.Equal(1, pipeline.EmergencyStatus!.Written);

        Directory.Delete(maintenanceLock);
        await pipeline.DisposeAsync();
        var recovered = Assert.IsType<DiagnosticRetentionStatus>(pipeline.JsonRetentionStatus);
        Assert.Equal(0, recovered.Failures); Assert.Null(recovered.LastError);
        Assert.Equal(1, recovered.RetainedRuns); Assert.Equal(0, recovered.ActiveRuns);
        Assert.False(recovered.IsOverBudget); Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task PipelineDispose_ReportsProtectedOversizedRunBeforeClosingEmergencyOutput()
    {
        using var directory = new DiagnosticRunTestDirectory();
        var writer = new EmergencyTestWriter();
        var options = Options(directory.Root, writer);
        options.JsonRetention.MaxTotalBytes = 1;
        await using var pipeline = new DiagnosticPipeline(options);
        pipeline.Publish("Managed.Tests", new(DiagnosticLevel.Error, 71, "Proof", "must survive retention"));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pipeline.JsonRetentionStatus!.IsOverBudget);
        Assert.Empty(writer.Lines);

        await pipeline.DisposeAsync();
        var status = Assert.IsType<DiagnosticRetentionStatus>(pipeline.JsonRetentionStatus);
        Assert.True(status.IsOverBudget); Assert.Equal(1, status.RetainedRuns);
        Assert.True(status.RetainedBytes > 1); Assert.Equal(0, status.DeletedRuns);
        Assert.Equal(new[] { 71 }, await ReadEventIds(pipeline.JsonDirectory!));
        var line = Assert.Single(writer.Lines);
        Assert.Contains("Stage=Retention Sink=jsonl", line);
        Assert.Equal(1, pipeline.EmergencyStatus!.Written); Assert.Equal(0, pipeline.EmergencyStatus.Queued);
        Assert.False(writer.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppManagedDiagnostics_CopiesNestedOptionsForBothResolvers(bool microsoftDI)
    {
        using var directory = new DiagnosticRunTestDirectory();
        var writer = new EmergencyTestWriter();
        var replacementWriter = new EmergencyTestWriter();
        var firstPath = await EmitFromApp(901);
        Assert.Equal(new[] { 901 }, (await ReadEventIds(firstPath)).Where(id => id == 901));
        var secondPath = await EmitFromApp(902);

        Assert.NotEqual(firstPath, secondPath);
        Assert.False(Directory.Exists(firstPath)); Assert.True(Directory.Exists(secondPath));
        Assert.Contains(902, await ReadEventIds(secondPath));
        Assert.False(Directory.Exists(Path.Combine(directory.TempRoot, "mutated")));
        Assert.Contains(writer.Lines, line => line.Contains("Stage=Write Sink=custom-0", StringComparison.Ordinal));
        Assert.DoesNotContain(writer.Lines, line => line.Contains("private-payload", StringComparison.Ordinal)
            || line.Contains("private-error-message", StringComparison.Ordinal));
        Assert.Empty(replacementWriter.Lines); Assert.False(writer.Disposed); Assert.False(replacementWriter.Disposed);

        async Task<string> EmitFromApp(int eventId)
        {
            var sink = new EmergencyTestSink { WriteError = new IOException("private-error-message") };
            var options = Options(directory.Root, writer);
            options.Emergency.QueueCapacity = 128; options.Emergency.MaxReports = 128;
            options.SinkFactories = [new Factory<IDiagnosticSink>(() => sink)];
            var name = "ManagedApp." + Guid.NewGuid().ToString("N");
            var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
            {
                context.UseDiagnostics(options, configuration => configuration.Filters(name));
                options.JsonUseRunDirectories = false;
                options.JsonDirectory = Path.Combine(directory.TempRoot, "mutated");
                options.JsonRetention.MaxRuns = 100;
                options.JsonRetention.MaxTotalBytes = 1;
                options.JsonRetention.MaxAge = TimeSpan.FromTicks(1);
                options.Emergency.Enabled = false; options.Emergency.Writer = replacementWriter;
            }).Build();
            var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
            try
            {
                var source = app.Services.Resolve<IEventsSourceFactory>()!.Create(name);
                source.Write(DiagnosticLevel.Information, eventId, "App.ManagedProof", "private-payload");
                await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Contains(pipeline.Read(), record => record.Source == name && record.EventId == eventId);
            }
            finally { await app.DisposeAsync(); }
            var status = Assert.IsType<DiagnosticRetentionStatus>(pipeline.JsonRetentionStatus);
            Assert.Equal(1, status.RetainedRuns); Assert.False(status.IsOverBudget);
            Assert.Equal(0, status.Failures); Assert.Equal(1, sink.DisposeAttempts);
            Assert.True(pipeline.EmergencyStatus!.Written > 0);
            return Assert.IsType<string>(pipeline.JsonDirectory);
        }
    }

    private static DiagnosticPipelineOptions Options(string root, EmergencyTestWriter writer) => new()
    {
        JsonDirectory = root, JsonUseRunDirectories = true, JsonFileSizeBytes = 64 * 1024,
        JsonMaxFiles = 2, JsonRetention = new() { MaxRuns = 1 }, Emergency = new() { Writer = writer }
    };

    private static DiagnosticRecord Record(int id) => new(DateTimeOffset.UtcNow, id, Guid.NewGuid(),
        Environment.ProcessId, "Managed.Tests", DiagnosticLevel.Information, id, "Proof", "message",
        new Dictionary<string, string?> { ["proof"] = "copied" }.ToFrozenDictionary(), null, null, null, null, null, null, false);

    private static async Task<int[]> ReadEventIds(string directory)
    {
        var ids = new List<int>();
        foreach (var path in Directory.GetFiles(directory, "events-*.jsonl"))
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync() is { } line)
            {
                using var json = JsonDocument.Parse(line);
                ids.Add(json.RootElement.GetProperty("EventId").GetInt32());
            }
        }
        return ids.ToArray();
    }
}
