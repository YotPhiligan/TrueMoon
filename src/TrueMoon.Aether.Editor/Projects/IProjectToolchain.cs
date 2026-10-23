namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Selected SDK preflight boundary. It does not build or evaluate the game project.</summary>
public interface IProjectToolchain
{
    /// <summary>Checks the SDK selected from the game project directory, including applicable global.json.</summary>
    /// <param name="gameProjectPath">Absolute game .csproj path.</param>
    /// <param name="cancellationToken">Cancellation; owned probe must be reaped before completion.</param>
    /// <returns>The selected SDK identity.</returns>
    Task<ProjectToolchainInfo> ValidateAsync(string gameProjectPath, CancellationToken cancellationToken = default);
}
