namespace TrueMoon.Aether.Input;

/// <summary>Held controls in the first gameplay profile.</summary>
[Flags]
public enum GameButtons
{
    /// <summary>No held controls.</summary>
    None = 0,
    /// <summary>Move forward.</summary>
    Forward = 1,
    /// <summary>Move backward.</summary>
    Backward = 2,
    /// <summary>Strafe left.</summary>
    Left = 4,
    /// <summary>Strafe right.</summary>
    Right = 8
}

/// <summary>Complete held state plus one bounded look delta; receipt acknowledges staging, not a simulation step.</summary>
/// <param name="Captured">Whether gameplay owns input; false requires a neutral packet.</param>
/// <param name="Buttons">Current held controls.</param><param name="Yaw">Yaw delta in radians.</param><param name="Pitch">Pitch delta in radians.</param>
/// <param name="InteractPressed">Coalesced press since the last packet, independent of current key-up state.</param>
public readonly record struct GameInputState(bool Captured, GameButtons Buttons = GameButtons.None, float Yaw = 0, float Pitch = 0, bool InteractPressed = false)
{
    /// <summary>Maximum absolute look delta per accepted packet.</summary>
    public const float MaximumLookDelta = .5f;
    internal const GameButtons AllButtons = GameButtons.Forward | GameButtons.Backward | GameButtons.Left | GameButtons.Right;
    /// <summary>Rejects unknown flags, non-finite/excessive deltas and non-neutral releases.</summary>
    public void Validate()
    {
        if ((Buttons & ~AllButtons) != 0 || !float.IsFinite(Yaw) || !float.IsFinite(Pitch) ||
            MathF.Abs(Yaw) > MaximumLookDelta || MathF.Abs(Pitch) > MaximumLookDelta ||
            !Captured && (Buttons != GameButtons.None || Yaw != 0 || Pitch != 0 || InteractPressed))
            throw new InvalidDataException("Invalid gameplay input state.");
    }
}

/// <summary>Input applied once at a fixed simulation boundary; actions/look are consumed, held controls may persist.</summary>
/// <param name="Buttons">Held controls.</param><param name="Yaw">Accumulated yaw radians, at most one.</param>
/// <param name="Pitch">Accumulated pitch radians, at most one.</param><param name="InteractPressed">Coalesced rising edge since the preceding tick.</param>
public readonly record struct GameInputTick(GameButtons Buttons, float Yaw, float Pitch, bool InteractPressed)
{
    /// <summary>Validates the tick contract before game mutation.</summary>
    public void Validate()
    {
        if ((Buttons & ~GameInputState.AllButtons) != 0 || !float.IsFinite(Yaw) || !float.IsFinite(Pitch) || MathF.Abs(Yaw) > 1 || MathF.Abs(Pitch) > 1)
            throw new InvalidDataException("Invalid gameplay tick input.");
    }
}

/// <summary>Explicit acknowledgement for an optional gameplay input command.</summary>
public enum GameInputOutcome
{
    /// <summary>Not an input response.</summary>
    None,
    /// <summary>Staged for a future fixed tick.</summary>
    Accepted,
    /// <summary>The loaded game has no input capability.</summary>
    Unsupported
}
