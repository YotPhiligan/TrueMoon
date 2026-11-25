using System.Numerics;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class CameraProjectionTests
{
    private static readonly PerspectiveProjection Perspective = new(MathF.PI / 2, 1, 11);

    [Fact]
    public void Projection_UsesLiteralVulkanMatrixAndSharedWorldCameraMath()
    {
        var pose = Matrix4x4.CreateTranslation(1, 2, 3);
        var projection = new CameraProjection(Perspective, pose, 320, 160);
        var expected = new Matrix4x4(.5f, 0, 0, 0, 0, -1, 0, 0, 0, 0, -1.1f, -1, -.5f, 2, 2.2f, 3);
        MatrixNear(expected, projection.ViewProjection);
        Assert.Equal(new PerspectiveCamera(Perspective.FieldOfView, 1, 11, 1, Vector3.Zero).CreateViewProjection(pose, 320, 160), projection.ViewProjection);
        MatrixNear(Matrix4x4.Identity, projection.ViewProjection * projection.InverseViewProjection);
        Assert.True(projection.TryUnproject(new(0, 0, 0), out var near)); VectorNear(new(1, 2, 2), near);
        Assert.True(projection.TryUnproject(new(0, 0, 1), out var far)); VectorNear(new(1, 2, -8), far);
    }

    [Theory]
    [InlineData(.6f, -.2f, .1f)] [InlineData(-1.3f, .5f, -.7f)]
    public void Unprojection_ArbitraryRigidPoseMatchesLiteralCameraSpaceRay(float yaw, float pitch, float roll)
    {
        var pose = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll) * Matrix4x4.CreateTranslation(2, -3, 7);
        var projection = new CameraProjection(Perspective, pose, 321, 161);
        Assert.True(projection.TryCreateRay(160, 80, out var ray));
        VectorNear(Vector3.Transform(new(0, 0, -1), pose), ray.Origin);
        VectorNear(Vector3.TransformNormal(-Vector3.UnitZ, pose), ray.Direction);
        Near(10, ray.MaximumDistance, .0002);
        Assert.True(projection.TryProject(Vector3.Transform(new(.5f, .25f, -5), pose), out var ndc));
        Near(.5 / (5 * (321d / 161)), ndc.X); Near(-.05, ndc.Y); Near(.88, ndc.Z);
        Assert.True(projection.TryUnproject(ndc, out var world));
        VectorNear(Vector3.Transform(new(.5f, .25f, -5), pose), world);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(103, 57)] [InlineData(319, 159)]
    public void Ray_UsesPixelCentersAndSingleYFlip(int x, int y)
    {
        var projection = new CameraProjection(Perspective, Matrix4x4.Identity, 320, 160);
        Assert.True(projection.TryCreateRay(x, y, out var ray));
        var nx = 2 * (x + .5d) / 320 - 1; var ny = 2 * (y + .5d) / 160 - 1;
        VectorNear(new((float)(nx * 2), (float)-ny, -1), ray.Origin);
        Assert.True(projection.TryProject(ray.Origin, out var ndc));
        Near(x + .5, (ndc.X + 1) * 160); Near(y + .5, (ndc.Y + 1) * 80);
        Near(1, ray.Direction.Length());
    }

    [Theory]
    [InlineData(1d)] [InlineData(1.5d)] [InlineData(2d)]
    public void LogicalMapping_UsesPhysicalExtentOnceAcrossDpi(double dpi)
    {
        // Host-normalized input; the image retains a distinct logical/physical size and letterbox origin.
        var image = new ViewportImageBounds(16, 14, 800, 450);
        Assert.True(image.TryMapPixel(616 * dpi / dpi, 126.5 * dpi / dpi, 640, 360, out var x, out var y));
        Assert.Equal(480, x); Assert.Equal(90, y);
    }

    [Fact]
    public void LogicalMapping_HalfOpenEdgesAndLetterboxNeverClamp()
    {
        var image = new ViewportImageBounds(16, 14, 800, 450);
        Assert.True(image.TryMapPixel(16, 14, 640, 360, out var x, out var y)); Assert.Equal(0, x); Assert.Equal(0, y);
        Assert.True(image.TryMapPixel(815.99, 463.99, 640, 360, out x, out y)); Assert.Equal(639, x); Assert.Equal(359, y);
        foreach (var point in new[] { (816d, 14d), (16d, 464d), (15.999, 20d), (16d, 13.999), (double.NaN, 20d), (20d, double.PositiveInfinity) })
        {
            Assert.False(image.TryMapPixel(point.Item1, point.Item2, 640, 360, out x, out y)); Assert.Equal(0, x); Assert.Equal(0, y);
        }
    }

    [Theory]
    [InlineData(0, 1, 10, 10)] [InlineData(1, 0, 10, 10)]
    [InlineData(1, 1, 0, 10)] [InlineData(1, 1, 10, 0)] [InlineData(1, 1, -10, 10)]
    public void LogicalMapping_InvalidOrSuspendedBoundsAreUnavailable(int physicalWidth, int physicalHeight, double width, double height)
        => Assert.False(new ViewportImageBounds(0, 0, width, height).TryMapPixel(0, 0, physicalWidth, physicalHeight, out _, out _));

    [Fact]
    public void LogicalMapping_RejectsOverflowNonfiniteAndUnresolvableBounds()
    {
        foreach (var image in new[] { new ViewportImageBounds(double.NaN, 0, 10, 10), new(0, 0, double.PositiveInfinity, 10),
            new(double.MaxValue, 0, double.MaxValue, 10), new(1e30, 0, 1, 10), new(0, double.NaN, 10, 10), new(0, 0, 10, double.NaN) })
            Assert.False(image.TryMapPixel(0, 0, 640, 360, out _, out _));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 32)] [InlineData(32, 0)] [InlineData(15, 32)] [InlineData(1921, 32)] [InlineData(32, 1081)]
    public void Projection_RejectsUnsupportedExtent(int width, int height)
        => Assert.Equal(RenderContentError.InvalidCamera, Assert.Throws<RenderContentException>(() => new CameraProjection(Perspective, Matrix4x4.Identity, width, height)).Error);

    [Fact]
    public void Projection_RejectsNonrepresentableInverseAndFiniteOverflow()
    {
        var tinyNear = new PerspectiveProjection(MathF.PI / 2, float.Epsilon, 1);
        tinyNear.Validate();
        Assert.Throws<RenderContentException>(() => new CameraProjection(tinyNear, Matrix4x4.Identity, 320, 160));
        // The forward matrix is finite, but Single inversion overflows internally; finite zeros are not an inverse.
        Assert.Throws<RenderContentException>(() => new CameraProjection(new(1e-30f, .1f, 100), Matrix4x4.Identity, 320, 160));
        Assert.Throws<RenderContentException>(() => new CameraProjection(new(1e-30f, .1f, 100), Matrix4x4.CreateTranslation(float.MaxValue, 0, 0), 320, 160));
    }

    [Theory]
    [InlineData("scale")] [InlineData("shear")] [InlineData("reflection")] [InlineData("non-affine")] [InlineData("nan")]
    public void Projection_SharedValidatorRejectsNonRigidPose(string kind)
    {
        var pose = Matrix4x4.Identity;
        switch (kind)
        {
            case "scale": pose.M11 = 2; break;
            case "shear": pose.M12 = .01f; break;
            case "reflection": pose.M33 = -1; break;
            case "non-affine": pose.M44 = 2; break;
            default: pose.M41 = float.NaN; break;
        }
        Assert.Equal(RenderContentError.InvalidCamera,
            Assert.Throws<RenderContentException>(() => new CameraProjection(Perspective, pose, 320, 160)).Error);
    }

    [Theory]
    [InlineData(0f, 1f, 11f)] [InlineData(float.NaN, 1f, 11f)] [InlineData(float.PositiveInfinity, 1f, 11f)]
    [InlineData(1f, 0f, 11f)] [InlineData(1f, float.NaN, 11f)] [InlineData(1f, 1f, 1f)] [InlineData(1f, 1f, float.PositiveInfinity)]
    public void ProjectionSettings_RejectMalformedSingleValues(float fov, float near, float far)
        => Assert.Equal(RenderContentError.InvalidCamera, Assert.Throws<RenderContentException>(() => new PerspectiveProjection(fov, near, far).Validate()).Error);

    [Fact]
    public void ProjectionSettings_AcceptNearestSingleBoundariesBeforeMatrixRepresentabilityCheck()
    {
        new PerspectiveProjection(float.Epsilon, float.Epsilon, float.BitIncrement(float.Epsilon)).Validate();
        new PerspectiveProjection(float.BitDecrement(MathF.PI), 1, float.BitIncrement(1)).Validate();
        Assert.Throws<RenderContentException>(() => new PerspectiveProjection(MathF.PI, 1, 11).Validate());
        var projection = new CameraProjection(new(float.BitDecrement(MathF.PI), .1f, 100), Matrix4x4.Identity, 16, 1080);
        Assert.True(projection.TryCreateRay(0, 0, out var ray)); Assert.True(ray.MaximumDistance > 0);
    }

    [Fact]
    public void Unprojection_ZeroHomogeneousWIsUnavailableAndUnstableTranslatedInverseIsRejected()
    {
        var projection = new CameraProjection(new(MathF.PI / 2, 1, 1e20f), Matrix4x4.Identity, 321, 161);
        Assert.True(projection.TryUnproject(new(0, 0, 0), out var near)); Assert.Equal(new Vector3(0, 0, -1), near);
        Assert.False(projection.TryUnproject(new(0, 0, 1), out var far)); Assert.Equal(Vector3.Zero, far);
        Assert.False(projection.TryCreateRay(160, 80, out var ray)); Assert.Equal(default, ray);
        // Large translations with insufficient Single precision fail inverse admission before querying.
        Assert.Throws<RenderContentException>(() => new CameraProjection(Perspective, Matrix4x4.CreateTranslation(0, 0, 1e8f), 321, 161));
    }

    [Fact]
    public void Projection_OverflowedNdcDoesNotPublishPartialCoordinates()
    {
        var projection = new CameraProjection(new(1e-10f, 1, 11), Matrix4x4.Identity, 320, 160);
        Assert.False(projection.TryProject(new(float.MaxValue, 0, -1), out var ndc)); Assert.Equal(Vector3.Zero, ndc);
    }

    [Fact]
    public void ProjectionAndUnprojection_RejectInvalidPointsAndPixelsWithoutPublishingPartialValues()
    {
        var projection = new CameraProjection(Perspective, Matrix4x4.Identity, 320, 160);
        foreach (var point in new[] { Vector3.Zero, new(0, 0, 1), new(float.NaN, 0, -1), new(0, float.PositiveInfinity, -1) })
        { Assert.False(projection.TryProject(point, out var ndc)); Assert.Equal(Vector3.Zero, ndc); }
        foreach (var point in new[] { new Vector3(0, 0, -.01f), new(0, 0, 1.01f), new(float.NaN, 0, 0), new(0, 0, float.NaN) })
        { Assert.False(projection.TryUnproject(point, out var world)); Assert.Equal(Vector3.Zero, world); }
        foreach (var pixel in new[] { (-1, 0), (0, -1), (320, 0), (0, 160) })
        { Assert.False(projection.TryCreateRay(pixel.Item1, pixel.Item2, out var ray)); Assert.Equal(default, ray); }
        Assert.True(projection.TryProject(new(0, 0, -.5f), out var outside)); Assert.True(outside.Z < 0);
    }

    private static void Near(double expected, double actual, double tolerance = .00005) => Assert.InRange(actual, expected - tolerance, expected + tolerance);
    private static void VectorNear(Vector3 expected, Vector3 actual) { Near(expected.X, actual.X); Near(expected.Y, actual.Y); Near(expected.Z, actual.Z); }
    private static void MatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        VectorNear(new(expected.M11, expected.M12, expected.M13), new(actual.M11, actual.M12, actual.M13));
        VectorNear(new(expected.M21, expected.M22, expected.M23), new(actual.M21, actual.M22, actual.M23));
        VectorNear(new(expected.M31, expected.M32, expected.M33), new(actual.M31, actual.M32, actual.M33));
        VectorNear(expected.Translation, actual.Translation);
        Near(expected.M14, actual.M14); Near(expected.M24, actual.M24); Near(expected.M34, actual.M34); Near(expected.M44, actual.M44);
    }
}
