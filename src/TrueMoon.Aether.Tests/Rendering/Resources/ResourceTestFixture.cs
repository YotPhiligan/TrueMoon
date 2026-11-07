using System.Collections.Concurrent;
using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

internal static class ResourceTestFixture
{
    internal static readonly Guid Project = Guid.Parse("dfa51bc3-3463-4d43-9020-f8b96e680001");
    internal static readonly RenderContentHash Build = RenderContentHash.Compute("render-resource-tests/v1"u8);
    internal static readonly Guid Generation = Guid.Parse("dfa51bc3-3463-4d43-9020-f8b96e680002");
    internal static readonly Guid Session = Guid.Parse("dfa51bc3-3463-4d43-9020-f8b96e680003");
    internal static readonly Guid World = Guid.Parse("dfa51bc3-3463-4d43-9020-f8b96e680004");
    internal static RenderContentManifest Content() => BuiltinRenderContent.Create(Project, Build, Generation);
    internal static RenderDeviceRequest Request(RenderContentManifest manifest, Guid? worldId = null, ulong generation = 1)
        => new(new(Session, worldId ?? World, manifest.ProjectId, manifest.BuildId, manifest.Generation), generation);
    internal static RenderContentManifest ChangedContent(RenderContentManifest source, float gray = .25f, bool changeMesh = false)
    {
        var meshEntry = source.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        if (changeMesh)
        {
            var mesh = (MeshDefinition)meshEntry.Definition;
            var positions = mesh.Vertices.ToArray(); positions[0] += new Vector3(.125f, 0, 0);
            var changed = new MeshDefinition(positions, mesh.Indices, mesh.Slots, mesh.Bounds);
            meshEntry = new(changed.CreateKey(source.ProjectId, RenderContentProfile.CubeAssetId), changed);
        }
        var material = new OpaqueUnlitMaterialDefinition(new(gray));
        var materialKey = material.CreateKey(source.ProjectId, RenderContentProfile.WhiteMaterialAssetId);
        var bindings = new MaterialBindingSetDefinition(meshEntry.Key, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
        return new(source.ProjectId, source.BuildId, source.Generation,
            [meshEntry, new(materialKey, material), new(bindings.CreateKey(source.ProjectId, RenderContentProfile.CubeBindingSetAssetId), bindings)]);
    }
}

internal sealed class ResourceTestBackendFactory(ResourceTestBackend backend) : IFactory<IRenderResourceBackend, RenderDeviceRequest>
{
    private int _calls;
    internal int Calls => Volatile.Read(ref _calls);
    internal RenderDeviceRequest? LastRequest { get; private set; }
    internal Func<RenderDeviceRequest, IRenderResourceBackend>? CreateOverride { get; set; }
    public IRenderResourceBackend Create(RenderDeviceRequest arguments)
    { Interlocked.Increment(ref _calls); LastRequest = arguments; return CreateOverride != null ? CreateOverride(arguments) : backend; }
}

internal sealed class ResourceTestBackend : IRenderResourceBackend
{
    private readonly ConcurrentQueue<ResourceTestUpload> _created = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<ResourceTestUpload>> _signals = new();
    private readonly bool _holdUploads;
    private int _creates, _disposals;
    internal ResourceTestBackend(bool holdUploads = false)
    {
        _holdUploads = holdUploads;
        MeshUploads = new Factory<IRenderMeshUpload, RenderMeshUploadRequest>(Create);
    }
    public IFactory<IRenderMeshUpload, RenderMeshUploadRequest> MeshUploads { get; }
    public bool IsReleased { get; private set; }
    internal int CreateCalls => Volatile.Read(ref _creates);
    internal int DisposeCalls => Volatile.Read(ref _disposals);
    internal long GeometryBytes { get; set; } = 256;
    internal long UploadBytes { get; set; } = 128;
    internal long CpuBytes { get; set; } = 240;
    internal bool IgnoreCancellation { get; set; }
    internal Exception? PrepareError { get; set; }
    internal int DisposeFailuresRemaining { get; set; }
    internal bool BackendDisposeBeforeUploads { get; private set; }
    internal bool ReportReleased { get; set; } = true;
    internal Func<RenderMeshUploadRequest, int, IRenderMeshUpload>? CreateOverride { get; set; }
    internal IReadOnlyList<ResourceTestUpload> CreatedUploads => _created.ToArray();
    internal ConcurrentQueue<string> DisposalLog { get; } = new();
    internal Task<ResourceTestUpload> WaitForUploadAsync(int ordinal) => _signals.GetOrAdd(ordinal,
        static _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(TimeSpan.FromSeconds(10));
    private IRenderMeshUpload Create(RenderMeshUploadRequest request)
    {
        var ordinal = Interlocked.Increment(ref _creates);
        if (CreateOverride != null) return CreateOverride(request, ordinal);
        var upload = new ResourceTestUpload(request, _holdUploads, IgnoreCancellation, GeometryBytes, UploadBytes, CpuBytes, PrepareError, DisposalLog, ordinal);
        _created.Enqueue(upload);
        _signals.GetOrAdd(ordinal, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(upload);
        return upload;
    }
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposals);
        BackendDisposeBeforeUploads |= _created.Any(static upload => !upload.IsReleased);
        DisposalLog.Enqueue("backend");
        if (DisposeFailuresRemaining > 0) { DisposeFailuresRemaining--; throw new InvalidOperationException("backend cleanup marker"); }
        IsReleased = ReportReleased; return ValueTask.CompletedTask;
    }
}

internal sealed class ResourceTestUpload : IRenderMeshUpload
{
    private readonly bool _ignoreCancellation;
    private readonly long _geometryBytes, _uploadBytes, _cpuBytes;
    private readonly Exception? _prepareError;
    private readonly ConcurrentQueue<string> _log;
    private readonly int _ordinal;
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RenderAllocationReservation? _reservation;
    private int _prepares, _disposals;
    internal ResourceTestUpload(RenderMeshUploadRequest request, bool held, bool ignoreCancellation, long geometryBytes,
        long uploadBytes, long cpuBytes, Exception? prepareError, ConcurrentQueue<string> log, int ordinal)
    {
        Request = request; _ignoreCancellation = ignoreCancellation; _geometryBytes = geometryBytes; _uploadBytes = uploadBytes;
        _cpuBytes = cpuBytes; _prepareError = prepareError; _log = log; _ordinal = ordinal;
        if (!held) _gate.TrySetResult();
    }
    internal RenderMeshUploadRequest Request { get; }
    internal TaskCompletionSource PrepareEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource PrepareExited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int PrepareCalls => Volatile.Read(ref _prepares);
    internal int DisposeCalls => Volatile.Read(ref _disposals);
    internal int DisposeFailuresRemaining { get; set; }
    internal bool DisposedBeforePrepareExit { get; private set; }
    internal bool ReportReady { get; set; } = true;
    internal bool ReportReleased { get; set; } = true;
    public bool IsReady { get; private set; }
    public bool IsReleased { get; private set; }
    internal void Complete() => _gate.TrySetResult();
    internal void Fail(Exception error) => _gate.TrySetException(error);
    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _prepares);
        using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
        try
        {
            _reservation = Request.Budget.Reserve(_geometryBytes, _uploadBytes, _cpuBytes);
            PrepareEntered.TrySetResult();
            if (_ignoreCancellation) await _gate.Task;
            else await _gate.Task.WaitAsync(cancellationToken);
            if (_prepareError != null) throw _prepareError;
            IsReady = ReportReady;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // WaitAsync may resume and dispose its registration before an earlier token callback executes.
            CancellationObserved.TrySetResult(); throw;
        }
        finally { PrepareExited.TrySetResult(); }
    }
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposals); _log.Enqueue("upload:" + _ordinal);
        DisposedBeforePrepareExit |= PrepareCalls > 0 && !PrepareExited.Task.IsCompleted;
        if (DisposeFailuresRemaining > 0) { DisposeFailuresRemaining--; throw new InvalidOperationException("upload cleanup marker"); }
        if (ReportReleased) { _reservation?.Dispose(); _reservation = null; IsReleased = true; }
        return ValueTask.CompletedTask;
    }
}
