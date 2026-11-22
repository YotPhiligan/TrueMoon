using System.Runtime.Versioning;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal sealed class EditorSandbox(string directory)
{
    internal string ProjectPath { get; } = Path.Combine(directory, "Sandbox.aetherproj");
    internal string ScenePath { get; } = Path.Combine(directory, "Scenes", "Empty.aetherscene");

    internal async Task PrepareAsync(IProjectStore projects, ISceneDocumentStore scenes)
    {
        const string gameProject = "Game/SandboxGame.csproj";
        var gameDirectory = Path.Combine(directory, "Game");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(Path.Combine(directory, "Scenes"));
        Directory.CreateDirectory(Path.Combine(directory, "Content"));

        var framework = new FrameworkName(AppContext.TargetFrameworkName!).Version.ToString(2);
        await File.WriteAllTextAsync(Path.Combine(directory, gameProject), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net{{framework}}</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="TrueMoon.Aether.Abstractions">
                  <HintPath>$(AetherAbstractionsPath)</HintPath>
                  <Private>false</Private>
                </Reference>
                <Reference Include="TrueMoon.Contracts">
                  <HintPath>$(TrueMoonContractsPath)</HintPath>
                  <Private>false</Private>
                </Reference>
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(gameDirectory, "SandboxGame.cs"), """
            using TrueMoon.Aether.Abstractions.Runtime;
            using TrueMoon.Aether.Abstractions.Scenes;
            using TrueMoon.Aether.Abstractions.Sessions;
            using TrueMoon.Services;

            namespace EditorSandbox;

            public sealed class SandboxGame : IGameSession, IGameSessionEntry
            {
                public static IGameSession Create(IServiceResolver? services) => new SandboxGame();
                public ulong TickNumber { get; private set; }
                public long Revision { get; private set; }
                public uint PatternColor => 0xff303040;
                public void Load(SceneSnapshot scene) => Revision = scene.Revision;
                public void Tick() => TickNumber++;
                public void Dispose() { }
            }
            """);
        var description = new ProjectDescription(1, Guid.NewGuid(), "Песочница редактора", gameProject,
            "SandboxGame.dll", "EditorSandbox.SandboxGame", "Scenes", "Content");
        await projects.WriteNewAsync(projects.Resolve(ProjectPath, description));
        var scene = new SceneDocument(new(Guid.NewGuid()), 0, [], [],
            [.. BuiltInComponents.Schemas, .. MeshRenderingComponents.Schemas]);
        await scenes.WriteAsync(ScenePath, scene, expected: null);
    }
}
