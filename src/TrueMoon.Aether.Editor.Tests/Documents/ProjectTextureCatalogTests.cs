using System.Buffers.Binary;
using System.IO.Compression;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed partial class ProjectTextureCatalogTests
{
    private static readonly byte[] TexturePaletteA = Convert.FromHexString("D51006FFE40911FFF2160BFFFC0316FF");
    private static readonly byte[] TexturePaletteB = Convert.FromHexString("2194D5FF7B730CFF304D90FF6D18AAFF");

    [Fact]
    public async Task ReplacePngPreservesAssetIdAndCommittedPixelsAcrossReopen()
    {
        using var files = new DocumentFiles();
        const string aPath = "first.png";
        const string bPath = "second.PNG";
        const string samePath = "same-pixels.png";
        await File.WriteAllBytesAsync(Path.Combine(files.Root, aPath), TexturePng(TexturePaletteA));
        await File.WriteAllBytesAsync(Path.Combine(files.Root, bPath), TexturePng(TexturePaletteB));
        var catalog = new ProjectTextureCatalog(files.Project, AetherEditorConfigurationExtensions.DecodeProjectTexture);
        Assert.True(catalog.CanEdit);
        var empty = await catalog.ReadAsync(default);
        Assert.Empty(empty.Textures);
        Assert.Null(empty.Stamp);
        var imported = await catalog.ImportAsync(empty, "  Painted  ", aPath, default);
        var first = Assert.Single(imported.Textures);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal("Painted", first.Name);
        Assert.Equal(aPath, first.SourcePath);
        AssertTexture(first, await catalog.ReadDefinitionAsync(first, default), TexturePaletteA);
        var firstBlob = TextureBlobPath(files, first);
        var firstBytes = await File.ReadAllBytesAsync(firstBlob);

        await File.WriteAllBytesAsync(Path.Combine(files.Root, samePath), TexturePng(TexturePaletteA, CompressionLevel.NoCompression));
        var moved = await catalog.ReplaceSourceAsync(imported, first.Id, samePath, default);
        var samePixels = Assert.Single(moved.Textures);
        Assert.Equal(first with { SourcePath = samePath }, samePixels);
        Assert.NotEqual(imported.Stamp, moved.Stamp);
        Assert.Equal(firstBytes, await File.ReadAllBytesAsync(firstBlob));

        var replaced = await catalog.ReplaceSourceAsync(moved, first.Id, bPath, default);
        var second = Assert.Single(replaced.Textures);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Name, second.Name);
        Assert.Equal(bPath, second.SourcePath);
        Assert.NotEqual(imported.Stamp, replaced.Stamp);
        Assert.NotEqual(first.Revision, second.Revision);
        File.Delete(Path.Combine(files.Root, aPath));
        File.Delete(Path.Combine(files.Root, bPath));
        File.Delete(Path.Combine(files.Root, samePath));

        var headless = new ProjectTextureCatalog(files.Project);
        Assert.False(headless.CanEdit);
        var reopened = await headless.ReadAsync(default);
        Assert.Equal(replaced.Stamp, reopened.Stamp);
        Assert.Equal(second, Assert.Single(reopened.Textures));
        AssertTexture(second, await headless.ReadDefinitionAsync(second, default), TexturePaletteB);
        AssertTexture(first, await headless.ReadDefinitionAsync(first, default), TexturePaletteA);
        Assert.Equal(firstBytes, await File.ReadAllBytesAsync(firstBlob));
        var renamed = await headless.RenameAsync(reopened, second.Id, "Renamed", default);
        Assert.Equal(second with { Name = "Renamed" }, Assert.Single(renamed.Textures));
        AssertTexture(second, await headless.ReadDefinitionAsync(second, default), TexturePaletteB);
    }

    [Fact]
    public async Task InvalidOrStaleTextureWriteLeavesCommittedRowIntact()
    {
        using var files = new DocumentFiles();
        const string aPath = "first.png";
        const string samePath = "same.png";
        var png = TexturePng(TexturePaletteA);
        await File.WriteAllBytesAsync(Path.Combine(files.Root, aPath), png);
        await File.WriteAllBytesAsync(Path.Combine(files.Root, samePath), TexturePng(TexturePaletteA, CompressionLevel.NoCompression));
        var catalog = new ProjectTextureCatalog(files.Project, AetherEditorConfigurationExtensions.DecodeProjectTexture);
        var before = await catalog.ImportAsync(await catalog.ReadAsync(default), "Original", aPath, default);
        var original = Assert.Single(before.Textures);
        Assert.Same(before, await catalog.ReplaceSourceAsync(before, original.Id, aPath, default));
        var committed = await catalog.RenameAsync(before, original.Id, "Committed", default);
        var row = Assert.Single(committed.Textures);
        var catalogPath = Path.Combine(files.Project.ContentDirectory, ".aether-texture-catalog.json");
        var committedBytes = await File.ReadAllBytesAsync(catalogPath);

        await Assert.ThrowsAsync<SceneFileConflictException>(() => catalog.RenameAsync(before, row.Id, row.Name, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => catalog.RenameAsync(before, row.Id, original.Name, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => catalog.ReplaceSourceAsync(before, row.Id, aPath, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => catalog.ReplaceSourceAsync(before, row.Id, samePath, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(committed, row.Id, "../first.png", default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(committed, row.Id, "first.png:ads.png", default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(committed, row.Id, Path.Combine(files.Root, aPath), default));
        await File.WriteAllBytesAsync(Path.Combine(files.Root, "malformed.png"), [0, 1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(files.Root, "truncated.png"), png[..40]);
        foreach (var invalid in new[] { "malformed.png", "truncated.png" })
            await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(committed, row.Id, invalid, default));
        var transparent = (byte[])TexturePaletteA.Clone();
        transparent[3] = 127;
        await File.WriteAllBytesAsync(Path.Combine(files.Root, "alpha.png"), TexturePng(transparent));
        await Assert.ThrowsAsync<RenderContentException>(() => catalog.ReplaceSourceAsync(committed, row.Id, "alpha.png", default));
        using (var oversized = File.Create(Path.Combine(files.Root, "oversized.png")))
            oversized.SetLength(16 * 1024 * 1024 + 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(committed, row.Id, "oversized.png", default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.ReplaceSourceAsync(committed, row.Id, samePath, cancelled.Token));
        AssertTexture(row, await catalog.ReadDefinitionAsync(row, default), TexturePaletteA);

        // Same-sized canonical TMRA for different pixels: catalog metadata still opens, but neither reading nor reuse trusts this file.
        var substituted = RenderContentCodec.Encode(new BaseColorTextureDefinition(2, 2, TexturePaletteB));
        await File.WriteAllBytesAsync(TextureBlobPath(files, row), substituted);
        var reopened = await catalog.ReadAsync(default);
        Assert.Equal(row, Assert.Single(reopened.Textures));
        Assert.Equal(committed.Stamp, reopened.Stamp);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReadDefinitionAsync(row, default));
        Assert.Contains(row.Name, failure.Message);
        Assert.Contains(row.Id.ToString("D"), failure.Message);
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ImportAsync(reopened, "Another", aPath, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(reopened, row.Id, aPath, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.ReplaceSourceAsync(reopened, row.Id, samePath, default));
        Assert.Same(reopened, await catalog.RenameAsync(reopened, row.Id, row.Name, default));
        Assert.Equal(committedBytes, await File.ReadAllBytesAsync(catalogPath));
        Assert.Equal(substituted, await File.ReadAllBytesAsync(TextureBlobPath(files, row)));
    }

    private static void AssertTexture(ProjectTexture row, BaseColorTextureDefinition texture, byte[] rgba)
    {
        Assert.Equal((2, 2), (row.Width, row.Height));
        Assert.Equal((row.Width, row.Height), (texture.Width, texture.Height));
        Assert.Equal(row.Revision, texture.Revision);
        Assert.Equal(rgba, texture.Pixels.ToArray());
    }

    private static string TextureBlobPath(DocumentFiles files, ProjectTexture row) =>
        Path.Combine(files.Project.ContentDirectory, ".aether-texture-data", row.Revision.ToString().ToUpperInvariant() + ".tmra");

    // Independent fixture encoder: two unfiltered RGBA8 scanlines, in PNG top-to-bottom order.
    internal static byte[] TexturePng(ReadOnlySpan<byte> rgba, CompressionLevel compression = CompressionLevel.Optimal)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteTexturePngChunk(output, "IHDR"u8, [0, 0, 0, 2, 0, 0, 0, 2, 8, 6, 0, 0, 0]);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, compression, leaveOpen: true))
        {
            zlib.WriteByte(0);
            zlib.Write(rgba[..8]);
            zlib.WriteByte(0);
            zlib.Write(rgba[8..]);
        }
        WriteTexturePngChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteTexturePngChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WriteTexturePngChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);
        output.Write(type);
        output.Write(data);
        var crc = UpdateTexturePngCrc(UpdateTexturePngCrc(uint.MaxValue, type), data);
        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc);
        output.Write(word);
    }

    private static uint UpdateTexturePngCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xEDB88320u);
        }
        return crc;
    }
}
