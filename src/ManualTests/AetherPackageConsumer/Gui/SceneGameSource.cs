using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.Json;

namespace AetherGuiConsumer;

// Consumer-owned source fixture compiled by the editor, never loaded into the editor CLR.
internal static class SceneGameSource
{
    internal static void Write(string directory)
    {
        var shared = Path.Combine(Path.GetDirectoryName(directory)!, "Shared"); Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "Game.Shared.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            <ItemGroup><PackageReference Include="Game.Packaged" Version="[1.0.0]"/></ItemGroup></Project>
            """);
        File.WriteAllText(Path.Combine(shared, "Constants.cs"), "namespace Game.Shared; public static class Constants { public static int Version => Game.Packaged.Marker.Version; }");
        WritePackage(Path.GetDirectoryName(directory)!, shared);
        File.WriteAllText(Path.Combine(directory, "Game.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Game.Logic</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Shared/Game.Shared.csproj" />
                <Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference>
                <Reference Include="TrueMoon.Contracts"><HintPath>$(TrueMoonContractsPath)</HintPath><Private>false</Private></Reference>
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Game.cs"), """
            using TrueMoon.Aether.Scenes;
            using TrueMoon.Aether.Sessions;
            using TrueMoon.Aether.Rendering;
            using TrueMoon.Aether.Checkpoints;
            namespace Game.Logic;
            public sealed class ProbeGame : ICheckpointGameSession, IRenderGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry
            {
                private PrimitiveScene? _scene;
                private static readonly bool RejectRestore = false;
                private static readonly string? RestoreGate = null;
                private static readonly string? CaptureNotice = null;
                public static IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new ProbeGame();
                public ulong TickNumber { get; private set; }
                public long Revision { get; private set; } = -1;
                public uint PatternColor => 0xff2080c0;
                public void Load(SceneSnapshot scene)
                {
                    if (Game.Shared.Constants.Version != 1) throw new InvalidOperationException("Unexpected private dependency payload.");
                    _scene = new PrimitiveScene(TrueMoon.Aether.Preview.PreviewGameSession.Camera,
                        scene.CopyEntities().Where(e => e.Primitive != null).Select(e => e.Primitive!.ToPrimitive()));
                    Revision = scene.Revision; TickNumber = 0;
                }
                public void Tick() => TickNumber++;
                public GameCheckpointState CaptureCheckpoint()
                {
                    if (CaptureNotice is { } notice) File.AppendAllText(notice, Environment.ProcessId + "\n");
                    return new(TickNumber, TrueMoon.Aether.Preview.PreviewGameSession.Camera, false);
                }
                public void RestoreCheckpoint(GameCheckpointState state)
                {
                    state.Validate();
                    if (RestoreGate is { } gate)
                    {
                        File.WriteAllText(gate + ".entered", Environment.ProcessId.ToString());
                        var wait = System.Diagnostics.Stopwatch.StartNew();
                        while (!File.Exists(gate + ".release"))
                        {
                            if (wait.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Acceptance restore gate was not released.");
                            Thread.Sleep(10);
                        }
                    }
                    if (RejectRestore) throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
                    TickNumber = state.Tick;
                }
                public PrimitiveScene CaptureRenderScene() => _scene ?? throw new InvalidOperationException("No scene loaded.");
                public void Dispose() { _scene = null; Revision = -1; }
            }
            """);
    }

    internal static void WritePackage(string root, string shared, bool alternate = false)
    {
        var packages = Path.Combine(root, "Packages"); Directory.CreateDirectory(packages);
        var path = Path.Combine(packages, "game.packaged.1.0.0.nupkg");
        using (var output = File.Create(path))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("Game.Packaged.nuspec").Open()))
                writer.Write("<package><metadata><id>Game.Packaged</id><version>1.0.0</version></metadata></package>");
            var assembly = new PersistedAssemblyBuilder(new AssemblyName("Game.Packaged"), typeof(object).Assembly);
            var type = assembly.DefineDynamicModule("Game.Packaged").DefineType("Game.Packaged.Marker", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var getter = type.DefineMethod("get_Version", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName, typeof(int), Type.EmptyTypes);
            var il = getter.GetILGenerator();
            if (alternate) il.Emit(OpCodes.Nop); // Different valid DLL bytes, same consumer-visible value.
            il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ret);
            type.DefineProperty("Version", PropertyAttributes.None, typeof(int), null).SetGetMethod(getter); type.CreateType();
            using var bytes = new MemoryStream(); assembly.Save(bytes);
            using var dll = archive.CreateEntry("lib/net10.0/Game.Packaged.dll").Open(); dll.Write(bytes.ToArray());
        }
        File.WriteAllText(Path.Combine(shared, "packages.lock.json"), JsonSerializer.Serialize(new
        {
            version = 1, dependencies = new Dictionary<string, object> { ["net10.0"] = new Dictionary<string, object>
            { ["Game.Packaged"] = new { type = "Direct", requested = "[1.0.0, 1.0.0]", resolved = "1.0.0", contentHash = Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(path))) } } }
        }));
    }
}
