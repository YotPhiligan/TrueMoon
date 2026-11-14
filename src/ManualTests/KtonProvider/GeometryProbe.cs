using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Shapes;

namespace KtonProviderProbe;

internal static partial class Program
{
    private static void GeometryAndJoints()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = factory.Create(Options(new(0, -9.81f, 0))))
        {
            var vertices = new Vector3[] { new(-10, 0, -10), new(10, 0, -10), new(-10, 0, 10), new(10, 0, 10) };
            var indices = new[] { 0, 1, 2, 1, 3, 2 }; var mesh = world.CreateShape(new PhysicsStaticMesh(vertices, indices));
            var floor = world.CreateBody(new(mesh, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            Array.Fill(vertices, new Vector3(0, 100, 0)); Array.Fill(indices, 0);
            var boxShape = world.CreateShape(new PhysicsBox(Vector3.One));
            var box = world.CreateBody(new(boxShape, PhysicsBodyMotion.Dynamic, new(new(-2, 4, 0), Quaternion.Identity)));
            var shared = world.CreateBody(new(boxShape, PhysicsBodyMotion.Dynamic, new(new(-4, 4, 0), Quaternion.Identity)));
            var capsule = world.CreateBody(new(world.CreateShape(new PhysicsCapsule(0.5f, 1)), PhysicsBodyMotion.Dynamic, new(new(2, 4, 0), Quaternion.Identity)));
            Reject("shape-in-use", PhysicsErrorCode.ResourceInUse, () => world.RemoveShape(boxShape));
            Reject("dynamic-mesh-rejected", PhysicsErrorCode.UnsupportedGeometry, () => world.CreateBody(new(mesh, PhysicsBodyMotion.Dynamic, PhysicsPose.Identity)));
            Step(world, 600);
            Near("captured-mesh-box-rest", world.GetBodyState(box).Pose.Position.Y, 0.5f, 0.04f);
            Near("capsule-full-cylinder-rest", world.GetBodyState(capsule).Pose.Position.Y, 1, 0.06f);
            Check("resting-box-sleeps", !world.GetBodyState(box).IsAwake);
            world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, box, new(0, 4, 0))]); Step(world, 5);
            Check("impulse-wakes-sleeper", world.GetBodyState(box).IsAwake && world.GetBodyState(box).Pose.Position.Y > 0.6f);
            world.RemoveBody(box); Step(world, 1); Check("shared-shape-remains-live", world.GetBodyState(shared).Body == shared);
            world.RemoveBody(shared); world.RemoveShape(boxShape);
            Reject("removed-shape-is-stale", PhysicsErrorCode.StaleIdentifier, () => world.RemoveShape(boxShape));
            var next = world.CreateShape(new PhysicsSphere(1)); Check("shape-generation-reused", next.Slot == boxShape.Slot && next.Generation > boxShape.Generation);
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, floor, Pose: new(new(0, -2, 0), Quaternion.Identity))]);
            Near("static-teleport", world.GetBodyState(floor).Pose.Position.Y, -2);
        }
        using (var world = factory.Create(Options(new(0, -9.81f, 0))))
        {
            var anchor = world.CreateBody(new(world.CreateShape(new PhysicsBox(Vector3.One)), PhysicsBodyMotion.Kinematic, new(new(0, 5, 0), Quaternion.Identity)) { Mass = 0 });
            var link = Ball(world, new(0, 4, 0)); var joint = world.CreateBallSocket(new(anchor, link, new(0, -0.5f, 0), new(0, 0.5f, 0)));
            Reject("body-in-use-by-joint", PhysicsErrorCode.ResourceInUse, () => world.RemoveBody(link));
            world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, link, new(2, 0, 0))]); Step(world, 300);
            var a = world.GetBodyState(anchor).Pose; var b = world.GetBodyState(link).Pose;
            var error = Vector3.Distance(a.Position + Vector3.Transform(new Vector3(0, -0.5f, 0), a.Orientation), b.Position + Vector3.Transform(new Vector3(0, 0.5f, 0), b.Orientation));
            Check("ball-socket-anchor-error", error < 0.04f, error);
            world.RemoveJoint(joint); Reject("removed-joint-is-stale", PhysicsErrorCode.StaleIdentifier, () => world.RemoveJoint(joint));
            world.RemoveBody(link); world.RemoveBody(anchor);
        }
        using (var world = factory.Create(Options()))
        {
            var shape = world.CreateShape(new PhysicsSphere(1));
            var a = world.CreateBody(new(shape, PhysicsBodyMotion.Kinematic, PhysicsPose.Identity) { Mass = 0 });
            var b = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(new(0.5f, 0, 0), Quaternion.Identity)));
            var description = new PhysicsBallSocket(a, b, new(0.25f, 0, 0), new(-0.25f, 0, 0));
            var first = world.CreateBallSocket(description); var second = world.CreateBallSocket(description); Step(world, 30);
            Near("connected-collision-disabled", world.GetBodyState(b).Pose.Position.X, 0.5f, 0.01f);
            world.RemoveJoint(first); Step(world, 30); Near("duplicate-joint-retains-pair-filter", world.GetBodyState(b).Pose.Position.X, 0.5f, 0.01f);
            world.RemoveJoint(second); Step(world, 30); Check("last-joint-removal-restores-collision", world.GetBodyState(b).Pose.Position.X > 0.7f);
        }
        foreach (var sensor in new[] { false, true })
        {
            using var world = factory.Create(Options(new(0, -9.81f, 0))); Floor(world);
            var body = world.CreateBody(new(world.CreateShape(new PhysicsSphere(0.5f)), PhysicsBodyMotion.Dynamic, new(new(0, 2, 0), Quaternion.Identity))
                { IsSensor = sensor, Filter = sensor ? PhysicsCollisionFilter.All : new(0, 0) });
            Step(world, 90); Check(sensor ? "sensor-no-response" : "symmetric-mask-no-response", world.GetBodyState(body).Pose.Position.Y < -2);
        }
        foreach (var friction in new[] { 0f, 0.8f })
        {
            using var world = factory.Create(Options(new(0, -9.81f, 0)));
            world.CreateBody(new(world.CreateShape(new PhysicsBox(new(100, 1, 100))), PhysicsBodyMotion.Static, new(new(0, -0.5f, 0), Quaternion.Identity)) { Mass = 0, Material = new(friction) });
            var body = world.CreateBody(new(world.CreateShape(new PhysicsBox(Vector3.One)), PhysicsBodyMotion.Dynamic, new(new(0, 0.5f, 0), Quaternion.Identity)) { LinearVelocity = new(5, 0, 0) });
            Step(world, 120); var speed = world.GetBodyState(body).LinearVelocity.X;
            if (friction == 0) Near("mixed-friction-min-zero-retains-speed", speed, 5, 0.01f);
            else Check("nonzero-friction-slows-body", MathF.Abs(speed) < 0.1f, speed);
        }
        Clean("geometry-joints-filters-clean", factory);
    }
    private static void CapacityAndRetirement()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = factory.Create(Options(limits: new() { MaxBodies = 1, MaxShapes = 2 })))
        {
            var body = Ball(world, Vector3.Zero); var shape = world.CreateShape(new PhysicsBox(Vector3.One));
            Reject("body-limit-includes-statics", PhysicsErrorCode.CapacityExceeded, () => world.CreateBody(new(shape, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 }));
            Reject("shape-limit-before-allocation", PhysicsErrorCode.CapacityExceeded, () => world.CreateShape(new PhysicsSphere(1)));
            Check("capacity-rejection-preserves-live-counts", ((BepuWorld)world).Bodies.Count == 1 && ((BepuWorld)world).Shapes.Count == 2 && world.GetBodyState(body).Body == body);
            world.RemoveBody(body); var replacement = world.CreateBody(new(shape, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            Check("static-dynamic-share-generational-storage", replacement.Slot == body.Slot && replacement.Generation > body.Generation);
        }
        using (var world = factory.Create(Options()))
        {
            var body = Ball(world, Vector3.Zero); var shape = world.CreateShape(new PhysicsSphere(1));
            Reject("live-mass-ratio-before-body-publication", PhysicsErrorCode.InvalidArgument, () => world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(new(10, 0, 0), Quaternion.Identity)) { Mass = 101 }));
            Check("mass-ratio-reject-keeps-body-count", ((BepuWorld)world).Bodies.Count == 1);
            world.RemoveBody(body); world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, PhysicsPose.Identity) { Mass = 101 }); Check("removed-mass-no-longer-constrains-ratio", ((BepuWorld)world).Bodies.Count == 1);
        }
        using (var world = factory.Create(Options(limits: new() { MaxBodies = 1, MaxShapes = 1 })))
        {
            var impl = (BepuWorld)world; impl.Shapes.SeedFreeGenerationForTest(0, uint.MaxValue); var shape = world.CreateShape(new PhysicsSphere(1));
            impl.Bodies.SeedFreeGenerationForTest(0, uint.MaxValue); var body = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, PhysicsPose.Identity));
            Check("last-generation-body-issued", body.Generation == uint.MaxValue); world.RemoveBody(body);
            Reject("exhausted-body-slot-retired", PhysicsErrorCode.CapacityExceeded, () => world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, PhysicsPose.Identity)));
            Reject("retired-body-id-stale", PhysicsErrorCode.StaleIdentifier, () => world.GetBodyState(body));
            world.RemoveShape(shape); Reject("exhausted-shape-slot-retired", PhysicsErrorCode.CapacityExceeded, () => world.CreateShape(new PhysicsSphere(1)));
        }
        using (var world = factory.Create(Options(limits: new() { MaxJoints = 1 })))
        {
            var a = Ball(world, Vector3.Zero); var b = Ball(world, Vector3.UnitY); var desc = new PhysicsBallSocket(a, b, Vector3.Zero, -Vector3.UnitY);
            ((BepuWorld)world).Joints.SeedFreeGenerationForTest(0, uint.MaxValue); var joint = world.CreateBallSocket(desc);
            Reject("joint-capacity-before-add", PhysicsErrorCode.CapacityExceeded, () => world.CreateBallSocket(desc));
            world.RemoveJoint(joint); Reject("exhausted-joint-slot-retired", PhysicsErrorCode.CapacityExceeded, () => world.CreateBallSocket(desc));
            world.RemoveBody(a); world.RemoveBody(b);
        }
        Clean("capacity-retirement-clean", factory);
    }
}
