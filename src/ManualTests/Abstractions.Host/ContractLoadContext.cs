using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.ManualTests.ContractHost;

/// <summary>Bounded acceptance helper, not a public mod loader or replacement API.</summary>
public sealed class ContractLoadContext : AssemblyLoadContext
{
    private readonly Dictionary<string, Assembly> _shared = new(StringComparer.Ordinal);
    public ContractLoadContext(string extensionPath) : base(isCollectible: true)
    {
        foreach (var assembly in new[] { typeof(IEcsSystem).Assembly, typeof(IFactory<>).Assembly })
        {
            var name = assembly.GetName().Name!; _shared.Add(name, assembly);
            var copy = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(extensionPath))!, name + ".dll");
            if (File.Exists(copy) && !SHA256.HashData(File.ReadAllBytes(copy)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(assembly.Location))))
                throw new InvalidDataException("Mismatched private shared-contract copy: " + name);
        }
    }
    protected override Assembly? Load(AssemblyName name)
    {
        if (!_shared.TryGetValue(name.Name!, out var shared)) return null;
        if (name.FullName != shared.GetName().FullName) throw new InvalidDataException("Shared-contract identity mismatch: " + name.Name);
        return shared;
    }
}
