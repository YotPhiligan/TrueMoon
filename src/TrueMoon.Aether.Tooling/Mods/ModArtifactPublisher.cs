using System.Reflection;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Runtime.Mods;

namespace TrueMoon.Aether.Tooling.Mods;

/// <summary>Explicit content-addressed managed module publication utility. It never compiles or activates module code.</summary>
public static class ModArtifactPublisher
{
    /// <summary>Copies private DLL bytes into staging, writes an exact manifest, then atomically publishes a new immutable directory.</summary>
    /// <param name="artifactRoot">Destination catalog root.</param><param name="manifest">Module identity/entry/dependencies. Files and SharedAssemblies are calculated.</param>
    /// <param name="privateAssemblies">Flat managed DLL source paths, including the entry.</param><param name="additionalSharedAssemblies">Optional explicitly shared host APIs.</param>
    /// <returns>Content-addressed directory. Existing matching publication is reused without overwriting bytes.</returns>
    public static string Publish(string artifactRoot, ModManifest manifest, IEnumerable<string> privateAssemblies, IEnumerable<Assembly>? additionalSharedAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(manifest); ArgumentNullException.ThrowIfNull(privateAssemblies);
        var sources = privateAssemblies.ToArray();
        if (sources.Length is 0 or > 64 || sources.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length)
            throw new ArgumentException("Use 1..64 distinct flat managed DLL files.", nameof(privateAssemblies));
        var root = Path.GetFullPath(artifactRoot); Directory.CreateDirectory(root);
        for (var path = root; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Publication root cannot traverse reparse points.");
        var stage = Path.Combine(root, ".staging-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try
        {
            var files = new List<ModAssemblyFile>(); long total = 0;
            foreach (var source in sources.OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(source);
                if (name.Length > 128 || !name.EndsWith(".dll", StringComparison.Ordinal) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-') || name.Contains(".."))
                    throw new InvalidDataException("Invalid private assembly file name.");
                using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length <= 0 || input.Length > 128 * 1024 * 1024 - total) throw new InvalidDataException("Module bytes exceed publication budget.");
                total += input.Length;
                var target = Path.Combine(stage, name);
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                _ = AssemblyName.GetAssemblyName(target);
                files.Add(new(name, input.Length, ModManifestCodec.Hash(File.ReadAllBytes(target))));
            }
            var shared = new[] { typeof(IModEntry).Assembly, typeof(IFactory<>).Assembly }.Concat(additionalSharedAssemblies ?? []).Distinct()
                .OrderBy(a => a.FullName, StringComparer.Ordinal).Select(a => new ModSharedAssembly(a.FullName!, ModManifestCodec.Hash(File.ReadAllBytes(a.Location)))).ToArray();
            var bytes = ModManifestCodec.Encode(manifest with { Files = files.ToArray(), SharedAssemblies = shared });
            File.WriteAllBytes(Path.Combine(stage, "manifest.json"), bytes);
            var destination = Path.Combine(root, ModManifestCodec.Hash(bytes));
            if (!Directory.Exists(destination)) Directory.Move(stage, destination);
            else
            {
                var expected = files.Select(f => f.Name).Append("manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0 ||
                    !expected.SetEquals(Directory.GetFileSystemEntries(destination).Select(path => Path.GetFileName(path)!)) ||
                    expected.Any(name => (File.GetAttributes(Path.Combine(destination, name)) & FileAttributes.ReparsePoint) != 0 ||
                        !File.ReadAllBytes(Path.Combine(stage, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(destination, name)))))
                    throw new InvalidDataException("Existing immutable publication is corrupt; no bytes were overwritten.");
            }
            return destination;
        }
        finally
        {
            var resolved = Path.GetFullPath(stage);
            if (!resolved.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Staging cleanup escaped publication root.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
}
