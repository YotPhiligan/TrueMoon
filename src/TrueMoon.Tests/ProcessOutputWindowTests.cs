using Xunit;

namespace TrueMoon.Tests;

public sealed class ProcessOutputWindowTests
{
    [Theory]
    [InlineData(0, "", 4)]
    [InlineData(1, "f", 3)]
    [InlineData(3, "def", 1)]
    [InlineData(4, "cdef", 0)]
    [InlineData(5, "cdef", 0)]
    public async Task ReadWindow_PreservesCaptureLossAndSeparatesDisplayOmission(int maximum, string expected, int omitted)
    {
        using var reader = new StringReader("abcdef"); var capture = new ProcessOutputCapture(reader, 4); await capture.Completion;
        var window = capture.Read(maximum); Assert.Equal(expected, window.Text); Assert.Equal(6, window.TotalCharacters);
        Assert.Equal(2, window.DroppedCharacters); Assert.Equal(omitted, window.OmittedCharacters); Assert.Equal(0, window.FileBytesDropped);
        Assert.Equal(window, capture.Read().Limit(maximum)); Assert.Equal("cdef", capture.Read().Text); Assert.Equal(0, capture.Read().OmittedCharacters);
    }

    [Theory]
    [InlineData("a😀z", 4, 2, "z", 0, 3)]
    [InlineData("a😀z", 2, 1, "z", 3, 0)]
    [InlineData("a😀z", 2, 0, "", 3, 1)]
    public async Task WindowBoundary_DoesNotExposeHalfSurrogateOrDoubleCountCaptureLoss(string text, int capacity, int window, string expected, int dropped, int omitted)
    {
        using var reader = new StringReader(text); var capture = new ProcessOutputCapture(reader, capacity); await capture.Completion;
        var snapshot = capture.Read(window); Assert.Equal(expected, snapshot.Text); Assert.Equal(dropped, snapshot.DroppedCharacters);
        Assert.Equal(omitted, snapshot.OmittedCharacters); Assert.Equal(snapshot, capture.Read().Limit(window));
    }

    [Fact]
    public async Task InvalidWindow_FailsWithoutChangingCaptureState()
    {
        using var reader = new StringReader("abc"); var capture = new ProcessOutputCapture(reader); await capture.Completion;
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.Read(-1)); Assert.Throws<ArgumentOutOfRangeException>(() => capture.Read(1024 * 1024 + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.Read().Limit(-1)); Assert.Equal("abc", capture.Read().Text);
    }
}
