namespace TrueMoon.Kul;

/// <summary>Startup settings copied and validated by UseAudio. All limits are per App.</summary>
public sealed class AudioOptions
{
    /// <summary>Output mode; a failed device open never falls back to offline.</summary>
    public AudioMode Mode { get; set; } = AudioMode.Device;
    /// <summary>Stereo output and decoded resource rate, 8–192 kHz.</summary>
    public uint SampleRate { get; set; } = 48000;
    /// <summary>Requested device period in frames. The device may negotiate a different period.</summary>
    public uint PeriodFrames { get; set; } = 256;
    /// <summary>Initial linear master gain, between zero and one.</summary>
    public float Volume { get; set; } = .5f;
    /// <summary>Mutes only this device stream, while mixing and playback still advance.</summary>
    public bool MuteOutput { get; set; }
    /// <summary>Maximum simultaneous caller-owned sessions.</summary>
    public int MaximumSessions { get; set; } = 8;
    /// <summary>Maximum loaded sound clips, shared by their playback instances.</summary>
    public int MaximumClips { get; set; } = 64;
    /// <summary>Maximum playback instances, including paused or stopped instances and streams.</summary>
    public int MaximumVoices { get; set; } = 64;
    /// <summary>Maximum simultaneous music stream instances; included in MaximumVoices.</summary>
    public int MaximumMusicStreams { get; set; } = 2;
    /// <summary>Maximum encoded size of one prepared sound file, checked before decoding.</summary>
    public long MaximumSoundFileBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Maximum retained decoded sound PCM bytes. Checked after synchronous native preparation.</summary>
    public long MaximumResidentSoundBytes { get; set; } = 128 * 1024 * 1024;

    internal AudioOptions Snapshot()
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentOutOfRangeException(nameof(Mode));
        if (SampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(SampleRate));
        if (PeriodFrames is < 64 or > 8192) throw new ArgumentOutOfRangeException(nameof(PeriodFrames));
        ValidateVolume(Volume);
        if (MaximumSessions is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaximumSessions));
        if (MaximumClips is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaximumClips));
        if (MaximumVoices is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaximumVoices));
        if (MaximumMusicStreams < 1 || MaximumMusicStreams > MaximumVoices) throw new ArgumentOutOfRangeException(nameof(MaximumMusicStreams));
        if (MaximumSoundFileBytes is < 1 or > 1024L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumSoundFileBytes));
        if (MaximumResidentSoundBytes is < 8 or > 2L * 1024 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumResidentSoundBytes));
        return Copy();
    }

    internal AudioOptions Copy() => (AudioOptions)MemberwiseClone();
    internal static void ValidateVolume(float value)
    {
        if (!float.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be finite and between zero and one.");
    }
}
