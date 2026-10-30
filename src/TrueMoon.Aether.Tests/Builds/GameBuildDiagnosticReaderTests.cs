using System.Text;
using System.Text.Json;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildDiagnosticReaderTests
{
    [Fact]
    public void Sarif_UsesProducerSeverityAndDecodesExactSourceCoordinates()
    {
        var path = Path.Combine(Path.GetTempPath(), "source folder", "mob.cs");
        var batch = Parse(Result("CS1002", "error", "Expected ;", new Uri(path).AbsoluteUri, 17, 9),
            Result("CS0168", "warning", "Unused variable"), Result("INFO", "note", "Advice"), Result("HIDDEN", "none", "Hidden"));
        Assert.Equal(GameBuildDiagnosticState.Available, batch.State); Assert.False(batch.IsTruncated); Assert.Equal(0, batch.Omitted);
        var first = batch.Entries[0]; Assert.Equal("CS1002", first.Code); Assert.Equal("Expected ;", first.Message);
        Assert.Equal(GameBuildDiagnosticSeverity.Error, first.Severity); Assert.Equal(path, first.FilePath); Assert.Equal(17, first.Line); Assert.Equal(9, first.Column);
        Assert.Equal(new[] { GameBuildDiagnosticSeverity.Error, GameBuildDiagnosticSeverity.Warning, GameBuildDiagnosticSeverity.Information, GameBuildDiagnosticSeverity.Hidden }, batch.Entries.Select(item => item.Severity));
        Assert.Null(batch.Entries[1].FilePath); Assert.Null(batch.Entries[1].Line);
    }

    [Theory]
    [InlineData(127, 127, 0)]
    [InlineData(128, 128, 0)]
    [InlineData(129, 128, 1)]
    public void RecordBudget_PreservesFirstResultsAndCountsExactOmissions(int count, int retained, int omitted)
    {
        var batch = Parse(Enumerable.Range(0, count).Select(index => Result("CS" + index, "warning", "message")).ToArray());
        Assert.Equal(retained, batch.Entries.Count); Assert.Equal(omitted, batch.Omitted); Assert.Equal(omitted > 0, batch.IsTruncated);
        Assert.Equal("CS0", batch.Entries[0].Code); Assert.Equal("CS" + (retained - 1), batch.Entries[^1].Code);
    }

    [Fact]
    public void TextBudget_DoesNotCutSurrogatePairAndOversizedPathCannotNavigate()
    {
        var batch = Parse(Result(new string('c', 129), "error", new string('m', 2047) + "😀tail", new string('p', 4097), 1, 1));
        var entry = Assert.Single(batch.Entries); Assert.Equal(128, entry.Code.Length); Assert.Equal(new string('m', 2047), entry.Message);
        Assert.Null(entry.FilePath); Assert.True(batch.IsTruncated); Assert.Equal(0, batch.Omitted);
    }

    [Theory]
    [InlineData("https://example.test/code.cs")]
    [InlineData("file://server/share/code.cs")]
    public void NonLocalLocations_RemainDiagnosticTextWithoutNavigation(string uri)
    {
        var entry = Assert.Single(Parse(Result("CS1", "error", "message", uri, 2, 3)).Entries);
        Assert.Null(entry.FilePath); Assert.Equal(2, entry.Line); Assert.Equal(GameBuildDiagnosticSeverity.Error, entry.Severity);
    }

    [Fact]
    public void RelativeLocation_ResolvesAgainstProjectAndMissingColumnDefaultsToOne()
    {
        var entry = Assert.Single(Parse(Result("CS1", "error", "message", "folder/mob%20one.cs", 4)).Entries);
        Assert.Equal(Path.GetFullPath("folder/mob one.cs", Path.GetTempPath()), entry.FilePath); Assert.Equal(4, entry.Line); Assert.Equal(1, entry.Column);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"version\":\"1.0.0\",\"runs\":[]}")]
    [InlineData("{\"version\":\"2.1.0\",\"runs\":[{\"results\":42}]}")]
    public void InvalidOrUnsupportedReport_IsUnavailableAsStructuredData(string json)
    {
        var batch = GameBuildDiagnosticReader.Parse(Encoding.UTF8.GetBytes(json), Path.GetTempPath());
        Assert.Equal(GameBuildDiagnosticState.Failed, batch.State); Assert.Empty(batch.Entries); Assert.NotNull(batch.FailureType);
    }

    [Theory]
    [InlineData(0, GameBuildDiagnosticState.Available)]
    [InlineData(1, GameBuildDiagnosticState.TooLarge)]
    public void ByteBudget_ExactLimitParsesAndNextByteIsRejected(int excess, GameBuildDiagnosticState state)
    {
        var json = "{\"version\":\"2.1.0\",\"runs\":[{\"results\":[]}]}";
        var bytes = Encoding.UTF8.GetBytes(json.PadRight(GameBuildDiagnosticReader.MaximumBytes + excess));
        var batch = GameBuildDiagnosticReader.Parse(bytes, Path.GetTempPath());
        Assert.Equal(state, batch.State); Assert.Empty(batch.Entries); Assert.Equal(excess > 0, batch.IsTruncated);
    }

    [Fact]
    public void MissingReport_IsDistinctFromValidEmptyCompilerReport()
    {
        var missing = GameBuildDiagnosticReader.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sarif"), Path.GetTempPath());
        Assert.Equal(GameBuildDiagnosticState.Unavailable, missing.State); Assert.Empty(missing.Entries);
        Assert.Equal(GameBuildDiagnosticState.Available, Parse().State);
    }

    private static GameBuildDiagnosticBatch Parse(params object[] results) => GameBuildDiagnosticReader.Parse(
        JsonSerializer.SerializeToUtf8Bytes(new { version = "2.1.0", runs = new[] { new { results } } }), Path.GetTempPath());
    private static object Result(string code, string level, string message, string? uri = null, int? line = null, int? column = null) => new
    {
        ruleId = code, level, message = new { text = message },
        locations = uri == null ? Array.Empty<object>() : new object[] { new { physicalLocation = new
        { artifactLocation = new { uri }, region = new { startLine = line, startColumn = column } } } }
    };
}
