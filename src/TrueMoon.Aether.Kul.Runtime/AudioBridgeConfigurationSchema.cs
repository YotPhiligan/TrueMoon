using TrueMoon.Configuration;

namespace TrueMoon.Aether.Kul.Runtime;

/// <summary>Common numeric scene policy. Capture identity/path remains explicit immutable runtime input.</summary>
public static class AudioBridgeConfigurationSchema
{
    /// <summary>Register this exact schema instance in the App profile.</summary>
    public static IConfigurationSchema<AudioBridgeOptions> Default { get; } = ConfigurationSchemaDefinitions.Create("Audio:Bridge",
        () => new AudioBridgeOptions(), value => value.Snapshot(), value => { _ = value.Snapshot(); },
        fields =>
        {
            fields.Field("MaximumSources", v => v.MaximumSources, (v, s) => v.MaximumSources = s);
            fields.Field("MaximumPreparedVoices", v => v.MaximumPreparedVoices, (v, s) => v.MaximumPreparedVoices = s);
            fields.Field("MaximumAssets", v => v.MaximumAssets, (v, s) => v.MaximumAssets = s);
            fields.Field("MaximumEncodedBytes", v => v.MaximumEncodedBytes, (v, s) => v.MaximumEncodedBytes = s);
        });
}
