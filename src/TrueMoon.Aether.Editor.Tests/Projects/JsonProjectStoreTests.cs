using System.Text;
using System.Text.Json;
using TrueMoon.Aether.Editor.Projects;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Projects;

public sealed class JsonProjectStoreTests
{
    [Fact]
    public void Resolve_NormalizesContainedPathsAndNameWithoutChangingPortableInput()
    {
        using var fixture = new ProjectFixture();
        var source = fixture.Description with { GameProject = "Game/../Game/Game.csproj", ScenesRoot = "Scenes/.", ContentRoot = "." };
        var actual = new JsonProjectStore().Resolve(fixture.Manifest, source);

        Assert.Equal(fixture.Manifest, actual.ManifestPath);
        Assert.Equal(fixture.Root, actual.RootDirectory);
        Assert.Equal(Path.Combine(fixture.Root, "Game", "Game.csproj"), actual.GameProjectPath);
        Assert.Equal(Path.Combine(fixture.Root, "Scenes"), actual.ScenesDirectory);
        Assert.Equal(fixture.Root, actual.ContentDirectory);
        Assert.Equal("Комната \"A\"", actual.Description.Name);
        Assert.Equal(("Game/Game.csproj", "Scenes", "."),
            (actual.Description.GameProject, actual.Description.ScenesRoot, actual.Description.ContentRoot));
        Assert.Equal(("  Комната \"A\"  ", "Game/../Game/Game.csproj", "Scenes/."),
            (source.Name, source.GameProject, source.ScenesRoot));
        Assert.False(File.Exists(fixture.Manifest));
    }

    [Fact]
    public async Task WriteNew_RoundTripsStrictPortableMetadataAndLeavesNoTemporaryFiles()
    {
        using var fixture = new ProjectFixture();
        var store = new JsonProjectStore();
        await store.WriteNewAsync(fixture.Resolved);
        var bytes = await File.ReadAllBytesAsync(fixture.Manifest);
        Assert.Equal((byte)'{', bytes[0]);
        using var json = JsonDocument.Parse(bytes);
        Assert.Equal(new[] { "contentRoot", "entryAssembly", "entryType", "formatVersion", "gameProject", "id", "name", "scenesRoot" },
            json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Комната \"A\"", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("Game/Game.csproj", json.RootElement.GetProperty("gameProject").GetString());
        Assert.Equal("82476a9d-dc9a-48cc-a187-0866c31c7021", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("Room.Logic.Game+Nested", json.RootElement.GetProperty("entryType").GetString());
        var read = await store.ReadAsync(fixture.Manifest);
        Assert.Equal("Room.Logic.dll", read.Description.EntryAssembly);
        Assert.Equal(Path.Combine(fixture.Root, "Content"), read.ContentDirectory);
        Assert.Equal(fixture.Resolved, read);
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("unknown")]
    [InlineData("case")]
    [InlineData("duplicate")]
    [InlineData("type")]
    [InlineData("trailing")]
    [InlineData("comment")]
    public async Task Read_RejectsInvalidJsonShapeOrStrictMemberContract(string kind)
    {
        using var fixture = new ProjectFixture();
        var valid = fixture.Json();
        var json = kind switch
        {
            "unknown" => valid[..^1] + ",\"future\":true}",
            "case" => valid.Replace("\"name\":", "\"Name\":"),
            "duplicate" => valid[..^1] + ",\"formatVersion\":1}",
            "type" => valid.Replace("\"formatVersion\":1", "\"formatVersion\":\"1\""),
            "trailing" => valid[..^1] + ",}",
            "comment" => "/* project */" + valid,
            _ => kind
        };
        await fixture.WriteJsonAsync(json);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new JsonProjectStore().ReadAsync(fixture.Manifest));
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
        Assert.Equal(json, await File.ReadAllTextAsync(fixture.Manifest));
    }

    [Theory]
    [InlineData(65535, true)]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public async Task Read_EnforcesUtf8ByteLimitIncludingExactMaximum(int length, bool accepted)
    {
        using var fixture = new ProjectFixture();
        var json = JsonSerializer.Serialize(fixture.Description, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.True(bytes.Length > json.Length);
        var padded = new byte[length];
        bytes.CopyTo(padded, 0);
        Array.Fill(padded, (byte)' ', bytes.Length, length - bytes.Length);
        await File.WriteAllBytesAsync(fixture.Manifest, padded);

        if (accepted)
        {
            var project = await new JsonProjectStore().ReadAsync(fixture.Manifest);
            Assert.Equal("Комната \"A\"", project.Description.Name);
            Assert.Equal(Path.Combine(fixture.Root, "Game", "Game.csproj"), project.GameProjectPath);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => new JsonProjectStore().ReadAsync(fixture.Manifest));
            Assert.Null(error.InnerException);
            Assert.Contains("65536", error.Message);
        }
        Assert.Equal(length, new FileInfo(fixture.Manifest).Length);
    }

    [Fact]
    public async Task Read_RequiresEveryVersionOneMemberInsteadOfAcceptingDeserializerDefaults()
    {
        using var fixture = new ProjectFixture();
        foreach (var missing in new[] { "formatVersion", "id", "name", "gameProject", "entryAssembly", "entryType", "scenesRoot", "contentRoot" })
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(fixture.Json())!.AsObject();
            Assert.True(json.Remove(missing));
            await fixture.WriteJsonAsync(json.ToJsonString());
            await Assert.ThrowsAsync<InvalidDataException>(() => new JsonProjectStore().ReadAsync(fixture.Manifest));
            Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
        }
    }

    [Fact]
    public async Task Read_AcceptsUtf8BomAtByteLimitButCountsItWhenRejectingFirstExcessByte()
    {
        using var fixture = new ProjectFixture();
        var json = Encoding.UTF8.GetBytes(fixture.Json());
        var bytes = Enumerable.Repeat((byte)' ', JsonProjectStore.MaximumBytes).ToArray();
        new byte[] { 0xEF, 0xBB, 0xBF }.CopyTo(bytes, 0);
        json.CopyTo(bytes, 3);
        await File.WriteAllBytesAsync(fixture.Manifest, bytes);
        var project = await new JsonProjectStore().ReadAsync(fixture.Manifest);
        Assert.Equal("Комната \"A\"", project.Description.Name);
        Assert.Equal(Path.Combine(fixture.Root, "Game", "Game.csproj"), project.GameProjectPath);
        Assert.Equal(65536, new FileInfo(fixture.Manifest).Length);
        await File.WriteAllBytesAsync(fixture.Manifest, [.. bytes, (byte)' ']);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new JsonProjectStore().ReadAsync(fixture.Manifest));
        Assert.Null(error.InnerException);
        Assert.Contains("65536", error.Message);
        Assert.Equal(65537, new FileInfo(fixture.Manifest).Length);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("assembly-path")]
    [InlineData("assembly-extension")]
    [InlineData("assembly-empty-name")]
    [InlineData("type")]
    [InlineData("game-extension")]
    public void Resolve_RejectsInvalidMetadataBeforePublishingManifest(string kind)
    {
        using var fixture = new ProjectFixture();
        var description = kind switch
        {
            "format" => fixture.Description with { FormatVersion = 2 },
            "id" => fixture.Description with { Id = Guid.Empty },
            "name" => fixture.Description with { Name = " " },
            "assembly-path" => fixture.Description with { EntryAssembly = "bin/Game.dll" },
            "assembly-extension" => fixture.Description with { EntryAssembly = "Game.exe" },
            "assembly-empty-name" => fixture.Description with { EntryAssembly = ".dll" },
            "type" => fixture.Description with { EntryType = "Game<T>" },
            "game-extension" => fixture.Description with { GameProject = "Game/Game.sln" },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Assert.Throws<InvalidDataException>(() => new JsonProjectStore().Resolve(fixture.Manifest, description));
        Assert.False(File.Exists(fixture.Manifest));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    [Theory]
    [InlineData("name", 128, true)]
    [InlineData("name", 129, false)]
    [InlineData("entryType", 512, true)]
    [InlineData("entryType", 513, false)]
    public void Resolve_PreservesLastAllowedTextLengthAndRejectsFirstExcess(string field, int length, bool accepted)
    {
        using var fixture = new ProjectFixture();
        var text = new string('a', length);
        var description = field == "name" ? fixture.Description with { Name = text } : fixture.Description with { EntryType = text };
        var store = new JsonProjectStore();
        if (accepted)
        {
            var project = store.Resolve(fixture.Manifest, description);
            Assert.Equal(text, field == "name" ? project.Description.Name : project.Description.EntryType);
            Assert.Equal(fixture.Manifest, project.ManifestPath);
        }
        else Assert.Throws<InvalidDataException>(() => store.Resolve(fixture.Manifest, description));
        Assert.False(File.Exists(fixture.Manifest));
    }

    [Theory]
    [InlineData("game", "../outside/Game.csproj")]
    [InlineData("scenes", "../sibling/Scenes")]
    [InlineData("content", "../sibling/Content")]
    [InlineData("game", "C:/outside/Game.csproj")]
    [InlineData("scenes", "/outside/Scenes")]
    [InlineData("content", "Content:alternate")]
    public void Resolve_RejectsEachEscapingOrRootedPath(string field, string path)
    {
        using var fixture = new ProjectFixture();
        var description = field switch
        {
            "game" => fixture.Description with { GameProject = path },
            "scenes" => fixture.Description with { ScenesRoot = path },
            "content" => fixture.Description with { ContentRoot = path },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        Assert.Throws<InvalidDataException>(() => new JsonProjectStore().Resolve(fixture.Manifest, description));
        Assert.False(File.Exists(fixture.Manifest));
    }

    [Theory]
    [InlineData("game")]
    [InlineData("scenes")]
    [InlineData("content")]
    public void Resolve_RequiresExistingGameAndRootsWithoutCreatingThem(string missing)
    {
        using var fixture = new ProjectFixture();
        var description = missing switch
        {
            "game" => fixture.Description with { GameProject = "Game/Missing.csproj" },
            "scenes" => fixture.Description with { ScenesRoot = "MissingScenes" },
            "content" => fixture.Description with { ContentRoot = "MissingContent" },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        if (missing == "game")
        {
            var error = Assert.Throws<FileNotFoundException>(() => new JsonProjectStore().Resolve(fixture.Manifest, description));
            Assert.Equal(Path.Combine(fixture.Root, "Game", "Missing.csproj"), error.FileName);
        }
        else Assert.Throws<DirectoryNotFoundException>(() => new JsonProjectStore().Resolve(fixture.Manifest, description));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "MissingScenes")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "MissingContent")));
        Assert.False(File.Exists(fixture.Manifest));
    }

    [Fact]
    public async Task WriteNew_ExistingFileIsPreservedAndTemporaryOutputIsRemoved()
    {
        using var fixture = new ProjectFixture();
        await fixture.WriteJsonAsync("existing project must survive");
        await Assert.ThrowsAsync<IOException>(() => new JsonProjectStore().WriteNewAsync(fixture.Resolved));
        Assert.Equal("existing project must survive", await File.ReadAllTextAsync(fixture.Manifest));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task ReadAndWrite_PreCancelledCallsPerformNoFilePublicationOrRead()
    {
        using var fixture = new ProjectFixture();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var store = new JsonProjectStore();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAsync(fixture.Manifest, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteNewAsync(fixture.Resolved, cancelled.Token));
        Assert.False(File.Exists(fixture.Manifest));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task WriteNew_RevalidatesInputAndRecomputesAbsolutePathsBeforeWriting()
    {
        using var fixture = new ProjectFixture();
        var project = fixture.Resolved with { GameProjectPath = "forged", RootDirectory = "forged" };
        await new JsonProjectStore().WriteNewAsync(project);
        var read = await new JsonProjectStore().ReadAsync(fixture.Manifest);
        Assert.Equal(Path.Combine(fixture.Root, "Game", "Game.csproj"), read.GameProjectPath);
        Assert.Equal(fixture.Root, read.RootDirectory);
        File.Delete(Path.Combine(fixture.Root, "Game", "Game.csproj"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => new JsonProjectStore().WriteNewAsync(project with { ManifestPath = Path.Combine(fixture.Root, "missing.aetherproj") }));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "missing.aetherproj")));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }
}
