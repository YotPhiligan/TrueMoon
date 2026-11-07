using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class PerspectiveCameraTests
{
    [Fact]
    public void Default_UsesExactProfileSettingsAndIsAnUnmanagedWorldValue()
    {
        var camera = PerspectiveCamera.Default;
        Assert.Equal(MathF.PI / 3, camera.FieldOfView);
        Assert.Equal(.1f, camera.Near);
        Assert.Equal(100f, camera.Far);
        Assert.Equal(uint.MaxValue, camera.Layers);
        Assert.Equal(new Vector3(.04f, .06f, .10f), camera.ClearColorSrgb);
        camera.Validate();
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PerspectiveCamera>());
        using var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, camera);
        Assert.Equal(uint.MaxValue, world.GetComponent<PerspectiveCamera>(entity).Layers);
        Error(RenderContentError.InvalidCamera, () => default(PerspectiveCamera).Validate());
    }

    [Fact]
    public void CreateViewProjection_UsesLiteralTranslatedRightHandedVulkanProjection()
    {
        var camera = new PerspectiveCamera(MathF.PI / 2, 1, 11, 0, Vector3.Zero);
        var matrix = camera.CreateViewProjection(Matrix4x4.CreateTranslation(1, 2, 3), 320, 160);
        MatrixNear(new(.5f, 0, 0, 0, 0, -1, 0, 0, 0, 0, -1.1f, -1, -.5f, 2, 2.2f, 3), matrix);
        Assert.Equal(0u, camera.Layers);
        Assert.Equal(Vector3.Zero, camera.ClearColorSrgb);
    }

    [Fact]
    public void CreateViewProjection_AcceptsProperRotationAndUsesItsInverseRatherThanTranslationOnly()
    {
        var camera = new PerspectiveCamera(MathF.PI / 2, 1, 11, uint.MaxValue, Vector3.Zero);
        var pose = new Matrix4x4(0, 0, -1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 2, 3, 1);
        var matrix = camera.CreateViewProjection(pose, 320, 160);
        MatrixNear(new(0, 0, -1.1f, -1, 0, -1, 0, 0, -.5f, 0, 0, 0, 1.5f, 2, 0, 1), matrix);
    }

    [Theory]
    [InlineData(16, 16)] [InlineData(1920, 1080)] [InlineData(640, 360)]
    public void CreateViewProjection_AcceptsInclusivePhysicalExtents(int width, int height)
    {
        var camera = new PerspectiveCamera(MathF.PI / 2, 1, 2, uint.MaxValue, Vector3.One);
        var matrix = camera.CreateViewProjection(Matrix4x4.Identity, width, height);
        Near((float)height / width, matrix.M11);
        Near(-1, matrix.M22);
        Assert.Equal(-1f, matrix.M34);
        Assert.Equal(0f, matrix.M44);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 32)] [InlineData(32, 0)] [InlineData(-1, 32)]
    [InlineData(15, 32)] [InlineData(32, 15)] [InlineData(1921, 32)] [InlineData(32, 1081)]
    public void CreateViewProjection_RejectsSuspendedPartialZeroAndUnsupportedExtents(int width, int height)
        => Error(RenderContentError.InvalidCamera, () => PerspectiveCamera.Default.CreateViewProjection(Matrix4x4.Identity, width, height));

    [Theory]
    [InlineData("fov-zero")] [InlineData("fov-negative")] [InlineData("fov-pi")] [InlineData("fov-nan")]
    [InlineData("fov-infinite")] [InlineData("near-zero")] [InlineData("near-negative")] [InlineData("near-nan")]
    [InlineData("far-equal")] [InlineData("far-less")] [InlineData("far-infinite")]
    [InlineData("clear-negative")] [InlineData("clear-high")] [InlineData("clear-nan")]
    public void Validate_RejectsEveryNumericProjectionOrColorPartition(string kind)
    {
        var camera = kind switch
        {
            "fov-zero" => PerspectiveCamera.Default with { FieldOfView = 0 },
            "fov-negative" => PerspectiveCamera.Default with { FieldOfView = -1 },
            "fov-pi" => PerspectiveCamera.Default with { FieldOfView = MathF.PI },
            "fov-nan" => PerspectiveCamera.Default with { FieldOfView = float.NaN },
            "fov-infinite" => PerspectiveCamera.Default with { FieldOfView = float.PositiveInfinity },
            "near-zero" => PerspectiveCamera.Default with { Near = 0 },
            "near-negative" => PerspectiveCamera.Default with { Near = -1 },
            "near-nan" => PerspectiveCamera.Default with { Near = float.NaN },
            "far-equal" => PerspectiveCamera.Default with { Far = .1f },
            "far-less" => PerspectiveCamera.Default with { Far = .01f },
            "far-infinite" => PerspectiveCamera.Default with { Far = float.PositiveInfinity },
            "clear-negative" => PerspectiveCamera.Default with { ClearColorSrgb = new(-float.Epsilon, 0, 0) },
            "clear-high" => PerspectiveCamera.Default with { ClearColorSrgb = new(0, float.BitIncrement(1), 0) },
            _ => PerspectiveCamera.Default with { ClearColorSrgb = new(0, 0, float.NaN) }
        };
        Error(RenderContentError.InvalidCamera, camera.Validate);
    }

    [Theory]
    [InlineData("scale")] [InlineData("shear")] [InlineData("reflection")]
    [InlineData("singular")] [InlineData("perspective")] [InlineData("last-row")]
    [InlineData("nan")] [InlineData("infinity")] [InlineData("basis-infinity")]
    public void CreateViewProjection_RejectsNonRigidAndMalformedCameraMatrices(string kind)
    {
        var matrix = Matrix4x4.Identity;
        switch (kind)
        {
            case "scale": matrix.M22 = 2; break;
            case "shear": matrix.M12 = .01f; break;
            case "reflection": matrix.M33 = -1; break;
            case "singular": matrix.M11 = 0; break;
            case "perspective": matrix.M14 = .01f; break;
            case "last-row": matrix.M44 = float.BitIncrement(1); break;
            case "nan": matrix.M41 = float.NaN; break;
            case "infinity": matrix.M42 = float.PositiveInfinity; break;
            default: matrix.M21 = float.NegativeInfinity; break;
        }
        Error(RenderContentError.InvalidCamera, () => PerspectiveCamera.Default.CreateViewProjection(matrix, 320, 160));
    }

    [Theory]
    [InlineData("length-x")] [InlineData("length-y")] [InlineData("length-z")]
    [InlineData("dot-xz")] [InlineData("dot-yz")]
    public void CreateViewProjection_ChecksEachRigidBasisTermIndependentlyOfDeterminant(string kind)
    {
        var matrix = Matrix4x4.Identity;
        switch (kind)
        {
            case "length-x": matrix.M11 = 1.000075f; break;
            case "length-y": matrix.M22 = 1.000075f; break;
            case "length-z": matrix.M33 = 1.000075f; break;
            case "dot-xz": matrix.M13 = .001f; break;
            default: matrix.M23 = .001f; break;
        }
        Assert.InRange(matrix.GetDeterminant(), .9999f, 1.0001f);
        Error(RenderContentError.InvalidCamera, () => PerspectiveCamera.Default.CreateViewProjection(matrix, 320, 160));
    }

    [Fact]
    public void CreateViewProjection_UsesInclusiveRigidBasisToleranceAndRejectsItsNextFloat()
    {
        var matrix = Matrix4x4.Identity;
        matrix.M12 = .0001f;
        var camera = new PerspectiveCamera(MathF.PI / 2, 1, 11, uint.MaxValue, Vector3.Zero);
        var accepted = camera.CreateViewProjection(matrix, 320, 160);
        Near(.0001f, accepted.M12);
        matrix.M12 = float.BitIncrement(.0001f);
        Error(RenderContentError.InvalidCamera, () => camera.CreateViewProjection(matrix, 320, 160));
    }

    [Fact]
    public void CreateViewProjection_RejectsFinalOverflowEvenWhenPoseAndProjectionInputsAreFinite()
    {
        var camera = PerspectiveCamera.Default with { FieldOfView = 1e-30f };
        camera.Validate();
        Error(RenderContentError.InvalidCamera, () => camera.CreateViewProjection(Matrix4x4.CreateTranslation(float.MaxValue, 0, 0), 320, 160));
    }

    private static void Error(RenderContentError expected, Action action)
        => Assert.Equal(expected, Assert.Throws<RenderContentException>(action).Error);

    private static void Near(float expected, float actual) => Assert.InRange(actual, expected - .00001f, expected + .00001f);

    private static void MatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        float[] left = [expected.M11, expected.M12, expected.M13, expected.M14, expected.M21, expected.M22, expected.M23, expected.M24,
            expected.M31, expected.M32, expected.M33, expected.M34, expected.M41, expected.M42, expected.M43, expected.M44];
        float[] right = [actual.M11, actual.M12, actual.M13, actual.M14, actual.M21, actual.M22, actual.M23, actual.M24,
            actual.M31, actual.M32, actual.M33, actual.M34, actual.M41, actual.M42, actual.M43, actual.M44];
        for (var index = 0; index < left.Length; index++) Near(left[index], right[index]);
    }
}
