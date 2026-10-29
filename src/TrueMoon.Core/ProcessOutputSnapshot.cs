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
