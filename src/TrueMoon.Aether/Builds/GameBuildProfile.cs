using System.Reflection;
using System.Runtime.Versioning;

namespace TrueMoon.Aether.Builds;

// Use the target baked into the module by MSBuild, including in independent package hosts.
// The entry executable's runtime or working directory must not change the game target.
internal static class GameBuildProfile
{
    private static readonly FrameworkName Framework = new(typeof(GameBuildProfile).Assembly
        .GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);

    internal static string TargetFramework { get; } = Framework.Identifier == ".NETCoreApp"
        ? $"net{Framework.Version.Major}.{Framework.Version.Minor}"
        : throw new NotSupportedException("Game builds require a .NETCoreApp module target.");

    internal static int SdkMajor => Framework.Version.Major;
}
