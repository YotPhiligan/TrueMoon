using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime;

/// <summary>Opt-in entry factory for a Runtime-access game with frozen render content in its selected build.</summary>
public interface IGameBuildSessionEntry
{
    /// <summary>Creates the owned session using the selected artifact's already validated content.</summary>
    /// <param name="services">Borrowed runtime services, or null for a standalone loader.</param>
    /// <param name="content">Immutable CPU render content of this exact build, safe to retain for the session lifetime.</param>
    /// <returns>A fresh game session owned by the loader.</returns>
    static abstract IGameSession Create(IServiceResolver? services, RenderContentManifest content);
}
