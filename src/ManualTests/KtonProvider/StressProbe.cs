using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;

namespace KtonProviderProbe;

internal static partial class Program
{
    private static void StressAndCcd()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = factory.Create(Options()))
        {
            var samples = new List<long>();
            for (var wave = 0; wave < 20; wave++)
            {
                var shapes = new PhysicsShapeId[16]; var bodies = new PhysicsBodyId[16];
                for (var i = 0; i < shapes.Length; i++)
                {
                    shapes[i] = world.CreateShape(new PhysicsStaticMesh(new Vector3[] { new(-1, 0, -1), new(1, 0, -1), new(-1, 0, 1) }, new[] { 0, 1, 2 }));
                    bodies[i] = world.CreateBody(new(shapes[i], PhysicsBodyMotion.Static, new(new(i * 3, 0, 0), Quaternion.Identity)) { Mass = 0 });
                }
                Step(world, 1);
                for (var i = 0; i < shapes.Length; i++) { world.RemoveBody(bodies[i]); world.RemoveShape(shapes[i]); }
                samples.Add(((BepuWorld)world).Resources.PoolBytes);
            }
            var plateau = samples.Skip(2).ToArray();
            Check("live-world-mesh-create-destroy-pool-plateau", plateau.Max() == plateau.Min(), plateau);
            Check("live-world-mesh-create-destroy-empty-stores", ((BepuWorld)world).Bodies.Count == 0 && ((BepuWorld)world).Shapes.Count == 0);
            Metrics["MeshPoolPlateau"] = new { Waves = 20, MeshesPerWave = 16, SamplesBytes = samples, Boundary = "Sampled BEPU pool counters; not all managed/process memory." };
        }
        Clean("live-world-mesh-cycles-clean", factory);
        for (var cycle = 0; cycle < 64; cycle++)
        {
            using (var world = factory.Create(Options(new(0, -9.81f, 0))))
            {
                Floor(world); var shape = world.CreateShape(new PhysicsSphere(0.2f));
                for (var i = 0; i < 16; i++) world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(new(i * 0.6f, 3, 0), Quaternion.Identity)));
                Step(world, 8);
                for (var i = 0; i < 32; i++) { var temporary = world.CreateShape(new PhysicsCapsule(0.2f, 0.3f)); world.RemoveShape(temporary); }
            }
            Clean($"create-delete-dispose-cycle-{cycle}", factory);
        }
        using (var world = factory.Create(new() { Quality = PhysicsQualityProfile.FastLinear60, Gravity = Vector3.Zero }))
        {
            world.CreateBody(new(world.CreateShape(new PhysicsBox(new(0.04f, 10, 10))), PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            var bullet = world.CreateBody(new(world.CreateShape(new PhysicsSphere(0.1f)), PhysicsBodyMotion.Dynamic, new(new(-5, 0, 0), Quaternion.Identity)) { LinearVelocity = new(600, 0, 0) });
            Step(world, 1); var first = world.GetBodyState(bullet).Pose.Position.X; var maximum = first;
            for (var i = 0; i < 60; i++) { Step(world, 1); maximum = MathF.Max(maximum, world.GetBodyState(bullet).Pose.Position.X); }
            Near("fast-linear-profile-first-impact", first, -0.12f, 0.005f);
            Check("fast-linear-profile-never-crosses-wall", maximum <= -0.115f, maximum);
            Metrics["FastLinear60"] = new { FirstPositionX = first, MaximumPositionX = maximum, Ticks = 61, Speed = 600, WallThickness = 0.04, SphereRadius = 0.1 };
        }
        Clean("final-factory-clean", factory); Metrics["Cleanup"] = factory.Resources;
    }
}
