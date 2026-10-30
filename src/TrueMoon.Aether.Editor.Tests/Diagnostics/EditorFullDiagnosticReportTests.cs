using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorFullDiagnosticReportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TrueMoon-full-report-" + Guid.NewGuid().ToString("N"));
    public EditorFullDiagnosticReportTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task MetadataReport_MergesArchiveWithMemoryByCollectorIdentityAndOmitsAllProducerText()
    {
        var record = DiagnosticConsoleFixture.Record(1, "PRIVATE_MESSAGE") with { ExceptionMessage = "PRIVATE_ERROR", Caller = "PRIVATE_CALLER",
            Properties = new Dictionary<string, string?> { ["SessionId"] = "session-a", ["Nonce"] = "PRIVATE_NONCE", ["Endpoint"] = "PRIVATE_ENDPOINT", ["Environment"] = "PRIVATE_ENV" } };
        var other = record with { ApplicationId = Guid.NewGuid(), MessageTemplate = "PRIVATE_ARCHIVE" };
        var archive = new Archive([record with { MessageTemplate = "old" }, other]);
        var path = Path.Combine(_directory, "metadata.json"); await new EditorDiagnosticReportWriter(archive, archive).WriteReportAsync(Snapshot(record), path, new());
        var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE", text);
        using var json = JsonDocument.Parse(text); var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32()); Assert.False(root.GetProperty("includesText").GetBoolean()); Assert.True(root.GetProperty("partial").GetBoolean());
        Assert.Equal(2, root.GetProperty("exportedRecords").GetInt32()); Assert.Equal(1, archive.Reads);
        Assert.Equal(32, root.GetProperty("collectorConfiguration").GetProperty("QueueCapacity").GetInt32());
        Assert.True(root.GetProperty("history").GetProperty("Available").GetBoolean());
        Assert.All(root.GetProperty("records").EnumerateArray(), row => Assert.Equal(JsonValueKind.Null, row.GetProperty("Text").ValueKind));
        Assert.Empty(root.GetProperty("rawStreams").EnumerateArray()); Assert.Empty(root.GetProperty("compilerDiagnostics").EnumerateArray());
        Assert.Contains(root.GetProperty("unavailable").EnumerateArray(), value => value.GetString() == "childSinkHealth");
    }

    [Fact]
    public async Task TextOptIn_ExportsRawCompilerAndExceptionTextWhileRedactingControlDataAndHostLiterals()
    {
        var record = DiagnosticConsoleFixture.Record(1, "PRIVATE_MESSAGE\nnonce=PRIVATE_NONCE\nendpoint=PRIVATE_ENDPOINT\nsecret-value") with
        { ExceptionType = "Failure", ExceptionMessage = "PRIVATE_ERROR", ExceptionStackTrace = "at E:/private/game.cs:line 9", Caller = "PRIVATE_CALLER",
            Properties = new Dictionary<string, string?> { ["SessionId"] = "session-a", ["Environment"] = "PRIVATE_ENV" } };
        var process = EditorDiagnosticContextTests.Context.Process("p", "RAW_DATA\nbootstrap=PRIVATE_BOOTSTRAP", "STDERR_DATA") with
        {
            CompilerDiagnostics = new(GameBuildDiagnosticState.Available, [new("CS1", GameBuildDiagnosticSeverity.Error, "COMPILER_DATA", "E:/private/game.cs", 9, 2)])
        };
        var snapshot = Snapshot(record) with { Processes = new(true, true, [process]) };
        var path = Path.Combine(_directory, "text.json"); await new EditorDiagnosticReportWriter().WriteReportAsync(snapshot, path,
            new() { IncludeText = true, RedactedValues = ["secret-value"] });
        var text = await File.ReadAllTextAsync(path); Assert.Contains("PRIVATE_MESSAGE", text); Assert.Contains("PRIVATE_ERROR", text); Assert.Contains("RAW_DATA", text); Assert.Contains("COMPILER_DATA", text);
        foreach (var secret in new[] { "PRIVATE_NONCE", "PRIVATE_ENDPOINT", "PRIVATE_BOOTSTRAP", "PRIVATE_ENV", "PRIVATE_CALLER", "secret-value" }) Assert.DoesNotContain(secret, text);
        using var report = JsonDocument.Parse(text); Assert.True(report.RootElement.GetProperty("includesText").GetBoolean());
        Assert.Equal(2, report.RootElement.GetProperty("exportedRawStreams").GetInt32()); Assert.Equal(1, report.RootElement.GetProperty("exportedCompilerDiagnostics").GetInt32());
        Assert.True(report.RootElement.GetProperty("textFieldsRedacted").GetInt32() > 0);
        Assert.Equal("E:/private/game.cs", Assert.Single(report.RootElement.GetProperty("compilerDiagnostics").EnumerateArray()).GetProperty("FilePath").GetString());
    }

    [Fact]
    public async Task TextBoundary_RedactsCrossingLiteralBeforeClippingAndPreservesSurrogatePairs()
    {
        var records = new[] { DiagnosticConsoleFixture.Record(1, new string('p', 127) + "SECRET-tail"), DiagnosticConsoleFixture.Record(2, new string('x', 127) + "😀") };
        var path = Path.Combine(_directory, "boundary.json"); await new EditorDiagnosticReportWriter().WriteReportAsync(new(records, 2, 0, 0, 0, true), path,
            new() { IncludeText = true, IncludePersistentHistory = false, MaximumTextCharacters = 128, RedactedValues = ["SECRET"] });
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(path)); var rows = report.RootElement.GetProperty("records").EnumerateArray().ToArray();
        Assert.Equal(new string('p', 127) + "[", rows[0].GetProperty("Text").GetProperty("Message").GetString());
        Assert.Equal(new string('x', 127), rows[1].GetProperty("Text").GetProperty("Message").GetString());
        Assert.Equal(2, report.RootElement.GetProperty("textFieldsTruncated").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SmallBudget_ProducesValidBoundedPartialJsonAndCountsDroppedRecords(bool text)
    {
        var records = Enumerable.Range(1, 100).Select(i => DiagnosticConsoleFixture.Record(i, new string('m', 4000))).ToArray(); var archive = new Archive([]);
        var path = Path.Combine(_directory, "bounded-" + text + ".json"); await new EditorDiagnosticReportWriter(archive, archive).WriteReportAsync(new(records, 100, 0, 0, 0, true), path,
            new() { MaximumBytes = 4096, IncludeText = text });
        Assert.InRange(new FileInfo(path).Length, 1, 4096); using var report = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.True(report.RootElement.GetProperty("partial").GetBoolean());
        var count = report.RootElement.GetProperty("exportedRecords").GetInt32(); Assert.Equal(100 - count, report.RootElement.GetProperty("omittedRecords").GetInt32());
        Assert.True(report.RootElement.GetProperty("omittedRecords").GetInt32() > 0);
    }

    [Fact]
    public async Task HistoryFailureAndDisabledHistoryAreExplicitWithoutPreventingMetadataReport()
    {
        var archive = new Archive([]) { Failure = new IOException("PRIVATE_PATH") }; var writer = new EditorDiagnosticReportWriter(archive, archive);
        var path = Path.Combine(_directory, "failure.json"); await writer.WriteReportAsync(Snapshot(), path, new());
        var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE_PATH", text); using var report = JsonDocument.Parse(text);
        Assert.Equal("IOException", report.RootElement.GetProperty("sourceFailures").GetProperty("history").GetString());
        Assert.Contains(report.RootElement.GetProperty("unavailable").EnumerateArray(), value => value.GetString() == "persistentHistory");
        await writer.WriteReportAsync(Snapshot(), Path.Combine(_directory, "disabled.json"), new() { IncludePersistentHistory = false }); Assert.Equal(1, archive.Reads);
    }

    [Fact]
    public async Task NewReport_NoOverwriteWriteFailureCancellationAndDeadlineLeaveNoTemporaryFiles()
    {
        var path = Path.Combine(_directory, "existing.json"); await File.WriteAllTextAsync(path, "preserve"); var writer = new EditorDiagnosticReportWriter();
        await Assert.ThrowsAsync<IOException>(() => writer.WriteReportAsync(Snapshot(), path, new())); Assert.Equal("preserve", await File.ReadAllTextAsync(path));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => writer.WriteReportAsync(Snapshot(), Path.Combine(_directory, "absent", "report.json"), new()));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteReportAsync(Snapshot(), Path.Combine(_directory, "cancelled.json"), new(), cancelled.Token));
        var held = new Archive([]) { Hold = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditorDiagnosticReportWriter(held, held).WriteReportAsync(Snapshot(), Path.Combine(_directory, "deadline.json"), new() { TimeBudget = TimeSpan.FromMilliseconds(20) }));
        Assert.Equal(new[] { path }, Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task UserCancellation_DrainsBeforeRetryAndNeverChangesHistoryOrFrozenChoices()
    {
        var archive = new Archive([]) { Hold = true }; using var source = new DiagnosticConsoleFixture(); source.Add(DiagnosticConsoleFixture.Record(1));
        using var model = new EditorDiagnosticsViewModel(source, new(), new(archive, archive)); model.Refresh(); model.ExportPath = Path.Combine(_directory, "cancelled.json");
        model.Export(); await archive.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(model.IsExporting); Assert.True(model.CanCancelExport); Assert.False(model.CanExport);
        Assert.Throws<InvalidOperationException>(() => model.ExportIncludesText = true); model.Export(); Assert.Equal(1, archive.Reads);
        model.CancelExport(); Assert.False(model.CanCancelExport); await ObserveDone(model); Assert.Contains("отменён", model.ExportStatus);
        Assert.False(File.Exists(model.ExportPath)); Assert.Single(source.Records); Assert.Single(model.Entries);
        archive.Hold = false; model.ExportPath = Path.Combine(_directory, "retry.json"); model.ExportIncludesText = true; model.Export(); await ObserveDone(model);
        Assert.True(File.Exists(model.ExportPath)); Assert.Contains("с выбранным текстом", model.ExportStatus); Assert.Equal(0, source.Disposals);
    }

    [Fact]
    public void View_ExposesExplicitTextHistoryChoicesCancellationAndActualPresentationValues()
    {
        var archive = new Archive([]) { Hold = true }; using var model = new EditorDiagnosticsViewModel(null, new(), new(archive, archive)) { IsOpen = true, FollowTail = false, ExportPath = Path.Combine(_directory, "ui.json") };
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle()); using var ui = new UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360)); ui.Update();
        Assert.Equal(1280, model.Snapshot().Presentation!.Width); Assert.False(string.IsNullOrEmpty(model.Snapshot().Presentation!.Backend));
        Assert.False(view.IncludeText.IsChecked); Assert.True(view.IncludeHistory.IsChecked); Assert.False(view.CancelExport.IsEnabled);
        ui.Focus(view.IncludeText); Key(); ui.Update(); Assert.True(model.ExportIncludesText); Assert.Contains("с текстом", view.Export.Value); Assert.Contains("личные пути", view.ExportPrivacy.Value);
        ui.Resize(new(1100, 340, 1.25f)); ui.Update();
        ui.Focus(view.Export); Key(); archive.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(); ui.Update();
        Assert.True(view.CancelExport.IsEnabled); Assert.False(view.IncludeText.IsEnabled); Assert.False(view.ExportPath.IsEnabled);
        ui.Focus(view.CancelExport); Key(); model.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(); model.Refresh(); ui.Update();
        Assert.Contains("отменён", view.ExportStatus.Value); Assert.False(File.Exists(model.ExportPath)); Assert.Equal(1100, model.Snapshot().Presentation!.Width);
        Assert.Equal(340, model.Snapshot().Presentation!.Height); Assert.Equal(1.25f, model.Snapshot().Presentation!.Scale);
        void Key() { ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter)); }
    }

    private static EditorDiagnosticSnapshot Snapshot(params DiagnosticRecord[] records) => new(records, records.Length, 0, 0, 0, true)
    { Presentation = new("SkiaRasterSurface", 1280, 360, 1) };
    private static async Task ObserveDone(EditorDiagnosticsViewModel model)
    {
        var timer = Stopwatch.StartNew(); while (model.IsExporting && timer.Elapsed < TimeSpan.FromSeconds(3)) { await Task.Delay(1); model.Refresh(); }
        model.Refresh(); Assert.False(model.IsExporting);
    }
    public void Dispose() => Directory.Delete(_directory, true);
    private sealed class Archive(IReadOnlyList<DiagnosticRecord> records) : IDiagnosticArchiveReader, IDiagnosticConfigurationReader
    {
        internal int Reads; internal bool Hold; internal Exception? Failure;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DiagnosticHistorySnapshot> ReadHistoryAsync(DiagnosticHistoryReadOptions options, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); Entered.TrySetResult(); if (Failure != null) throw Failure; if (Hold) await _release.Task.WaitAsync(cancellationToken); return new(true, records); }
        public DiagnosticCollectorConfiguration ReadConfiguration() => new() { QueueCapacity = 32, MemoryCapacity = 512, JsonEnabled = true };
    }
}
