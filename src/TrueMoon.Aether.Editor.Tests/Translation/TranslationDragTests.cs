using System.Numerics;
using TrueMoon.Aether.Editor.Translation;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Translation;

public sealed class TranslationDragTests
{
    [Theory]
    [InlineData(TranslationHandle.X, 1, 0, 0)]
    [InlineData(TranslationHandle.Y, 0, 1, 0)]
    [InlineData(TranslationHandle.Z, 0, 0, 1)]
    public void AxisMove_MatchesKnownWorldDeltaAndPreservesOtherAxes(TranslationHandle kind, float x, float y, float z)
    {
        var geometry = TranslationGeometryTests.Geometry(); var axis = new Vector3(x, y, z);
        var start = geometry.Origin + axis * geometry.Length * .7f;
        Project(start, out var a); Project(start + axis * .5f, out var b);
        Assert.True(TranslationDrag.TryCreate(geometry, kind, a.X, a.Y, out var drag));
        Assert.True(drag!.TryMove(b.X, b.Y, out var delta));
        Near(axis * .5f, delta);
        Assert.True(drag.TryMove(a.X, a.Y, out var original)); Assert.Equal(Vector3.Zero, original);
        Assert.False(drag.TryMove(double.NaN, b.Y, out _));
        void Project(Vector3 point, out TranslationPoint screen) => Assert.True(TranslationGeometry.Project(point, geometry.Projection, geometry.Image, out screen));
    }

    [Theory]
    [InlineData(TranslationHandle.XY, .2f, .3f, 0f)]
    [InlineData(TranslationHandle.XZ, .2f, 0f, .3f)]
    [InlineData(TranslationHandle.YZ, 0f, .2f, .3f)]
    public void PlaneMove_MatchesKnownWorldDeltaAndPreservesNormal(TranslationHandle kind, float x, float y, float z)
    {
        var geometry = TranslationGeometryTests.Geometry();
        var basis = kind == TranslationHandle.XY ? new Vector3(1, 1, 0) : kind == TranslationHandle.XZ ? new(1, 0, 1) : new Vector3(0, 1, 1);
        var start = geometry.Origin + basis * geometry.Length * .28f;
        Assert.True(TranslationGeometry.Project(start, geometry.Projection, geometry.Image, out var a));
        Assert.True(TranslationGeometry.Project(start + new Vector3(x, y, z), geometry.Projection, geometry.Image, out var b));
        Assert.True(TranslationDrag.TryCreate(geometry, kind, a.X, a.Y, out var drag));
        Assert.True(drag!.TryMove(b.X, b.Y, out var delta)); Near(new(x, y, z), delta);
    }

    [Fact]
    public void CapturedMovement_CanLeaveImageButParallelOrInvalidStartCannotBegin()
    {
        var geometry = TranslationGeometryTests.Geometry(false); var handle = geometry.Handles[0]; var a = handle.Points[1];
        Assert.True(TranslationDrag.TryCreate(geometry, TranslationHandle.X, a.X, a.Y, out var drag));
        Assert.True(drag!.TryMove(geometry.Image.Left + geometry.Image.Width + 50, a.Y, out var delta));
        Assert.True(delta.X > geometry.Length); Assert.Equal(0, delta.Y); Assert.Equal(0, delta.Z);
        Assert.False(TranslationDrag.TryCreate(geometry, TranslationHandle.Z, a.X, a.Y, out _));
        Assert.False(TranslationDrag.TryCreate(geometry, TranslationHandle.XY, double.PositiveInfinity, a.Y, out _));
    }

    private static void Near(Vector3 expected, Vector3 actual)
    { Assert.InRange(Math.Abs(expected.X - actual.X), 0, .0001f); Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, .0001f); Assert.InRange(Math.Abs(expected.Z - actual.Z), 0, .0001f); }
}
