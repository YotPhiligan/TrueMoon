using TrueMoon.Aether.Runtime.Ecs.Loading;

namespace TrueMoon.Aether.Kul.Runtime;

/// <summary>Explicit source schema binding; validation never opens native devices or files.</summary>
public static class AudioComponentRegistry
{
    /// <summary>Adds the optional v1 source schema to the caller's registry.</summary><param name="builder">Composition-time builder.</param><returns>Same builder.</returns>
    public static ComponentRegistryBuilder Register(ComponentRegistryBuilder builder)
    { ArgumentNullException.ThrowIfNull(builder); builder.Register<AudioSource>(AudioComponentSchemas.Source, Read, validate: context => { _ = Read(context); }); return builder; }
    private static AudioSource Read(ComponentValidationContext context)
    {
        if (!Guid.TryParseExact(context.GetString("asset"), "D", out var id) || id == Guid.Empty) throw new InvalidDataException("Audio source requires a nonempty captured asset GUID.");
        return new(new(id), context.GetSingle("volume", 0, 1), context.GetBoolean("loop"), context.GetBoolean("autoPlay"));
    }
}
