using System.Security.Cryptography;
using TrueMoon.Aether.Builds;

namespace TrueMoon.Aether.Editor.Editing;

// One pass owns its streams. No leases, timestamp cache, MSBuild evaluation or package extraction.
internal sealed class EditorSourceCensus(TimeProvider time, Func<string, Stream>? open = null)
{
    private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;

    internal async Task<EditorSourceScan> ScanAsync(GameBuildSourceWatchDescriptor descriptor, CancellationToken token)
    {
        var budget = new PassBudget();
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return await ScanCoreAsync(descriptor, budget, token).ConfigureAwait(false); }
            catch (NotSupportedException error) { return EditorSourceScan.Unavailable(error.Message, false); }
            catch (InvalidDataException error) { return EditorSourceScan.Unavailable(error.Message); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                if (attempt == 3) return EditorSourceScan.Unavailable("Source read/coverage failed: " + error.GetType().Name + ": " + error.Message);
                await Task.Delay(TimeSpan.FromMilliseconds(30 * (attempt + 1)), time, token).ConfigureAwait(false);
            }
        }
    }

    private sealed class PassBudget { internal long Bytes; internal int Entries; }

    private async Task<EditorSourceScan> ScanCoreAsync(GameBuildSourceWatchDescriptor descriptor, PassBudget budget, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) throw new NotSupportedException("Source observation currently requires local Windows paths.");
        if (descriptor.Files.Count > GameBuildSourceWatchPolicy.MaximumPaths || descriptor.Probes.Count > GameBuildSourceWatchPolicy.MaximumPaths ||
            descriptor.SourceTrees.Count > GameBuildSourceWatchPolicy.MaximumTrees || descriptor.TreeFiles.Count > GameBuildSourceWatchPolicy.MaximumPaths)
            throw new NotSupportedException("Source descriptor exceeds its path budget.");
        var trees = new HashSet<string>(Paths);
        foreach (var root in descriptor.SourceTrees)
        {
            ValidatePath(root);
            if (!Attributes(root, out var attributes)) return EditorSourceScan.Difference(root);
            if ((attributes & FileAttributes.Directory) == 0) return EditorSourceScan.Difference(root);
            Census(root, 0);
        }
        var expectedTrees = new HashSet<string>(descriptor.TreeFiles, Paths);
        if (!trees.SetEquals(expectedTrees))
            return EditorSourceScan.Difference(trees.Except(expectedTrees, Paths).Concat(expectedTrees.Except(trees, Paths)).Take(16).ToArray());

        var buffer = new byte[65536];
        foreach (var file in descriptor.Files)
        {
            token.ThrowIfCancellationRequested(); ValidatePath(file.Path);
            if (!Attributes(file.Path, out var attributes) || (attributes & FileAttributes.Directory) != 0)
                return EditorSourceScan.Difference(file.Path);
            try
            {
                var before = new FileInfo(file.Path); var length = before.Length; var written = before.LastWriteTimeUtc;
                if (length > GameBuildSourceWatchPolicy.MaximumFileBytes || budget.Bytes > GameBuildSourceWatchPolicy.MaximumTotalBytes - length)
                    throw new InvalidDataException("Source observation exceeds its byte budget.");
                await using var stream = open?.Invoke(file.Path) ?? new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                    65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long read = 0; int count;
                while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    read += count;
                    budget.Bytes += count;
                    if (read > GameBuildSourceWatchPolicy.MaximumFileBytes || budget.Bytes > GameBuildSourceWatchPolicy.MaximumTotalBytes)
                        throw new InvalidDataException("Source observation exceeds its byte budget.");
                    hash.AppendData(buffer, 0, count);
                }
                var after = new FileInfo(file.Path);
                if (!after.Exists || read != length || after.Length != length || after.LastWriteTimeUtc != written)
                    throw new IOException("Source file changed during observation.");
                if (read != file.Length || !StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexString(hash.GetHashAndReset()), file.Sha256))
                    return EditorSourceScan.Difference(file.Path);
            }
            catch (FileNotFoundException) { return EditorSourceScan.Difference(file.Path); }
            catch (DirectoryNotFoundException) { return EditorSourceScan.Difference(file.Path); }
        }
        foreach (var probe in descriptor.Probes)
        {
            token.ThrowIfCancellationRequested(); ValidatePath(probe.Path);
            var exists = Attributes(probe.Path, out var attributes);
            if (exists && (attributes & FileAttributes.Directory) != 0)
                throw new NotSupportedException("Source probe is occupied by a directory.");
            if (exists != probe.Exists) return EditorSourceScan.Difference(probe.Path);
        }
        return EditorSourceScan.Same;

        void Census(string directory, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > GameBuildSourceWatchPolicy.MaximumDepth) throw new InvalidDataException("Source tree exceeds its depth budget.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested();
                if (++budget.Entries > GameBuildSourceWatchPolicy.MaximumEntries) throw new InvalidDataException("Source tree exceeds its entry budget.");
                // Keep capture's ordering: artifact subtree is ignored even if its contents are not readable.
                if (descriptor.Policy.ExcludesEntry(path, false)) continue;
                if (!Attributes(path, out var attributes, true)) throw new IOException("Source entry disappeared during enumeration.");
                var directoryEntry = (attributes & FileAttributes.Directory) != 0;
                if (descriptor.Policy.ExcludesEntry(path, directoryEntry)) continue;
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Source observation does not traverse reparse points.");
                if (directoryEntry) Census(path, depth + 1); else trees.Add(path);
                if (trees.Count > GameBuildSourceWatchPolicy.MaximumPaths) throw new InvalidDataException("Source tree exceeds its file budget.");
            }
        }
    }

    private static bool Attributes(string path, out FileAttributes attributes, bool allowReparse = false)
    {
        try
        {
            attributes = File.GetAttributes(path);
            if (!allowReparse && (attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Source observation does not traverse reparse points.");
            return true;
        }
        catch (FileNotFoundException) { attributes = default; return false; }
        catch (DirectoryNotFoundException) { attributes = default; return false; }
    }

    private static void ValidatePath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new NotSupportedException("Source observation requires representable local drive paths.");
        // Probe absence must not hide an unsupported ancestor or an access error.
        for (var current = Path.GetDirectoryName(path); current != null; current = Path.GetDirectoryName(current))
            Attributes(current, out _);
    }
}
