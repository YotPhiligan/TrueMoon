namespace TrueMoon.Nom.Storage.Format;

// Wire ceilings are independent of public descriptor admission limits.
internal sealed record NomFormatLimits
{
    internal int MaximumKeyBytes { get; init; } = 1024;
    internal int MaximumValueBytes { get; init; } = 128 * 1024;
    internal int MaximumPayloadBytes { get; init; } = 4 * 1024 * 1024;
    internal int MaximumMutations { get; init; } = 8192;
    internal long MaximumSegmentBytes { get; init; } = 64L * 1024 * 1024;
    internal int MaximumIndexKeys { get; init; } = 2_000_000;
    internal long MaximumIndexBytes { get; init; } = 256L * 1024 * 1024;
    internal int MaximumReplayBatches { get; init; } = 65_536;
    internal long MaximumCheckpointBytes { get; init; } = 256L * 1024 * 1024;
    internal long MaximumMaintenanceBytes { get; init; } = 2L * 1024 * 1024 * 1024 + 256L * 1024 * 1024 + 256;

    internal void Validate()
    {
        NomBinary.Require(MaximumKeyBytes is >= 1 and <= 1024 &&
            MaximumValueBytes is >= 1 and <= 128 * 1024 &&
            MaximumPayloadBytes is >= 13 and <= 4 * 1024 * 1024 &&
            MaximumMutations is >= 1 and <= 8192 &&
            MaximumSegmentBytes is >= 160 and <= 64L * 1024 * 1024 &&
            MaximumIndexKeys is >= 1 and <= 2_000_000 &&
            MaximumIndexBytes is >= 97 and <= 256L * 1024 * 1024 &&
            MaximumReplayBatches is >= 1 and <= 65_536 && MaximumCheckpointBytes is >= 160 and <= 256L * 1024 * 1024 &&
            MaximumMaintenanceBytes is >= 576 and <= 2L * 1024 * 1024 * 1024 + 256L * 1024 * 1024 + 256, "Invalid Nom format limits.");
    }
}
