namespace TrueMoon;

/// <summary>A bounded tail and explicit loss/IO status for a redirected text stream.</summary>
/// <param name="Text">Most recent retained UTF16 characters, including output without newlines.</param>
/// <param name="TotalCharacters">Characters read since start.</param>
/// <param name="DroppedCharacters">Characters evicted from the tail.</param>
/// <param name="FileBytesWritten">Actual UTF8 bytes persisted.</param>
/// <param name="FileBytesDropped">Encoded bytes omitted by the file budget, queue overload or IO failure.</param>
/// <param name="FileError">Bounded file failure details; reading continues after a file failure.</param>
public sealed record ProcessOutputSnapshot(string Text, long TotalCharacters, long DroppedCharacters,
    long FileBytesWritten, long FileBytesDropped, string? FileError)
{
    /// <summary>Characters excluded only by a requested smaller read window, separate from capture eviction/file loss.</summary>
    public long OmittedCharacters { get; init; }
    /// <summary>Reader progress: Queued, AwaitingRead, Capturing, EndOfStream or Failed; independent of task continuations.</summary>
    public string? ReadStage { get; init; }
    /// <summary>UTC time the reader entered its current stage; null for a manually constructed snapshot.</summary>
    public DateTimeOffset? ReadStageUtc { get; init; }
    /// <summary>Whether the borrowed reader actually returned zero, rather than merely faulting or completing a wait.</summary>
    public bool EndOfStream { get; init; }
    /// <summary>File worker stage, sampled with the counters; null for a manually constructed snapshot.</summary>
    public string? FileStage { get; init; }
    /// <summary>UTC time the file worker entered its current stage.</summary>
    public DateTimeOffset? FileStageUtc { get; init; }
    /// <summary>Admitted chunks still queued, excluding any chunk currently being written; bounded by eight.</summary>
    public int QueuedFileChunks { get; init; }
    /// <summary>Whether the producer has closed the queue and all queued chunks have been taken; file close may still be pending.</summary>
    public bool FileQueueDrained { get; init; }
    /// <summary>Managed ID of the dedicated file worker, or zero when disabled/not yet started.</summary>
    public int FileWorkerThreadId { get; init; }
    /// <summary>Whether retained output is incomplete.</summary>
    public bool IsTruncated => DroppedCharacters != 0 || FileBytesDropped != 0 || OmittedCharacters != 0;

    /// <summary>Returns the most recent bounded display window without changing the original capture counters.</summary>
    public ProcessOutputSnapshot Limit(int maximumCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCharacters);
        var start = Math.Max(0, Text.Length - maximumCharacters);
        if (start < Text.Length && char.IsLowSurrogate(Text[start])) start++;
        return this with { Text = Text[start..], OmittedCharacters = OmittedCharacters + start };
    }
}
