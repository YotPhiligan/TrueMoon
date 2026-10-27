using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Sessions;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Aether.Hosting;

/// <summary>Exact managed entry/shared binary fingerprints, independent of game loading and deployment directory names.</summary>
public static class BuildIdentity
{
    private static string Entry => Assembly.GetEntryAssembly()?.Location is { Length: > 0 } path ? path
        : throw new NotSupportedException("A managed entry assembly is required for build verification.");
    private static string[] Shared => [typeof(SessionIdentity).Assembly.Location, typeof(IFactory<>).Assembly.Location,
        typeof(PipesInvocationClient<>).Assembly.Location, typeof(ChildProcessUnitHandle).Assembly.Location];

    /// <summary>Exact current-entry/shared identity for an explicitly selected same-executable runtime profile.</summary>
    public static string HostId => Manifest([Entry, .. Shared]);
    /// <summary>Legacy fixed Game.Logic identity. Selected artifacts and built-in preview do not use this path.</summary>
    public static string GameId => Manifest([Path.Combine(AppContext.BaseDirectory, "Game.Logic.dll")]);
    /// <summary>Same-engine built-in preview identity; does not inspect a game DLL or source checkout.</summary>
    public static string PreviewId => Hash("TrueMoon.Aether.EditPreview.v1\n" + Manifest([typeof(SessionIdentity).Assembly.Location]));

    /// <summary>Computes the exact parent/runtime entry pair and shared managed binary identity, checking counterpart shared binaries.</summary>
    /// <param name="runtimeAssemblyPath">Absolute selected managed runtime entry assembly path.</param>
    /// <returns>A role-labelled pair fingerprint, or the existing HostId for the same-executable profile.</returns>
    /// <remarks>Does not load the runtime or game into the parent CLR. This is not a fingerprint of all native/private deployment dependencies.</remarks>
    public static string ForRuntime(string runtimeAssemblyPath) => Pair(CurrentLaunchIdentity(runtimeAssemblyPath));

    /// <summary>Captures the current parent and explicitly selected runtime entry paths for the native bootstrap.</summary>
    /// <param name="runtimeAssemblyPath">Absolute runtime entry assembly path.</param>
    /// <returns>Versioned counterpart paths. Capturing paths does not open files.</returns>
    public static RuntimeLaunchIdentity CurrentLaunchIdentity(string runtimeAssemblyPath)
        => new(Path.GetFullPath(Entry), Absolute(runtimeAssemblyPath));

    /// <summary>Verifies bootstrap paths, the actual child entry, shared binaries and the exact parent/runtime fingerprint.</summary>
    /// <param name="launch">Native launch including versioned counterpart identity.</param>
    /// <exception cref="InvalidDataException">The runtime is not the selected counterpart or binaries no longer match.</exception>
    public static void ValidateRuntime(SessionLaunch launch) => Validate(launch, runtime: true);

    internal static void ValidateHost(SessionLaunch launch) => Validate(launch, runtime: false);
    private static void Validate(SessionLaunch launch, bool runtime)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var identity = launch.RuntimeIdentity ?? throw new InvalidDataException("Missing native runtime counterpart identity.");
        ValidatePaths(identity);
        var expectedEntry = runtime ? identity.RuntimeAssemblyPath : identity.HostAssemblyPath;
        if (!SamePath(expectedEntry, Entry) || launch.Identity.HostContractId != Pair(identity))
            throw new InvalidDataException("Native host/runtime entry or shared build fingerprint mismatch.");
    }

    private static string Pair(RuntimeLaunchIdentity identity)
    {
        ValidatePaths(identity);
        var shared = Shared;
        var local = Manifest(shared);
        foreach (var entry in new[] { identity.HostAssemblyPath, identity.RuntimeAssemblyPath })
        {
            var directory = Path.GetDirectoryName(entry)!;
            if (Manifest(shared.Select(path => Path.Combine(directory, Path.GetFileName(path)))) != local)
                throw new InvalidDataException("Host and runtime require identical shared Aether/Contracts/Enerit/Mithril binaries.");
        }
        if (SamePath(identity.HostAssemblyPath, identity.RuntimeAssemblyPath))
            return Manifest([identity.HostAssemblyPath, .. shared]);
        return Hash("TrueMoon.Aether.HostRuntime.v1\nhost:" + FileHash(identity.HostAssemblyPath) +
            "\nruntime:" + FileHash(identity.RuntimeAssemblyPath) + "\nshared:" + local);
    }

    private static void ValidatePaths(RuntimeLaunchIdentity identity)
    {
        if (identity.Version != 1) throw new InvalidDataException("Unsupported native runtime identity version.");
        _ = Absolute(identity.HostAssemblyPath); _ = Absolute(identity.RuntimeAssemblyPath);
    }
    private static string Absolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Contains('\0'))
            throw new InvalidDataException("Native counterpart identity requires absolute managed entry paths.");
        return Path.GetFullPath(path);
    }
    internal static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Manifest(IEnumerable<string> files)
        => Hash(string.Join("\n", files.OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => Path.GetFileName(path) + ":" + FileHash(path))));
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
