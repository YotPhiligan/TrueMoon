using System.Security.Cryptography;
using TrueMoon.Aether.Builds;

namespace TrueMoon.Aether.Editor.Editing;

// One attempt owns its streams and bounded directory pins; no timestamp cache, MSBuild evaluation or package extraction.
internal sealed class EditorSourceCensus(TimeProvider time, Func<string, Stream>? open = null,
    Func<CancellationToken, EditorSourceDirectoryCache>? directories = null,
    Func<string, EditorSourceFile.Snapshot?>? metadata = null,
    Func<IReadOnlyList<GameBuildInputFile>, int, Action<string>, CancellationToken, Task<EditorSourceOpenBatch>>? batches = null)
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
        using var validation = directories?.Invoke(token) ?? new EditorSourceDirectoryCache(token);
        var trees = new HashSet<string>(Paths);
        foreach (var root in descriptor.SourceTrees)
        {
            validation.ValidatePath(root);
            if (!Attributes(root, out var attributes)) return EditorSourceScan.Difference(root);
            if ((attributes & FileAttributes.Directory) == 0) return EditorSourceScan.Difference(root);
            Census(root, 0);
        }
        var expectedTrees = new HashSet<string>(descriptor.TreeFiles, Paths);
        if (!trees.SetEquals(expectedTrees))
            return EditorSourceScan.Difference(trees.Except(expectedTrees, Paths).Concat(expectedTrees.Except(trees, Paths)).Take(16).ToArray());

        var buffer = new byte[65536];
        var batched = descriptor.Files.Count >= EditorSourceOpenBatch.MinimumFiles && open == null && metadata == null && directories == null;
        EditorSourceOpenBatch? batch = null;
        try
        {
            for (var index = 0; index < descriptor.Files.Count; index++)
            {
                var file = descriptor.Files[index];
                token.ThrowIfCancellationRequested();
                if (batched)
                {
                    if (index % EditorSourceOpenBatch.Capacity == 0)
                    {
                        batch?.Dispose(); batch = null;
                        batch = await (batches != null ? batches(descriptor.Files, index, validation.ValidatePath, token) :
                            EditorSourceOpenBatch.CreateAsync(descriptor.Files, index, validation.ValidatePath, token)).ConfigureAwait(false);
                    }
                }
                else validation.ValidatePath(file.Path);
                try
                {
                    // Native reads get their initial metadata from the opened no-follow handle.
                    // Injected streams retain fresh path-before/path-after validation for read hooks.
                    var before = open == null ? null : Metadata(file.Path);
                    if (open != null && (before == null || (before.Value.Attributes & FileAttributes.Directory) != 0))
                        return EditorSourceScan.Difference(file.Path);
                    if (before is { } injected) CheckByteBudget(injected.Length);
                    await using var stream = batched ? batch!.Take(index % EditorSourceOpenBatch.Capacity) : open != null ? open(file.Path) : EditorSourceFile.Open(file.Path);
                    if (stream == null) return EditorSourceScan.Difference(file.Path);
                    before ??= ((EditorSourceFile)stream).Before;
                    token.ThrowIfCancellationRequested();
                    var length = before.Value.Length; var written = before.Value.Written;
                    CheckByteBudget(length);
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
                    var after = Metadata(file.Path, stream as EditorSourceFile);
                    if (after == null || (after.Value.Attributes & FileAttributes.Directory) != 0 || read != length || after.Value.Length != length || after.Value.Written != written)
                        throw new IOException("Source file changed during observation.");
                    if (read != file.Length || !StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexString(hash.GetHashAndReset()), file.Sha256))
                        return EditorSourceScan.Difference(file.Path);
                }
                catch (FileNotFoundException) { return EditorSourceScan.Difference(file.Path); }
                catch (DirectoryNotFoundException) { return EditorSourceScan.Difference(file.Path); }
            }
        }
        finally { batch?.Dispose(); }
        foreach (var probe in descriptor.Probes)
        {
            token.ThrowIfCancellationRequested(); validation.ValidatePath(probe.Path);
            var probeMetadata = Metadata(probe.Path); var exists = probeMetadata != null;
            if (exists && (probeMetadata!.Value.Attributes & FileAttributes.Directory) != 0)
                throw new NotSupportedException("Source probe is occupied by a directory.");
            if (exists != probe.Exists) return EditorSourceScan.Difference(probe.Path);
        }
        validation.Revalidate();
        return EditorSourceScan.Same;

        void CheckByteBudget(long length)
        {
            if (length > GameBuildSourceWatchPolicy.MaximumFileBytes || budget.Bytes > GameBuildSourceWatchPolicy.MaximumTotalBytes - length)
                throw new InvalidDataException("Source observation exceeds its byte budget.");
        }

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

    private EditorSourceFile.Snapshot? Metadata(string path, EditorSourceFile? file = null)
    {
        var value = metadata != null ? metadata(path) : file != null ? file.ReadMetadata() : EditorSourceFile.ReadMetadata(path);
        if (value is { } snapshot) EditorSourceFile.RejectReparse(snapshot);
        return value;
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

}
