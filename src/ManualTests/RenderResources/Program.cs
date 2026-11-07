using System.Numerics;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Gpu.Vulkan;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;

namespace RenderResourcesConsumer;

internal static class Program
{
    private static readonly Guid Project = Guid.Parse("092ae42f-57d8-4013-994f-30ba16e1498d");
    private static readonly RenderContentHash Build = RenderContentHash.Compute("render-resources-proof/v1"u8);
    public static async Task<int> Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var native = args.Length > 1 && args[1] == "native";
        var report = new Dictionary<string, object?> { ["Completed"] = false, ["GpuRun"] = native, ["WindowRun"] = false,
            ["BackendKind"] = native ? "Vulkan" : "DeterministicMemory", ["Generations"] = new List<object>() };
        try
        {
            RenderResourceHandle? oldHandle = null;
            for (var generation = 1; generation <= 2; generation++)
            {
                var (entry, handle) = await RunAsync(generation, native, false, oldHandle);
                ((List<object>)report["Generations"]!).Add(entry); oldHandle = handle;
            }
            var denied = await RunAsync(3, native, true, null); report["DeniedBeforeAllocation"] = denied.Entry;
            report["Entities"] = 3; report["SourceComponents"] = 6; report["Assets"] = 3; report["Completed"] = true;
            return 0;
        }
        catch (Exception error) { report["Error"] = error.ToString(); return 1; }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
    }
    private static async Task<(object Entry, RenderResourceHandle? Handle)> RunAsync(int generation, bool native, bool deny, RenderResourceHandle? oldHandle)
    {
        var content = BuiltinRenderContent.Create(Project, Build, Guid.NewGuid());
        VulkanRenderResourceBackend? observed = null; MemoryBackend? memory = null; RenderManifestLease? rootScope = null;
        var app = App.Builder(_ => { }).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ =>
                new Factory<IRenderResourceBackend, RenderDeviceRequest>(request => native ? observed = new(request) : memory = new())));
            context.UseAetherEcs(); context.UseAetherRenderResources(options => { if (deny) options.MaximumGeometryBytes = 0; });
            context.UseAetherSceneLoading(_ => RenderResourceComponents.Register(PrimitiveEcsComponents.Register(new()), rootScope!).Build());
        }).Build();
        var proof = new Dictionary<string, object?> { ["Generation"] = generation, ["ExpectedDenial"] = deny };
        RenderResourceHandle? published = null;
        try
        {
            var host = app.Services.Resolve<RenderResourceHost>()!;
            rootScope = app.Services.Resolve<IFactory<RenderManifestLease, RenderContentManifest>>()!.Create(content);
            Require(observed == null && memory == null, "Resource factory activated before explicit device preparation.");
            await app.StartAsync();
            var scene = app.Services.Resolve<IFactory<SceneWorld, SceneWorldLoadRequest>>()!.Create(new(new(Document())));
            try
            {
                var identity = new RenderResourceIdentity(Guid.NewGuid(), scene.World.Id, Project, Build, content.Generation);
                var device = app.Services.Resolve<IFactory<RenderDeviceOwner, RenderDeviceRequest>>()!.Create(new(identity, (ulong)generation, native, native));
                var binding = app.Services.Resolve<IFactory<RenderWorldBinding, RenderWorldBindingRequest>>()!.Create(new(device, scene.World, rootScope));
                try
                {
                    var initialRenderer = scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity(1)]);
                    if (deny)
                    {
                        try { await binding.InitializeAsync(); throw new InvalidOperationException("Zero geometry budget was accepted."); }
                        catch (RenderResourceException failure) when (failure.Error == RenderResourceError.LimitExceeded) { proof["ExpectedFailureObserved"] = true; }
                        Require(binding.Current == null && binding.Revision == 0, "Denied preparation published a binding.");
                        Require(scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity(1)]) == initialRenderer, "Denied preparation changed World.");
                        Require(device.Counts.EntryCount == 0 && device.AllocationBudget.Counts == new RenderAllocationCounts(0, 0, 0), "Denied preparation retained allocations.");
                        if (native) Require(observed!.Diagnostics.AllocationsCreated == 0, "Geometry denial happened after native allocation.");
                    }
                    else
                    {
                        await binding.InitializeAsync(); var current = binding.Current!; var handle = current.GetMeshHandle(initialRenderer.Mesh);
                        if (oldHandle is { } stale) ExpectStale(device, stale);
                        var duplicate = app.Services.Resolve<IFactory<RenderPreparation, RenderPreparationRequest>>()!.Create(new(device, rootScope));
                        try { await duplicate.Completion; Require(duplicate.GetMeshHandle(initialRenderer.Mesh) == handle, "Exact mesh sharing did not reuse one device entry."); }
                        finally { await duplicate.DisposeAsync(); }
                        Require(device.ValidateHandle(handle) == initialRenderer.Mesh, "Releasing one user invalidated a shared mesh.");
                        Require(device.Counts.EntryCount == 1, "Duplicate preparation created another mesh.");
                        if (native) Require(observed!.Diagnostics.VerifiedUploads == 1, "Initial resource GPU readback was not confirmed.");
                        var changed = Changed(content);
                        using var candidate = host.Content.Acquire(changed);
                        var result = await binding.ReloadAsync(candidate);
                        Require(result.Committed && result.CleanupError == null && binding.Revision == 2, "Reload failed publication/cleanup.");
                        var first = scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity(1)]);
                        var second = scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity(2)]);
                        Require(first.Mesh == second.Mesh && first.Bindings == second.Bindings && first.Mesh != initialRenderer.Mesh, "Reload did not atomically rebind both renderers.");
                        Require(first.TintEnabled && !second.TintEnabled && first.TintLinear == new Vector3(.5f), "Reload changed per-instance state.");
                        published = binding.Current!.GetMeshHandle(first.Mesh); ExpectStale(device, handle);
                        Require(device.AllocationBudget.Counts.UploadStagingBytes == 0 && device.AllocationBudget.Counts.CpuStagingBytes == 0, "Ready upload retained staging buffers.");
                        proof["InitialHandle"] = handle; proof["ReloadedHandle"] = published; proof["WorldId"] = scene.World.Id;
                        proof["ReadyGeometryBytes"] = device.AllocationBudget.Counts.GeometryBytes; proof["ResourceRevision"] = binding.Revision;
                    }
                }
                finally { await binding.DisposeAsync(); }
                await device.DisposeAsync();
                Require(device.IsReleased && device.Counts.EntryCount == 0 && device.AllocationBudget.Counts == new RenderAllocationCounts(0, 0, 0), "Device failed to release resources.");
            }
            finally { scene.Dispose(); }
            rootScope.Dispose(); rootScope = null;
            Require(host.Content.Counts.RevisionCount == 0 && host.Content.Counts.ResidentBytes == 0 && host.Content.Counts.LeaseCount == 0, "Scene/operation CPU leases leaked.");
            await host.DisposeAsync(); Require(host.IsReleased && host.Content.IsReleased, "App-owned resources did not release.");
            if (native)
            {
                var diagnostics = observed!.Diagnostics;
                Require(observed.IsReleased && diagnostics.ValidationEnabled && diagnostics.Errors == 0 && diagnostics.Warnings == 0 &&
                    diagnostics.DevicesCreated == 1 && diagnostics.DevicesReleased == 1 && diagnostics.LiveUploads == 0 &&
                    diagnostics.BuffersCreated == diagnostics.BuffersReleased && diagnostics.AllocationsCreated == diagnostics.AllocationsReleased,
                    "Native validation/teardown counters failed.");
                Require(deny ? diagnostics.VerifiedUploads == 0 : diagnostics.VerifiedUploads == 2, "Native verification count mismatch.");
                proof["Native"] = diagnostics;
            }
            else Require(memory!.IsReleased && memory.LiveUploads == 0, "Deterministic backend cleanup failed.");
            proof["Cpu"] = host.Content.Counts; proof["Released"] = true; return (proof, published);
        }
        finally { rootScope?.Dispose(); await app.DisposeAsync(); }
    }
    private static RenderContentManifest Changed(RenderContentManifest previous)
    {
        var original = (MeshDefinition)previous.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Definition;
        var mesh = new MeshDefinition(original.Vertices.Select(static value => value with { X = value.X * .5f }).ToArray(), original.Indices, original.Slots, original.Bounds);
        var material = new OpaqueUnlitMaterialDefinition(new(.5f, .25f, .75f));
        var meshKey = mesh.CreateKey(Project, RenderContentProfile.CubeAssetId); var materialKey = material.CreateKey(Project, RenderContentProfile.WhiteMaterialAssetId);
        var bindings = new MaterialBindingSetDefinition(meshKey, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
        return new(Project, Build, previous.Generation, [new(meshKey, mesh), new(materialKey, material), new(bindings.CreateKey(Project, RenderContentProfile.CubeBindingSetAssetId), bindings)]);
    }
    private static SceneDocument Document()
    {
        var definitions = new List<EntityDefinition>(); var entities = new List<EntityInstance>();
        for (var i = 1; i <= 3; i++)
        {
            var id = new EntityDefinitionId(Id(100 + i));
            var components = new List<ComponentData> { new(new(Id(200 + i)), BuiltInComponents.TransformId, 1) };
            components.Add(i == 3 ? new(new(Id(300 + i)), MeshRenderingComponents.CameraId, 1) :
                new(new(Id(300 + i)), MeshRenderingComponents.MeshRendererId, 1, i == 1 ?
                    [new("tintEnabled", PropertyValue.From(true)), new("tintR", PropertyValue.From(.5)), new("tintG", PropertyValue.From(.5)), new("tintB", PropertyValue.From(.5))] : null));
            definitions.Add(new(id, new(components))); entities.Add(new(Entity(i), id));
        }
        return new(new(Id(99)), 5, entities, definitions, BuiltInComponents.Schemas.Concat(MeshRenderingComponents.Schemas));
    }
    private static Guid Id(int value) => Guid.Parse($"092ae42f-57d8-4013-994f-{value:000000000000}");
    private static EntityId Entity(int value) => new(Id(value));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void ExpectStale(RenderDeviceOwner owner, RenderResourceHandle handle)
    { try { owner.ValidateHandle(handle); throw new InvalidOperationException("Stale/foreign handle accepted."); } catch (RenderResourceException error) when (error.Error == RenderResourceError.StaleHandle) { } }

    // This mode proves public ownership composition only; native mode substitutes the real Vulkan backend.
    private sealed class MemoryBackend : IRenderResourceBackend, IFactory<IRenderMeshUpload, RenderMeshUploadRequest>
    {
        public IFactory<IRenderMeshUpload, RenderMeshUploadRequest> MeshUploads => this;
        public bool IsReleased { get; private set; }
        internal int LiveUploads;
        public IRenderMeshUpload Create(RenderMeshUploadRequest request) { LiveUploads++; return new MemoryUpload(this, request); }
        public ValueTask DisposeAsync() { Require(LiveUploads == 0, "Backend disposed before uploads."); IsReleased = true; return ValueTask.CompletedTask; }
    }
    private sealed class MemoryUpload(MemoryBackend owner, RenderMeshUploadRequest request) : IRenderMeshUpload
    {
        private RenderAllocationReservation? _geometry;
        public bool IsReady { get; private set; }
        public bool IsReleased { get; private set; }
        public Task PrepareAsync(CancellationToken token)
        { token.ThrowIfCancellationRequested(); _geometry = request.Budget.Reserve(256, 0, 0); using var staging = request.Budget.Reserve(0, 128, 240); IsReady = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { if (!IsReleased) { _geometry?.Dispose(); _geometry = null; owner.LiveUploads--; IsReleased = true; } return ValueTask.CompletedTask; }
    }
}
