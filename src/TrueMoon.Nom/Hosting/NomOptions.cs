using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Hosting;

/// <summary>App-local admission limits. UseNom validates and copies these values before registering services.</summary>
public sealed class NomOptions
{
    /// <summary>Gets or sets the maximum retained live/failed stores per App, from1to4.</summary>
    public int MaximumOpenStores { get; set; } = 4;
    /// <summary>Gets or sets queued metadata commands per store, from1to32, plus one internal completion slot.</summary>
    public int QueueCount { get; set; } = 32;
    /// <summary>Gets or sets owned queued metadata bytes per store, from1to16MiB.</summary>
    public int QueueBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Gets or sets affected assets per batch, from1to256.</summary>
    public int BatchRows { get; set; } = 256;
    /// <summary>Gets or sets serialized asset metadata bytes per batch, from1to4MiB.</summary>
    /// <remarks>The complete atomic publication, including revision and operation records, must also fit the4MiB KV payload ceiling.</remarks>
    public int BatchBytes { get; set; } = 4 * 1024 * 1024;
    /// <summary>Gets or sets serialized canonical asset bytes per record, from1to128KiB.</summary>
    public int DocumentBytes { get; set; } = 128 * 1024;
    /// <summary>Gets or sets the domain index logical byte ceiling, from1byte to256MiB. The default remains64MiB; this is not an RSS limit.</summary>
    public long DomainIndexBytes { get; set; } = 64L * 1024 * 1024;
    /// <summary>Gets or sets a positive per-store drain timeout, at most10seconds. Timeout retains live ownership.</summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

    internal NomSettings Snapshot()
    {
        Bound(MaximumOpenStores, 4, nameof(MaximumOpenStores));
        Bound(QueueCount, 32, nameof(QueueCount)); Bound(QueueBytes, 16 * 1024 * 1024, nameof(QueueBytes));
        Bound(BatchRows, 256, nameof(BatchRows)); Bound(BatchBytes, 4 * 1024 * 1024, nameof(BatchBytes));
        Bound(DocumentBytes, 128 * 1024, nameof(DocumentBytes));
        if(DomainIndexBytes is < 1 or > 256L*1024*1024) throw new ArgumentOutOfRangeException(nameof(DomainIndexBytes));
        if (DrainTimeout <= TimeSpan.Zero || DrainTimeout > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(DrainTimeout));
        return new(MaximumOpenStores, new(QueueCount, QueueBytes, BatchRows, BatchBytes, DocumentBytes,DomainIndexBytes), DrainTimeout);
    }
    private static void Bound(int value, int maximum, string name)
    { if (value < 1 || value > maximum) throw new ArgumentOutOfRangeException(name, value, $"Expected1..{maximum}."); }
}

internal sealed record NomSettings(int MaximumOpenStores, CatalogLimits Limits, TimeSpan DrainTimeout);
