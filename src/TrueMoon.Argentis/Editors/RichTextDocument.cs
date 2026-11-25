namespace TrueMoon.Argentis.Editors;

/// <summary>An immutable rich-document snapshot suitable for one/two-way property binding.</summary>
public sealed class RichTextDocument
{
    /// <summary>The shared empty snapshot.</summary>
    public static RichTextDocument Empty { get; } = new([]);
    /// <summary>Snapshots runs and coalesces adjacent runs with equal formatting.</summary>
    /// <param name="runs">Ordered text runs; empty runs are ignored.</param>
    public RichTextDocument(IEnumerable<RichTextRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var result = new List<RichTextRun>();
        foreach (var run in runs)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (run.Text.Length == 0) continue;
            if (result.Count > 0 && result[^1].Style == run.Style)
                result[^1] = new(result[^1].Text + run.Text, run.Style);
            else result.Add(run);
        }
        Runs = Array.AsReadOnly(result.ToArray());
        Text = string.Concat(result.Select(run => run.Text));
    }
    /// <summary>Ordered, immutable styled runs.</summary>
    public IReadOnlyList<RichTextRun> Runs { get; }
    /// <summary>Plain text with LF line endings and UTF-16 indices.</summary>
    public string Text { get; }
    /// <summary>Creates a plain snapshot and normalizes all line endings to LF.</summary>
    /// <param name="text">Literal text.</param>
    /// <returns>An immutable snapshot.</returns>
    public static RichTextDocument FromText(string text)
    { ArgumentNullException.ThrowIfNull(text); return new([new(text.ReplaceLineEndings("\n"))]); }
    internal RichTextStyle StyleAt(int index)
    {
        var offset = 0;
        foreach (var run in Runs) { if (index < offset + run.Text.Length) return run.Style; offset += run.Text.Length; }
        return Runs.Count > 0 ? Runs[^1].Style : new();
    }
    internal IEnumerable<RichTextRun> Slice(int start, int length)
    {
        var end = start + length; var offset = 0;
        foreach (var run in Runs)
        {
            var from = Math.Max(start, offset); var to = Math.Min(end, offset + run.Text.Length);
            if (to > from) yield return new(run.Text.Substring(from - offset, to - from), run.Style);
            offset += run.Text.Length;
        }
    }
    internal RichTextDocument Replace(int start, int length, string text, RichTextStyle style)
        => new(Slice(0, start).Concat([new RichTextRun(text, style)]).Concat(Slice(start + length, Text.Length - start - length)));
    internal RichTextDocument Format(int start, int length, RichTextStyle patch)
        => new(Slice(0, start).Concat(Slice(start, length).Select(run => new RichTextRun(run.Text, run.Style.Merge(patch))))
            .Concat(Slice(start + length, Text.Length - start - length)));
}