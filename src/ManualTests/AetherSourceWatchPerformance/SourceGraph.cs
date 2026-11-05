using System.Text;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Projects;

namespace AetherSourceWatchPerformance;

// Consumer-owned synthetic source, built by the real frozen compiler. No fabricated descriptor or friend access.
internal sealed record SourceGraph(ResolvedProject Project, GameBuildRequest Request, int GeneratedFiles, long GeneratedBytes)
{
    internal static SourceGraph Write(string root, string profile)
    {
        var (filesPerProject, bytesPerFile) = profile switch
        {
            "control" => (16, 1024),
            "files" => (640, 4096),
            "bytes" => (32, 1024 * 1024),
            _ => throw new ArgumentException("Expected control, files or bytes.", nameof(profile))
        };
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Scenes")); Directory.CreateDirectory(Path.Combine(root, "Content"));
        File.WriteAllText(Path.Combine(root, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        var names = new[] { "Root", "Left", "Right", "Shared" };
        for (var project = 0; project < names.Length; project++)
        {
            var name = names[project]; var directory = Path.Combine(root, name); Directory.CreateDirectory(directory);
            var references = project switch
            {
                0 => "<ProjectReference Include=\"../Left/Perf.Left.csproj\" /><ProjectReference Include=\"../Right/Perf.Right.csproj\" />",
                1 or 2 => "<ProjectReference Include=\"../Shared/Perf.Shared.csproj\" />",
                _ => ""
            };
            File.WriteAllText(Path.Combine(directory, $"Perf.{name}.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Perf.{{name}}</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
                  <ItemGroup>{{references}}
                    <Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference>
                    <Reference Include="TrueMoon.Contracts"><HintPath>$(TrueMoonContractsPath)</HintPath><Private>false</Private></Reference>
                  </ItemGroup>
                </Project>
                """);
            for (var file = 0; file < filesPerProject; file++)
            {
                var folder = Path.Combine(directory, $"Part{file / 32:D3}"); Directory.CreateDirectory(folder);
                var header = $"namespace Perf.{name}.Generated; public static class C{file:D4} {{ public const int Value = {file}; }}\n/*";
                var content = header + new string('x', bytesPerFile - header.Length - 3) + "*/\n";
                // All tree members are observed. Non-Compile data isolates byte volume from the compiler's shared input budget.
                var extension = profile == "bytes" ? "dat" : "cs";
                File.WriteAllText(Path.Combine(folder, $"C{file:D4}.{extension}"), content, new UTF8Encoding(false));
            }
            if (profile == "bytes") File.WriteAllText(Path.Combine(directory, "Marker.cs"), $"namespace Perf.{name}; public static class Marker {{ public const int Value = 1; }}");
        }
        File.WriteAllText(Path.Combine(root, "Root", "Game.cs"), """
            using TrueMoon.Aether.Scenes;
            using TrueMoon.Aether.Sessions;
            namespace Perf.Root;
            public sealed class Game : IGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry
            {
                public static IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new Game();
                public ulong TickNumber { get; private set; }
                public long Revision { get; private set; } = -1;
                public uint PatternColor => 0xff2080c0;
                public void Load(SceneSnapshot scene) { Revision = scene.Revision; TickNumber = 0; }
                public void Tick() { TickNumber++; }
                public void Dispose() { Revision = -1; }
            }
            """);
        var path = Path.Combine(root, "Root", "Perf.Root.csproj");
        var description = new ProjectDescription(2, Guid.NewGuid(), "Observer performance", "Root/Perf.Root.csproj", "Perf.Root.dll", "Perf.Root.Game", "Scenes", "Content")
            { Dependencies = ["Perf.Left.dll", "Perf.Right.dll", "Perf.Shared.dll"] };
        var resolved = new ResolvedProject(description, Path.Combine(root, "perf.aetherproj"), root, path, Path.Combine(root, "Scenes"), Path.Combine(root, "Content"));
        return new(resolved, new(path, description.EntryAssembly, description.EntryType, 1, description.Dependencies)
            { TrackInputs = true, CaptureCompilation = true, CapturePreparation = true, CaptureProjectReferences = true },
            filesPerProject * 4, (long)filesPerProject * 4 * bytesPerFile);
    }
}
