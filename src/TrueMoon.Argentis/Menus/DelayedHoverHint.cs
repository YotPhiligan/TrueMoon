namespace TrueMoon.Argentis.Menus;

/// <summary>Tracks the original target and delay of a pointer hint. The caller owns hit testing and popup presentation.</summary>
public struct DelayedHoverHint
{
    private Rect _bounds;
    private TimeSpan _remaining;

    /// <summary>The borrowed target, or <see langword="null"/> after reset.</summary>
    public Element? Target { get; private set; }
    /// <summary>The original pointer's root-coordinate X position.</summary>
    public float X { get; private set; }
    /// <summary>The original pointer's root-coordinate Y position.</summary>
    public float Y { get; private set; }
    /// <summary>Whether the delay or a geometry update is still pending.</summary>
    public bool IsPending { get; private set; }

    /// <summary>Tracks the target and restarts its delay.</summary>
    /// <param name="target">The hovered control. Disabled controls can still have hints.</param>
    /// <param name="x">Pointer X in root coordinates.</param>
    /// <param name="y">Pointer Y in root coordinates.</param>
    /// <param name="delay">Time before showing the hint; zero tracks the target without scheduling a hint.</param>
    public void Start(Element target, float x, float y, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        Target = target; X = x; Y = y;
        _bounds = target.Bounds;
        _remaining = delay;
        IsPending = delay > TimeSpan.Zero;
    }

    /// <summary>Checks whether the same visible target still occupies the original bounds and pointer position.</summary>
    /// <param name="hit">The caller's current hit at <see cref="X"/> and <see cref="Y"/>, using up-to-date layout.</param>
    /// <returns>Whether the pointer still reaches the target through each ancestor's clip.</returns>
    public readonly bool IsCurrent(Element? hit)
    {
        if (Target is not { IsAttached: true, IsDisposed: false, IsVisible: true } target ||
            !ReferenceEquals(hit, target) || target.Bounds != _bounds || !_bounds.Contains(X, Y)) return false;
        for (var node = target.Parent; node != null; node = node.Parent)
            if (!node.IsVisible || !node.ChildClipBounds.Contains(X, Y)) return false;
        return true;
    }

    /// <summary>Advances the delay and admits presentation only once, after the current layout is ready.</summary>
    /// <param name="elapsed">Nonnegative time since the previous frame.</param>
    /// <param name="layoutPending">Whether a layout change has not yet been applied.</param>
    /// <returns>Whether the pending hint can now be presented.</returns>
    public bool Advance(TimeSpan elapsed, bool layoutPending)
    {
        if (!IsPending) return false;
        _remaining = elapsed >= _remaining ? TimeSpan.Zero : _remaining - elapsed;
        if (layoutPending || _remaining > TimeSpan.Zero) return false;
        IsPending = false;
        return true;
    }

    /// <summary>Clears the target and cancels the delay.</summary>
    public void Reset() => this = default;
}
