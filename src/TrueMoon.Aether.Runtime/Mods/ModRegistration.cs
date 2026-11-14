using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;

namespace TrueMoon.Aether.Runtime.Mods;

internal sealed class ModRegistration : IModRegistration
{
    internal ComponentRegistryBuilder? Components = new();
    internal readonly List<EcsSystemRegistration> Systems = [];
    internal readonly List<(string Id, IFactory<IModService> Factory)> Services = [];
    private readonly HashSet<string> _systemIds = new(StringComparer.Ordinal), _serviceIds = new(StringComparer.Ordinal);
    private bool _sealed;
    public void AddSystem(EcsSystemMetadata metadata, IFactory<ISystem> factory)
    {
        Check(); ArgumentNullException.ThrowIfNull(metadata); ArgumentNullException.ThrowIfNull(factory);
        if (Systems.Count >= 256 || !_systemIds.Add(metadata.Id)) throw new InvalidOperationException("Duplicate/excessive module system registration.");
        Systems.Add(new(metadata, factory));
    }
    public void AddComponent<T>(ComponentSchema schema, IFactory<T, ComponentData> factory) where T : struct
    { Check(); Components!.RegisterValues(schema, factory); }
    public void AddService(string id, IFactory<IModService> factory)
    {
        Check(); ArgumentNullException.ThrowIfNull(factory);
        if (!ModArtifactGroup.Id(id) || Services.Count >= 256 || !_serviceIds.Add(id)) throw new InvalidOperationException("Duplicate/invalid/excessive module service registration.");
        Services.Add((id, factory));
    }
    internal void Seal() => _sealed = true;
    internal void Clear() { Seal(); Components = null; Systems.Clear(); Services.Clear(); _systemIds.Clear(); _serviceIds.Clear(); }
    private void Check() { if (_sealed) throw new InvalidOperationException("Module registration has ended."); }
}
