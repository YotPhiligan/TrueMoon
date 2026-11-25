using System.Reflection;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Tooling.Mods;

if (args.Length is < 3 or > 5) throw new ArgumentException("artifactRoot modDll privateDependencyDll [entryType] [moduleVersion]");
Console.WriteLine(ModArtifactPublisher.Publish(args[0], new ModManifest
{
    Id = "root", Version = args.Length == 5 ? args[4] : "1.0.0", ReloadGroup = "proof", EntryAssembly = "ModFixture.dll",
    EntryType = args.Length >= 4 ? args[3] : "TrueMoon.ManualTests.ModFixture.Entry"
}, [args[1], args[2]]));
