using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Input;

/// <summary>Optional fixed-tick gameplay input capability. Transient input is never checkpoint state.</summary>
public interface IInputGameSession : IGameSession
{
    /// <summary>Advances exactly one 1/60-second simulation tick with the supplied input.</summary>
    /// <param name="input">Validated state consumed at this simulation boundary.</param>
    void Tick(GameInputTick input);
}
