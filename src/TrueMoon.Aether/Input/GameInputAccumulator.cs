namespace TrueMoon.Aether.Input;

// Access is serialized by RuntimeProtocol's simulation lock; no wall-clock dependency.
internal sealed class GameInputAccumulator
{
    internal const int LeaseTicks = 12;
    private GameButtons _held;
    private float _yaw, _pitch;
    private bool _interact;
    private int _remaining;

    internal void Set(GameInputState input)
    {
        input.Validate();
        if (!input.Captured) { Clear(); return; }
        _interact |= input.InteractPressed;
        _held = input.Buttons;
        _yaw = Math.Clamp(_yaw + input.Yaw, -1, 1); _pitch = Math.Clamp(_pitch + input.Pitch, -1, 1);
        _remaining = LeaseTicks;
    }
    internal GameInputTick Take()
    {
        if (_remaining == 0) { Clear(); return default; }
        _remaining--;
        var result = new GameInputTick(_held, _yaw, _pitch, _interact);
        _yaw = _pitch = 0; _interact = false; return result;
    }
    internal void Clear() { _held = GameButtons.None; _yaw = _pitch = 0; _interact = false; _remaining = 0; }
}
