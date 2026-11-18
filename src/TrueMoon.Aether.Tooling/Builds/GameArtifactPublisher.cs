using System.Text.Json;
using static TrueMoon.Aether.Runtime.Builds.GameBuildArtifact;

namespace TrueMoon.Aether.Tooling.Builds;

/// <summary>Explicit tooling publication of a completed managed game output.</summary>
public static class GameArtifactPublisher
{
    /// <summary>Copies a completed game output into a content-addressed artifact directory.</summary>
    /// <remarks>Only explicitly declared flat managed DLLs are supported. Native libraries/assets are outside ENG3a.</remarks>
    public static GameBuildArtifact Publish(string outputDirectory, string artifactRoot, string hostContractId,
        string entryAssembly, string entryType, long sourceRevision, IEnumerable<string>? dependencies = null,
        CancellationToken cancellationToken = default, bool runtimeAccess = false) =>
        Publish(outputDirectory, artifactRoot, hostContractId, entryAssembly, entryType, sourceRevision,
            dependencies, cancellationToken, runtimeAccess, null);

    internal static GameBuildArtifact Publish(string outputDirectory, string artifactRoot, string hostContractId,
        string entryAssembly, string entryType, long sourceRevision, IEnumerable<string>? dependencies,
        CancellationToken cancellationToken, bool runtimeAccess, GameBuildRenderContent? renderContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory); ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ValidateIdentity(hostContractId); ValidateEntry(entryType, sourceRevision);
        var names = new[] { entryAssembly }.Concat(dependencies ?? []).ToArray(); ValidateNames(names);
        var root = Path.GetFullPath(artifactRoot); System.IO.Directory.CreateDirectory(root);
        RejectLink(root);
        var staging = Path.Combine(root, ".staging-" + Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(staging);
        try
        {
            var files = new List<BuildFile>();
            foreach (var name in names.Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = Path.Combine(Path.GetFullPath(outputDirectory), name); RejectLink(source);
                var length = new FileInfo(source).Length;
                if (length is <= 0 or > 64 * 1024 * 1024) throw new InvalidDataException("Game DLL size exceeds the artifact limit.");
                var target = Path.Combine(staging, name); File.Copy(source, target);
                files.Add(new(name, HashFile(target), length));
            }
            RenderPayload? payload = null;
            if (renderContent != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(staging, RenderContentFile);
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    renderContent.WriteTo(output);
                payload = new RenderPayload(HashFile(path), new FileInfo(path).Length);
            }
            var manifest = new Manifest(payload == null ? 2 : 3, GameBuildProfile.TargetFramework, hostContractId, RuntimeHash,
                runtimeAccess, GameSharedContracts.Describe(runtimeAccess), entryAssembly, entryType, sourceRevision, files.ToArray(), payload);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Json); var id = Hash(bytes);
            ValidateManifest(staging, manifest, id);
            File.WriteAllBytes(Path.Combine(staging, ManifestFile), bytes);
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.Combine(root, id);
            if (!System.IO.Directory.Exists(destination)) System.IO.Directory.Move(staging, destination);
            return GameBuildArtifact.Open(destination, hostContractId, id);
        }
        finally { DeleteOwned(staging, root); }
    }

    internal static void DeleteOwned(string path, string root)
    {
        var resolved = Path.GetFullPath(path); var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cleanup escaped owned build directory.");
        if (System.IO.Directory.Exists(resolved)) System.IO.Directory.Delete(resolved, true);
    }
}
