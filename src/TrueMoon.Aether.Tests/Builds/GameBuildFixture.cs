using System.Security;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildFixture : IAsyncLifetime
{
    public const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-GameBuildTests-" + Guid.NewGuid().ToString("N"));
    public GameBuildArtifact A { get; private set; } = null!;
    public GameBuildArtifact B { get; private set; } = null!;
    public string ProjectA { get; private set; } = "";
    internal bool PreserveArtifacts { get; set; }
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Root);
        await File.WriteAllTextAsync(Path.Combine(Root, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        await using var compiler = new GameBuildCompiler(Hash, Path.Combine(Root, "artifacts"));
        var dependency = Project("private", "Game.Private", "namespace Game.Private; public static class Palette { public static uint Color => 0xff2080c0; }");
        var library = await compiler.BuildAsync(new(dependency, "Game.Private.dll", "Game.Private.Palette", 0));
        var reference = $"<Reference Include=\"Game.Private\"><HintPath>{SecurityElement.Escape(Path.Combine(library.DirectoryPath, "Game.Private.dll"))}</HintPath><Private>true</Private></Reference>";
        ProjectA = Project("a", "Game.Candidate", Source("global::Game.Private.Palette.Color"), reference);
        var projectB = Project("b", "Game.Candidate", Source("global::Game.Private.Palette.Color ^ 0x00e0e0e0u"), reference);
        A = await compiler.BuildAsync(new(ProjectA, "Game.Candidate.dll", "Game.Candidate.Game", 7, ["Game.Private.dll"]));
        B = await compiler.BuildAsync(new(projectB, "Game.Candidate.dll", "Game.Candidate.Game", 8, ["Game.Private.dll"]));
    }
    public string Project(string directory, string assembly, string source, string references = "", string target = "")
    {
        var root = Path.Combine(Root, directory); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Game.cs"), source);
        var project = Path.Combine(root, assembly + ".csproj");
        File.WriteAllText(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>{assembly}</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
              <ItemGroup><Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference><Reference Include="TrueMoon.Contracts"><HintPath>$(TrueMoonContractsPath)</HintPath><Private>false</Private></Reference>{references}</ItemGroup>
              {target}
            </Project>
            """);
        return project;
    }
    public (string Project, string Marker, string Release) HeldProject()
    {
        var id = "held-" + Guid.NewGuid().ToString("N"); var directory = Path.Combine(Root, id); Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, "entered"); var release = Path.Combine(directory, "release");
        var script = Path.Combine(directory, "hold.ps1");
        File.WriteAllText(script, "param($marker,$release)\n[IO.File]::WriteAllText($marker,[Environment]::ProcessId.ToString())\nwhile (!(Test-Path -LiteralPath $release)) { Start-Sleep -Milliseconds 20 }\n");
        var command = SecurityElement.Escape($"pwsh -NoProfile -NonInteractive -File \"{script}\" \"{marker}\" \"{release}\"");
        var target = $"<Target Name=\"HoldBuild\" BeforeTargets=\"CoreCompile\"><Exec Command=\"{command}\" /></Target>";
        return (Project(id, "Game.Held", Source("0xff2080c0u"), target: target), marker, release);
    }
    public GameBuildArtifact CopyA() => GameBuildArtifact.Publish(A.DirectoryPath, Path.Combine(Root, "copy-" + Guid.NewGuid().ToString("N")),
        Hash, A.EntryAssembly, A.EntryType, A.SourceRevision, ["Game.Private.dll"]);
    public static string Source(string color) => $$"""
        using TrueMoon.Aether.Scenes;
        using TrueMoon.Aether.Sessions;
        namespace Game.Candidate;
        public sealed class Game : IGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry
        {
            public static IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new Game();
            public ulong TickNumber { get; private set; }
            public long Revision { get; private set; } = -1;
            public uint PatternColor => {{color}};
            public void Load(SceneSnapshot scene) { Revision=scene.Revision; TickNumber=0; }
            public void Tick() { TickNumber++; }
            public void Dispose() { Revision=-1; }
        }
        """;
    public Task DisposeAsync()
    {
        if (B == null || PreserveArtifacts) return Task.CompletedTask; // Preserve failed fixture compiler diagnostics.
        // These direct loader tests run in a long-lived test host. Production pins
        // last until child reap; here allow collectible contexts to release CLR mappings.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test cleanup escaped temp directory.");
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
        return Task.CompletedTask;
    }
}
