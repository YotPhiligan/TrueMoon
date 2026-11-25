namespace TrueMoon.Argentis.Editors;

/// <summary>Backend-owned rich-text metrics with UTF-16 indices.</summary>
public interface IRichTextLayout : IDisposable
{
    /// <summary>The content extent in logical units.</summary>
    Size Size { get; }
    /// <summary>Maps a content point to a UTF-16 caret index.</summary>
    int HitTest(float x, float y);
    /// <summary>Gets the caret bounds for a UTF-16 index.</summary>
    Rect GetCaretBounds(int index);
    /// <summary>Moves the caret one visual line, preserving a preferred logical x coordinate.</summary>
    int MoveVertical(int index, int direction, float preferredX);
    /// <summary>Gets the start or end of the caret's visual line.</summary>
    int GetLineBoundary(int index, bool end);
}