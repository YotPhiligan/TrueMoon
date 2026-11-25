using TrueMoon.Aether.Mods;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>App-local artifact/entry composition. Factory results belong exclusively to ModHost, never to root DI.</summary>
public sealed class ModGroupFactory : IFactory<ModGroup, ModLoadRequest>
{
    private readonly ModLoaderOptions _options;
    private readonly IServiceResolver _resolver;
    private readonly IFactory<IModEntry, ModEntryArguments> _entries;
    private readonly IFactory<ModWorldSession> _worlds;
    /// <summary>Copies policy and borrows stable dependencies.</summary>
    /// <param name="options">Shared API and admission bounds.</param><param name="resolver">Composition-only resolver, protected from collectible Type requests.</param>
    /// <param name="entries">Entry activation factory without Type/result caching.</param><param name="worlds">Fresh private session owner factory.</param>
    public ModGroupFactory(ModLoaderOptions options, IServiceResolver resolver, IFactory<IModEntry, ModEntryArguments> entries, IFactory<ModWorldSession> worlds)
    { _options = options.Snapshot(); _resolver = resolver; _entries = entries; _worlds = worlds; }
    /// <summary>Validates and pins the complete closure, then allocates a collectible group without executing module code.</summary>
    /// <param name="arguments">Complete artifact closure.</param><returns>Unpublished group owned by the caller.</returns>
    public ModGroup Create(ModLoadRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var artifacts = ModArtifactGroup.Open(arguments, _options, CancellationToken.None);
        try { return new(artifacts, _entries, _worlds, new ModHostServices(_resolver, artifacts.Shared.Values.ToArray())); }
        catch { artifacts.Dispose(); throw; }
    }
}
