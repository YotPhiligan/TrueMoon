using TrueMoon.Aether.Sessions;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime;

/// <summary>Explicit game activation implemented in the selected game assembly.</summary>
/// <remarks>Called only by the runtime's injected factory. Resolve dependencies here,
/// then pass them to the game constructor. Never cache runtime services globally.</remarks>
public interface IGameSessionEntry
{
    /// <summary>Creates one caller-owned game session.</summary>
    /// <param name="services">Borrowed runtime services; null for standalone low-level loading.</param>
    /// <returns>A fresh session owned by GameBuildLoader.</returns>
    static abstract IGameSession Create(IServiceResolver? services);
}
