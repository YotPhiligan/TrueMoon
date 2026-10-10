using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class WindowAppearanceTests
{
    [Fact]
    public void Default_PreservesOpaqueDecoratedWindow()
    {
        var settings = new WindowAppearance(); settings.Validate();
        Assert.Equal(WindowTransparencyMode.Opaque, settings.Transparency);
        Assert.Equal(1, settings.Opacity); Assert.True(settings.Decorated);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.5f)]
    [InlineData(1f)]
    public void OpacityMode_AcceptsEndpointsAndIntermediateValue(float opacity)
    {
        var settings = new WindowAppearance { Transparency = WindowTransparencyMode.Opacity, Opacity = opacity, Decorated = false };
        settings.Validate();
        Assert.Equal(opacity, settings.Opacity); Assert.False(settings.Decorated);
    }

    [Theory]
    [InlineData(-.001f)]
    [InlineData(1.001f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Opacity_RejectsOutOfRangeAndNonFiniteValues(float opacity)
    {
        var settings = new WindowAppearance { Transparency = WindowTransparencyMode.Opacity, Opacity = opacity };
        Assert.Equal("opacity", Assert.Throws<ArgumentOutOfRangeException>(settings.Validate).ParamName);
        Assert.Equal("opacity", Assert.Throws<ArgumentOutOfRangeException>(() => WindowAppearance.ValidateOpacity(opacity)).ParamName);
    }

    [Fact]
    public void Opacity_RejectsValuesImmediatelyOutsideInclusiveBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowAppearance.ValidateOpacity(float.BitDecrement(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowAppearance.ValidateOpacity(float.BitIncrement(1)));
        WindowAppearance.ValidateOpacity(float.BitIncrement(0));
        WindowAppearance.ValidateOpacity(float.BitDecrement(1));
    }

    [Theory]
    [InlineData(WindowTransparencyMode.Opaque, 0f)]
    [InlineData(WindowTransparencyMode.Opaque, .5f)]
    [InlineData(WindowTransparencyMode.PerPixel, 0f)]
    [InlineData(WindowTransparencyMode.PerPixel, .5f)]
    public void NonOpacityModes_RejectUniformOpacity(WindowTransparencyMode mode, float opacity)
    {
        var settings = new WindowAppearance { Transparency = mode, Opacity = opacity };
        Assert.Equal("Opacity", Assert.Throws<ArgumentException>(settings.Validate).ParamName);
    }

    [Theory]
    [InlineData(WindowTransparencyMode.Opaque, true)]
    [InlineData(WindowTransparencyMode.Opaque, false)]
    [InlineData(WindowTransparencyMode.PerPixel, true)]
    [InlineData(WindowTransparencyMode.PerPixel, false)]
    public void Decoration_IsIndependentOfAlphaMode(WindowTransparencyMode mode, bool decorated)
    {
        var settings = new WindowAppearance { Transparency = mode, Decorated = decorated };
        settings.Validate(); Assert.Equal(mode, settings.Transparency); Assert.Equal(decorated, settings.Decorated);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void UnknownMode_IsRejected(int mode)
    {
        var settings = new WindowAppearance { Transparency = (WindowTransparencyMode)mode };
        Assert.Equal("Transparency", Assert.Throws<ArgumentOutOfRangeException>(settings.Validate).ParamName);
    }
}
