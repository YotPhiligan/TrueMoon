using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using A = TrueMoon.Aether.Ecs;

namespace TrueMoon.ManualTests.KtonPerformance;

// Only enabled by the allocation-attribution mode. No component access or mutations.
internal sealed class AllocationCounters
{
    internal bool Enabled;
    private long _last, _physics, _movement, _playback, _transforms, _extraction;
    internal int Frames;
    internal void Mark(int marker)
    {
        if (!Enabled) return;
        var now = GC.GetAllocatedBytesForCurrentThread();
        switch (marker)
        {
            case 1: _physics += now - _last; break;
            case 2: _movement += now - _last; break;
            case 3: _playback += now - _last; break;
            case 4: _transforms += now - _last; break;
            case 6: _extraction += now - _last; Frames++; break;
        }
        _last = now;
    }
    internal object Snapshot(long primaryBytes, long nativeBytes, int samples, bool ecs) => new
    {
        Scope = "Primary-thread bytes only; markers bracket scheduler systems/phases. No timing claims from attribution mode. Physics system includes native Step; exclusive bridge subtracts that same counter, not percentiles.",
        Frames, NativeStepBytesPerTick = nativeBytes / (double)samples,
        PhysicsSystemInclusiveBytesPerTick = _physics / (double)samples,
        PhysicsBridgeExclusiveBytesPerTick = ecs ? (_physics - nativeBytes) / (double)samples : 0,
        BuiltinMovementBytesPerTick = _movement / (double)samples,
        MovementPlaybackBytesPerTick = _playback / (double)samples,
        TransformSystemBytesPerTick = _transforms / (double)samples,
        ExtractionSystemBytesPerTick = _extraction / (double)samples,
        OtherPrimaryBytesPerTick = (primaryBytes - (ecs ? _physics + _movement + _playback + _transforms + _extraction : nativeBytes)) / (double)samples
    };
}

internal sealed class AllocationMarkerSubsystem : ISceneSubsystem
{
    public IReadOnlyList<EcsSystemRegistration> Systems { get; }
    internal AllocationMarkerSubsystem(AllocationCounters counters)
    {
        Systems =
        [
            Registration(new A.EcsSystemDescription("probe.physics.start", A.EcsPhase.Movement).Before("kton.physics"), 0),
            Registration(new A.EcsSystemDescription("probe.physics.end", A.EcsPhase.Movement).After("kton.physics").Before("builtin.movement"), 1),
            Registration(new A.EcsSystemDescription("probe.movement.end", A.EcsPhase.Movement).After("builtin.movement"), 2),
            Registration(new A.EcsSystemDescription("probe.transform.start", A.EcsPhase.Transform).Before("builtin.transform"), 3),
            Registration(new A.EcsSystemDescription("probe.transform.end", A.EcsPhase.Transform).After("builtin.transform"), 4),
            Registration(new A.EcsSystemDescription("probe.extraction.start", A.EcsPhase.Extraction).Before("builtin.primitive-extraction"), 5),
            Registration(new A.EcsSystemDescription("probe.extraction.end", A.EcsPhase.Extraction).After("builtin.primitive-extraction"), 6)
        ];
        EcsSystemRegistration Registration(A.EcsSystemDescription description, int marker) => new(description,
            new Factory<A.ISystem, World>(_ => new Marker(counters, marker)));
    }
    public void SetPlaying(bool playing) { }
    public void Dispose() { }
    private sealed class Marker(AllocationCounters counters, int marker) : A.ISystem
    {
        public void Execute(A.EcsSystemContext context) => counters.Mark(marker);
        public void Dispose() { }
    }
}
