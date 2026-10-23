using System.Security.Cryptography;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

[Trait("Category", "Integration")]
public sealed class JsonSceneDocumentStoreTests
{
    [Fact]
    public async Task WriteReadAndReplace_UseExactByteFingerprintsAndRetainAuthoringCatalogs()
    {
        using var files = new DocumentFiles(); var codec = new SceneFileCodec(DocumentData.Factory()); var store = new JsonSceneDocumentStore(codec);
        var created = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null);
        var bytes = await File.ReadAllBytesAsync(files.ScenePath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), created.Sha256);
        var loaded = await store.ReadAsync(files.ScenePath);
        DocumentData.AssertScene(loaded.Document); Assert.Equal(created, loaded.Stamp);
        var changed = new SceneDocument(loaded.Document.Id, 8, loaded.Document.Entities, loaded.Document.Definitions, loaded.Document.Schemas);
        var saved = await store.WriteAsync(files.ScenePath, changed, loaded.Stamp);
        Assert.NotEqual(created, saved); Assert.Equal(saved, (await store.ReadAsync(files.ScenePath)).Stamp);
        Assert.Equal(8, (await store.ReadAsync(files.ScenePath)).Document.Revision);
        Assert.Equal(0, new FileInfo(files.ScenePath + ".write-lock").Length);
        Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp"));
        using var lockAgain = new FileStream(files.ScenePath + ".write-lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(lockAgain.CanWrite);
    }

    [Fact]
    public async Task CreateOnly_ExistingFileIsNeverOverwrittenAndConflictReleasesWriterLock()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var stamp = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); var original = await File.ReadAllBytesAsync(files.ScenePath);
        var error = await Assert.ThrowsAsync<SceneFileConflictException>(() => store.WriteAsync(files.ScenePath, DocumentData.Empty(), null));
        Assert.Contains(files.ScenePath, error.Message);
        Assert.Equal(original, await File.ReadAllBytesAsync(files.ScenePath));
        Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp"));
        Assert.Equal(stamp, await store.WriteAsync(files.ScenePath, DocumentData.Scene(), stamp));
    }

    [Fact]
    public async Task Replace_ExternalContentChangeWithIdenticalTimestampIsConflictAndRetryRequiresNewObservedStamp()
    {
        using var files = new DocumentFiles(); var codec = new SceneFileCodec(DocumentData.Factory()); var store = new JsonSceneDocumentStore(codec);
        var expected = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); var time = File.GetLastWriteTimeUtc(files.ScenePath);
        var external = codec.Encode(DocumentData.Empty(9)); await File.WriteAllBytesAsync(files.ScenePath, external); File.SetLastWriteTimeUtc(files.ScenePath, time);
        await Assert.ThrowsAsync<SceneFileConflictException>(() => store.WriteAsync(files.ScenePath, DocumentData.Scene(), expected));
        Assert.Equal(external, await File.ReadAllBytesAsync(files.ScenePath)); Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp"));
        var observed = await store.ReadAsync(files.ScenePath); Assert.NotEqual(expected, observed.Stamp);
        var replaced = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), observed.Stamp);
        DocumentData.AssertScene((await store.ReadAsync(files.ScenePath)).Document); Assert.Equal(replaced, (await store.ReadAsync(files.ScenePath)).Stamp);
    }

    [Fact]
    public async Task Replace_MissingPreviouslyObservedFileIsConflictRatherThanImplicitRecreation()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var expected = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); File.Delete(files.ScenePath);
        var error = await Assert.ThrowsAsync<SceneFileConflictException>(() => store.WriteAsync(files.ScenePath, DocumentData.Empty(), expected));
        Assert.IsType<FileNotFoundException>(error.InnerException); Assert.False(File.Exists(files.ScenePath));
        Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp"));
        await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null);
        Assert.Equal(7, (await store.ReadAsync(files.ScenePath)).Document.Revision);
    }

    [Fact]
    public async Task SaveAs_UsesIndependentNewFileAndOriginalRemainsUntouched()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var first = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); var original = await File.ReadAllBytesAsync(files.ScenePath);
        var other = Path.Combine(files.Project.ScenesDirectory, "copy.AETHERSCENE");
        var second = await store.WriteAsync(other, DocumentData.Empty(17), null);
        Assert.Equal(original, await File.ReadAllBytesAsync(files.ScenePath)); Assert.Equal(first, (await store.ReadAsync(files.ScenePath)).Stamp);
        Assert.Equal(17, (await store.ReadAsync(other)).Document.Revision); Assert.Equal(second, (await store.ReadAsync(other)).Stamp);
        await Assert.ThrowsAsync<SceneFileConflictException>(() => store.WriteAsync(other, DocumentData.Scene(), null));
        Assert.Equal(17, (await store.ReadAsync(other)).Document.Revision);
    }

    [Fact]
    public async Task Read_StampIncludesBomAndWhitespaceWithoutNormalizingTheObservedFile()
    {
        using var files = new DocumentFiles(); var codec = new SceneFileCodec(DocumentData.Factory()); var store = new JsonSceneDocumentStore(codec);
        var canonical = codec.Encode(DocumentData.Scene()); byte[] actual = [0xEF, 0xBB, 0xBF, .. canonical, (byte)' ', (byte)'\n'];
        await File.WriteAllBytesAsync(files.ScenePath, actual);
        var loaded = await store.ReadAsync(files.ScenePath); DocumentData.AssertScene(loaded.Document);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(actual)), loaded.Stamp.Sha256);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(canonical)), loaded.Stamp.Sha256);
        Assert.Equal(actual, await File.ReadAllBytesAsync(files.ScenePath));
        var saved = await store.WriteAsync(files.ScenePath, loaded.Document, loaded.Stamp);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(canonical)), saved.Sha256);
        Assert.Equal(canonical, await File.ReadAllBytesAsync(files.ScenePath));
    }

    [Fact]
    public async Task CancellationAndInvalidCandidate_PreserveOriginalBeforeCreatingAnyTemporaryFile()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var stamp = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); var original = await File.ReadAllBytesAsync(files.ScenePath);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(files.ScenePath, DocumentData.Empty(), stamp, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAsync(files.ScenePath, cancellation.Token));
        var source = DocumentData.Scene(); var bad = new SceneDocument(source.Id, 7, source.Entities, [], source.Schemas);
        await Assert.ThrowsAsync<SceneResolutionException>(() => store.WriteAsync(files.ScenePath, bad, stamp));
        Assert.Equal(original, await File.ReadAllBytesAsync(files.ScenePath));
        Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp")); Assert.Equal(stamp, (await store.ReadAsync(files.ScenePath)).Stamp);
    }

    [Fact]
    public async Task CooperativeWriterLock_RejectsConcurrentPublicationWithoutChangingEitherFile()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var stamp = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); var original = await File.ReadAllBytesAsync(files.ScenePath);
        using (var held = new FileStream(files.ScenePath + ".write-lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => store.WriteAsync(files.ScenePath, DocumentData.Empty(), stamp));
            Assert.Equal(original, await File.ReadAllBytesAsync(files.ScenePath));
            Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp"));
        }
        Assert.Equal(stamp, await store.WriteAsync(files.ScenePath, DocumentData.Scene(), stamp));
    }

    [Fact]
    public async Task Replace_FailedAtomicCommitPreservesOriginalAndCleansPreparedTemporaryForRetry()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        var stamp = await store.WriteAsync(files.ScenePath, DocumentData.Scene(), null); var original = await File.ReadAllBytesAsync(files.ScenePath);
        using (var preventReplace = new FileStream(files.ScenePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAsync<IOException>(() => store.WriteAsync(files.ScenePath, DocumentData.Empty(8), stamp));
            Assert.Equal(original, await File.ReadAllBytesAsync(files.ScenePath));
            Assert.Empty(Directory.GetFiles(files.Project.ScenesDirectory, "*.tmp"));
        }
        var next = await store.WriteAsync(files.ScenePath, DocumentData.Empty(8), stamp);
        Assert.NotEqual(stamp, next); Assert.Equal(8, (await store.ReadAsync(files.ScenePath)).Document.Revision);
    }

    [Theory]
    [InlineData("extension")] [InlineData("missing-directory")]
    public async Task Store_RejectsInvalidPathsBeforeActivatingAuthoringOrWritingFiles(string kind)
    {
        using var files = new DocumentFiles(); var sessions = new DocumentData.Sessions(); var store = new JsonSceneDocumentStore(new(sessions));
        var path = kind == "extension" ? Path.Combine(files.Root, "room.json") : Path.Combine(files.Root, "missing", "room.aetherscene");
        if (kind == "extension")
        {
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(path));
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(path, DocumentData.Scene(), null));
        }
        else
        {
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => store.ReadAsync(path));
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => store.WriteAsync(path, DocumentData.Scene(), null));
        }
        Assert.Empty(sessions.Products); Assert.False(File.Exists(path)); Assert.False(File.Exists(path + ".write-lock"));
    }

    [Fact]
    public async Task Read_RejectsOversizedAndMalformedFilesWithoutMutatingTheirBytes()
    {
        using var files = new DocumentFiles(); var store = new JsonSceneDocumentStore(new(DocumentData.Factory()));
        await File.WriteAllBytesAsync(files.ScenePath, new byte[SceneFileCodec.MaximumBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(files.ScenePath));
        Assert.Equal(SceneFileCodec.MaximumBytes + 1, new FileInfo(files.ScenePath).Length);
        await File.WriteAllTextAsync(files.ScenePath, "{\"formatVersion\":2}");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(files.ScenePath));
        Assert.Equal("{\"formatVersion\":2}", await File.ReadAllTextAsync(files.ScenePath));
        Assert.False(File.Exists(files.ScenePath + ".write-lock"));
    }
}
