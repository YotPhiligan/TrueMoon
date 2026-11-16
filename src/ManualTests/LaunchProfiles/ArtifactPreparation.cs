using System.Text.Json;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;

namespace TrueMoon.ManualTests.LaunchProfiles;

// A separate preparation process running the exact parent entry computes the
// labelled parent/counterpart fingerprint. It validates bytes without loading game code.
internal static class ArtifactPreparation
{
    internal static int Publish(string gameDirectory, string counterpart, string artifactRoot)
    {
        var artifact = GameArtifactPublisher.Publish(gameDirectory, artifactRoot, BuildIdentity.ForRuntime(Path.GetFullPath(counterpart)),
            "RenderSurface.Game.dll", "RenderSurface.Game.MeshGame", 41, runtimeAccess: true);
        Console.WriteLine(JsonSerializer.Serialize(new { artifact.DirectoryPath, artifact.BuildId, artifact.HostContractId }));
        return 0;
    }
}
