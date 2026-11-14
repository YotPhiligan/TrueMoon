using System.Numerics;
using TrueMoon.Configuration;

namespace TrueMoon.Kton.Bepu;

/// <summary>Common startup configuration for Kton's BEPU provider; no independent OS or file readers.</summary>
public static class BepuPhysicsConfigurationSchema
{
    /// <summary>Register this exact schema instance in the App configuration profile.</summary>
    public static IConfigurationSchema<BepuPhysicsOptions> Default { get; } = ConfigurationSchemaDefinitions.Create("Kton",
        () => new BepuPhysicsOptions(), value => value.Copy(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("DefaultWorld:Quality", v => v.DefaultWorld.Quality, (v, x) => v.DefaultWorld = v.DefaultWorld with { Quality = x });
            fields.Field("DefaultWorld:Gravity:X", v => v.DefaultWorld.Gravity.X, (v, x) => SetGravity(v, x, v.DefaultWorld.Gravity.Y, v.DefaultWorld.Gravity.Z));
            fields.Field("DefaultWorld:Gravity:Y", v => v.DefaultWorld.Gravity.Y, (v, x) => SetGravity(v, v.DefaultWorld.Gravity.X, x, v.DefaultWorld.Gravity.Z));
            fields.Field("DefaultWorld:Gravity:Z", v => v.DefaultWorld.Gravity.Z, (v, x) => SetGravity(v, v.DefaultWorld.Gravity.X, v.DefaultWorld.Gravity.Y, x));
            fields.Field("DefaultWorld:StepDiagnosticBudgetMilliseconds", v => v.DefaultWorld.StepDiagnosticBudgetMilliseconds,
                (v, x) => v.DefaultWorld = v.DefaultWorld with { StepDiagnosticBudgetMilliseconds = x });
            fields.Field("DefaultWorld:Limits:MaxBodies", v => v.DefaultWorld.Limits.MaxBodies, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxBodies = x }));
            fields.Field("DefaultWorld:Limits:MaxShapes", v => v.DefaultWorld.Limits.MaxShapes, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxShapes = x }));
            fields.Field("DefaultWorld:Limits:MaxJoints", v => v.DefaultWorld.Limits.MaxJoints, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxJoints = x }));
            fields.Field("DefaultWorld:Limits:MaxCommandsPerTick", v => v.DefaultWorld.Limits.MaxCommandsPerTick, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxCommandsPerTick = x }));
            fields.Field("DefaultWorld:Limits:MaxContactEvents", v => v.DefaultWorld.Limits.MaxContactEvents, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxContactEvents = x }));
            fields.Field("DefaultWorld:Limits:MaxQueryHits", v => v.DefaultWorld.Limits.MaxQueryHits, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxQueryHits = x }));
            fields.Field("DefaultWorld:Limits:MaxQueryScratchBytes", v => v.DefaultWorld.Limits.MaxQueryScratchBytes, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxQueryScratchBytes = x }));
            fields.Field("DefaultWorld:Limits:MaxMeshVertices", v => v.DefaultWorld.Limits.MaxMeshVertices, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxMeshVertices = x }));
            fields.Field("DefaultWorld:Limits:MaxMeshTriangles", v => v.DefaultWorld.Limits.MaxMeshTriangles, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxMeshTriangles = x }));
            fields.Field("DefaultWorld:Limits:MaxMeshInputBytes", v => v.DefaultWorld.Limits.MaxMeshInputBytes, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { MaxMeshInputBytes = x }));
            fields.Field("DefaultWorld:Limits:PoolDiagnosticBudgetBytes", v => v.DefaultWorld.Limits.PoolDiagnosticBudgetBytes, (v, x) => SetLimits(v, v.DefaultWorld.Limits with { PoolDiagnosticBudgetBytes = x }));
        });

    private static void SetGravity(BepuPhysicsOptions value, float x, float y, float z)
        => value.DefaultWorld = value.DefaultWorld with { Gravity = new Vector3(x, y, z) };
    private static void SetLimits(BepuPhysicsOptions value, PhysicsLimits limits)
        => value.DefaultWorld = value.DefaultWorld with { Limits = limits };
}
