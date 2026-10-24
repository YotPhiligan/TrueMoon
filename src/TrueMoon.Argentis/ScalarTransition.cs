namespace TrueMoon.Argentis;

/// <summary>A reversible numeric presentation transition. Its owning control supplies frame time and lifetime.</summary>
public sealed class ScalarTransition
{
    private float _from, _target;
    private double _elapsed;
    private TimeSpan _duration;
    /// <summary>Gets the current interpolated value.</summary>
    public float Value { get; private set; }
    /// <summary>Gets whether the target has not been reached.</summary>
    public bool IsRunning => Value != _target;
    /// <summary>Changes target without jumping from the current value. Zero duration completes immediately.</summary>
    /// <param name="target">Finite target.</param><param name="duration">Nonnegative duration.</param>
    public void MoveTo(float target, TimeSpan duration)
    {
        if (!float.IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(target));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (_target == target && _duration == duration) return;
        _from = Value; _target = target; _duration = duration; _elapsed = 0;
        if (duration == TimeSpan.Zero) Value = target;
    }
    /// <summary>Advances a smoothstep transition; no overshoot, including after a long/suspended frame.</summary>
    /// <param name="elapsed">Nonnegative monotonic elapsed time.</param><returns>Whether the value changed.</returns>
    public bool Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsRunning) return false;
        var previous = Value; _elapsed += elapsed.TotalSeconds;
        var t = (float)Math.Min(1, _elapsed / _duration.TotalSeconds);
        Value = t == 1 ? _target : _from + (_target - _from) * t * t * (3 - 2 * t);
        return previous != Value;
    }
    /// <summary>Finishes the transition; useful when reducing motion or ending an attachment.</summary>
    public void Complete() { Value = _target; _elapsed = 0; }
}
