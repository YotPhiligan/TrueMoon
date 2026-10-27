namespace TrueMoon.Enerit.IO.Pipes;

/// <summary>Finite resource limits for one pipes client or server. Owners snapshot these values at construction.</summary>
public sealed class PipesTransportOptions
{
    /// <summary>Maximum admitted client requests, including sent requests whose caller cancelled locally.</summary>
    public int MaxPendingRequests { get; set; } = 64;
    /// <summary>Maximum callers waiting for client admission without serializing a payload. Zero rejects immediately.</summary>
    public int MaxQueuedRequests { get; set; } = 64;
    /// <summary>Maximum server requests being read, queued, handled or answered across all connections.</summary>
    public int MaxConcurrentRequests { get; set; } = 64;
    /// <summary>Maximum admitted request payload bytes retained by one server, excluding bounded headers and discard scratch.</summary>
    public long MaxBufferedRequestBytes { get; set; } = 4 * 1024 * 1024;
    /// <summary>Maximum owned server connections, including the instance awaiting connection. Range: 1..64.</summary>
    public int MaxConnections { get; set; } = 4;
    /// <summary>Local budget covering admission, connection, send and response. Remote execution is not rolled back.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(50);
    /// <summary>Writes the versioned 51-byte request header when a valid W3C context exists, and accepts its remote parent.
    /// Off by default for legacy peers. New receivers always parse the bounded extension.</summary>
    public bool PropagateTraceContext { get; set; }
    /// <summary>Emits detailed per-request events. Off by default; traces and operation metrics are independent.</summary>
    public bool CaptureRequestEvents { get; set; }
    /// <summary>Includes this owner's resource state, failure/reconnect counters and queue wait totals in the process-wide TrueMoon.Enerit.Pipes meter.
    /// On by default; independent of Core operation telemetry and request events. Options are copied by each owner.</summary>
    public bool MetricsEnabled { get; set; } = true;

    internal PipesTransportOptions Snapshot()
    {
        var copy = (PipesTransportOptions)MemberwiseClone();
        ArgumentOutOfRangeException.ThrowIfLessThan(copy.MaxPendingRequests, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(copy.MaxQueuedRequests);
        ArgumentOutOfRangeException.ThrowIfLessThan(copy.MaxConcurrentRequests, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(copy.MaxBufferedRequestBytes, 1);
        if (copy.MaxConnections is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(MaxConnections));
        if (copy.RequestTimeout <= TimeSpan.Zero || copy.RequestTimeout > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        return copy;
    }
}
