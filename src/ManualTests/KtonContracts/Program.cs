using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace KtonContracts;

internal static class Program
{
    private static readonly List<object> Outcomes = [];
    private static readonly PhysicsWorldId World = new(new Guid("f5ec37db-f490-4676-b991-3a7e6c563c51"));
    private static readonly PhysicsWorldId Other = new(new Guid("981d5988-6f4c-419d-aed7-fde80e10d8f8"));
    private static readonly PhysicsShapeId Shape = new(World, 0, 1);
    private static readonly PhysicsBodyId Body = new(World, 0, 1);
    private static readonly PhysicsWorldOptions Options = new();
    private static void Check(string name, bool passed)
    {
        Outcomes.Add(new { Name = name, Passed = passed });
        if (!passed) throw new InvalidOperationException(name);
    }
    private static void Error(string name, PhysicsError actual, PhysicsErrorCode expected)
        => Check(name, actual.Code == expected && (expected == PhysicsErrorCode.None || !string.IsNullOrWhiteSpace(actual.Field)));
    private static void Ok(string name, PhysicsError actual) => Error(name, actual, PhysicsErrorCode.None);
    private static void Bad(string name, PhysicsError actual) => Error(name, actual, PhysicsErrorCode.InvalidArgument);

    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        string? failure = null;
        var assembly = typeof(IPhysicsWorld).Assembly;
        try { Identities(); Profiles(); Geometry(); Bodies(); CommandsAndQueries(); Boundary(assembly); }
        catch (Exception e) { failure = e.ToString(); Console.Error.WriteLine(e); }
        finally
        {
            var report = new {
                Completed = failure is null, Failure = failure, Utc = DateTimeOffset.UtcNow,
                CheckCount = Outcomes.Count, Outcomes, Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Assembly = new { Name = assembly.GetName().Name, Path = assembly.Location,
                    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                    References = assembly.GetReferencedAssemblies().Select(a => a.Name).Order().ToArray() },
                ProviderOperationsExecuted = false,
                Scope = "PHYS1 neutral contracts and real validation; no fake world/provider, simulation, App/DI/ECS or query/event runtime acceptance."
            };
            File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"Completed={failure is null}; checks={Outcomes.Count}");
        return failure is null ? 0 : 1;
    }

    private static void Identities()
    {
        Check("default-world-invalid", !default(PhysicsWorldId).IsValid);
        Check("default-body-invalid", !default(PhysicsBodyId).IsValid);
        Check("default-shape-invalid", !default(PhysicsShapeId).IsValid);
        Check("default-joint-invalid", !default(PhysicsJointId).IsValid);
        Check("world-incarnations-distinct", World != Other);
        Ok("live-body", PhysicsIdentity.Validate(World, Body, Body));
        Error("default-body-rejected", PhysicsIdentity.Validate(World, default, Body), PhysicsErrorCode.InvalidIdentifier);
        Error("zero-generation-rejected", PhysicsIdentity.Validate(World, new PhysicsBodyId(World, 0, 0), Body), PhysicsErrorCode.InvalidIdentifier);
        Error("negative-slot-rejected", PhysicsIdentity.Validate(World, new PhysicsBodyId(World, -1, 1), Body), PhysicsErrorCode.InvalidIdentifier);
        Error("foreign-world-precedes-liveness", PhysicsIdentity.Validate(World, new PhysicsBodyId(Other, 0, 1), default), PhysicsErrorCode.ForeignWorld);
        Error("stale-body-after-slot-reuse", PhysicsIdentity.Validate(World, Body, Body with { Generation = 2 }), PhysicsErrorCode.StaleIdentifier);
        Error("removed-body", PhysicsIdentity.Validate(World, Body, default), PhysicsErrorCode.StaleIdentifier);
        Error("different-live-slot", PhysicsIdentity.Validate(World, Body, Body with { Slot = 1 }), PhysicsErrorCode.StaleIdentifier);
        var last = new PhysicsBodyId(World, 0, uint.MaxValue);
        Ok("last-generation-is-structurally-live", PhysicsIdentity.Validate(World, last, last));
        Error("retired-slot-is-stale", PhysicsIdentity.Validate(World, last, default), PhysicsErrorCode.StaleIdentifier);
        Ok("live-shape", PhysicsIdentity.Validate(World, Shape, Shape));
        Error("stale-shape", PhysicsIdentity.Validate(World, Shape, Shape with { Generation = 2 }), PhysicsErrorCode.StaleIdentifier);
        Error("foreign-shape", PhysicsIdentity.Validate(Other, Shape, Shape), PhysicsErrorCode.ForeignWorld);
        var joint = new PhysicsJointId(World, 0, 1);
        Ok("live-joint", PhysicsIdentity.Validate(World, joint, joint));
        Error("removed-joint", PhysicsIdentity.Validate(World, joint, default), PhysicsErrorCode.StaleIdentifier);
        Error("foreign-joint", PhysicsIdentity.Validate(Other, joint, joint), PhysicsErrorCode.ForeignWorld);
        var typed = new PhysicsError(PhysicsErrorCode.CapacityExceeded, "Bodies");
        try { typed.ThrowIfFailure(); Check("typed-exception-thrown", false); }
        catch (PhysicsException e) { Check("typed-error-preserved", e.Error == typed); }
        default(PhysicsError).ThrowIfFailure(); Check("success-does-not-throw", true);
        try { _ = new PhysicsException(default); Check("success-exception-rejected", false); }
        catch (ArgumentException) { Check("success-exception-rejected", true); }
    }

    private static void Profiles()
    {
        Ok("default-options", PhysicsValidation.Validate(Options));
        Bad("null-options", PhysicsValidation.Validate((PhysicsWorldOptions?)null));
        Bad("null-limits", PhysicsValidation.Validate(Options with { Limits = null! }));
        Error("unknown-quality", PhysicsValidation.Validate(Options with { Quality = (PhysicsQualityProfile)42 }), PhysicsErrorCode.UnsupportedProfile);
        Bad("nan-gravity", PhysicsValidation.Validate(Options with { Gravity = new(float.NaN, 0, 0) }));
        Bad("infinite-diagnostic-time", PhysicsValidation.Validate(Options with { StepDiagnosticBudgetMilliseconds = double.PositiveInfinity }));
        Bad("zero-diagnostic-time", PhysicsValidation.Validate(Options with { StepDiagnosticBudgetMilliseconds = 0 }));
        var baseline = PhysicsProfileSettings.For(PhysicsQualityProfile.Baseline60);
        var fast = PhysicsProfileSettings.For(PhysicsQualityProfile.FastLinear60);
        Check("baseline-matches-phys0", baseline.FixedDeltaSeconds == 1f / 60 && baseline.SolverIterations == 8 && baseline.SolverSubsteps == 4
            && baseline.CollisionSteps == 1 && baseline.Workers == 1 && baseline.CcdMode == PhysicsCcdMode.Speculative
            && baseline.ContactSpringFrequency == 120 && baseline.MaximumSpeculativeMargin == float.MaxValue);
        Check("fast-avoids-known-soft-ccd-combination", fast.CcdMode == PhysicsCcdMode.Swept && fast.ContactSpringFrequency == 600
            && fast.ContactDampingRatio == 1 && fast.MaximumRecoveryVelocity == 2 && fast.MaximumSpeculativeMargin == 0.01f
            && fast.MinimumSweepTimestep == 1e-6f && fast.SweepConvergenceThreshold == 1e-6f && fast.MaximumLinearSpeed == 600);
        Check("profile-has-no-public-tuning-setters", typeof(PhysicsProfileSettings).GetProperties().All(p => p.SetMethod is null));
        try { PhysicsProfileSettings.For((PhysicsQualityProfile)(-1)); Check("unknown-profile-resolution-rejected", false); }
        catch (PhysicsException e) { Check("unknown-profile-resolution-rejected", e.Error.Code == PhysicsErrorCode.UnsupportedProfile); }
        var invalid = new[] {
            new PhysicsLimits { MaxBodies = 0 }, new PhysicsLimits { MaxBodies = 1025 }, new PhysicsLimits { MaxShapes = 2049 },
            new PhysicsLimits { MaxJoints = -1 }, new PhysicsLimits { MaxJoints = 4097 }, new PhysicsLimits { MaxCommandsPerTick = 4097 },
            new PhysicsLimits { MaxContactEvents = 8193 }, new PhysicsLimits { MaxQueryHits = 257 }, new PhysicsLimits { MaxQueryScratchBytes = 0 },
            new PhysicsLimits { MaxMeshVertices = 2 }, new PhysicsLimits { MaxMeshTriangles = 65537 }, new PhysicsLimits { MaxMeshInputBytes = 47 },
            new PhysicsLimits { PoolDiagnosticBudgetBytes = 0 }, new PhysicsLimits { PoolDiagnosticBudgetBytes = long.MaxValue }
        };
        for (var i = 0; i < invalid.Length; i++) Bad($"invalid-limit-{i}", PhysicsValidation.Validate(invalid[i]));
        Ok("reduced-limits", PhysicsValidation.Validate(new PhysicsLimits { MaxBodies = 8, MaxShapes = 8, MaxQueryHits = 1 }));
        Ok("admission-at-capacity", PhysicsValidation.ValidateCapacity(1023, 1, 1024, "Bodies"));
        Error("admission-over-capacity", PhysicsValidation.ValidateCapacity(1024, 1, 1024, "Bodies"), PhysicsErrorCode.CapacityExceeded);
        Error("capacity-sum-does-not-wrap", PhysicsValidation.ValidateCapacity(int.MaxValue, int.MaxValue, int.MaxValue, "Bodies"), PhysicsErrorCode.CapacityExceeded);
        Bad("negative-capacity-input", PhysicsValidation.ValidateCapacity(-1, 1, 10, "Bodies"));
        Ok("first-tick", PhysicsValidation.ValidateTick(0, 1));
        Error("repeated-tick", PhysicsValidation.ValidateTick(1, 1), PhysicsErrorCode.InvalidTick);
        Error("skipped-tick", PhysicsValidation.ValidateTick(1, 3), PhysicsErrorCode.InvalidTick);
        Error("tick-does-not-wrap", PhysicsValidation.ValidateTick(ulong.MaxValue, 0), PhysicsErrorCode.InvalidTick);
    }

    private static PhysicsStaticMesh Mesh() => new(new Vector3[] { new(-1, 0, -1), new(1, 0, -1), new(-1, 0, 1) }, new[] { 0, 1, 2 });
    private sealed record UnknownGeometry : PhysicsShapeDefinition;
    private static void Geometry()
    {
        Ok("identity-pose", PhysicsValidation.Validate(PhysicsPose.Identity));
        Bad("default-quaternion", PhysicsValidation.Validate(default(PhysicsPose)));
        Bad("scaled-quaternion-not-normalized", PhysicsValidation.Validate(new PhysicsPose(Vector3.Zero, new(0, 0, 0, 2))));
        Bad("nonfinite-quaternion", PhysicsValidation.Validate(new PhysicsPose(Vector3.Zero, new(0, 0, 0, float.NaN))));
        Bad("unbounded-position", PhysicsValidation.Validate(new PhysicsPose(new(10001, 0, 0), Quaternion.Identity)));
        Ok("sphere", PhysicsValidation.ValidateShape(new PhysicsSphere(0.5f), Options.Limits));
        Ok("box-full-sizes", PhysicsValidation.ValidateShape(new PhysicsBox(new(1, 2, 3)), Options.Limits));
        Ok("capsule-full-cylinder", PhysicsValidation.ValidateShape(new PhysicsCapsule(0.5f, 1), Options.Limits));
        Bad("zero-sphere", PhysicsValidation.ValidateShape(new PhysicsSphere(0), Options.Limits));
        Bad("nan-sphere", PhysicsValidation.ValidateShape(new PhysicsSphere(float.NaN), Options.Limits));
        Bad("negative-box", PhysicsValidation.ValidateShape(new PhysicsBox(new(-1, 1, 1)), Options.Limits));
        Bad("capsule-total-height", PhysicsValidation.ValidateShape(new PhysicsCapsule(400, 400), Options.Limits));
        Bad("capsule-zero-segment-use-sphere-instead", PhysicsValidation.ValidateShape(new PhysicsCapsule(1, 0), Options.Limits));
        Error("unknown-shape", PhysicsValidation.ValidateShape(new UnknownGeometry(), Options.Limits), PhysicsErrorCode.UnsupportedGeometry);
        Ok("static-mesh", PhysicsValidation.ValidateShape(Mesh(), Options.Limits));
        Bad("missing-index-triple", PhysicsValidation.ValidateShape(Mesh() with { Indices = new[] { 0, 1 } }, Options.Limits));
        Bad("negative-mesh-index", PhysicsValidation.ValidateShape(Mesh() with { Indices = new[] { 0, -1, 2 } }, Options.Limits));
        Bad("out-of-range-mesh-index", PhysicsValidation.ValidateShape(Mesh() with { Indices = new[] { 0, 1, 3 } }, Options.Limits));
        Bad("degenerate-mesh-triangle", PhysicsValidation.ValidateShape(Mesh() with { Indices = new[] { 0, 0, 2 } }, Options.Limits));
        Bad("nonfinite-mesh-vertex", PhysicsValidation.ValidateShape(Mesh() with { Vertices = new Vector3[] { new(float.PositiveInfinity, 0, 0), Vector3.UnitX, Vector3.UnitZ } }, Options.Limits));
        Error("mesh-byte-admission-before-cook", PhysicsValidation.ValidateShape(Mesh(), Options.Limits with { MaxMeshInputBytes = 48 }), PhysicsErrorCode.None);
        Error("mesh-triangle-admission", PhysicsValidation.ValidateShape(Mesh() with { Indices = new[] { 0, 1, 2, 0, 1, 2 } }, Options.Limits with { MaxMeshTriangles = 1 }), PhysicsErrorCode.CapacityExceeded);
        Error("mesh-byte-budget", PhysicsValidation.ValidateShape(Mesh() with { Indices = new[] { 0, 1, 2, 0, 1, 2 } }, Options.Limits with { MaxMeshInputBytes = 48 }), PhysicsErrorCode.CapacityExceeded);
    }

    private static void Bodies()
    {
        var body = new PhysicsBodyDescription(Shape, PhysicsBodyMotion.Dynamic, PhysicsPose.Identity);
        var sphere = new PhysicsSphere(0.5f);
        Ok("dynamic-body", PhysicsValidation.ValidateBody(Options, body, sphere));
        Ok("static-body", PhysicsValidation.ValidateBody(Options, body with { Motion = PhysicsBodyMotion.Static, Mass = 0 }, sphere));
        Ok("kinematic-body", PhysicsValidation.ValidateBody(Options, body with { Motion = PhysicsBodyMotion.Kinematic, Mass = 0, LinearVelocity = Vector3.UnitX }, sphere));
        Bad("dynamic-zero-mass", PhysicsValidation.ValidateBody(Options, body with { Mass = 0 }, sphere));
        Bad("nan-mass", PhysicsValidation.ValidateBody(Options, body with { Mass = float.NaN }, sphere));
        Bad("static-moving-rejected", PhysicsValidation.ValidateBody(Options, body with { Motion = PhysicsBodyMotion.Static, Mass = 0, LinearVelocity = Vector3.UnitX }, sphere));
        Error("dynamic-concave-mesh", PhysicsValidation.ValidateBody(Options, body, Mesh()), PhysicsErrorCode.UnsupportedGeometry);
        Error("kinematic-concave-mesh", PhysicsValidation.ValidateBody(Options, body with { Motion = PhysicsBodyMotion.Kinematic, Mass = 0 }, Mesh()), PhysicsErrorCode.UnsupportedGeometry);
        Bad("baseline-speed-envelope", PhysicsValidation.ValidateBody(Options, body with { LinearVelocity = new(100, 0, 0) }, sphere));
        Ok("fast-linear-envelope", PhysicsValidation.ValidateBody(Options with { Quality = PhysicsQualityProfile.FastLinear60 }, body with { LinearVelocity = new(600, 0, 0) }, sphere));
        Bad("fast-linear-over-envelope", PhysicsValidation.ValidateBody(Options with { Quality = PhysicsQualityProfile.FastLinear60 }, body with { LinearVelocity = new(601, 0, 0) }, sphere));
        Bad("angular-envelope", PhysicsValidation.ValidateBody(Options, body with { AngularVelocity = new(0, 65, 0) }, sphere));
        Bad("invalid-body-mode", PhysicsValidation.ValidateBody(Options, body with { Motion = (PhysicsBodyMotion)45 }, sphere));
        Bad("invalid-friction", PhysicsValidation.ValidateBody(Options, body with { Material = new(-0.1f) }, sphere));
        Bad("layer-index-not-mask", PhysicsValidation.ValidateBody(Options, body with { Filter = new(32, uint.MaxValue) }, sphere));
        Ok("mass-ratio-at-limit", PhysicsValidation.ValidateMassRatio(1, 100));
        Bad("mass-ratio-over-limit", PhysicsValidation.ValidateMassRatio(1, 101));
        Check("friction-combine-symmetric-min", new PhysicsMaterial(0.8f).CombineFriction(new(0.3f)) == 0.3f && new PhysicsMaterial(0.3f).CombineFriction(new(0.8f)) == 0.3f);
        Check("collision-masks-symmetric", new PhysicsCollisionFilter(1, 1u << 2).Allows(new(2, 1u << 1)));
        Check("one-sided-mask-insufficient", !new PhysicsCollisionFilter(1, 1u << 2).Allows(new(2, 0)));
        Check("invalid-layer-never-wraps-shift", !new PhysicsCollisionFilter(32, uint.MaxValue).Allows(PhysicsCollisionFilter.All));
        var joint = new PhysicsBallSocket(Body, new(World, 1, 1), Vector3.Zero, Vector3.UnitY);
        Ok("ball-socket-input", PhysicsValidation.ValidateJoint(World, joint));
        Bad("self-joint", PhysicsValidation.ValidateJoint(World, joint with { BodyB = Body }));
        Error("foreign-joint-body", PhysicsValidation.ValidateJoint(World, joint with { BodyB = new(Other, 1, 1) }), PhysicsErrorCode.ForeignWorld);
        Bad("nonfinite-anchor", PhysicsValidation.ValidateJoint(World, joint with { LocalAnchorA = new(float.NaN, 0, 0) }));
    }

    private static void CommandsAndQueries()
    {
        Error("empty-batch-still-needs-valid-world", PhysicsValidation.ValidateCommands(default, Options, [], 0), PhysicsErrorCode.InvalidIdentifier);
        Ok("velocity-command", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.SetVelocity, Body, Vector3.UnitX)], 0));
        Ok("force-command", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.ApplyLinearForce, Body, new(2, 0, 0))], 0));
        Ok("teleport-command", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.Teleport, Body, Pose: PhysicsPose.Identity)], 0));
        Bad("invalid-later-command-rejects-whole-validation", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.Wake, Body), new(PhysicsCommandKind.None, Body)], 0));
        Error("command-budget-cumulative", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.Wake, Body)], 4096), PhysicsErrorCode.CapacityExceeded);
        Error("foreign-command", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.Sleep, new(Other, 0, 1))], 0), PhysicsErrorCode.ForeignWorld);
        Bad("nan-force", PhysicsValidation.ValidateCommands(World, Options, [new(PhysicsCommandKind.ApplyLinearForce, Body, new(float.NaN, 0, 0))], 0));
        var ray = new PhysicsRay(new(0, 5, 0), -Vector3.UnitY, 10);
        Ok("ray-metres", PhysicsValidation.ValidateRay(World, ray));
        Bad("ray-unit-direction-required", PhysicsValidation.ValidateRay(World, ray with { Direction = new(0, -2, 0) }));
        Bad("zero-ray-distance", PhysicsValidation.ValidateRay(World, ray with { MaximumDistance = 0 }));
        Bad("nan-ray-distance", PhysicsValidation.ValidateRay(World, ray with { MaximumDistance = float.NaN }));
        Error("query-exclusion-foreign-world", PhysicsValidation.ValidateRay(World, ray with { Filter = new(uint.MaxValue, true, new(Other, 0, 1)) }), PhysicsErrorCode.ForeignWorld);
        Error("malformed-exclusion-not-treated-as-none", PhysicsValidation.ValidateRay(World, ray with { Filter = new(uint.MaxValue, true, new(World, 0, 0)) }), PhysicsErrorCode.InvalidIdentifier);
        var sweep = new PhysicsSweep(Shape, PhysicsPose.Identity, Vector3.UnitX, 1);
        Ok("convex-sweep", PhysicsValidation.ValidateSweep(World, sweep, new PhysicsSphere(1), Options.Limits));
        Error("mesh-query-shape-rejected", PhysicsValidation.ValidateSweep(World, sweep, Mesh(), Options.Limits), PhysicsErrorCode.UnsupportedGeometry);
        Error("foreign-sweep-shape", PhysicsValidation.ValidateSweep(Other, sweep, new PhysicsSphere(1), Options.Limits), PhysicsErrorCode.ForeignWorld);
        Ok("convex-overlap", PhysicsValidation.ValidateOverlap(World, new(Shape, PhysicsPose.Identity), new PhysicsBox(Vector3.One), Options.Limits));
        Bad("overlap-pose-invalid", PhysicsValidation.ValidateOverlap(World, new(Shape, default), new PhysicsSphere(1), Options.Limits));
    }

    private static void Boundary(Assembly assembly)
    {
        Check("runtime-net11", Environment.Version.Major == 11);
        Check("no-backend-or-engine-references", assembly.GetReferencedAssemblies().All(a => a.Name?.StartsWith("System.") == true || a.Name == "System"));
        Check("only-kton-public-namespaces", assembly.GetExportedTypes().All(t => t.Namespace?.StartsWith("TrueMoon.Kton", StringComparison.Ordinal) == true));
        Check("commands-are-values", !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsBodyCommand>());
        Check("hits-are-values", !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsHit>());
        Check("contact-events-are-values", !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsContactEvent>());
    }

    // Compiled against the real public interface; deliberately not executed using a fake physics provider.
    private static PhysicsStepResult CompileWorldConsumer(IPhysicsWorld world, PhysicsShapeDefinition shape)
    {
        var required = PhysicsWorldCapabilities.RigidBodySimulation | PhysicsWorldCapabilities.Queries | PhysicsWorldCapabilities.ContactEvents;
        if ((world.Capabilities & required) != required) throw new PhysicsException(new(PhysicsErrorCode.UnsupportedProfile, "Capabilities"));
        var shapeId = world.CreateShape(shape);
        var body = world.CreateBody(new(shapeId, PhysicsBodyMotion.Dynamic, PhysicsPose.Identity));
        world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, body, Vector3.UnitY)]);
        var step = world.Step(1);
        Span<PhysicsHit> hits = stackalloc PhysicsHit[8];
        world.Raycast(new(new(0, 5, 0), -Vector3.UnitY, 10), hits);
        world.Sweep(new(shapeId, PhysicsPose.Identity, Vector3.UnitX, 1), hits);
        Span<PhysicsOverlapHit> overlaps = stackalloc PhysicsOverlapHit[8];
        world.Overlap(new(shapeId, PhysicsPose.Identity), overlaps);
        var events = new PhysicsContactEvent[step.ContactEventCount]; world.CopyContactEvents(events);
        world.RemoveBody(body); world.RemoveShape(shapeId); return step;
    }
}
