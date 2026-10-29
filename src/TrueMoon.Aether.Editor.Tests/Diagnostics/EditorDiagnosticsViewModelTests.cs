using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorDiagnosticsViewModelTests
{
    [Fact]
    public void Refresh_ReadsOneBatchAndEvictsOldestAtRecordLimit()
    {
        using var source = new DiagnosticConsoleFixture(); source.Add(Enumerable.Range(1, 6).Select(i => DiagnosticConsoleFixture.Record(i)).ToArray());
        using var model = source.Model(new() { MaximumRecords = 3, RecordsPerRefresh = 2 });
        model.Refresh(); Assert.Equal(1, source.Reads); Assert.Equal(2, source.LastMaximum); Assert.Equal(2, model.Cursor); Assert.Equal(2, model.RetainedCount);
        model.Refresh(); Assert.Equal(2, source.Reads); Assert.Equal(new long[] { 2, 3, 4 }, model.Snapshot().Records.Select(record => record.Sequence));
        Assert.Equal(1, model.PresentationEvicted); Assert.Equal(0, model.UnavailableSequences);
        model.Refresh(); Assert.Equal(new long[] { 4, 5, 6 }, model.Snapshot().Records.Select(record => record.Sequence)); Assert.Equal(3, model.PresentationEvicted);
    }

    [Fact]
    public void ByteBudget_ExactLimitFitsThenNextRecordEvictsOldest()
    {
        using var source = new DiagnosticConsoleFixture();
        DiagnosticRecord Event(long sequence) => DiagnosticConsoleFixture.Record(sequence, new string('m', 382)) with
        { Source = "R", EventName = "N", Properties = new Dictionary<string, string?>() };
        source.Add(Event(1), Event(2), Event(3), Event(4));
        using var model = source.Model(new() { MaximumRetainedBytes = 4096, MaximumRecordCharacters = 512 }); model.Refresh();
        Assert.Equal(4096, model.RetainedBytes); Assert.Equal(4, model.RetainedCount); Assert.Equal(0, model.PresentationEvicted);
        source.Add(Event(5)); model.Refresh();
        Assert.Equal(4096, model.RetainedBytes); Assert.Equal(new long[] { 2, 3, 4, 5 }, model.Snapshot().Records.Select(record => record.Sequence)); Assert.Equal(1, model.PresentationEvicted);
    }

    [Fact]
    public void OversizedRecord_IsBoundedAndCannotExceedRetentionBudget()
    {
        using var source = new DiagnosticConsoleFixture();
        source.Add(DiagnosticConsoleFixture.Record(1, new string('m', 100_000)) with { ExceptionType = "Failure", ExceptionStackTrace = new string('s', 100_000) });
        using var model = source.Model(new() { MaximumRecordCharacters = 512 }); model.Refresh();
        var record = Assert.Single(model.Snapshot().Records); Assert.True(record.IsTruncated); Assert.InRange(model.RetainedBytes, 1, 4096);
        Assert.True(record.MessageTemplate.Length <= 512); Assert.Equal(1, model.PresentationTruncated);
        using var small = source.Model(new() { MaximumRetainedBytes = 4096 }); small.Refresh();
        Assert.Equal(0, small.RetainedCount); Assert.Equal(0, small.RetainedBytes); Assert.Equal(1, small.PresentationEvicted);
    }

    [Fact]
    public void CursorGaps_AndClearAreIndependentOfCollectorHistory()
    {
        using var source = new DiagnosticConsoleFixture(); source.Add(DiagnosticConsoleFixture.Record(5), DiagnosticConsoleFixture.Record(7)); source.LatestSequence = 9;
        using var model = source.Model(); model.Refresh(); Assert.Equal(5, model.UnavailableSequences); Assert.Equal(7, model.Cursor);
        model.Refresh(); Assert.Equal(7, model.UnavailableSequences); Assert.Equal(9, model.Cursor);
        model.Select("5"); model.Clear(); Assert.Empty(model.Entries); Assert.Equal(0, model.UnavailableSequences); Assert.Equal(2, source.Records.Count);
        model.Refresh(); Assert.Empty(model.Entries);
        source.Add(DiagnosticConsoleFixture.Record(10)); model.Refresh(); Assert.Equal(10, Assert.Single(model.Entries).Record.Sequence); Assert.Equal(0, model.UnavailableSequences);
    }

    [Theory]
    [InlineData("Source")]
    [InlineData("EventName")]
    [InlineData("Session")]
    [InlineData("Generation")]
    [InlineData("RuntimeRole")]
    [InlineData("Search")]
    [InlineData("MinimumLevel")]
    public void Filters_RequireEverySelectedCondition(string rejecting)
    {
        using var source = new DiagnosticConsoleFixture();
        source.Add(DiagnosticConsoleFixture.Record(1, "first", "1"), DiagnosticConsoleFixture.Record(2, "needle", "2"),
            DiagnosticConsoleFixture.Record(3, "needle", "2", "other"), DiagnosticConsoleFixture.Record(4, "needle", "2", level: DiagnosticLevel.Information));
        using var model = source.Model(); model.Refresh();
        model.Source = "runtime"; model.EventName = "started"; model.Session = "session-a"; model.Generation = "2"; model.RuntimeRole = "game";
        model.Search = "needle"; model.MinimumLevel = DiagnosticLevel.Warning;
        Assert.Equal(2, Assert.Single(model.Entries).Record.Sequence);
        if (rejecting == "MinimumLevel") model.MinimumLevel = DiagnosticLevel.Critical;
        else typeof(EditorDiagnosticsViewModel).GetProperty(rejecting)!.SetValue(model, "absent");
        Assert.Empty(model.Entries); Assert.Equal(4, model.RetainedCount);
    }

    [Fact]
    public void Grouping_CombinesEquivalentEventsAndSeparatesGenerationBuildAndException()
    {
        using var source = new DiagnosticConsoleFixture();
        var otherBuild = DiagnosticConsoleFixture.Record(4) with { Properties = new Dictionary<string, string?> { ["SessionId"] = "session-a", ["Generation"] = "1", ["RuntimeRole"] = "Game", ["GameBuildId"] = "build-b" } };
        source.Add(DiagnosticConsoleFixture.Record(1), DiagnosticConsoleFixture.Record(2), DiagnosticConsoleFixture.Record(3, generation: "2"),
            otherBuild, DiagnosticConsoleFixture.Record(5) with { ExceptionType = "System.IO.IOException", ExceptionMessage = "disk" });
        using var model = source.Model(); model.Refresh(); Assert.Equal(new[] { 2, 1, 1, 1 }, model.Entries.Select(entry => entry.Count));
        Assert.StartsWith("×2 · ", model.Entries[0].Label); model.GroupRepeats = false; Assert.Equal(5, model.Entries.Count);
        model.Search = "disk"; Assert.Equal(5, Assert.Single(model.Entries).Record.Sequence);
    }

    [Theory]
    [InlineData("Session", "session")]
    [InlineData("Generation", "2")]
    [InlineData("RuntimeRole", "Gam")]
    public void IdentityFilters_RejectSubstringMatches(string property, string partial)
    {
        using var source = new DiagnosticConsoleFixture(); source.Add(DiagnosticConsoleFixture.Record(1, generation: "20"));
        using var model = source.Model(); model.Refresh(); Assert.Single(model.Entries);
        typeof(EditorDiagnosticsViewModel).GetProperty(property)!.SetValue(model, partial); Assert.Empty(model.Entries);
    }

    [Theory]
    [InlineData("Session")]
    [InlineData("Process")]
    [InlineData("OriginApplication")]
    public void Grouping_KeepsIndependentSessionProcessAndOriginIdentity(string identity)
    {
        using var source = new DiagnosticConsoleFixture(); var foreign = DiagnosticConsoleFixture.Record(3);
        foreign = identity switch
        {
            "Session" => DiagnosticConsoleFixture.Record(3, session: "session-b"),
            "Process" => foreign with { ProcessId = 456 },
            _ => foreign with { Origin = new(Guid.Parse("d1be8e9b-efbc-49d9-97df-dd153bd58e20"), 1) }
        };
        source.Add(DiagnosticConsoleFixture.Record(1), DiagnosticConsoleFixture.Record(2), foreign);
        using var model = source.Model(); model.Refresh(); Assert.Equal(new[] { 2, 1 }, model.Entries.Select(entry => entry.Count));
        Assert.Equal(3, model.Entries[1].Record.Sequence);
    }

    [Fact]
    public void SelectionAndFrozenSnapshot_SurviveFilterAndCallerMutationUntilEviction()
    {
        using var source = new DiagnosticConsoleFixture(); var original = DiagnosticConsoleFixture.Record(1);
        source.Add(original); using var model = source.Model(new() { MaximumRecords = 1 }); model.Refresh(); model.Select("1");
        var snapshot = model.Snapshot(); var details = model.Details; model.Search = "absent";
        Assert.Equal(1, snapshot.PresentationLimits!.MaximumRecords);
        Assert.Empty(model.Entries); Assert.Equal("1", model.SelectedId); Assert.Equal(details, model.Details);
        ((Dictionary<string, string?>)original.Properties)["Generation"] = "mutated";
        Assert.Equal("1", snapshot.Records[0].Properties["Generation"]);
        model.Search = ""; Assert.Equal("1", Assert.Single(model.Entries).Id);
        source.Add(DiagnosticConsoleFixture.Record(2)); model.Refresh(); Assert.Null(model.SelectedId); Assert.DoesNotContain("#1", model.Details);
        Assert.Equal(1, Assert.Single(snapshot.Records).Sequence);
    }

    [Fact]
    public void MissingCollectorAndDispose_AreExplicitAndDoNotOwnBorrowedReader()
    {
        using var missing = new EditorDiagnosticsViewModel(null, new(), new()); missing.Refresh();
        Assert.False(missing.Snapshot().CollectorAvailable); Assert.Contains("недоступна", missing.Summary);
        using var source = new DiagnosticConsoleFixture(); var model = source.Model(); model.Search = new string('a', 1000); Assert.Equal(256, model.Search.Length);
        model.Dispose(); model.Dispose(); Assert.Equal(0, source.Disposals); Assert.Throws<ObjectDisposedException>(model.Refresh);
    }

    [Fact]
    public void MalformedUtf16AndWindowsNewlines_AreSafeForBoundRichTextAndLabels()
    {
        using var source = new DiagnosticConsoleFixture();
        source.Add(DiagnosticConsoleFixture.Record(1, "line1\r\nline2\ud800") with { ExceptionType = "Failure", ExceptionStackTrace = "stack\r\nnext" });
        using var model = source.Model(); model.Refresh(); model.Select("1");
        Assert.DoesNotContain('\r', model.Details); Assert.Contains("line1\nline2\uFFFD", model.Details);
        Assert.DoesNotContain('\n', Assert.Single(model.Entries).Label); Assert.Equal(1, model.PresentationTruncated);
        model.IsOpen = true;
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        using var ui = new TrueMoon.Alloy.Runtime.UiSession(view, new EditorDiagnosticsViewTests.Surface(), new(1280, 360)); ui.Update();
        Assert.Equal(model.Details, view.Details.Text);
    }

    [Fact]
    public void InvalidExportPath_IsReportedWithoutThrowingThroughUiCommand()
    {
        using var source = new DiagnosticConsoleFixture(); using var model = source.Model(); model.ExportPath = "";
        model.Export(); Assert.False(model.IsExporting); Assert.True(model.CanExport); Assert.Contains("ArgumentException", model.ExportStatus);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(5001)]
    public void InvalidRefreshInterval_IsRejected(int milliseconds)
    {
        using var source = new DiagnosticConsoleFixture();
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Model(new() { RefreshInterval = TimeSpan.FromMilliseconds(milliseconds) }));
        Assert.Equal(0, source.Reads);
    }

    [Theory]
    [InlineData("MaximumRecords", 0)]
    [InlineData("MaximumRecords", 1025)]
    [InlineData("MaximumRetainedBytes", 4095)]
    [InlineData("MaximumRetainedBytes", 16777217)]
    [InlineData("MaximumRecordCharacters", 511)]
    [InlineData("MaximumRecordCharacters", 16385)]
    [InlineData("RecordsPerRefresh", 0)]
    [InlineData("RecordsPerRefresh", 129)]
    public void InvalidBudget_IsRejectedBeforeReading(string property, int value)
    {
        var options = new EditorDiagnosticsOptions(); typeof(EditorDiagnosticsOptions).GetProperty(property)!.SetValue(options, value);
        using var source = new DiagnosticConsoleFixture(); Assert.Throws<ArgumentOutOfRangeException>(() => source.Model(options)); Assert.Equal(0, source.Reads);
    }
}
