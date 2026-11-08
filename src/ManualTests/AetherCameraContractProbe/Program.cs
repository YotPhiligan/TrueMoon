using System.Numerics;
using System.Text.Json;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.ManualTests.AetherCameraContractProbe;

// Numerical design probe only; no runtime navigation or production picking is implemented here.
internal static class Program
{
    private static void Main(string[] args)
    {
        var checks = new List<string>();
        var camera = Matrix4x4.CreateTranslation(0, 1, 6);
        var projection = PerspectiveCamera.Default.CreateViewProjection(camera, 640, 360);
        var legacy = new PrimitiveScene(PreviewGameSession.Camera, 0, Vector3.One).CreateViewProjection(640, 360);
        Check("ExistingProjectionMatchesPrimitiveCamera", () => MatrixNear(projection, legacy));
        var center = RayAt(projection, .5f, .5f);
        Check("NearPlaneDepthZero", () => Vector3.Distance(center.Origin, new(0, 1, 5.9f)) < .0001f);
        Check("FarPlaneDepthOne", () => Vector3.Distance(center.End, new(0, 1, -94)) < .02f);
        Check("TopLeftImageYUsesNdcMinusOne", () => RayAt(projection, .5f, .125f).Origin.Y > 1);
        Check("BottomImageYUsesNdcPlusOne", () => RayAt(projection, .5f, .875f).Origin.Y < 1);
        var pixel = RayAt(projection, (103.5f / 640), (57.5f / 360));
        var clip = Vector4.Transform(new Vector4(pixel.Origin, 1), projection);
        Check("PixelCenterReprojection", () => Math.Abs((clip.X / clip.W + 1) * 320 - 103.5) < .001 &&
            Math.Abs((clip.Y / clip.W + 1) * 180 - 57.5) < .001);
        foreach (var scale in new[] { 1d, 1.5d, 2d })
        {
            var logicalX = 616d * scale / scale; var logicalY = 126.5d * scale / scale;
            var mapped = MapPixel(logicalX, logicalY);
            Check("LogicalCoordinatesDpi" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => mapped == (480, 90));
        }
        Check("InvalidLogicalCoordinatesRejected", () => MapPixel(double.NaN, 14) == null && MapPixel(16, double.PositiveInfinity) == null);
        Check("HalfOpenLetterboxEdges", () => MapPixel(16, 14) == (0, 0) && MapPixel(816, 14) == null &&
            MapPixel(16, 464) == null && MapPixel(15, 14) == null);
        var forward = new Ray(new(0, 0, 0), -Vector3.UnitZ, 100);
        Check("NearPlaneInsideCubePicksExitSurface", () => Math.Abs(HitCube(forward, Matrix4x4.Identity)!.Value - .5) < 1e-6);
        Check("FarClippedSurfaceDoesNotHitAtZero", () => HitCube(forward with { Maximum = .25 }, Matrix4x4.Identity) == null);
        Check("BehindCameraDoesNotHit", () => HitCube(forward, Matrix4x4.CreateTranslation(0, 0, 2)) == null);
        var nonuniform = Matrix4x4.CreateScale(2, 1, 3) * Matrix4x4.CreateTranslation(0, 1, -1);
        var axisRay = new Ray(new(0, 1, 6), -Vector3.UnitZ, 100);
        Check("LocalRayKeepsWorldDistanceParameter", () => Math.Abs(HitCube(axisRay, nonuniform)!.Value - 5.5) < 1e-5);
        var shear = Matrix4x4.Identity; shear.M31 = .4f; shear.M32 = .2f;
        var sheared = Matrix4x4.CreateScale(2, 1, 3) * shear * Matrix4x4.CreateTranslation(0, 1, -1);
        new RenderPrimitive(sheared, Vector3.One).Validate();
        Check("AffineShearCubePick", () => Math.Abs(HitCube(axisRay, sheared)!.Value - 5.5) < 1e-5);
        var rotated = Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll(.6f, -.2f, .1f)) * camera;
        var rotatedRay = RayAt(PerspectiveCamera.Default.CreateViewProjection(rotated, 640, 360), .5f, .5f);
        var expectedDirection = Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, rotated));
        Check("RigidRotatedCameraCenterRay", () => Vector3.Dot(rotatedRay.Direction, expectedDirection) > .99999f);
        Check("ReflectedCameraRejected", () => Reject(() => PerspectiveCamera.Default.CreateViewProjection(Matrix4x4.CreateScale(-1, 1, 1), 640, 360)));
        Check("ScaledCameraRejected", () => Reject(() => PerspectiveCamera.Default.CreateViewProjection(Matrix4x4.CreateScale(2), 640, 360)));
        Check("SuspendedExtentHasNoProjection", () => Reject(() => PerspectiveCamera.Default.CreateViewProjection(camera, 0, 0)));
        var report = new { Passed = true, Count = checks.Count, Checks = checks, NativeWindow = false, ProductionNavigation = false,
            Kind = "Numerical contract probe over current PerspectiveCamera and PrimitiveScene; ray/cube routines are prototypes",
            CenterNear = new[] { center.Origin.X, center.Origin.Y, center.Origin.Z }, CenterFar = new[] { center.End.X, center.End.Y, center.End.Z } };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length > 0) File.WriteAllText(args[0], json);
        Console.WriteLine(json);

        void Check(string name, Func<bool> check)
        { if (!check()) throw new InvalidOperationException(name); checks.Add(name); }
        static bool Reject(Action action)
        { try { action(); return false; } catch (TrueMoon.Aether.Rendering.RenderContentException) { return true; } }
        static bool MatrixNear(Matrix4x4 a, Matrix4x4 b)
        {
            var av = new[] { a.M11,a.M12,a.M13,a.M14,a.M21,a.M22,a.M23,a.M24,a.M31,a.M32,a.M33,a.M34,a.M41,a.M42,a.M43,a.M44 };
            var bv = new[] { b.M11,b.M12,b.M13,b.M14,b.M21,b.M22,b.M23,b.M24,b.M31,b.M32,b.M33,b.M34,b.M41,b.M42,b.M43,b.M44 };
            return av.Zip(bv).All(pair => Math.Abs(pair.First - pair.Second) < 1e-5);
        }
        static Ray RayAt(Matrix4x4 viewProjection, float u, float v)
        {
            if (!Matrix4x4.Invert(viewProjection, out var inverse)) throw new InvalidOperationException("No inverse");
            Vector3 Unproject(float z)
            {
                var point = Vector4.Transform(new Vector4(u * 2 - 1, v * 2 - 1, z, 1), inverse);
                return new(point.X / point.W, point.Y / point.W, point.Z / point.W);
            }
            var near = Unproject(0); var far = Unproject(1); var delta = far - near;
            return new(near, Vector3.Normalize(delta), delta.Length());
        }
        static (int X, int Y)? MapPixel(double x, double y)
        {
            const double left = 16, top = 14, width = 800, height = 450;
            if (!double.IsFinite(x) || !double.IsFinite(y) || x < left || y < top || x >= left + width || y >= top + height) return null;
            return ((int)Math.Floor((x - left) / width * 640), (int)Math.Floor((y - top) / height * 360));
        }
        static double? HitCube(Ray ray, Matrix4x4 world)
        {
            if (!Matrix4x4.Invert(world, out var inverse)) throw new InvalidOperationException("No world inverse");
            var origin = Vector3.Transform(ray.Origin, inverse); var direction = Vector3.TransformNormal(ray.Direction, inverse);
            var start = double.NegativeInfinity; var end = double.PositiveInfinity;
            for (var axis = 0; axis < 3; axis++)
            {
                var o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
                var d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
                if (d == 0) { if (o < -.5 || o > .5) return null; continue; }
                var a = (-.5 - o) / d; var b = (.5 - o) / d;
                start = Math.Max(start, Math.Min(a, b)); end = Math.Min(end, Math.Max(a, b));
                if (start > end) return null;
            }
            var hit = start >= 0 ? start : end;
            return hit >= 0 && hit <= ray.Maximum ? hit : null;
        }
    }
    private readonly record struct Ray(Vector3 Origin, Vector3 Direction, double Maximum)
    { public Vector3 End => Origin + Direction * (float)Maximum; }
}
