using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;

namespace KtonResearch;

internal static class Program
{
    private static readonly List<object> Checks = [];
    private static readonly Dictionary<string, object?> Metrics = [];
    private static void Check(string name, bool outcome, object? actual = null)
    {
        Checks.Add(new { Name = name, Passed = outcome, Actual = actual });
        if (!outcome) throw new InvalidOperationException($"Probe failed: {name}; actual={actual}");
    }
    private static void Near(string name, float actual, float expected, float tolerance)
        => Check(name, float.IsFinite(actual) && MathF.Abs(actual - expected) <= tolerance, new { actual, expected, tolerance });
    private static void Reject<T>(string name, Action action) where T : Exception
    {
        try { action(); } catch (T) { Check(name, true); return; }
        Check(name, false, $"Expected {typeof(T).Name}");
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Usage: KtonResearch <report.json>"); return 2; }
        var watch = Stopwatch.StartNew(); string? failure = null;
        try
        {
            Check("runtime-major-11", Environment.Version.Major == 11, Environment.Version.ToString());
            Check("windows-x64-profile", OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64);
            var cold = Stopwatch.StartNew();
            using (var world = ResearchWorldFactory.Create(new(0, -9.81f, 0)))
            {
                world.Dynamic(new Box(1, 1, 1), new(0, 5, 0)); world.StaticBox(new(0, -0.5f, 0), new(100, 1, 100)); world.Step();
            }
            Metrics["ColdCreateFirstStepDisposeMilliseconds"] = cold.Elapsed.TotalMilliseconds;
            IsolationAndForces(); GeometryAndSleep(); QueriesAndFilters(); Joints(); Limits(); TimingAndCleanup(); Ccd();
            Check("all-world-owners-released", ProbeWorld.LiveOwners == 0, ProbeWorld.LiveOwners);
            Check("all-owned-pools-cleared", ProbeWorld.MaximumBytesAfterCleanup == 0, ProbeWorld.MaximumBytesAfterCleanup);
            Check("sampled-per-world-pools-within-research-budget", ProbeWorld.MaximumPoolBytes <= ProbeWorld.PoolBudgetBytes, ProbeWorld.MaximumPoolBytes);
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            var assemblies = new[] { typeof(Simulation).Assembly, typeof(BepuUtilities.Vector3Wide).Assembly }.Select(a => new
            {
                a.GetName().Name, Version = a.GetName().Version?.ToString(),
                InformationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Path = a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))
            }).ToArray();
            var report = new
            {
                Completed = failure == null, Utc = DateTimeOffset.UtcNow, Failure = failure,
                CheckCount = Checks.Count, Checks, Metrics, ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
                Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
                VectorFloatCount = Vector<float>.Count, Vector.IsHardwareAccelerated, Avx2 = Avx2.IsSupported,
                ServerGC = GCSettings.IsServerGC, Assemblies = assemblies, LiveOwners = ProbeWorld.LiveOwners,
                MaximumOwnedPoolBytes = ProbeWorld.MaximumPoolBytes, MaximumBytesAfterCleanup = ProbeWorld.MaximumBytesAfterCleanup,
                ResearchProfile = new { FixedDeltaSeconds = ProbeWorld.Delta, Workers = 1, SolverIterations = 8, SolverSubsteps = 4,
                    CollisionSteps = 1, ContactFrequency = 120, DampingRatio = 1, Friction = 0.8, MaximumRecoveryVelocity = 2,
                    BodyLimit = ProbeWorld.MaximumBodies, ShapeLimit = 2048, ContactSampleLimit = 8192, QueryResults = "nearest only",
                    SampledPerWorldPoolBudgetBytes = ProbeWorld.PoolBudgetBytes, WarmP99DiagnosticBudgetMs = 1,
                    BudgetScope = "Research fixture only; sampled BEPU pool budget, not a hard total-memory cap or product frame budget." },
                Scope = "PHYS0 low-level single-worker Windows x64 research; not production API/App/ECS, platform acceptance, hard RAM cap or formal benchmark."
            };
            File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Completed={failure == null}, checks={Checks.Count}, owners={ProbeWorld.LiveOwners}");
        }
        return failure == null ? 0 : 1;
    }

    private static void IsolationAndForces()
    {
        using var a = ResearchWorldFactory.Create(new(0, -9.81f, 0));
        using var b = ResearchWorldFactory.Create(Vector3.Zero);
        var falling = a.Dynamic(new Sphere(0.5f), new(0, 10, 0));
        var stationary = b.Dynamic(new Sphere(0.5f), new(0, 10, 0));
        a.Step(30); b.Step(30);
        Check("gravity-affects-first-world", a.Position(falling).Y < 9);
        Near("second-world-isolated", b.Position(stationary).Y, 10, 1e-5f);
        var impulse = b.Dynamic(new Sphere(0.5f), new(10, 0, 0), 2);
        b.Impulse(impulse, new(4, 0, 0));
        Near("impulse-obeys-mass", b.Velocity(impulse).X, 2, 1e-5f);
        var force = b.Dynamic(new Sphere(0.5f), new(20, 0, 0), 2);
        for (var i = 0; i < 60; i++) { b.ForceForNextStep(force, new(2, 0, 0)); b.Step(); }
        Near("force-integrated-as-impulse-per-step", b.Velocity(force).X, 1, 1e-4f);
        var kine = a.Kinematic(new(30, 0, 0), new(3, 0, 0)); a.Step(60);
        Near("kinematic-linear-motion", a.Position(kine).X, 33, 1e-3f);
        Near("kinematic-not-gravity-integrated", a.Position(kine).Y, 0, 1e-5f);
        a.Remove(kine); Check("removed-body-does-not-exist", !a.Simulation.Bodies[kine].Exists);
    }

    private static void GeometryAndSleep()
    {
        using var world = ResearchWorldFactory.Create(new(0, -9.81f, 0));
        world.MeshFloor();
        var box = world.Dynamic(new Box(1, 1, 1), new(-2, 5, 0));
        var capsule = world.Dynamic(new Capsule(0.5f, 1), new(2, 5, 0));
        world.Step(600);
        Near("box-rests-on-static-mesh", world.Position(box).Y, 0.5f, 0.04f);
        Near("capsule-half-length-convention", world.Position(capsule).Y, 1, 0.06f);
        Check("resting-box-sleeps", !world.Simulation.Bodies[box].Awake);
        var before = world.Position(box); world.Impulse(box, new(0, 4, 0)); world.Step(5);
        Check("impulse-wakes-sleeping-box", world.Simulation.Bodies[box].Awake && world.Position(box).Y > before.Y + 0.1f);
        var shared = world.Shape(new Sphere(0.2f)); var inertia = new Sphere(0.2f).ComputeInertia(1);
        var first = world.SharedDynamic(shared, inertia, new(5, 2, 0));
        var second = world.SharedDynamic(shared, inertia, new(6, 2, 0));
        world.Remove(first); world.Step(30);
        Check("shared-shape-survives-body-removal", world.Simulation.Bodies[second].Exists && ProbeWorld.Finite(world.Position(second)));
    }

    private static void QueriesAndFilters()
    {
        using (var world = ResearchWorldFactory.Create(Vector3.Zero))
        {
            var body = world.Dynamic(new Box(2, 2, 2), Vector3.Zero);
            world.StaticBox(new(0, -5, 0), new(20, 1, 20)); world.Step();
            var ray = new NearestRay(); world.Simulation.RayCast(new(0, 5, 0), -Vector3.UnitY, 20, world.Pool, ref ray);
            Check("ray-hits-nearest-body", ray.Hit && ray.Collidable.Mobility == CollidableMobility.Dynamic && ray.Collidable.BodyHandle.Value == body.Value);
            Near("ray-distance", ray.T, 4, 1e-4f);
            var miss = new NearestRay(); world.Simulation.RayCast(new(50, 5, 0), Vector3.UnitY, 10, world.Pool, ref miss);
            Check("ray-miss", !miss.Hit);
            var filtered = new NearestRay { OnlyBody = body.Value }; world.Simulation.RayCast(new(0, -2, 0), -Vector3.UnitY, 20, world.Pool, ref filtered);
            Check("ray-filter-excludes-floor", !filtered.Hit);
            var sweep = new SweepHits(); world.Simulation.Sweep(new Sphere(0.5f), new RigidPose(new(0, 5, 0)), new BodyVelocity(-Vector3.UnitY), 10, world.Pool, ref sweep);
            Check("sweep-hit", sweep.Hit); Near("sweep-to-box-distance", sweep.T, 3.5f, 0.01f);
            var overlap = new SweepHits(); world.Simulation.Sweep(new Sphere(0.25f), new RigidPose(Vector3.Zero), new BodyVelocity(), 1, world.Pool, ref overlap);
            Check("stationary-sweep-detects-overlap", overlap.Hit && overlap.ZeroCount > 0);
            var disjoint = new SweepHits(); world.Simulation.Sweep(new Sphere(0.5f), new RigidPose(new(1.4f, 1.4f, 0)), new BodyVelocity(), 1, world.Pool, ref disjoint);
            Check("overlap-narrowphase-rejects-aabb-corner", !disjoint.Hit);
        }
        using (var world = ResearchWorldFactory.Create(new(0, -9.81f, 0)))
        {
            world.StaticBox(new(0, -0.5f, 0), new(20, 1, 20));
            var body = world.Dynamic(new Sphere(0.5f), new(0, 2, 0));
            var sawContact = false;
            for (var i = 0; i < 100; i++) { world.Step(); sawContact |= world.ContactBuffer.Count > 0; }
            Check("raw-contact-manifolds-observed", sawContact);
            world.ContactBuffer.SensorBody = body.Value;
            world.Impulse(body, new(0, -2, 0)); var sawSensor = false;
            for (var i = 0; i < 90; i++) { world.Step(); sawSensor |= world.ContactBuffer.Count > 0; }
            Check("sensor-records-manifold", sawSensor);
            Check("sensor-suppresses-response", world.Position(body).Y < -2);
        }
        using (var world = ResearchWorldFactory.Create(Vector3.Zero))
        {
            var a = world.Dynamic(new Sphere(1), Vector3.Zero);
            world.Dynamic(new Sphere(1), new(0.5f, 0, 0));
            world.ContactBuffer.ExcludedBody = a.Value; world.Step(30);
            Near("collision-filter-prevents-separation", world.Position(a).X, 0, 1e-6f);
            Check("filtered-pair-has-no-manifold", world.ContactBuffer.Count == 0);
        }
    }

    private static (float FirstX, float MaximumX, float FinalX) Projectile(bool continuous, float tolerance = 1e-6f, int substeps = 4, int collisionSteps = 1, float margin = float.MaxValue, float frequency = 600)
    {
        using var world = ResearchWorldFactory.Create(Vector3.Zero, substeps, collisionSteps, frequency);
        world.StaticBox(Vector3.Zero, new(0.04f, 10, 10));
        var bullet = world.Dynamic(new Sphere(0.1f), new(-5, 0, 0), velocity: new(600, 0, 0), ccd: continuous, ccdTimeTolerance: tolerance, maximumSpeculativeMargin: margin);
        world.Step(); var first = world.Position(bullet).X; var maximum = first;
        for (var i = 0; i < 60; i++) { world.Step(); maximum = Math.Max(maximum, world.Position(bullet).X); }
        return (first, maximum, world.Position(bullet).X);
    }
    private static void Ccd()
    {
        var passive = Projectile(false, margin: 0.01f);
        var capped = Projectile(true, margin: 0.01f); var unbounded = Projectile(true);
        var speculativeOnly = Projectile(false); var soft = Projectile(true, margin: 0.01f, frequency: 120);
        static object Result((float FirstX, float MaximumX, float FinalX) result)
            => new { result.FirstX, result.MaximumX, result.FinalX };
        Metrics["CCD"] = new {
            PassiveCappedNegativeControl = Result(passive), ContinuousCapped = Result(capped), ContinuousUnbounded = Result(unbounded),
            PassiveUnbounded = Result(speculativeOnly), SoftContinuousKnownLimitation = Result(soft),
            Speed = 600, SphereRadius = 0.1, StartX = -5, WallThickness = 0.04, FollowupTicks = 60,
            TimeToleranceSeconds = 1e-6, CappedMargin = 0.01, SolverIterations = 8, SolverSubsteps = 4, CollisionSteps = 1,
            AcceptedContactFrequency = 600, SoftContactFrequency = 120, SurfaceCenterX = -0.12, PositionTolerance = 0.005
        };
        Check("passive-capped-thin-wall-negative-control", passive.FirstX > 1, passive.FirstX);
        Near("continuous-capped-first-impact", capped.FirstX, -0.12f, 0.005f);
        Check("continuous-capped-never-crosses-wall-in-61-ticks", capped.MaximumX <= -0.115f, capped.MaximumX);
        Check("continuous-capped-post-impact-finite-on-near-side", float.IsFinite(capped.FinalX) && capped.FinalX < -0.115f && capped.FinalX > -3, capped.FinalX);
        Near("continuous-unbounded-first-impact", unbounded.FirstX, -0.12f, 0.005f);
        Check("continuous-unbounded-never-crosses-wall-in-61-ticks", unbounded.MaximumX <= -0.115f, unbounded.MaximumX);
        Check("passive-unbounded-speculative-contacts-stop-projectile", speculativeOnly.MaximumX <= -0.115f, speculativeOnly.MaximumX);
        Check("soft-contact-diagnostic-finite", float.IsFinite(soft.MaximumX), soft.MaximumX);
    }
    private static void Joints()
    {
        using var world = ResearchWorldFactory.Create(new(0, -9.81f, 0));
        var anchor = world.Kinematic(new(0, 5, 0), Vector3.Zero);
        var link = world.Dynamic(new Sphere(0.2f), new(0, 4, 0));
        var joint = world.Simulation.Solver.Add(anchor, link, new BallSocket { LocalOffsetA = new(0, -0.5f, 0), LocalOffsetB = new(0, 0.5f, 0), SpringSettings = new(30, 1) });
        world.Impulse(link, new(2, 0, 0)); world.Step(300);
        var a = world.Simulation.Bodies[anchor].Pose; var b = world.Simulation.Bodies[link].Pose;
        var error = Vector3.Distance(a.Position + Vector3.Transform(new Vector3(0, -0.5f, 0), a.Orientation), b.Position + Vector3.Transform(new Vector3(0, 0.5f, 0), b.Orientation));
        Check("ball-socket-retains-connection", error < 0.04f, error);
        world.Simulation.Solver.Remove(joint); world.Remove(link); world.Remove(anchor);
        Check("joint-body-removal", !world.Simulation.Bodies[link].Exists && !world.Simulation.Bodies[anchor].Exists);
    }
    private static void Limits()
    {
        Reject<ArgumentOutOfRangeException>("invalid-world-preset-rejected", () => ResearchWorldFactory.Create(Vector3.Zero, substeps: 0));
        Check("failed-world-preset-does-not-acquire-owner", ProbeWorld.LiveOwners == 0);
        var world = ResearchWorldFactory.Create(Vector3.Zero);
        try
        {
            Reject<ArgumentOutOfRangeException>("invalid-mass-rejected", () => world.Dynamic(new Sphere(1), Vector3.Zero, 0));
            Reject<ArgumentOutOfRangeException>("nonfinite-pose-rejected", () => world.Dynamic(new Sphere(1), new(float.NaN, 0, 0)));
            var shape = world.Shape(new Sphere(0.1f)); var inertia = new Sphere(0.1f).ComputeInertia(1);
            for (var i = 0; i < ProbeWorld.MaximumBodies; i++) world.SharedDynamic(shape, inertia, new(i * 2, 0, 0));
            Reject<InvalidOperationException>("body-capacity-rejected-before-add", () => world.SharedDynamic(shape, inertia, Vector3.Zero));
            Check("capacity-unchanged-after-rejection", world.Simulation.Bodies.ActiveSet.Count == ProbeWorld.MaximumBodies);
        }
        finally { world.Dispose(); }
        world.Dispose(); Check("dispose-idempotent", ProbeWorld.LiveOwners == 0);
        Reject<ObjectDisposedException>("step-after-dispose-rejected", () => world.Step());
    }
    private static void TimingAndCleanup()
    {
        using (var world = ResearchWorldFactory.Create(new(0, -9.81f, 0)))
        {
            world.StaticBox(new(0, -0.5f, 0), new(100, 1, 100));
            var shape = world.Shape(new Sphere(0.4f)); var inertia = new Sphere(0.4f).ComputeInertia(1);
            var bodies = new BodyHandle[100];
            for (var i = 0; i < bodies.Length; i++) bodies[i] = world.SharedDynamic(shape, inertia, new((i % 10) * 1.1f, 5 + i / 10, 0));
            world.Step(120);
            foreach (var body in bodies) world.Simulation.Bodies[body].Activity.SleepThreshold = -1;
            var samples = new double[600];
            var allocated = GC.GetTotalAllocatedBytes(true);
            for (var i = 0; i < samples.Length; i++)
            {
                var start = Stopwatch.GetTimestamp(); world.Step(); samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            allocated = GC.GetTotalAllocatedBytes(true) - allocated;
            Array.Sort(samples);
            Metrics["Warm100ActiveBodies"] = new { WarmupSteps = 120, Steps = samples.Length, P50Ms = samples[299], P95Ms = samples[569], P99Ms = samples[593], MaxMs = samples[^1], AllocatedManagedBytes = allocated,
                P99DiagnosticBudgetMs = 1, WithinDiagnosticBudget = samples[593] <= 1,
                MeasurementScope = "Whole research Step including callback reset and pool counters; no ECS. Timing budget is diagnostic, not a hardware-independent gate." };
            Check("warm-workload-finite", bodies.All(b => ProbeWorld.Finite(world.Position(b))));
            Check("warm-workload-active", world.Simulation.Bodies.ActiveSet.Count == 100, world.Simulation.Bodies.ActiveSet.Count);
        }
        var samplesAfterCleanup = new List<ulong>();
        for (var cycle = 0; cycle < 64; cycle++)
        {
            using (var world = ResearchWorldFactory.Create(new(0, -9.81f, 0)))
            {
                world.MeshFloor(); var body = world.Dynamic(new Capsule(0.3f, 0.6f), new(0, 3, 0)); world.Step(8); world.Remove(body);
            }
            samplesAfterCleanup.Add(ProbeWorld.MaximumBytesAfterCleanup);
            Check($"cleanup-cycle-{cycle}", ProbeWorld.LiveOwners == 0 && ProbeWorld.MaximumBytesAfterCleanup == 0);
        }
        Metrics["Cleanup"] = new { Cycles = 64, MaximumOwnedPoolBytes = ProbeWorld.MaximumPoolBytes, MaximumBytesAfterCleanup = samplesAfterCleanup.Max(), LeakProofBoundary = "Owned BEPU pool counters and owner count only; not proof against every CLR/process allocation leak." };
    }
}
