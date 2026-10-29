using System.Text.Json;
using TrueMoon.Aether.Editor.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorDiagnosticReportWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TrueMoon-console-report-tests-" + Guid.NewGuid().ToString("N"));
    public EditorDiagnosticReportWriterTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task PartialReport_OmitsTextPathsSecretsAndRetainsAuthoritativeCorrelation()
    {
        var record = DiagnosticConsoleFixture.Record(1, "PRIVATE_MESSAGE C:\\Users\\secret\\game.cs") with
        {
            ExceptionType = "PRIVATE_EXCEPTION", ExceptionMessage = "PRIVATE_ERROR", ExceptionStackTrace = "PRIVATE_STACK", Caller = "PRIVATE_CALLER",
            Properties = new Dictionary<string, string?> { ["SessionId"] = "session-a", ["Generation"] = "2", ["RuntimeRole"] = "Game", ["GameBuildId"] = "build-a",
                ["Nonce"] = "PRIVATE_NONCE", ["Endpoint"] = "PRIVATE_ENDPOINT", ["Environment"] = "PRIVATE_ENV", ["Path"] = "PRIVATE_PATH", ["JobId"] = "C:\\private\\job" }
        };
        var path = Path.Combine(_directory, "report.json");
        await new EditorDiagnosticReportWriter().WriteAsync(new([record], 9, 3, 4, 5, true), path);
        var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE", text); Assert.DoesNotContain("C:\\", text);
        using var json = JsonDocument.Parse(text); var root = json.RootElement;
        Assert.True(root.GetProperty("partial").GetBoolean()); Assert.Equal(3, root.GetProperty("unavailableSequences").GetInt64()); Assert.Equal(4, root.GetProperty("presentationEvicted").GetInt64());
        Assert.Equal(EditorDiagnosticReportWriter.DefaultMaximumBytes, root.GetProperty("maximumBytes").GetInt32());
        Assert.Equal(5, root.GetProperty("presentationTruncated").GetInt64());
        var exported = Assert.Single(root.GetProperty("records").EnumerateArray());
        Assert.Equal("Runtime.Started", exported.GetProperty("EventName").GetString());
        Assert.Equal("2", exported.GetProperty("Context").GetProperty("Generation").GetString());
        Assert.False(exported.GetProperty("Context").TryGetProperty("JobId", out _));
        Assert.Contains(root.GetProperty("unavailable").EnumerateArray(), item => item.GetString() == "rawStdoutStderr");
        Assert.NotEmpty(root.GetProperty("editorVersion").GetString()!); Assert.NotEmpty(root.GetProperty("os").GetString()!);
    }

    [Fact]
    public async Task SizeBound_ExportsRecentRecordsInSequenceOrderAndCountsOmissions()
    {
        var records = Enumerable.Range(1, 100).Select(i => DiagnosticConsoleFixture.Record(i)).ToArray();
        var path = Path.Combine(_directory, "bounded.json"); await new EditorDiagnosticReportWriter().WriteAsync(new(records, 100, 0, 0, 0, false), path, 4096);
        Assert.InRange(new FileInfo(path).Length, 1, 4096);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path)); var root = json.RootElement;
        var exported = root.GetProperty("records").EnumerateArray().Select(item => item.GetProperty("Sequence").GetInt64()).ToArray();
        Assert.InRange(exported.Length, 1, 99); Assert.Equal(100, exported[^1]); Assert.Equal(exported.Order(), exported);
        Assert.Equal(100 - exported.Length, root.GetProperty("omittedRecords").GetInt32()); Assert.False(root.GetProperty("collectorAvailable").GetBoolean());
    }

    [Fact]
    public async Task ExistingDestinationAndMissingDirectory_FailWithoutOverwritingOrLeavingTemporaryFiles()
    {
        var writer = new EditorDiagnosticReportWriter(); var snapshot = new EditorDiagnosticSnapshot([], 0, 0, 0, 0, false);
        var path = Path.Combine(_directory, "existing.json"); await File.WriteAllTextAsync(path, "keep");
        await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(snapshot, path)); Assert.Equal("keep", await File.ReadAllTextAsync(path));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => writer.WriteAsync(snapshot, Path.Combine(_directory, "missing", "report.json")));
        Assert.Equal(new[] { path }, Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task CancelledExport_CreatesNoFileAndOwnerCanDrainAndDisposeRepeatedly()
    {
        var path = Path.Combine(_directory, "cancelled.json"); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditorDiagnosticReportWriter().WriteAsync(new([], 0, 0, 0, 0, false), path, cancellationToken: cancelled.Token));
        Assert.Empty(Directory.GetFiles(_directory));
        using var source = new DiagnosticConsoleFixture(); var model = source.Model(); await model.StopAsync(); model.ExportPath = path; model.Export();
        await model.StopAsync(); model.Refresh(); Assert.Contains("отменён", model.ExportStatus); Assert.False(model.IsExporting);
        model.Dispose(); await model.StopAsync(); model.Dispose(); Assert.False(File.Exists(path)); Assert.Equal(0, source.Disposals);
    }
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
