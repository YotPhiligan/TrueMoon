using System.Text.Json;
using TrueMoon.Aether.Editor.Projects;

namespace TrueMoon.Aether.Editor.Tests.Projects;

internal sealed class ProjectFixture : IDisposable
{
    public ProjectFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "TrueMoon-ProjectTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "Game"));
        Directory.CreateDirectory(Path.Combine(Root, "Scenes"));
        Directory.CreateDirectory(Path.Combine(Root, "Content"));
        File.WriteAllText(Path.Combine(Root, "Game", "Game.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
    }

    public string Root { get; }
    public string Manifest => Path.Combine(Root, "room.aetherproj");
    public ProjectDescription Description => new(1, Guid.Parse("82476a9d-dc9a-48cc-a187-0866c31c7021"),
        "  Комната \"A\"  ", "Game/Game.csproj", "Room.Logic.dll", "Room.Logic.Game+Nested", "Scenes", "Content");
    public ResolvedProject Resolved => new JsonProjectStore().Resolve(Manifest, Description);
    public string Json(ProjectDescription? description = null) => JsonSerializer.Serialize(description ?? Description,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public Task WriteJsonAsync(string json) => File.WriteAllTextAsync(Manifest, json);

    public void Dispose()
    {
        var temporary = Path.GetFullPath(Path.GetTempPath());
        var path = Path.GetFullPath(Root);
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(temporary) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith("TrueMoon-ProjectTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test cleanup target.");
        Directory.Delete(path, recursive: true);
    }
}
