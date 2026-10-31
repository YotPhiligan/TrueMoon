namespace AetherGuiConsumer;

// Consumer-owned source fixture compiled by the editor, never loaded into the editor CLR.
internal static class SceneGameSource
{
    internal static void Write(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "Game.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Game.Logic</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
              <ItemGroup>
                <Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference>
                <Reference Include="TrueMoon.Contracts"><HintPath>$(TrueMoonContractsPath)</HintPath><Private>false</Private></Reference>
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Game.cs"), """
            using TrueMoon.Aether.Scenes;
            using TrueMoon.Aether.Sessions;
            using TrueMoon.Aether.Rendering;
            namespace Game.Logic;
            public sealed class ProbeGame : IGameSession, IRenderGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry
            {
                private PrimitiveScene? _scene;
                public static IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new ProbeGame();
                public ulong TickNumber { get; private set; }
                public long Revision { get; private set; } = -1;
                public uint PatternColor => 0xff2080c0;
                public void Load(SceneSnapshot scene)
                {
                    _scene = new PrimitiveScene(TrueMoon.Aether.Preview.PreviewGameSession.Camera,
                        scene.CopyEntities().Where(e => e.Primitive != null).Select(e => e.Primitive!.ToPrimitive()));
                    Revision = scene.Revision; TickNumber = 0;
                }
                public void Tick() => TickNumber++;
                public PrimitiveScene CaptureRenderScene() => _scene ?? throw new InvalidOperationException("No scene loaded.");
                public void Dispose() { _scene = null; Revision = -1; }
            }
            """);
    }
}
