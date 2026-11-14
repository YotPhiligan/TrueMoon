using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Abstractions.Mods;

/// <summary>Pure neutral component transformation. Factory results are group-owned and disposed before commit; failure remains retryable.</summary>
public interface IModComponentMigrator : IDisposable
{
    /// <summary>Transforms exact source schema/version into target data without touching either World or external state.</summary>
    /// <param name="arguments">Detached source and target contracts.</param><returns>Same component/type identity with target schema version and complete values.</returns>
    ComponentData Migrate(ModComponentMigrationArguments arguments);
}
/// <summary>Neutral exact migration input, with no source CLR type/instance.</summary>
public sealed record ModComponentMigrationArguments(ComponentSchema SourceSchema, ComponentSchema TargetSchema, ComponentData Data);

/// <summary>Explicit host admission for data loss. Default replacement rejects removed fields.</summary>
public sealed record ModWorldMigrationOptions(bool AllowFieldRemoval = false);
/// <summary>Reset is a distinct explicit operation, never an error fallback.</summary>
public enum ModWorldResetAdmission
{
    /// <summary>No reset is authorized.</summary>
    Deny,
    /// <summary>Caller explicitly replaces current state with the supplied seed snapshot.</summary>
    Allow
}
