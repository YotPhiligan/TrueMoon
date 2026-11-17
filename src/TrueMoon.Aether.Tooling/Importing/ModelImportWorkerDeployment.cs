using System.Collections.ObjectModel;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Explicit trusted worker closure. The caller supplies hashes from its reviewed publish manifest.</summary>
public sealed class ModelImportWorkerDeployment
{
    /// <summary>Creates a descriptor without file IO. Entry point is TrueMoon.Aether.Importing.Assimp.dll.</summary>
    /// <param name="directory">Absolute directory containing only the published closure.</param>
    /// <param name="dotnetHost">Absolute trusted dotnet host executable, supplied by composition root.</param>
    /// <param name="sha256">All publish files, indexed by canonical relative name; no wildcards or extra files.</param>
    public ModelImportWorkerDeployment(string directory, string dotnetHost, IReadOnlyDictionary<string, string> sha256)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        if (!Path.IsPathFullyQualified(directory) || !Path.IsPathFullyQualified(dotnetHost)) throw new ArgumentException("Absolute worker and host paths required.");
        Directory = Path.GetFullPath(directory); DotnetHost = Path.GetFullPath(dotnetHost);
        if (sha256.Count is < 3 or > 128) throw new ArgumentException("Worker closure count must be 3..128.");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, hash) in sha256)
        {
            if (ModelSourcePath.Normalize(name) != name || hash.Length != 64 || !hash.All(Uri.IsHexDigit) || !files.TryAdd(name, hash.ToUpperInvariant()))
                throw new ArgumentException("Noncanonical worker manifest name/hash or duplicate.");
        }
        foreach (var name in new[] { EntryAssembly, "TrueMoon.Aether.Importing.Assimp.deps.json", "TrueMoon.Aether.Importing.Assimp.runtimeconfig.json" })
            if (!files.ContainsKey(name)) throw new ArgumentException("Incomplete worker manifest.");
        Sha256 = new ReadOnlyDictionary<string, string>(files);
    }
    /// <summary>Fixed worker entry assembly.</summary>
    public const string EntryAssembly = "TrueMoon.Aether.Importing.Assimp.dll";
    /// <summary>Absolute published directory.</summary>
    public string Directory { get; }
    /// <summary>Trusted .NET host, outside the native worker closure.</summary>
    public string DotnetHost { get; }
    /// <summary>Copied immutable expected hashes.</summary>
    public IReadOnlyDictionary<string, string> Sha256 { get; }

    internal async Task<ModelSourceFiles> PinAsync(CancellationToken token)
    {
        var files = new ModelSourceFiles(Directory, _ => { });
        try
        {
            // Do not recursively enumerate an unbounded or redirected tree.
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var pending = new Stack<(string Path, int Depth)>(); pending.Push((Directory, 0)); var directories = 0;
            while (pending.TryPop(out var next))
            {
                token.ThrowIfCancellationRequested();
                if (++directories > 64 || next.Depth > 8 || (File.GetAttributes(next.Path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Worker directory quota or reparse point.");
                foreach (var entry in System.IO.Directory.EnumerateFileSystemEntries(next.Path))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Worker reparse point.");
                    if ((attributes & FileAttributes.Directory) != 0) { if (pending.Count >= 64) throw new InvalidDataException("Worker directory quota."); pending.Push((entry, next.Depth + 1)); }
                    else if (!found.Add(Path.GetRelativePath(Directory, entry).Replace('\\','/')) || found.Count > 128)
                        throw new InvalidDataException("Worker file quota or collision.");
                }
            }
            if (!found.SetEquals(Sha256.Keys)) throw new InvalidDataException("Worker closure differs from the supplied manifest.");
            long total = 0;
            foreach (var (name, expected) in Sha256)
            {
                var file = await files.ReadAsync(name, (int)Math.Min(64 * 1024 * 1024, 128 * 1024 * 1024 - total), token).ConfigureAwait(false);
                total += file.Length; if (file.Sha256 != expected) throw new InvalidDataException("Worker file hash mismatch: " + name);
            }
            await files.RevalidateAsync(token).ConfigureAwait(false); return files;
        }
        catch { files.Dispose(); throw; }
    }
}
