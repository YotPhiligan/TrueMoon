using System.Numerics;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton;

/// <summary>Neutral, non-mutating admission checks. Providers additionally check live IDs, resource references, body modes and current budgets.</summary>
public static class PhysicsValidation
{
    /// <summary>Maximum absolute world/local coordinate in metres.</summary>
    public const float MaximumCoordinate = 10000;
    /// <summary>Minimum primitive dimension in metres.</summary>
    public const float MinimumDimension = 0.001f;
    /// <summary>Maximum primitive full dimension in metres.</summary>
    public const float MaximumDimension = 1000;
    /// <summary>Allowed squared unit quaternion/vector length error.</summary>
    public const float UnitLengthTolerance = 1e-4f;
    /// <summary>Maximum explicit angular velocity magnitude in radians/second. Angular CCD is not guaranteed.</summary>
    public const float MaximumAngularSpeed = 64;
    /// <summary>Maximum ratio of largest to smallest live dynamic mass, checked by the provider on admission.</summary>
    public const float MaximumMassRatio = 100;

    private static PhysicsError Invalid(string field) => new(PhysicsErrorCode.InvalidArgument, field);
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool Bounded(Vector3 v, float bound) => Finite(v) && MathF.Abs(v.X) <= bound && MathF.Abs(v.Y) <= bound && MathF.Abs(v.Z) <= bound;
    private static bool Speed(Vector3 v, float maximum) => Finite(v) && v.LengthSquared() <= maximum * maximum;
    private static bool Dimension(float value) => float.IsFinite(value) && value >= MinimumDimension && value <= MaximumDimension;
    private static bool Unit(Vector3 value) => Finite(value) && MathF.Abs(value.LengthSquared() - 1) <= UnitLengthTolerance;

    /// <summary>Validates known quality, finite gravity and version-one admission ceilings.</summary>
    /// <param name="options">Arguments to snapshot before resource creation.</param><returns>First input failure or success.</returns>
    public static PhysicsError Validate(PhysicsWorldOptions? options)
    {
        if (options is null) return Invalid("Options");
        if (options.Quality is not (PhysicsQualityProfile.Baseline60 or PhysicsQualityProfile.FastLinear60))
            return new(PhysicsErrorCode.UnsupportedProfile, "Quality");
        if (!Speed(options.Gravity, 1000)) return Invalid("Gravity");
        if (!double.IsFinite(options.StepDiagnosticBudgetMilliseconds) || options.StepDiagnosticBudgetMilliseconds <= 0)
            return Invalid("StepDiagnosticBudgetMilliseconds");
        return Validate(options.Limits);
    }

    /// <summary>Validates configurable counts/bytes against fixed version-one ceilings; this does not reserve or prove memory usage.</summary>
    /// <param name="limits">Non-null immutable policy.</param><returns>First invalid field.</returns>
    public static PhysicsError Validate(PhysicsLimits? limits)
    {
        if (limits is null) return Invalid("Limits");
        if (limits.MaxBodies is < 1 or > 1024) return Invalid("Limits.MaxBodies");
        if (limits.MaxShapes is < 1 or > 2048) return Invalid("Limits.MaxShapes");
        if (limits.MaxJoints is < 1 or > 4096) return Invalid("Limits.MaxJoints");
        if (limits.MaxCommandsPerTick is < 1 or > 4096) return Invalid("Limits.MaxCommandsPerTick");
        if (limits.MaxContactEvents is < 1 or > 8192) return Invalid("Limits.MaxContactEvents");
        if (limits.MaxQueryHits is < 1 or > 256) return Invalid("Limits.MaxQueryHits");
        if (limits.MaxQueryScratchBytes is < 1 or > 1024 * 1024) return Invalid("Limits.MaxQueryScratchBytes");
        if (limits.MaxMeshVertices is < 3 or > 65536) return Invalid("Limits.MaxMeshVertices");
        if (limits.MaxMeshTriangles is < 1 or > 65536) return Invalid("Limits.MaxMeshTriangles");
        if (limits.MaxMeshInputBytes is < 48 or > 16 * 1024 * 1024) return Invalid("Limits.MaxMeshInputBytes");
        if (limits.PoolDiagnosticBudgetBytes is < 1 or > 16 * 1024 * 1024) return Invalid("Limits.PoolDiagnosticBudgetBytes");
        return default;
    }

    /// <summary>Checks admission arithmetic without overflow or mutation.</summary>
    /// <param name="current">Existing count.</param><param name="additional">Requested count.</param><param name="limit">Positive ceiling.</param>
    /// <param name="field">Budget field path.</param><returns>CapacityExceeded or invalid policy, otherwise success.</returns>
    public static PhysicsError ValidateCapacity(int current, int additional, int limit, string field)
    {
        if (current < 0 || additional < 0 || limit < 1) return Invalid(field);
        return (long)current + additional > limit ? new(PhysicsErrorCode.CapacityExceeded, field) : default;
    }

    /// <summary>Checks a finite bounded pose without normalization or scale decomposition.</summary>
    /// <param name="pose">Centre-of-mass pose.</param><returns>Input failure or success.</returns>
    public static PhysicsError Validate(PhysicsPose pose)
    {
        if (!Bounded(pose.Position, MaximumCoordinate)) return Invalid("Pose.Position");
        var q = pose.Orientation;
        if (!float.IsFinite(q.X) || !float.IsFinite(q.Y) || !float.IsFinite(q.Z) || !float.IsFinite(q.W)
            || MathF.Abs(q.LengthSquared() - 1) > UnitLengthTolerance) return Invalid("Pose.Orientation");
        return default;
    }

    /// <summary>Checks primitive sizes or borrowed mesh data. Unknown derived shapes are explicitly unsupported.</summary>
    /// <param name="shape">Input definition.</param><param name="limits">Validated captured-input policy.</param><returns>Input/capacity/unsupported error or success.</returns>
    public static PhysicsError ValidateShape(PhysicsShapeDefinition? shape, PhysicsLimits limits)
    {
        var policy = Validate(limits); if (policy.IsFailure) return policy;
        switch (shape)
        {
            case null: return Invalid("Shape");
            case PhysicsSphere sphere:
                return Dimension(sphere.Radius) && sphere.Radius <= MaximumDimension / 2 ? default : Invalid("Shape.Radius");
            case PhysicsBox box:
                return Dimension(box.Size.X) && Dimension(box.Size.Y) && Dimension(box.Size.Z) ? default : Invalid("Shape.Size");
            case PhysicsCapsule capsule:
                return Dimension(capsule.Radius) && Dimension(capsule.CylinderLength) && capsule.CylinderLength + 2 * capsule.Radius <= MaximumDimension
                    ? default : Invalid("Shape.CapsuleDimensions");
            case PhysicsStaticMesh mesh:
                return ValidateMesh(mesh, limits);
            default: return new(PhysicsErrorCode.UnsupportedGeometry, "Shape.Kind");
        }
    }

    private static PhysicsError ValidateMesh(PhysicsStaticMesh mesh, PhysicsLimits limits)
    {
        var vertices = mesh.Vertices.Span; var indices = mesh.Indices.Span;
        if (vertices.Length < 3 || indices.Length < 3 || indices.Length % 3 != 0) return Invalid("Shape.MeshCounts");
        if (vertices.Length > limits.MaxMeshVertices || indices.Length / 3 > limits.MaxMeshTriangles
            || (long)vertices.Length * 12 + (long)indices.Length * 4 > limits.MaxMeshInputBytes)
            return new(PhysicsErrorCode.CapacityExceeded, "Shape.MeshInput");
        foreach (var vertex in vertices) if (!Bounded(vertex, MaximumCoordinate)) return Invalid("Shape.Vertices");
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = indices[i]; var b = indices[i + 1]; var c = indices[i + 2];
            if ((uint)a >= (uint)vertices.Length || (uint)b >= (uint)vertices.Length || (uint)c >= (uint)vertices.Length)
                return Invalid("Shape.Indices");
            if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).LengthSquared() <= 1e-12f)
                return Invalid("Shape.DegenerateTriangle");
        }
        return default;
    }

    /// <summary>Checks a body descriptor using the actual captured shape definition. Live slot checks remain mandatory at the provider boundary.</summary>
    /// <param name="options">World policy.</param><param name="body">Body input.</param><param name="shape">Actual captured shape for body.Shape.</param>
    /// <returns>Neutral admission error or success.</returns>
    public static PhysicsError ValidateBody(PhysicsWorldOptions options, PhysicsBodyDescription? body, PhysicsShapeDefinition? shape)
        => ValidateBody(options, body, shape, body?.Pose ?? default, body?.LinearVelocity ?? default, body?.AngularVelocity ?? default);

    /// <summary>Checks immutable body settings against a current motion snapshot without allocating a replacement descriptor.
    /// Performs the same complete policy, shape, mass, material and filter admission as the descriptor overload.</summary>
    /// <param name="options">World policy.</param><param name="body">Captured immutable settings, including shape identity and motion kind.</param>
    /// <param name="shape">Actual captured shape for body.Shape.</param><param name="pose">Current pose, replacing body.Pose for this check.</param>
    /// <param name="linearVelocity">Current linear velocity, replacing body.LinearVelocity.</param>
    /// <param name="angularVelocity">Current angular velocity, replacing body.AngularVelocity.</param><returns>Neutral admission error or success.</returns>
    public static PhysicsError ValidateBody(PhysicsWorldOptions options, PhysicsBodyDescription? body, PhysicsShapeDefinition? shape,
        PhysicsPose pose, Vector3 linearVelocity, Vector3 angularVelocity)
    {
        var error = Validate(options); if (error.IsFailure) return error;
        if (body is null) return Invalid("Body");
        if (!body.Shape.IsValid) return new(PhysicsErrorCode.InvalidIdentifier, "Body.Shape");
        if (!Enum.IsDefined(body.Motion)) return Invalid("Body.Motion");
        error = Validate(pose); if (error.IsFailure) return error;
        error = ValidateShape(shape, options.Limits); if (error.IsFailure) return error;
        if (shape is PhysicsStaticMesh && body.Motion != PhysicsBodyMotion.Static) return new(PhysicsErrorCode.UnsupportedGeometry, "Body.Shape");
        if (!float.IsFinite(body.Mass) || (body.Motion == PhysicsBodyMotion.Dynamic ? body.Mass is < 0.01f or > 10000 : body.Mass != 0))
            return Invalid("Body.Mass");
        if (!Speed(linearVelocity, PhysicsProfileSettings.For(options.Quality).MaximumLinearSpeed)) return Invalid("Body.LinearVelocity");
        if (!Speed(angularVelocity, MaximumAngularSpeed)) return Invalid("Body.AngularVelocity");
        if (body.Motion == PhysicsBodyMotion.Static && (linearVelocity != Vector3.Zero || angularVelocity != Vector3.Zero))
            return Invalid("Body.StaticVelocity");
        if (!float.IsFinite(body.Material.Friction) || body.Material.Friction is < 0 or > 4) return Invalid("Body.Material.Friction");
        if (body.Filter.Layer >= 32) return Invalid("Body.Filter.Layer");
        return default;
    }

    /// <summary>Checks the proposed live dynamic mass envelope; provider computes it including the candidate before publication.</summary>
    /// <param name="minimum">Smallest live/proposed mass.</param><param name="maximum">Largest live/proposed mass.</param><returns>Input failure or success.</returns>
    public static PhysicsError ValidateMassRatio(float minimum, float maximum)
        => !float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum < 0.01f || maximum > 10000 || maximum < minimum || maximum / minimum > MaximumMassRatio
            ? Invalid("Body.MassRatio") : default;

    /// <summary>Checks structural joint input and ownership; provider additionally checks liveness and permitted body modes.</summary>
    /// <param name="world">Owner world.</param><param name="joint">Joint input.</param><returns>Validation result.</returns>
    public static PhysicsError ValidateJoint(PhysicsWorldId world, PhysicsBallSocket joint)
    {
        var error = Owner(world, joint.BodyA); if (error.IsFailure) return error;
        error = Owner(world, joint.BodyB); if (error.IsFailure) return error;
        if (joint.BodyA == joint.BodyB) return Invalid("Joint.Bodies");
        return Bounded(joint.LocalAnchorA, MaximumDimension) && Bounded(joint.LocalAnchorB, MaximumDimension) ? default : Invalid("Joint.Anchors");
    }

    /// <summary>Checks tick continuity without unsigned wrap.</summary>
    /// <param name="lastCompleted">Last published tick.</param><param name="requested">Next requested tick.</param><returns>InvalidTick or success.</returns>
    public static PhysicsError ValidateTick(ulong lastCompleted, ulong requested)
        => lastCompleted != ulong.MaxValue && requested == lastCompleted + 1 ? default : new(PhysicsErrorCode.InvalidTick, "Tick");

    /// <summary>Checks a full command batch without applying it. State-dependent modes, live generations and resultant speeds must also be prevalidated by the provider.</summary>
    /// <param name="world">Owner world.</param><param name="options">Policy.</param><param name="commands">Borrowed batch.</param>
    /// <param name="alreadyAdmitted">Count admitted since the last successful step.</param><returns>First failure, before any mutation.</returns>
    public static PhysicsError ValidateCommands(PhysicsWorldId world, PhysicsWorldOptions options, ReadOnlySpan<PhysicsBodyCommand> commands, int alreadyAdmitted)
    {
        if (!world.IsValid) return new(PhysicsErrorCode.InvalidIdentifier, "World");
        var error = Validate(options); if (error.IsFailure) return error;
        error = ValidateCapacity(alreadyAdmitted, commands.Length, options.Limits.MaxCommandsPerTick, "Commands"); if (error.IsFailure) return error;
        foreach (var command in commands)
        {
            error = Owner(world, command.Body); if (error.IsFailure) return error;
            switch (command.Kind)
            {
                case PhysicsCommandKind.SetVelocity:
                    if (!Speed(command.Linear, PhysicsProfileSettings.For(options.Quality).MaximumLinearSpeed) || !Speed(command.Angular, MaximumAngularSpeed))
                        return Invalid("Command.Velocity");
                    break;
                case PhysicsCommandKind.ApplyLinearImpulse:
                case PhysicsCommandKind.ApplyLinearForce:
                    if (!Speed(command.Linear, 1e6f)) return Invalid("Command.Linear");
                    break;
                case PhysicsCommandKind.Teleport:
                    error = Validate(command.Pose); if (error.IsFailure) return error;
                    break;
                case PhysicsCommandKind.Wake:
                case PhysicsCommandKind.Sleep: break;
                default: return Invalid("Command.Kind");
            }
        }
        return default;
    }

    private static PhysicsError Owner(PhysicsWorldId world, PhysicsBodyId body)
        => !world.IsValid || !body.IsValid ? new(PhysicsErrorCode.InvalidIdentifier, "Body")
            : body.World != world ? new(PhysicsErrorCode.ForeignWorld, "Body.World") : default;
    private static PhysicsError Filter(PhysicsWorldId world, PhysicsQueryFilter filter)
        => filter.ExcludeBody == default ? default : Owner(world, filter.ExcludeBody);
    private static PhysicsError Direction(Vector3 direction, float distance)
        => Unit(direction) && float.IsFinite(distance) && distance is > 0 and <= 20000 ? default : Invalid("Query.DirectionOrDistance");

    /// <summary>Checks ray values and excluded-body ownership; a provider must also check the live excluded generation.</summary>
    /// <param name="world">Owner world.</param><param name="query">Input ray.</param><returns>Validation result.</returns>
    public static PhysicsError ValidateRay(PhysicsWorldId world, PhysicsRay? query)
    {
        if (!world.IsValid) return new(PhysicsErrorCode.InvalidIdentifier, "World");
        if (query is null || !Bounded(query.Origin, MaximumCoordinate)) return Invalid("Query.Origin");
        var error = Direction(query.Direction, query.MaximumDistance); return error.IsFailure ? error : Filter(world, query.Filter);
    }

    /// <summary>Checks sweep values and rejects concave/unknown query geometry.</summary>
    /// <param name="world">Owner world.</param><param name="query">Sweep input.</param><param name="shape">Actual captured query shape.</param><param name="limits">Input policy.</param>
    /// <returns>Validation result; live shape/excluded-body checks remain provider obligations.</returns>
    public static PhysicsError ValidateSweep(PhysicsWorldId world, PhysicsSweep? query, PhysicsShapeDefinition? shape, PhysicsLimits limits)
    {
        if (query is null) return Invalid("Query");
        var error = QueryShape(world, query.Shape, query.Pose, query.Filter, shape, limits); if (error.IsFailure) return error;
        return Direction(query.Direction, query.MaximumDistance);
    }

    /// <summary>Checks exact-overlap input and convex query geometry.</summary>
    /// <param name="world">Owner world.</param><param name="query">Overlap input.</param><param name="shape">Actual captured shape.</param><param name="limits">Input policy.</param>
    /// <returns>Neutral validation result.</returns>
    public static PhysicsError ValidateOverlap(PhysicsWorldId world, PhysicsOverlap? query, PhysicsShapeDefinition? shape, PhysicsLimits limits)
        => query is null ? Invalid("Query") : QueryShape(world, query.Shape, query.Pose, query.Filter, shape, limits);

    private static PhysicsError QueryShape(PhysicsWorldId world, PhysicsShapeId id, PhysicsPose pose, PhysicsQueryFilter filter, PhysicsShapeDefinition? shape, PhysicsLimits limits)
    {
        if (!world.IsValid || !id.IsValid) return new(PhysicsErrorCode.InvalidIdentifier, "Query.Shape");
        if (id.World != world) return new(PhysicsErrorCode.ForeignWorld, "Query.Shape.World");
        var error = Validate(pose); if (error.IsFailure) return error;
        error = ValidateShape(shape, limits); if (error.IsFailure) return error;
        if (shape is PhysicsStaticMesh) return new(PhysicsErrorCode.UnsupportedGeometry, "Query.Shape");
        return Filter(world, filter);
    }
}
