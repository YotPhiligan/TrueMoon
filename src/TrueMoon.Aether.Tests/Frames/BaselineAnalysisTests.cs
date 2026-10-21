using EngineSessionProbe.Measurements;
using Xunit;

namespace TrueMoon.Aether.Tests.Frames;

public sealed class BaselineAnalysisTests
{
    private static CaptureData Capture(FrameSample[] frames, long frequency = 1000, int overflow = 0, string[]? errors = null)
        => new(frequency, frames,
            [new(120, 1024, 4096, 8192, 10, 5, 1, 0, 0), new(280, 2048, 5120, 9216, 12, 13, 2, 0, 0)], overflow, errors ?? []);
    private static FrameSample Frame(string phase, long runtime, long surface, long frame, long started, long ended)
        => new(phase, "session", runtime, surface, frame, started, ended);

    [Fact]
    public void Analyze_JoinsSurfaceAndRuntimeGenerations_DeduplicatesPresentsAndExcludesWarmup()
    {
        var production = Capture([
            Frame("production", 1, 1, 1, 100, 110), Frame("production", 1, 2, 1, 160, 170),
            Frame("production", 2, 1, 1, 220, 230), Frame("production", 1, 1, 2, 90, 105),
            Frame("production", 2, 1, 2, 310, 320), Frame("producer-drop", 1, 2, 2, 240, 241)]);
        var editor = Capture([
            Frame("copy", 1, 1, 1, 115, 125), Frame("copy", 1, 2, 1, 180, 190),
            Frame("present", 1, 1, 1, 150, 150), Frame("present", 1, 1, 1, 155, 155),
            Frame("present", 1, 2, 1, 200, 200)]);
        var result = BaselineAnalysis.Analyze(new("steady", 640, 360, 100, 300), editor, [production], false);

        Assert.Equal(3, result.Produced); Assert.Equal(2, result.Received); Assert.Equal(2, result.Presented);
        Assert.Equal(1, result.Unreceived); Assert.Equal(1, result.Unpresented); Assert.Equal(1, result.ProducerDroppedAttempts);
        Assert.Equal(10, result.PresentedFps); Assert.Equal(new Distribution(2, 40, 50, 50), result.ProduceToPresent);
        Assert.Equal(new Distribution(3, 10, 10, 10), result.Stages["Production"]);
        Assert.Equal(new Distribution(2, 10, 10, 10), result.Stages["CpuCopyIncludingCrc"]);
        Assert.Equal(new Distribution(0, null, null, null), result.Stages["GpuProduceRoundTrip"]);
        Assert.True(result.EditorResources.Available); Assert.Equal(1024, result.EditorResources.AllocatedBytes);
        Assert.Equal(.16, result.EditorResources.Seconds); Assert.Equal(.05, result.EditorResources.CpuCores);
        Assert.Equal(5120, result.EditorResources.PeakPrivateBytes); Assert.Equal(12, result.EditorResources.PeakHandles);
        Assert.Equal(new[] { 1, 0, 0 }, result.EditorResources.Collections);
    }

    [Fact]
    public void Analyze_GpuReceivesUseExactSessionIdentity_AbsentResourcesRemainUnavailable()
    {
        var child = Capture([Frame("production", 1, 1, 1, 100, 110)]);
        var editor = Capture([Frame("receive", 1, 1, 1, 100, 115) with { Session = "foreign" }, Frame("present", 1, 1, 1, 125, 125) with { Session = "foreign" }]);
        var result = BaselineAnalysis.Analyze(new("steady", 16, 16, 100, 200), editor, [child], true);
        Assert.Equal(0, result.Received); Assert.Equal(0, result.Presented); Assert.Equal(1, result.Unpresented);
        Assert.Null(result.ProduceToPresent.P95Ms); Assert.False(result.EditorResources.Available);
        Assert.Null(result.EditorResources.AllocatedBytes); Assert.Empty(result.RuntimeResources);
    }

    [Fact]
    public void Analyze_RejectsClockMismatchAndIncompleteCaptures()
    {
        var window = new MeasurementWindow("steady", 16, 16, 100, 300); var editor = Capture([]);
        Assert.Throws<InvalidDataException>(() => BaselineAnalysis.Analyze(window, editor, [Capture([], frequency: 100)], false));
        Assert.Throws<InvalidDataException>(() => BaselineAnalysis.Analyze(window, editor, [Capture([], overflow: 1)], false));
        Assert.Throws<InvalidDataException>(() => BaselineAnalysis.Analyze(window, editor, [Capture([], errors: ["broken manifest"])], false));
        Assert.Throws<InvalidDataException>(() => BaselineAnalysis.Analyze(window with { Ended = 100 }, editor, [], false));
    }

    [Fact]
    public void Summarize_UsesAllSamplesAndNearestRank_EmptyIsUnavailable()
    {
        Assert.Equal(new Distribution(1000, 500, 950, 1000), BaselineAnalysis.Summarize(Enumerable.Range(1, 1000).Select(x => (double)x).Reverse()));
        Assert.Equal(new Distribution(1, 7.5, 7.5, 7.5), BaselineAnalysis.Summarize([7.5]));
        Assert.Equal(new Distribution(0, null, null, null), BaselineAnalysis.Summarize([]));
    }

    [Theory]
    [InlineData(-.001)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Summarize_RejectsInvalidLatency(double value)
        => Assert.Throws<InvalidDataException>(() => BaselineAnalysis.Summarize([value]));
}
