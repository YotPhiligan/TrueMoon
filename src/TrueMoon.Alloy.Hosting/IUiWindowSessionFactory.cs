namespace TrueMoon.Alloy.Hosting;

/// <summary>Session factory with an explicit standalone target/presenter contract.</summary>
/// <remarks>External hosts can continue to implement IUiSessionFactory alone. Standalone
/// custom factories must declare compatibility before a native window is allocated.</remarks>
public interface IUiWindowSessionFactory : IUiSessionFactory
{
    /// <summary>Checks whether sessions can present through this window graphics API, without creating resources.</summary>
    /// <param name="graphicsApi">Target and presenter selected by the window.</param>
    /// <returns>True only if the factory produces a compatible surface for that target/presenter.</returns>
    bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi);
}
