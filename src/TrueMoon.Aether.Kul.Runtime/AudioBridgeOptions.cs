namespace TrueMoon.Aether.Kul.Runtime;

/// <summary>Copied scene admission limits, independent of Kul's total App limits.</summary>
public sealed class AudioBridgeOptions
{
    /// <summary>Simultaneous source attachments,1..256.</summary>
    public int MaximumSources { get; set; } = 64;
    /// <summary>Total fixed prepared voices across the capture,1..256.</summary>
    public int MaximumPreparedVoices { get; set; } = 64;
    /// <summary>Captured assets,1..64.</summary>
    public int MaximumAssets { get; set; } = 32;
    /// <summary>Total encoded file bytes,1..1GiB.</summary>
    public long MaximumEncodedBytes { get; set; } = 256L * 1024 * 1024;
    internal AudioBridgeOptions Snapshot()
    {
        if (MaximumSources is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(MaximumSources));
        if (MaximumPreparedVoices is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(MaximumPreparedVoices));
        if (MaximumAssets is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(MaximumAssets));
        if (MaximumEncodedBytes is < 1 or > 1024L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumEncodedBytes));
        return (AudioBridgeOptions)MemberwiseClone();
    }
}
