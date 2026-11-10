using System.Numerics;
using TrueMoon.Aether.Editor.Translation;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Translation;

public sealed class TranslationGeometryTests
{
    internal static readonly Vector3 Pivot = new(0, 1, 0);
    internal static readonly ViewportImageBounds Image = new(20, 30, 640, 360);
    internal static CameraProjection Oblique()
    {
        var eye = new Vector3(5, 5, 7);
        return new(PerspectiveProjection.Default, Matrix4x4.CreateWorld(eye, Vector3.Normalize(Pivot - eye), Vector3.UnitY), 640, 360);
    }
    internal static TranslationGeometry Geometry(bool oblique = true)
    {
        Assert.True(TranslationGeometry.TryCreate(oblique ? Oblique() : PreviewCamera.Default.CreateProjection(640, 360), Image, Pivot, out var geometry));
        return geometry!;
    }

    [Fact]
    public void DefaultCamera_SuppressesViewAlignedAxisAndEdgeOnPlanesAndKeepsLogicalLength()
    {
        var geometry = Geometry(false);
        Assert.Equal(new[] { TranslationHandle.X, TranslationHandle.Y, TranslationHandle.XY }, geometry.Handles.Select(h => h.Kind));
        var x = geometry.Handles[0];
        Assert.InRange(x.Points[1].X - x.Points[0].X, 58.9, 59.2);
        Assert.Equal(TranslationHandle.X, geometry.Hit((x.Points[0].X + x.Points[1].X) / 2, x.Points[0].Y));
        Assert.Equal(TranslationHandle.None, geometry.Hit(Image.Left + Image.Width, 100));
        Assert.Equal(TranslationHandle.None, geometry.Hit(double.NaN, 100));
    }

    [Theory]
    [InlineData(TranslationHandle.XY)] [InlineData(TranslationHandle.XZ)] [InlineData(TranslationHandle.YZ)]
    public void ObliqueView_PlaneInteriorHitsItsExactConstraint(TranslationHandle kind)
    {
        var geometry = Geometry(); var handle = geometry.Handles.Single(h => h.Kind == kind);
        var x = handle.Points.Average(p => p.X); var y = handle.Points.Average(p => p.Y);
        Assert.Equal(kind, geometry.Hit(x, y));
        Assert.Equal(4, handle.Points.Length);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(-1, 360)] [InlineData(640, 0)] [InlineData(double.NaN, 360)]
    public void InvalidImage_HasNoGeometry(double width, double height)
        => Assert.False(TranslationGeometry.TryCreate(Oblique(), new(20, 30, width, height), Pivot, out _));

    [Theory]
    [InlineData(0, 1, 7)] [InlineData(1000, 1, 0)] [InlineData(float.NaN, 1, 0)]
    public void BehindOffscreenAndNonfinitePivots_AreUnavailable(float x, float y, float z)
        => Assert.False(TranslationGeometry.TryCreate(PreviewCamera.Default.CreateProjection(640, 360), Image, new(x, y, z), out _));
}
