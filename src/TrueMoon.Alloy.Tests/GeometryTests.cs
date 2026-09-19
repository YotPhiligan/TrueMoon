using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class GeometryTests
{
    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(39.99f, 59.99f, true)]
    [InlineData(40, 30, false)]
    [InlineData(20, 60, false)]
    [InlineData(9.99f, 30, false)]
    [InlineData(20, 19.99f, false)]
    public void Contains_OffsetRectangle_UsesHalfOpenEdges(float x, float y, bool expected)
    {
        Assert.Equal(expected, new Rect(10, 20, 30, 40).Contains(x, y));
    }

    [Fact]
    public void Deflate_AsymmetricInsets_OffsetsOriginAndSubtractsBothEdges()
    {
        var result = new Rect(10, 20, 100, 80).Deflate(new Thickness(3, 5, 7, 11));

        Assert.Equal(new Rect(13, 25, 90, 64), result);
    }

    [Fact]
    public void Deflate_InsetsExceedExtent_ClampsDimensionsToZero()
    {
        var result = new Rect(10, 20, 4, 6).Deflate(new Thickness(3, 5, 7, 11));

        Assert.Equal(new Rect(13, 25, 0, 0), result);
        Assert.False(result.Contains(13, 25));
    }
}
