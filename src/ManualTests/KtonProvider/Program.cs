using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace KtonProviderProbe;

internal static partial class Program
{
    private static readonly List<object> Checks = [];
    private static readonly Dictionary<string, object> Metrics = [];
    private static void Check(string name, bool outcome, object? actual = null)
    {
        Checks.Add(new { Name = name, Passed = outcome, Actual = actual });
        if (!outcome) throw new InvalidOperationException($"{name}: {actual}");
    }
    private static void Near(string name, float actual, float expected, float tolerance = 1e-4f)
        => Check(name, float.IsFinite(actual) && MathF.Abs(actual - expected) <= tolerance, new { actual, expected, tolerance });
    private static void Reject(string name, PhysicsErrorCode code, Action operation)
    {
        try { operation(); } catch (PhysicsException error) { Check(name, error.Error.Code == code, error.Error); return; }
        Check(name, false, "No rejection");
    }
    private static PhysicsWorldOptions Options(Vector3? gravity = null, PhysicsLimits? limits = null) => new() { Gravity = gravity ?? Vector3.Zero, Limits = limits ?? new() };
    private static PhysicsBodyId Ball(IPhysicsWorld world, Vector3 position, float mass = 1, Vector3 velocity = default)
        => world.CreateBody(new(world.CreateShape(new PhysicsSphere(0.2f)), PhysicsBodyMotion.Dynamic, new(position, Quaternion.Identity)) { Mass = mass, LinearVelocity = velocity });
    private static PhysicsBodyId Floor(IPhysicsWorld world) => world.CreateBody(new(world.CreateShape(new PhysicsBox(new(100, 1, 100))), PhysicsBodyMotion.Static,
        new(new(0, -0.5f, 0), Quaternion.Identity)) { Mass = 0 });
    private static void Step(IPhysicsWorld world, int count) { for (var i = 0; i < count; i++) world.Step(world.LastCompletedTick + 1); }
    private static void Clean(string name, BepuPhysicsWorldFactory factory)
        => Check(name, factory.Resources.ActiveResourceOwners == 0 && factory.Resources.PublishedWorlds == 0 && factory.Resources.MaximumPoolBytesAfterCleanup == 0 && !factory.Resources.PendingFailedCreationCleanup, factory.Resources);
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2; string? failure = null;
        try { IsolationAndIds(); Commands(); GeometryAndJoints(); CapacityAndRetirement(); FailureAndCleanup(); StressAndCcd(); QueryAcceptance(); ContactAcceptance(); }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            var assemblies = new[] { typeof(IPhysicsWorld).Assembly, typeof(BepuPhysicsWorldFactory).Assembly, typeof(IFactory<>).Assembly,
                typeof(BepuPhysics.Simulation).Assembly, typeof(BepuUtilities.Vector3Wide).Assembly }.Select(a => new {
                    Name = a.GetName().Name, Path = a.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))),
                    InformationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    References = a.GetReferencedAssemblies().Select(r => r.Name).Order().ToArray()
                }).ToArray();
            File.WriteAllText(args[0], JsonSerializer.Serialize(new { Completed = failure is null, Failure = failure, Utc = DateTimeOffset.UtcNow,
                CheckCount = Checks.Count, Checks, Metrics, Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Assemblies = assemblies,
                Scope = "PHYS2/3 real BEPU simulation, bounded queries and contacts; one worker Windows x64. Not App/ECS/platform acceptance." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"Completed={failure is null}; checks={Checks.Count}"); return failure is null ? 0 : 1;
    }
    private static void IsolationAndIds()
    {
        using var factory = new BepuPhysicsWorldFactory(); IFactory<IPhysicsWorld, PhysicsWorldOptions> typed = factory;
        Reject("invalid-options-before-resources", PhysicsErrorCode.UnsupportedProfile, () => typed.Create(new() { Quality = (PhysicsQualityProfile)99 }));
        Clean("invalid-creation-no-owner", factory);
        using (var a = typed.Create(Options(new(0, -9.81f, 0))))
        using (var b = typed.Create(Options()))
        {
            Check("independent-world-incarnations", a.Id != b.Id);
            Check("explicit-provider-capabilities", a.Capabilities == (PhysicsWorldCapabilities.RigidBodySimulation | PhysicsWorldCapabilities.Queries | PhysicsWorldCapabilities.ContactEvents | PhysicsWorldCapabilities.Characters));
            var fall = Ball(a, new(0, 10, 0)); var still = Ball(b, new(0, 10, 0)); Step(a, 30); Step(b, 30);
            Check("gravity-first-world", a.GetBodyState(fall).Pose.Position.Y < 9);
            Near("second-world-not-integrated-by-first", b.GetBodyState(still).Pose.Position.Y, 10);
            Reject("foreign-live-body", PhysicsErrorCode.ForeignWorld, () => a.GetBodyState(still));
            Reject("default-body", PhysicsErrorCode.InvalidIdentifier, () => a.GetBodyState(default));
            a.RemoveBody(fall); Reject("removed-body-is-stale", PhysicsErrorCode.StaleIdentifier, () => a.GetBodyState(fall));
            var replacement = Ball(a, new(3, 0, 0));
            Check("body-slot-reused-with-new-generation", replacement.Slot == fall.Slot && replacement.Generation == fall.Generation + 1);
            Reject("reused-body-does-not-revive-old-id", PhysicsErrorCode.StaleIdentifier, () => a.GetBodyState(fall));
            Check("query-empty-world-region", !a.RaycastNearest(new(Vector3.Zero, Vector3.UnitX, 1), out _));
            Check("no-contacts-empty-snapshot", a.CopyContactEvents(new PhysicsContactEvent[1]) == 0);
        }
        Clean("two-world-resources-clean", factory);
        var callerWorld = typed.Create(Options()); var body = Ball(callerWorld, Vector3.Zero);
        factory.Dispose(); Step(callerWorld, 1); Check("factory-does-not-dispose-caller-world", callerWorld.GetBodyState(body).Body == body);
        Reject("closed-factory-rejects-create", PhysicsErrorCode.Disposed, () => typed.Create(Options()));
        callerWorld.Dispose(); callerWorld.Dispose(); Clean("caller-dispose-idempotent", factory);
        Reject("disposed-world-rejects-step", PhysicsErrorCode.Disposed, () => callerWorld.Step(2));
    }
    private static void Commands()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = factory.Create(Options()))
        {
            var body = Ball(world, Vector3.Zero, 2);
            world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, body, new(4, 0, 0))]);
            Near("impulse-mass", world.GetBodyState(body).LinearVelocity.X, 2);
            var before = world.GetBodyState(body);
            Reject("whole-batch-rejects-invalid-later-command", PhysicsErrorCode.StaleIdentifier, () => world.ApplyCommands([
                new(PhysicsCommandKind.SetVelocity, body, new(3, 0, 0)), new(PhysicsCommandKind.Wake, body with { Generation = body.Generation + 1 })]));
            Check("rejected-batch-no-prefix-mutation", world.GetBodyState(body) == before);
            Reject("projected-impulse-speed-rejected", PhysicsErrorCode.InvalidArgument, () => world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, body, new(200, 0, 0))]));
            Check("bad-impulse-no-mutation", world.GetBodyState(body) == before);
            world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, body)]);
            for (var i = 0; i < 60; i++) { world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearForce, body, new(2, 0, 0))]); Step(world, 1); }
            Near("force-integrated-once-per-fixed-step", world.GetBodyState(body).LinearVelocity.X, 1);
            world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearForce, body, new(2, 0, 0)), new(PhysicsCommandKind.Sleep, body)]);
            Step(world, 1); Near("sleep-clears-pending-force", world.GetBodyState(body).LinearVelocity.X, 0);
            world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearForce, body, new(2, 0, 0))]);
            var token = new CancellationToken(true); var tick = world.LastCompletedTick;
            try { world.Step(tick + 1, token); Check("cancel-before-step", false); } catch (OperationCanceledException) { Check("cancel-before-step", true); }
            Check("cancel-keeps-ready-tick", world.State == PhysicsWorldState.Ready && world.LastCompletedTick == tick);
            Step(world, 1); Near("cancel-retains-force-for-one-retry", world.GetBodyState(body).LinearVelocity.X, 1f / 60);
            Reject("repeat-tick-rejected", PhysicsErrorCode.InvalidTick, () => world.Step(world.LastCompletedTick));
            Reject("skipped-tick-rejected", PhysicsErrorCode.InvalidTick, () => world.Step(world.LastCompletedTick + 2));
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: new(new(5, 2, 0), Quaternion.Identity))]);
            Near("teleport-updates-pose", world.GetBodyState(body).Pose.Position.X, 5);
            Near("teleport-preserves-velocity", world.GetBodyState(body).LinearVelocity.X, 1f / 60);
            var shape = world.CreateShape(new PhysicsBox(Vector3.One));
            var kine = world.CreateBody(new(shape, PhysicsBodyMotion.Kinematic, new(new(20, 0, 0), Quaternion.Identity)) { Mass = 0, LinearVelocity = new(3, 0, 0) });
            Step(world, 60); Near("kinematic-motion", world.GetBodyState(kine).Pose.Position.X, 23, 0.001f);
            Reject("kinematic-force-rejected", PhysicsErrorCode.InvalidArgument, () => world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearForce, kine, Vector3.UnitY)]));
        }
        using (var world = factory.Create(Options(limits: new() { MaxCommandsPerTick = 2 })))
        {
            var body = Ball(world, Vector3.Zero); world.ApplyCommands([new(PhysicsCommandKind.Wake, body), new(PhysicsCommandKind.Wake, body)]);
            Reject("command-budget-cumulative", PhysicsErrorCode.CapacityExceeded, () => world.ApplyCommands([new(PhysicsCommandKind.Wake, body)]));
            Step(world, 1); world.ApplyCommands([new(PhysicsCommandKind.Wake, body)]); Check("step-resets-command-budget", world.State == PhysicsWorldState.Ready);
        }
        Clean("command-worlds-clean", factory);
    }
}
