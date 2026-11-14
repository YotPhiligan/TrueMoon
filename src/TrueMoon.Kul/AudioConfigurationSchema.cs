using TrueMoon.Configuration;

namespace TrueMoon.Kul;

/// <summary>Common configuration schema for startup Audio settings; no independent file/environment readers.</summary>
public static class AudioConfigurationSchema
{
    /// <summary>Register this exact schema instance in the App configuration profile.</summary>
    public static IConfigurationSchema<AudioOptions> Default { get; } = ConfigurationSchemaDefinitions.Create("Audio",
        () => new AudioOptions(), value => value.Copy(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("Mode", v => v.Mode, (v, x) => v.Mode = x);
            fields.Field("SampleRate", v => v.SampleRate, (v, x) => v.SampleRate = x);
            fields.Field("PeriodFrames", v => v.PeriodFrames, (v, x) => v.PeriodFrames = x);
            fields.Field("Volume", v => v.Volume, (v, x) => v.Volume = x);
            fields.Field("MuteOutput", v => v.MuteOutput, (v, x) => v.MuteOutput = x);
            fields.Field("MaximumSessions", v => v.MaximumSessions, (v, x) => v.MaximumSessions = x);
            fields.Field("MaximumClips", v => v.MaximumClips, (v, x) => v.MaximumClips = x);
            fields.Field("MaximumVoices", v => v.MaximumVoices, (v, x) => v.MaximumVoices = x);
            fields.Field("MaximumMusicStreams", v => v.MaximumMusicStreams, (v, x) => v.MaximumMusicStreams = x);
            fields.Field("MaximumSoundFileBytes", v => v.MaximumSoundFileBytes, (v, x) => v.MaximumSoundFileBytes = x);
            fields.Field("MaximumResidentSoundBytes", v => v.MaximumResidentSoundBytes, (v, x) => v.MaximumResidentSoundBytes = x);
        });
}
