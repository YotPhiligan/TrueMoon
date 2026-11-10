using System.Text.Json;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Hosting;

/// <summary>Immutable public runtime flags, separate from protocol identity.</summary>
/// <param name="GpuValidation">Whether native GPU validation is requested.</param>
public sealed record RuntimeStartupSettings(bool GpuValidation = false)
{
    /// <summary>Existing flag alias, including 1/0.</summary>
    public const string LegacyValidationKey = "TRUEMOON_GPU_VALIDATION";
    /// <summary>Canonical public flag.</summary>
    public const string ValidationKey = "TRUEMOON_AETHER_RUNTIME__GPUVALIDATION";
    private sealed record Data { public bool GpuValidation { get; set; } }
    private static readonly IConfigurationSchema<Data> Schema = ConfigurationSchemaDefinitions.Create("AetherRuntime", () => new Data(),
        value => value with { }, _ => { }, fields => fields.Field("GpuValidation", value => value.GpuValidation, (value, next) => value.GpuValidation = next));

    /// <summary>Captures selected flags through the common OS boundary.</summary>
    /// <returns>Frozen public flags.</returns>
    public static RuntimeStartupSettings ReadEnvironment() => Parse(ConfigurationEnvironment.Capture(LegacyValidationKey, ValidationKey));

    /// <summary>Parses explicit selected input without App, DI or OS access.</summary>
    /// <param name="environment">Captured values; null means absent.</param><returns>Frozen flags.</returns>
    public static RuntimeStartupSettings Parse(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema);
            Add(LegacyValidationKey, "runtime-legacy-environment"); Add(ValidationKey, "runtime-environment");
            void Add(string name, string source)
            {
                if (!environment.TryGetValue(name, out var value) || value is null) return;
                builder.AddValues(source, ConfigurationSourcePriority.Environment,
                    [new("AetherRuntime:GpuValidation", JsonSerializer.SerializeToElement(value is "1" ? "true" : value is "0" ? "false" : value))]);
            }
        });
        return new(Schema.Bind(profile.LoadAsync().GetAwaiter().GetResult()).GpuValidation);
    }
}
