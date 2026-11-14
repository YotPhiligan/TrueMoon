using System.Reflection;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Tooling.Mods;

if (args.Length != 3) throw new ArgumentException("artifactRoot modDll privateDependencyDll");
Console.WriteLine(ModArtifactPublisher.Publish(args[0], new ModManifest
{
    Id = "root", Version = "1.0.0", ReloadGroup = "proof", EntryAssembly = "ModFixture.dll",
    EntryType = "TrueMoon.ManualTests.ModFixture.Entry"
}, [args[1], args[2]]));
