using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceNativeMetadataCensusTests
{
    [Fact]
    public async Task Scan_PreparedNativePathHashesLongUnicodeExplicitInputAndRechecksAStableStampAcrossPasses()
    {
        using var files = new SourceWatchFiles(); var directory = Path.Combine(files.Root, "external");
        for (var index = 0; directory.Length < 300; index++) directory = Path.Combine(directory, "длинный-λ-путь-" + index);
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "явный-😀.bin");
        var stamp = new DateTime(2024, 3, 4, 5, 6, 8, DateTimeKind.Utc);
        File.WriteAllText(path, "original"); File.SetLastWriteTimeUtc(path, stamp);
        var descriptor = files.Descriptor([files.Project.GameProjectPath, files.Source, path], probes: [new(path, true)]);
        var census = new EditorSourceCensus(TimeProvider.System);
        AssertSame(await census.ScanAsync(descriptor, default));
        File.WriteAllText(path, "modified"); File.SetLastWriteTimeUtc(path, stamp);
        Assert.Equal(8, new FileInfo(path).Length); Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        var changed = await census.ScanAsync(descriptor, default);
        Assert.True(changed.Available); Assert.True(changed.Changed); Assert.Equal(new[] { path }, changed.ChangedPaths);
        File.WriteAllText(path, "original"); File.SetLastWriteTimeUtc(path, stamp);
        AssertSame(await census.ScanAsync(descriptor, default));
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(8, exclusive.Length);
    }

    [Fact]
    public async Task Scan_InjectedNativeReaderCannotBypassFinalMetadataOverrideOrRetainOwners()
    {
        using var files = new SourceWatchFiles(); var readers = new List<EditorSourceFile>();
        var queries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var census = new EditorSourceCensus(TimeProvider.System, path =>
        {
            var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException("Expected test input", path);
            readers.Add(reader); return reader;
        }, metadata: path =>
        {
            var count = queries.GetValueOrDefault(path) + 1; queries[path] = count;
            var value = EditorSourceFile.ReadMetadata(path);
            return path == files.Source && count == 2 ? value!.Value with { Attributes = FileAttributes.ReparsePoint } : value;
        });
        try
        {
            var result = await census.ScanAsync(files.Descriptor(), default);
            Assert.False(result.Available); Assert.False(result.Changed); Assert.False(result.Recoverable);
            Assert.Contains("reparse", result.Reason); Assert.Empty(result.ChangedPaths);
            Assert.Equal(2, queries.Count); Assert.Equal(2, queries[files.Project.GameProjectPath]); Assert.Equal(2, queries[files.Source]);
            Assert.Equal(2, readers.Count); Assert.All(readers, reader => Assert.False(reader.CanRead));
            using var exclusive = new FileStream(files.Source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(8, exclusive.Length);
        }
        finally { foreach (var reader in readers) reader.Dispose(); }
    }

    private static void AssertSame(EditorSourceScan result)
    { Assert.True(result.Available, result.Reason); Assert.False(result.Changed); Assert.Null(result.Reason); Assert.Empty(result.ChangedPaths); }
}
