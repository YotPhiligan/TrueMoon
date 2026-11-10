using System.Numerics;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;

namespace RenderSnapshotsConsumer;

internal static class Program
{
    private static int _checks;
    public static async Task<int> Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var report = new Dictionary<string, object?> { ["Completed"] = false, ["GpuRun"] = false, ["WindowRun"] = false, ["BackendKind"] = "DeterministicMemory" };
        try
        {
            await RunAsync(report); report["Checks"] = _checks; report["Completed"] = true; return 0;
        }
        catch (Exception error) { report["Error"] = error.ToString(); return 1; }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
    }
    private static async Task RunAsync(Dictionary<string, object?> report)
    {
        var content = BuiltinRenderContent.Create(Guid.NewGuid(), RenderContentHash.Compute("render-snapshots-consumer/v1"u8), Guid.NewGuid());
        MemoryBackend? backend = null; MemoryUse? nativeUse = null;
        RenderSnapshotView? observedView = null; RenderSnapshotData? submittedData = null;
        var app = App.Builder(_ => { }).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ =>
                new Factory<IRenderResourceBackend, RenderDeviceRequest>(_ => backend = new())));
            context.Services(services => services.Singleton<IFactory<IRenderSnapshotSubmission, RenderSnapshotData>>(_ =>
                new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(data =>
                {
                    Require(observedView!.Counts.Submissions == 1 && data == submittedData && backend!.LiveMeshes == 1, "Backend factory ran before ledger/resource retain.");
                    return nativeUse = new();
                })));
            context.UseAetherEcs(); context.UseAetherRenderResources();
        }).Build();
        RenderManifestLease? scope = null; World? world = null;
        try
        {
            await app.StartAsync(); var host = app.Services.Resolve<RenderResourceHost>()!;
            scope = app.Services.Resolve<IFactory<RenderManifestLease, RenderContentManifest>>()!.Create(content);
            world = app.Services.Resolve<IFactory<World>>()!.Create() ?? throw new InvalidOperationException("World factory returned null.");
            var mesh = content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key;
            var bindings = content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key;
            var first = world.CreateEntity(); var second = world.CreateEntity(); var camera = world.CreateEntity();
            world.AddComponent(first, new MeshRenderer(mesh, bindings, true, uint.MaxValue, true, new(.5f, 1, 1)));
            world.AddComponent(second, new MeshRenderer(mesh, bindings, true, uint.MaxValue, false, new(.1f)));
            var shear = Matrix4x4.Identity; shear.M21 = 1.5f;
            world.AddComponent(first, new WorldTransform(shear, 0));
            world.AddComponent(second, new WorldTransform(Matrix4x4.CreateTranslation(2, 0, 0), 0));
            world.AddComponent(camera, PerspectiveCamera.Default); world.AddComponent(camera, new WorldTransform(Matrix4x4.CreateTranslation(0, 0, 6), 0));
            var device = app.Services.Resolve<IFactory<RenderDeviceOwner, RenderDeviceRequest>>()!.Create(new(new(Guid.NewGuid(), world.Id, content.ProjectId, content.BuildId, content.Generation)));
            Require(backend == null, "Registration/device creation activated native work.");
            var binding = app.Services.Resolve<IFactory<RenderWorldBinding, RenderWorldBindingRequest>>()!.Create(new(device, world, scope));
            await binding.InitializeAsync();
            var views = app.Services.Resolve<IFactory<RenderSnapshotView, RenderSnapshotViewRequest>>()!;
            Require(views is not IDisposable && views is not IAsyncDisposable, "Factory alias owns its product twice.");
            var view = views.Create(new(binding, new(Guid.NewGuid()), 7));
            observedView = view;
            var settings = new RenderViewConfiguration(RenderCameraSource.FromEntity(camera), 640, 360);
            var initial = (await view.CaptureAsync(0, settings))!;
            Require(initial.Identity.WorldId == world.Id && initial.SourceRevision == 7 && initial.BindingRevision == 1 && initial.Tick == 0 && initial.Sequence == 1, "Snapshot identity/stamp mismatch.");
            Require(initial.Draws.Length == 2 && initial.Draws[0].Entity == first && initial.Draws[1].Entity == second, "Draw order/count mismatch.");
            Require(initial.Draws[0].Bounds.Minimum == new Vector3(-1.25f, -.5f, -.5f) && initial.Draws[0].Bounds.Maximum == new Vector3(1.25f, .5f, .5f), "Shear bounds discarded corners.");
            Require(initial.Draws[0].ColorLinear == new Vector3(.5f, 1, 1) && initial.Draws[1].ColorLinear == Vector3.One, "Tint leaked into shared material.");
            Require(view.Counts.Snapshots == 1 && view.Counts.HasPending && host.Content.Counts.LeaseCount == 3, "Current/pending failed to share one resource-retained envelope.");
            var recording = (await view.TakeLatestAsync())!;
            Advance(world, first, second, camera, 1);
            var next = await view.CaptureAsync(1, settings);
            Require(view.Counts.Snapshots == 2 && recording.Snapshot == initial && next!.Sequence == 2, "Recording/coalescing lifetime mismatch.");
            var useFactory = app.Services.Resolve<IFactory<IRenderSnapshotSubmission, RenderSnapshotData>>()!;
            submittedData = initial;
            var use = await view.SubmitAsync(recording, useFactory);
            await WaitUntilAsync(() => nativeUse != null);
            await nativeUse!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Require(!use.Completion.IsCompleted && recording.IsReleased, "Accepted use released before completion.");
            using var changed = host.Content.Acquire(Changed(content));
            var reload = await binding.ReloadAsync(changed);
            Require(reload.Committed && backend!.LiveMeshes == 2 && device.ValidateHandle(initial.Draws[0].Handle) == initial.Draws[0].Mesh, "Reload freed a resource retained by old frames.");
            Advance(world, first, second, camera, 2);
            var reloaded = (await view.CaptureAsync(2, settings))!;
            Require(reloaded.BindingRevision == 2 && reloaded.ManifestDigest != initial.ManifestDigest && reloaded.Draws[0].Mesh != initial.Draws[0].Mesh, "Reload mixed snapshot revisions.");
            Require(backend!.LiveMeshes == 2, "Coalescing destroyed a submitted resource.");
            nativeUse.Complete(); await use.Completion.WaitAsync(TimeSpan.FromSeconds(10)); await view.CollectAsync();
            Require(use.IsReleased && backend.LiveMeshes == 1 && view.Counts.Snapshots == 1, "Completion failed to retire old resources.");
            var inspection = initial.Draws[0];
            var suspended = settings with { Width = 0, Height = 0, SurfaceGeneration = 2 };
            Require(await view.CaptureAsync(2, suspended) == null && view.Current == null && view.Counts.Snapshots == 0, "Suspension retained pending/current buffers.");
            settings = settings with { SurfaceGeneration = 3 };
            var resumed = (await view.CaptureAsync(2, settings))!;
            Require(resumed.Sequence > reloaded.Sequence && resumed.Draws.Length == 2, "Paused same-tick view failed to resume.");
            var stale = (await view.TakeLatestAsync())!;
            await view.CaptureAsync(2, suspended with { SurfaceGeneration = 4 });
            try { await view.SubmitAsync(stale, useFactory); throw new InvalidOperationException("Suspended recording submitted."); }
            catch (RenderResourceException error) when (error.Error == RenderResourceError.Conflict) { _checks++; }
            await stale.DisposeAsync(); await view.DisposeAsync();
            Require(view.IsReleased && view.Counts.PayloadBytes == 0 && inspection.World.M21 == 1.5f, "Released view lost detached inspection data or retained buffers.");
            await binding.DisposeAsync(); await device.DisposeAsync(); world.Dispose(); world = null;
            scope.Dispose(); scope = null; changed.Dispose();
            Require(host.Content.Counts.LeaseCount == 0 && host.Content.Counts.ResidentBytes == 0 && device.AllocationBudget.Counts == new RenderAllocationCounts(0, 0, 0), "Resource counters leaked.");
            await host.DisposeAsync(); Require(host.IsReleased && backend.IsReleased && backend.LiveMeshes == 0, "App owner cleanup failed.");
            report["SourceRevision"] = initial.SourceRevision; report["InitialPayloadBytes"] = initial.PayloadBytes;
            report["DrawRanges"] = initial.Draws.Length; report["SubmissionCompleted"] = use.IsReleased;
            report["FinalSnapshotBytes"] = view.Counts.PayloadBytes; report["FinalResourceLeases"] = host.Content.Counts.LeaseCount;
            report["FinalGeometryBytes"] = device.AllocationBudget.Counts.GeometryBytes;
        }
        finally
        {
            nativeUse?.Complete(); scope?.Dispose();
            await app.DisposeAsync(); world?.Dispose();
        }
    }
    private static void Advance(World world, EntityHandle first, EntityHandle second, EntityHandle camera, ulong tick)
    {
        foreach (var entity in new[] { first, second, camera }) world.SetComponent(entity, world.GetComponent<WorldTransform>(entity) with { Tick = tick });
    }
    private static RenderContentManifest Changed(RenderContentManifest source)
    {
        var original = (MeshDefinition)source.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Definition;
        var positions = original.Vertices.ToArray(); positions[0] += new Vector3(.125f, 0, 0);
        var mesh = new MeshDefinition(positions, original.Indices, original.Slots, original.Bounds);
        var material = new OpaqueUnlitMaterialDefinition(new(.5f));
        var meshKey = mesh.CreateKey(source.ProjectId, RenderContentProfile.CubeAssetId);
        var materialKey = material.CreateKey(source.ProjectId, RenderContentProfile.WhiteMaterialAssetId);
        var bindings = new MaterialBindingSetDefinition(meshKey, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
        return new(source.ProjectId, source.BuildId, source.Generation, [new(meshKey, mesh), new(materialKey, material), new(bindings.CreateKey(source.ProjectId, RenderContentProfile.CubeBindingSetAssetId), bindings)]);
    }
    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!predicate()) { if (DateTime.UtcNow > end) throw new TimeoutException(); await Task.Delay(5); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); _checks++; }

    private sealed class MemoryUse : IRenderSnapshotSubmission
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsComplete { get; private set; }
        public bool IsReleased { get; private set; }
        internal void Complete() => _completion.TrySetResult();
        public async Task ExecuteAsync(CancellationToken cancellationToken) { Entered.TrySetResult(); await _completion.Task; IsComplete = true; }
        public async ValueTask DisposeAsync() { await _completion.Task; IsReleased = true; }
    }
    private sealed class MemoryBackend : IRenderResourceBackend
    {
        internal int Uploads, LiveMeshes;
        public bool IsReleased { get; private set; }
        public IFactory<IRenderMeshUpload, RenderMeshUploadRequest> MeshUploads => new Factory<IRenderMeshUpload, RenderMeshUploadRequest>(request => new MemoryMesh(this, request));
        public ValueTask DisposeAsync() { Require(LiveMeshes == 0, "Backend released before meshes."); IsReleased = true; return ValueTask.CompletedTask; }
        private sealed class MemoryMesh(MemoryBackend owner, RenderMeshUploadRequest request) : IRenderMeshUpload
        {
            private RenderAllocationReservation? _reservation;
            public bool IsReady { get; private set; }
            public bool IsReleased { get; private set; }
            public Task PrepareAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested(); _reservation = request.Budget.Reserve(256, 0, 0);
                Interlocked.Increment(ref owner.Uploads); Interlocked.Increment(ref owner.LiveMeshes); IsReady = true; return Task.CompletedTask;
            }
            public ValueTask DisposeAsync()
            {
                if (IsReleased) return ValueTask.CompletedTask;
                _reservation?.Dispose(); if (IsReady) Interlocked.Decrement(ref owner.LiveMeshes); IsReleased = true; return ValueTask.CompletedTask;
            }
        }
    }
}
