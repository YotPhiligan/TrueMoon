using System.Reflection;
using System.Runtime.Loader;

namespace TrueMoon.Aether.Runtime.Mods;

internal sealed class ModLoadContext(ModArtifactGroup artifacts) : AssemblyLoadContext("Aether.ModGroup-" + Guid.NewGuid().ToString("N"), true)
{
    protected override Assembly? Load(AssemblyName requested)
    {
        var name = requested.Name ?? throw new FileLoadException("Assembly name is required.");
        if (artifacts.Shared.TryGetValue(name, out var shared))
        {
            if (!ModArtifactGroup.Same(requested.FullName, shared.FullName)) throw new FileLoadException("Shared identity mismatch.");
            return shared;
        }
        if (artifacts.Images.TryGetValue(name, out var image))
        {
            if (!ModArtifactGroup.Same(requested.FullName, image.Identity)) throw new FileLoadException("Private identity mismatch.");
            return LoadFromStream(new MemoryStream(image.Bytes, false));
        }
        if (ModArtifactGroup.Framework.TryGetValue(name, out var framework) && ModArtifactGroup.CompatibleFramework(requested, framework)) return null;
        throw new FileLoadException("Assembly is outside the declared module closure.");
    }
    internal Assembly Entry(string file) => LoadFromAssemblyName(new AssemblyName(artifacts.Images[Path.GetFileNameWithoutExtension(file)].Identity));
    protected override nint LoadUnmanagedDll(string unmanagedDllName) => throw new DllNotFoundException("Native module libraries are not supported.");
}
