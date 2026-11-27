using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class ProjectMaterialCatalogTests
{
    [Fact]
    public async Task RoundTripPreservesMaterialIdAndRejectsForeignProject()
    {
        using var files = new DocumentFiles();
        var catalog = new ProjectMaterialCatalog(files.Project);
        var path = PathFor(files);
        var empty = await catalog.ReadAsync(default);
        Assert.Empty(empty.Materials);
        Assert.Null(empty.Stamp);
        Assert.False(File.Exists(path));

        var material = new ProjectMaterial(Guid.NewGuid(), "  Plaster  ", new(.17f, .46f, .91f), Guid.NewGuid());
        var written = await catalog.WriteAsync(empty, material, default);
        var reopened = await new ProjectMaterialCatalog(files.Project).ReadAsync(default);
        Assert.NotNull(written.Stamp);
        Assert.Equal(written.Stamp, reopened.Stamp);
        Assert.Equal(material with { Name = "Plaster" }, Assert.Single(reopened.Materials));

        var foreign = files.Project with { Description = files.Project.Description with { Id = Guid.NewGuid() } };
        await Assert.ThrowsAsync<InvalidDataException>(() => new ProjectMaterialCatalog(foreign).ReadAsync(default));
    }

    [Fact]
    public async Task StaleMaterialWriteLeavesExternalValueIntact()
    {
        using var files = new DocumentFiles();
        var first = new ProjectMaterialCatalog(files.Project);
        var second = new ProjectMaterialCatalog(files.Project);
        var material = new ProjectMaterial(Guid.NewGuid(), "Plaster", Vector3.One, null);
        var createOnly = await second.ReadAsync(default);
        var original = await first.WriteAsync(await first.ReadAsync(default), material, default);
        var stale = await second.ReadAsync(default);
        var external = material with { BaseColorSrgb = new(.2f, .3f, .4f) };
        var current = await first.WriteAsync(original, external, default);
        var path = PathFor(files);
        var bytes = await File.ReadAllBytesAsync(path);

        await Assert.ThrowsAsync<SceneFileConflictException>(() => second.WriteAsync(createOnly, material, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => second.WriteAsync(stale, material with { Name = "Stale" }, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => second.WriteAsync(stale, material, default));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(external, (await second.ReadAsync(default)).Find(material.Id));

        File.SetLastWriteTimeUtc(path, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var modified = File.GetLastWriteTimeUtc(path);
        var unchanged = await first.WriteAsync(current, external, default);
        Assert.Equal(current.Stamp, unchanged.Stamp);
        Assert.Equal(modified, File.GetLastWriteTimeUtc(path));

        var oversized = new byte[1024 * 1024 + 1];
        await File.WriteAllBytesAsync(path, oversized);
        await Assert.ThrowsAsync<SceneFileConflictException>(() => first.WriteAsync(current, external, default));
        await Assert.ThrowsAsync<SceneFileConflictException>(() => first.WriteAsync(current, material, default));
        Assert.Equal(oversized, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task InvalidMaterialDoesNotReplaceCommittedCatalog()
    {
        using var files = new DocumentFiles();
        var catalog = new ProjectMaterialCatalog(files.Project);
        var material = new ProjectMaterial(Guid.NewGuid(), "Plaster", Vector3.One, null);
        var original = await catalog.WriteAsync(await catalog.ReadAsync(default), material, default);
        var path = PathFor(files);
        var bytes = await File.ReadAllBytesAsync(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.WriteAsync(original, material with { Name = "  " }, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.WriteAsync(original, material with { BaseColorTextureId = Guid.Empty }, default));
        await Assert.ThrowsAsync<RenderContentException>(() => catalog.WriteAsync(original,
            material with { BaseColorSrgb = new(float.NaN, 1, 1) }, default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.WriteAsync(original, material with { Name = "Renamed" }, cancelled.Token));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(original.Stamp, (await catalog.ReadAsync(default)).Stamp);
        Assert.Empty(Directory.GetFiles(files.Project.ContentDirectory, "*.tmp"));
    }

    [Theory]
    [InlineData("\"baseColorSrgb\":{\"r\":1,\"g\":1},\"baseColorTextureId\":null")]
    [InlineData("\"baseColorSrgb\":{\"r\":1,\"g\":1,\"b\":1}")]
    [InlineData("\"BaseColorSrgb\":{\"r\":1,\"g\":1,\"b\":1},\"baseColorTextureId\":null")]
    [InlineData("\"baseColorSrgb\":{\"r\":1,\"r\":0,\"g\":1,\"b\":1},\"baseColorTextureId\":null")]
    public async Task IncompleteOrNonCanonicalJsonIsNotTreatedAsAnEmptyCatalog(string fields)
    {
        using var files = new DocumentFiles();
        var path = PathFor(files);
        var json = $$"""{"formatVersion":1,"projectId":"{{files.Project.Description.Id:D}}","materials":[{"id":"{{Guid.NewGuid():D}}","name":"Plaster",{{fields}}}]}""";
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ProjectMaterialCatalog(files.Project).ReadAsync(default));
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    private static string PathFor(DocumentFiles files) => Path.Combine(files.Project.ContentDirectory, ".aether-material-catalog.json");
}
