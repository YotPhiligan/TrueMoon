using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;
using TrueMoon.Aether.Tests.Rendering.Resources;

namespace TrueMoon.Aether.Tests.Rendering.Snapshots;

internal sealed class SnapshotFixture : IAsyncDisposable
{
    internal World World { get; } = new();
    internal ResourceTestBackend Backend { get; } = new();
    internal RenderContentManifest Content { get; }
    internal RenderResourceHost Host { get; }
    internal RenderManifestLease Scope { get; }
    internal RenderDeviceOwner Device { get; }
    internal RenderWorldBinding Binding { get; }
    internal RenderSnapshotView View { get; }
    internal EntityHandle First { get; }
    internal EntityHandle Second { get; }
    internal EntityHandle Camera { get; }
    internal RenderViewConfiguration Configuration { get; }
    private SnapshotFixture(RenderSnapshotOptions? options, RenderContentManifest? content)
    {
        Content = content ?? ResourceTestFixture.Content(); Host = new(new(), new ResourceTestBackendFactory(Backend)); Scope = Host.Content.Acquire(Content);
        First = AddRenderer(Matrix4x4.Identity); Second = AddRenderer(Matrix4x4.CreateTranslation(2, 0, 0));
        Camera = World.CreateEntity(); World.AddComponent(Camera, PerspectiveCamera.Default);
        World.AddComponent(Camera, new WorldTransform(Matrix4x4.CreateTranslation(0, 0, 6), 0));
        Device = Host.Create(ResourceTestFixture.Request(Content, World.Id)); Binding = Host.Create(new RenderWorldBindingRequest(Device, World, Scope));
        View = Host.Create(new RenderSnapshotViewRequest(Binding, new(Guid.NewGuid()), 7, options));
        Configuration = new(RenderCameraSource.FromEntity(Camera), 640, 360);
    }
    internal static async Task<SnapshotFixture> CreateAsync(RenderSnapshotOptions? options = null, RenderContentManifest? content = null)
    {
        var value = new SnapshotFixture(options, content);
        try { await value.Binding.InitializeAsync(); return value; }
        catch { await value.DisposeAsync(); throw; }
    }
    internal EntityHandle AddRenderer(Matrix4x4 matrix)
    {
        var entity = World.CreateEntity();
        World.AddComponent(entity, new MeshRenderer(Content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key,
            Content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key, true, uint.MaxValue, false, Vector3.One));
        World.AddComponent(entity, new WorldTransform(matrix, 0)); return entity;
    }
    internal void Advance(ulong tick)
    {
        var entities = new List<EntityHandle>(); World.Query<WorldTransform>().ForEach((EntityHandle entity, in WorldTransform _) => entities.Add(entity));
        foreach (var entity in entities) World.SetComponent(entity, World.GetComponent<WorldTransform>(entity) with { Tick = tick });
    }
    internal async Task<RenderSnapshotData> CaptureAsync(ulong tick = 0, RenderViewConfiguration? configuration = null) =>
        (await View.CaptureAsync(tick, configuration ?? Configuration))!;
    public async ValueTask DisposeAsync()
    {
        await View.DisposeAsync(); await Binding.DisposeAsync(); Scope.Dispose(); await Host.DisposeAsync(); World.Dispose();
    }
}

internal sealed class ControlledSubmission : IRenderSnapshotSubmission
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool ReportComplete { get; set; } = true;
    internal bool ReportReleased { get; set; } = true;
    internal int CleanupFailures { get; set; }
    internal int CleanupCalls { get; private set; }
    public bool IsComplete { get; private set; }
    public bool IsReleased { get; private set; }
    internal void Complete() => _finish.TrySetResult();
    internal void Fail(Exception error) => _finish.TrySetException(error);
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => Cancelled.TrySetResult()); Entered.TrySetResult();
        try { await _finish.Task; IsComplete = ReportComplete; }
        finally { _exited.TrySetResult(); }
    }
    public async ValueTask DisposeAsync()
    {
        await _exited.Task; CleanupCalls++;
        if (CleanupFailures > 0) { CleanupFailures--; throw new IOException("submission cleanup"); }
        IsReleased = ReportReleased;
    }
}
