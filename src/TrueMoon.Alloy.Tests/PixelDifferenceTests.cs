using AlloyVulkanTest;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class PixelDifferenceTests
{
    [Fact]
    public void Compare_IdenticalPixelsHaveZeroMetricsAndNoFailureCoordinate()
    {
        byte[] pixels = [12, 34, 56, 78, 90, 23, 45, 255];
        var result = PixelDifference.Compare(pixels, pixels.ToArray(), 2, 1, 0);
        Assert.Equal(new PixelDifference(true, 0, 0, 0, 0, 0, null, null), result);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Compare_ChannelToleranceIncludesBoundaryButRejectsNextValue(int difference, bool passed)
    {
        byte[] expected = [10, 20, 30, 255]; byte[] actual = [(byte)(10 + difference), 20, 30, 255];
        var result = PixelDifference.Compare(expected, actual, 1, 1, 2);
        Assert.Equal(passed, result.Passed); Assert.Equal(difference, result.MaximumChannelDifference);
        Assert.Equal(difference / 4d, result.MeanChannelDifference);
        Assert.Equal(difference == 0 ? 0 : 1, result.DifferentPixels);
        Assert.Equal(passed ? 0 : 1, result.OutsideTolerancePixels);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Compare_ChecksEveryChannelIncludingAlphaAndLastPixel(int channel)
    {
        var expected = new byte[24]; var actual = new byte[24]; actual[20 + channel] = 3;
        var result = PixelDifference.Compare(expected, actual, 3, 2, 2);
        Assert.False(result.Passed); Assert.Equal(1, result.OutsideTolerancePixels);
        Assert.Equal(2, result.FirstX); Assert.Equal(1, result.FirstY); Assert.Equal(3, result.MaximumChannelDifference);
    }

    [Fact]
    public void Compare_CountsDifferentPixelsOnceAndIncludesAllChannelDeltasInMean()
    {
        var result = PixelDifference.Compare([0, 0, 0, 0, 10, 10, 10, 10], [3, 4, 5, 6, 11, 12, 10, 10], 2, 1, 2);
        Assert.Equal(new PixelDifference(false, 2, 1, 0, 6, 21 / 8d, 0, 0), result);
    }

    [Fact]
    public void Compare_EdgeEnvelopeAcceptsAntialiasCoverageButRejectsChangedFlatRegion()
    {
        byte[] expected = [0, 0, 0, 255, 100, 100, 100, 255, 200, 200, 200, 255];
        byte[] actual = [0, 0, 0, 255, 120, 120, 120, 255, 200, 200, 200, 255];
        var result = PixelDifference.Compare(expected, actual, 3, 1, 2, 1);
        Assert.True(result.Passed); Assert.Equal(1, result.EdgeMatchedPixels); Assert.Equal(20, result.MaximumChannelDifference);
        Assert.False(PixelDifference.Compare(expected, actual, 3, 1, 2).Passed);
        Assert.False(PixelDifference.Compare(new byte[36], Enumerable.Repeat((byte)5, 36).ToArray(), 3, 3, 2, 1).Passed);
    }

    [Fact]
    public void Compare_SymmetricEnvelopeRejectsDisappearedEdgeAndShiftBeyondOnePixel()
    {
        byte[] edge = [0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0];
        var missing = new byte[20];
        Assert.False(PixelDifference.Compare(edge, missing, 5, 1, 2, 1).Passed);
        Assert.False(PixelDifference.Compare(missing, edge, 5, 1, 2, 1).Passed);
        byte[] shifted = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255];
        Assert.False(PixelDifference.Compare(edge, shifted, 5, 1, 2, 1).Passed);
    }

    [Theory]
    [InlineData(34, true)]
    [InlineData(35, false)]
    public void Compare_EdgeCoverageToleranceHasAnExactBound(int coverage, bool passed)
    {
        byte[] expected = [0, 0, 0, 255, 10, 10, 10, 255, 0, 0, 0, 255];
        byte[] actual = [0, 0, 0, 255, (byte)coverage, (byte)coverage, (byte)coverage, 255, 0, 0, 0, 255];
        Assert.Equal(passed, PixelDifference.Compare(expected, actual, 3, 1, 2, 1).Passed);
    }

    [Fact]
    public void Compare_LowContrastNeighborhoodKeepsStrictTolerance()
    {
        byte[] expected = [0, 0, 0, 255, 7, 7, 7, 255, 0, 0, 0, 255];
        byte[] actual = [0, 0, 0, 255, 10, 10, 10, 255, 0, 0, 0, 255];
        Assert.False(PixelDifference.Compare(expected, actual, 3, 1, 2, 1).Passed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Compare_RejectsInvalidEdgeTolerance(int tolerance)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PixelDifference.Compare(new byte[4], new byte[4], 1, 1, 2, 1, tolerance));

    [Theory]
    [InlineData(0, 1, 4, 4)]
    [InlineData(-1, 1, 4, 4)]
    [InlineData(1, 0, 4, 4)]
    [InlineData(1, -1, 4, 4)]
    [InlineData(1, 1, 3, 3)]
    [InlineData(1, 1, 4, 8)]
    public void Compare_RejectsInvalidDimensionsOrIncompleteBuffers(int width, int height, int expectedLength, int actualLength)
        => Assert.Throws<ArgumentException>(() => PixelDifference.Compare(new byte[expectedLength], new byte[actualLength], width, height, 2));

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Compare_RejectsInvalidTolerance(int tolerance)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PixelDifference.Compare(new byte[4], new byte[4], 1, 1, tolerance));

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Compare_RejectsUnsupportedEdgeRadius(int radius)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PixelDifference.Compare(new byte[4], new byte[4], 1, 1, 2, radius));

    [Fact]
    public void Compare_MaximumToleranceAcceptsFullChannelRange()
        => Assert.True(PixelDifference.Compare([0, 0, 0, 0], [255, 255, 255, 255], 1, 1, 255).Passed);

    [Fact]
    public void Diff_MagnifiesMaximumDifferenceIncludingAlphaAndClampsWithoutWrapping()
    {
        var actual = PixelDifference.Diff(new byte[12], [0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 20]);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 32, 0, 32, 255, 255, 0, 255, 255 }, actual);
        Assert.Throws<ArgumentException>(() => PixelDifference.Diff(new byte[4], new byte[8]));
        Assert.Throws<ArgumentException>(() => PixelDifference.Diff(new byte[3], new byte[3]));
    }
}
