using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorDiagnosticContextTests
{
    [Fact]
    public void RawSnapshots_AreBoundedAndSeparateStreamIdentityFromStructuredSeverity()
    {
        using var source = new Context(); using var model = new EditorDiagnosticsViewModel(null, new() { MaximumProcesses = 2, MaximumRawCharacters = 8 }, new(), source, source);
        source.Batch = new(true, true, Enumerable.Range(1, 10).Select(i => Context.Process("p" + i, "stdout-" + i + "-tail", "stderr-" + i + "-tail")).ToArray());
        model.Mode = EditorDiagnosticMode.StandardOutput; model.Refresh(); Assert.Equal(2, source.LastCount); Assert.Equal(8, source.LastCharacters);
        Assert.Equal(2, model.Rows.Count); Assert.Empty(model.Entries); Assert.All(model.Processes!.Processes, process => Assert.InRange(process.StandardOutput!.Output.Text.Length, 0, 8));
        model.MinimumLevel = DiagnosticLevel.Critical; model.EventName = "not-a-structured-event"; Assert.Equal(2, model.Rows.Count);
        model.Select("p1"); Assert.Contains("RAW STDOUT", model.Details); Assert.DoesNotContain("stderr-1", model.Details);
        Assert.Contains("display omitted", model.Details); Assert.DoesNotContain("Critical", model.Details); Assert.False(model.CanClear);
        model.Search = "stderr"; Assert.Empty(model.Rows); Assert.Equal("p1", model.SelectedId);
        model.Mode = EditorDiagnosticMode.StandardError; Assert.Equal(2, model.Rows.Count); model.Select("p1");
        Assert.All(model.Rows, row => Assert.False(row.Warning));
        Assert.Contains("RAW STDERR", model.Details); Assert.Contains("severity не определяется", model.Details); Assert.DoesNotContain("RAW STDOUT", model.Details);
        model.Clear(); Assert.Equal(2, model.Rows.Count); // Clear belongs only to structured history.
    }

    [Theory]
    [InlineData("Session", "00000000")]
    [InlineData("Generation", "1")]
    [InlineData("RuntimeRole", "Edit")]
    [InlineData("Source", "Absent")]
    public void RawFilters_KeepCorrelationExactAndNeverMixGeneration(string property, string partial)
    {
        using var source = new Context(); using var model = new EditorDiagnosticsViewModel(null, new(), new(), source, source);
        model.Mode = EditorDiagnosticMode.StandardOutput; model.Refresh(); Assert.Single(model.Rows);
        typeof(EditorDiagnosticsViewModel).GetProperty(property)!.SetValue(model, partial); Assert.Empty(model.Rows);
        typeof(EditorDiagnosticsViewModel).GetProperty(property)!.SetValue(model, ""); Assert.Single(model.Rows);
        model.Session = Context.Session.ToString("D"); model.Generation = "10"; model.RuntimeRole = "EditPreview"; model.Source = "Runtime"; Assert.Single(model.Rows);
    }

    [Fact]
    public void InspectedRawTail_IsFrozenThroughPollingFilterAndThemeUntilExplicitRefreshOrEviction()
    {
        using var source = new Context(); using var model = new EditorDiagnosticsViewModel(null, new(), new(), source, source);
        model.IsOpen = true; model.Mode = EditorDiagnosticMode.StandardOutput; model.Refresh();
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        using var ui = new UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360)); ui.Update();
        view.Events.Select("p1"); ui.Update(); view.Details.Select(0, 12); ui.Focus(view.Search);
        var inspected = model.Details; var selection = view.Details.SelectedText;
        source.Batch = new(true, true, [Context.Process("p1", "NEW raw data", "stderr") with { Completed = true, ResourcesReleased = true }]);
        model.Refresh(); ui.Update(); Assert.Equal(inspected, model.Details); Assert.Equal(selection, view.Details.SelectedText); Assert.True(view.Search.IsFocused);
        model.Search = "absent"; ui.Update(); Assert.Empty(view.Events.Items); Assert.Equal("p1", model.SelectedId);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Equal(selection, view.Details.SelectedText);
        model.Search = ""; model.RefreshDetails(); ui.Update(); Assert.Contains("NEW raw data", model.Details); Assert.Contains("Completed True", model.Details);
        source.Batch = new(true, true, []); model.Refresh(); ui.Update(); Assert.Null(model.SelectedId); Assert.DoesNotContain("NEW raw data", model.Details);
        Assert.Equal(0, source.Disposals);
    }

    [Fact]
    public void HealthMode_DistinguishesMissingProvidersFromZeroAndDisplaysIndependentLossCounters()
    {
        using var missing = new EditorDiagnosticsViewModel(null, new(), new()); missing.Mode = EditorDiagnosticMode.Health; missing.Refresh();
        Assert.Contains(missing.Rows, row => row.Label.Contains("недоступно")); Assert.Null(missing.CollectorHealth); Assert.Null(missing.Processes);
        using var source = new Context(); using var model = new EditorDiagnosticsViewModel(null, new(), new(), source, source);
        model.Mode = EditorDiagnosticMode.Health; model.Refresh(); model.Select("sink:0");
        Assert.Contains("Dropped = 5", model.Details); Assert.Contains("Failures = 2", model.Details); Assert.Contains("disk durability", model.Details);
        Assert.Equal(7, model.CollectorHealth!.MemoryEvicted); Assert.Equal(0, model.UnavailableSequences);
        model.Select("process:p1"); Assert.Contains("Lost = 3", model.Details); Assert.Contains("Unconfirmed = 4", model.Details);
        Assert.Contains("capture evicted", model.Details); Assert.Contains("file dropped", model.Details);
        source.Health = source.Health with { MemoryEvicted = 99 }; model.Refresh(); Assert.DoesNotContain("99", model.Details);
        model.Select("collector"); Assert.Contains("Memory evicted 99", model.Details);
        model.IsOpen = true;
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle()); using var ui = new UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360)); ui.Update();
        Assert.False(view.Search.IsEnabled); Assert.False(view.EventName.IsEnabled); Assert.False(view.Level.IsEnabled); Assert.False(view.Clear.IsEnabled);
        model.Mode = EditorDiagnosticMode.StandardOutput; ui.Update(); Assert.True(view.Search.IsEnabled); Assert.False(view.EventName.IsEnabled);
        model.Mode = EditorDiagnosticMode.Structured; ui.Update(); Assert.True(view.EventName.IsEnabled); Assert.True(view.Clear.IsEnabled);
    }

    [Fact]
    public void UnchangedSnapshots_DoNotKeepUiDirtyOnEveryPoll()
    {
        using var source = new Context(); using var model = new EditorDiagnosticsViewModel(null, new(), new(), source, source);
        model.Mode = EditorDiagnosticMode.StandardOutput; model.IsOpen = true; model.FollowTail = false; model.Refresh();
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle()); using var ui = new UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360));
        ui.Update(); ui.Update(); var rows = model.Rows;
        for (var i = 0; i < 5; i++) model.Refresh();
        Assert.Same(rows, model.Rows); Assert.False(ui.Update()); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void ModeCycle_RestoresStructuredSelectionAndEnabledFiltersAfterRawHealthInspection()
    {
        using var history = new DiagnosticConsoleFixture(); history.Add(DiagnosticConsoleFixture.Record(1));
        using var source = new Context(); using var model = new EditorDiagnosticsViewModel(history, new(), new(), source, source);
        model.IsOpen = true; model.FollowTail = false; model.Refresh();
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle()); using var ui = new UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360));
        ui.Update(); view.Events.Select("1"); ui.Update(); model.Mode = EditorDiagnosticMode.StandardOutput; ui.Update(); view.Events.Select("p1"); ui.Update();
        model.Mode = EditorDiagnosticMode.Health; ui.Update(); view.Events.Select("process:p1"); ui.Update(); model.Mode = EditorDiagnosticMode.Structured; ui.Update();
        Assert.Equal("1", model.SelectedId); Assert.Equal("1", view.Events.SelectedId); Assert.True(view.Source.IsEnabled); Assert.True(view.Session.IsEnabled);
        model.Source = "runtime"; model.Session = "session-a"; ui.Update(); Assert.Single(view.Events.Items); Assert.Equal("1", view.Events.SelectedId);
    }

    [Fact]
    public async Task ContextReport_ExportsNumericHealthAndCorrelationsWhileOmittingAllRawAndErrorText()
    {
        var root = Path.Combine(Path.GetTempPath(), "TrueMoon-context-report-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var source = new Context(); using var model = new EditorDiagnosticsViewModel(null, new(), new(), source, source); model.Refresh();
            var path = Path.Combine(root, "report.json"); await new EditorDiagnosticReportWriter().WriteAsync(model.Snapshot(), path);
            var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE", text); Assert.DoesNotContain("stdout-private", text); Assert.DoesNotContain("stderr-private", text);
            using var report = JsonDocument.Parse(text); var json = report.RootElement;
            Assert.Equal(7, json.GetProperty("coreHealth").GetProperty("MemoryEvicted").GetInt64());
            var process = Assert.Single(json.GetProperty("processes").EnumerateArray()); Assert.Equal(10, process.GetProperty("Generation").GetInt32());
            Assert.Equal(5, process.GetProperty("StandardOutput").GetProperty("FileBytesDropped").GetInt64());
            Assert.Equal(4, process.GetProperty("Delivery").GetProperty("Unconfirmed").GetInt64());
            Assert.DoesNotContain(json.GetProperty("unavailable").EnumerateArray(), value => value.GetString() == "sinkHealth");
            Assert.Contains(json.GetProperty("omittedByPolicy").EnumerateArray(), value => value.GetString() == "rawText");
            var limited = Path.Combine(root, "bounded.json"); await new EditorDiagnosticReportWriter().WriteAsync(model.Snapshot(), limited, 4096);
            Assert.InRange(new FileInfo(limited).Length, 1, 4096); using var bounded = JsonDocument.Parse(await File.ReadAllTextAsync(limited)); Assert.True(bounded.RootElement.GetProperty("partial").GetBoolean());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ContextOverExportBudget_ReportsOmissionsAndStillPublishesValidBoundedJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-context-budget-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            using var source = new Context(); source.Health = source.Health with
            { Sinks = Enumerable.Range(1, 66).Select(i => new DiagnosticSinkHealth(new string('n', 256), i, 0, 0, 1, 1, 10, "PRIVATE_ERROR")).ToArray() };
            source.Batch = new(true, true, Enumerable.Range(1, 64).Select(i => Context.Process("p" + i, "PRIVATE_RAW", "PRIVATE_RAW")).ToArray());
            using var model = new EditorDiagnosticsViewModel(null, new() { MaximumProcesses = 64 }, new(), source, source); model.Refresh();
            var path = Path.Combine(directory, "report.json"); await new EditorDiagnosticReportWriter().WriteAsync(model.Snapshot(), path, 4096);
            Assert.InRange(new FileInfo(path).Length, 1, 4096); var text = await File.ReadAllTextAsync(path); Assert.DoesNotContain("PRIVATE", text);
            using var json = JsonDocument.Parse(text); Assert.True(json.RootElement.GetProperty("coreHealthOmittedByBudget").GetBoolean());
            Assert.True(json.RootElement.GetProperty("processMetadataOmitted").GetInt32() > 0); Assert.True(json.RootElement.GetProperty("partial").GetBoolean());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("MaximumProcesses", 0)]
    [InlineData("MaximumProcesses", 65)]
    [InlineData("MaximumRawCharacters", -1)]
    [InlineData("MaximumRawCharacters", 16385)]
    public void InvalidContextBudgets_FailBeforeReadingSources(string property, int value)
    {
        var options = new EditorDiagnosticsOptions(); typeof(EditorDiagnosticsOptions).GetProperty(property)!.SetValue(options, value);
        using var source = new Context(); Assert.Throws<ArgumentOutOfRangeException>(() => new EditorDiagnosticsViewModel(null, options, new(), source, source)); Assert.Equal(0, source.Reads);
    }

    internal sealed class Context : IDiagnosticHealthReader, IProcessDiagnosticsSource, IDisposable
    {
        internal static readonly Guid Session = Guid.Parse("5c82d5ab-3dfe-4cf0-8dc9-c1e262498c52");
        internal int Reads, LastCount, LastCharacters, Disposals;
        internal ProcessDiagnosticBatch Batch = new(true, true, [Process("p1", "stdout-private", "stderr-private")]);
        internal DiagnosticHealthSnapshot Health = new(Guid.NewGuid(), false, 20, 4, 4096, 7, 2, 1,
            [new("jsonl", 10, 3, 5, 2, 2, 200, "PRIVATE_SINK_ERROR")]);
        public DiagnosticHealthSnapshot ReadHealth() => Health;
        public ProcessDiagnosticBatch Read(int maximumCount, int maximumOutputCharacters) { Reads++; LastCount = maximumCount; LastCharacters = maximumOutputCharacters; return Batch; }
        public void Dispose() => Disposals++;
        internal static ProcessDiagnosticSnapshot Process(string id, string stdout, string stderr) => new(id, "Runtime", DateTimeOffset.UnixEpoch, 1234,
            Session, 10, "EditPreview", "build-a", null, null, false, false, Stream(stdout), Stream(stderr),
            new(true, false, false, false, 10, 2, 3, 4, 5, 1, 1, 0, 1, 2, "PRIVATE_DELIVERY_ERROR"));
        private static ProcessStreamDiagnostic Stream(string text) => new(new(text, 100, 100 - text.Length, 20, 5, "PRIVATE_FILE_ERROR"), false, false, null, null);
    }
}
