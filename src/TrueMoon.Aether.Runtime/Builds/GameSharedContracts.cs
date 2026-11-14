using System.Reflection;
using TrueMoon.Aether.Abstractions.Sessions;

namespace TrueMoon.Aether.Runtime.Builds;

internal static class GameSharedContracts
{
    internal sealed record Descriptor(string Name, string Identity, string Sha256);
    internal static IEnumerable<Assembly> For(bool runtimeAccess) => runtimeAccess
        ? [typeof(IGameSession).Assembly, typeof(IFactory<>).Assembly, typeof(GameBuildArtifact).Assembly]
        : [typeof(IGameSession).Assembly, typeof(IFactory<>).Assembly];
    internal static Descriptor[] Describe(bool runtimeAccess) => For(runtimeAccess).OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
        .Select(a => new Descriptor(a.GetName().Name!, a.GetName().FullName, GameBuildArtifact.HashFile(a.Location))).ToArray();
    internal static bool IsReserved(string? name) => string.Equals(name, "TrueMoon.Aether", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "TrueMoon.Aether.Tooling", StringComparison.OrdinalIgnoreCase) ||
        For(true).Any(assembly => string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
}
