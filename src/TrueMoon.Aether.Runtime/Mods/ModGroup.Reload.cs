using System.Reflection;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime.Ecs;

namespace TrueMoon.Aether.Runtime.Mods;

public sealed partial class ModGroup
{
    private readonly ModGeneration _generation = new();
    private readonly List<EcsScheduler> _reloadSchedulers = [];
    internal string RootId => ModuleIds[^1];
    internal void PublishGeneration() => _generation.Open();
    internal void RetireGeneration() => _generation.Retire();
    internal void CloseGeneration() => _generation.Close();
    private bool Stable(Type type) => !type.Assembly.IsCollectible &&
        (_artifacts!.Shared.Values.Contains(type.Assembly) || ModArtifactGroup.Framework.ContainsKey(type.Assembly.GetName().Name!)) &&
        (!type.HasElementType || Stable(type.GetElementType()!)) && type.GetGenericArguments().All(Stable);
    private void ValidateReload()
    {
        if (_registration!.ComponentCount != 0 || _modules.Any(m => m.Value is not IModReloadParticipant) ||
            _registration.Systems.Any(s => s.Plan.Reads.Concat(s.Plan.Writes).Any(t => !Stable(t))))
            throw new ModFailure(ModLoadError.ReloadNotSupported, "System replacement requires reversible entries, stable shared access types and no component registrations.");
    }
    internal EcsScheduler PrepareSystems(World world, EcsScheduler? previous)
    {
        try { world.ValidateStableComponents(Stable); }
        catch { throw new ModFailure(ModLoadError.ReloadNotSupported, "World contains unsupported component identities or is not idle/healthy."); }
        return EcsScheduler.PrepareDetached(world, _registration!.Systems, scheduler => _reloadSchedulers.Add(scheduler), previous);
    }
    internal async Task QuiesceReloadAsync(CancellationToken token)
    {
        await _generation.QuiesceAsync(token).ConfigureAwait(false);
        for (var i = _modules.Count - 1; i >= 0; i--)
        { _modules[i].Quiesced = true; await ((IModReloadParticipant)_modules[i].Value!).QuiesceAsync(token).ConfigureAwait(false); }
        for (var i = _services.Count - 1; i >= 0; i--)
        { _services[i].Quiesced = true; await ((IModReloadParticipant)_services[i].Value!).QuiesceAsync(token).ConfigureAwait(false); }
    }
    internal async Task ResumeReloadAsync()
    {
        foreach (var service in _services)
            if (service.Quiesced) { await ((IModReloadParticipant)service.Value!).ResumeAsync(CancellationToken.None).ConfigureAwait(false); service.Quiesced = false; }
        foreach (var module in _modules)
            if (module.Quiesced) { await ((IModReloadParticipant)module.Value!).ResumeAsync(CancellationToken.None).ConfigureAwait(false); module.Quiesced = false; }
        _generation.Open();
    }
}
