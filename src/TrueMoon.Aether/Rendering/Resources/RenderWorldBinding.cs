using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;

namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Typed resource binding factory request; World and the input lease are borrowed.</summary>
/// <param name="Device">Required device owner.</param><param name="World">Serialized World, matching device identity.</param><param name="Content">Initial frozen CPU scope.</param>
public sealed record RenderWorldBindingRequest(RenderDeviceOwner Device, World World, RenderManifestLease Content);
/// <summary>Reload outcome distinguishing publication from later old-resource cleanup.</summary>
/// <param name="Committed">New manifest and component keys became current.</param><param name="CleanupError">Retained old-owner cleanup failure, if any.</param>
public sealed record RenderReloadResult(bool Committed, Exception? CleanupError);

/// <summary>Caller-owned required resource binding and explicit atomic key reload. Borrows, never disposes, World.</summary>
public sealed class RenderWorldBinding : IAsyncDisposable
{
    /// <summary>Maximum renderer components in this required-view resource binding; raw manifest preparation has separate limits.</summary>
    public const int MaximumRenderers = 4096;
    private readonly RenderResourceHost _host;
    private readonly RenderDeviceOwner _device;
    private readonly World _world;
    private readonly RenderManifestLease _initial;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _end = new();
    private readonly List<RenderPreparation> _retired = [];
    private RenderPreparation? _candidate;
    private bool _closing;
    internal RenderWorldBinding(RenderResourceHost host, RenderWorldBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.World); ArgumentNullException.ThrowIfNull(request.Content);
        ResourceGuard.Require(request.World.Id == request.Device.Identity.WorldId && !request.World.IsDisposed && !request.World.IsFaulted,
            RenderResourceError.InvalidIdentity, "Resource binding requires the matching healthy World.");
        request.Device.ValidateContent(request.Content);
        _host = host; _device = request.Device; _world = request.World; _initial = request.Content.Retain();
    }
    /// <summary>Gets the current ready resource lease, borrowed until replacement/disposal.</summary>
    public RenderPreparation? Current { get; private set; }
    /// <summary>Gets zero before initialization, one after initial readiness, then a monotonic reload revision.</summary>
    public ulong Revision { get; private set; }
    /// <summary>Whether all current/candidate/retired resource ownership was released.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Gets retained candidate/old-owner cleanup failure without hiding the primary operation outcome.</summary>
    public Exception? CleanupError { get; private set; }
    /// <summary>Prepares initial required resources without modifying component keys or claiming frame readiness.</summary>
    /// <param name="cancellationToken">Cooperative preparation cancellation.</param><returns>Confirmed complete resources.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _end.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Guard(); ResourceGuard.Require(Current == null, RenderResourceError.Conflict, "Binding is already initialized.");
            var rows = Capture(_initial.Manifest);
            _candidate = _host.Create(new RenderPreparationRequest(_device, _initial, linked.Token));
            try
            {
                await _candidate.Completion.ConfigureAwait(false); Guard(); linked.Token.ThrowIfCancellationRequested(); CheckUnchanged(rows, _initial.Manifest);
                Current = _candidate; _candidate = null; Revision = 1; _initial.Dispose();
            }
            catch { await RetainFailedCandidateAsync().ConfigureAwait(false); throw; }
        }
        finally { _gate.Release(); }
    }
    /// <summary>Prepares a complete candidate, rechecks inputs and changes pinned keys plus current binding at one serialized boundary.</summary>
    /// <param name="content">Same project/build/catalog generation with explicit candidate dependency revisions.</param><param name="cancellationToken">Preflight/preparation cancellation.</param><returns>Publication plus separately observed old cleanup outcome.</returns>
    public async Task<RenderReloadResult> ReloadAsync(RenderManifestLease content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _end.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Guard(); var old = Current ?? throw new RenderResourceException(RenderResourceError.Conflict, "Initialize before reload.");
            _device.ValidateContent(content);
            var nextRevision = checked(Revision + 1);
            var rows = Capture(old.Manifest);
            var replacements = rows.Select(row => row.Value with
            {
                Mesh = content.Manifest.Resolve(row.Value.Mesh.AssetId, RenderContentProfile.MeshKindId).Key,
                Bindings = content.Manifest.Resolve(row.Value.Bindings.AssetId, RenderContentProfile.BindingSetKindId).Key
            }).ToArray();
            foreach (var value in replacements) value.Validate(content.Manifest);
            _candidate = _host.Create(new RenderPreparationRequest(_device, content, linked.Token));
            try
            {
                await _candidate.Completion.ConfigureAwait(false); Guard(); CheckUnchanged(rows, old.Manifest); linked.Token.ThrowIfCancellationRequested();
                try
                {
                    for (var i = 0; i < rows.Length; i++) _world.SetComponent(rows[i].Entity, replacements[i]);
                    Current = _candidate; _candidate = null; Revision = nextRevision;
                }
                catch (Exception error) { _world.MarkFaulted(error); _closing = true; throw; }
            }
            catch { await RetainFailedCandidateAsync().ConfigureAwait(false); throw; }
            try { await old.DisposeAsync().ConfigureAwait(false); return new(true, null); }
            catch (Exception error) { _retired.Add(old); CleanupError = error; return new(true, error); }
        }
        finally { _gate.Release(); }
    }
    private Row[] Capture(RenderContentManifest content, bool validate = true, int maximumRows = MaximumRenderers)
    {
        Guard(); var rows = new List<Row>();
        _world.Query<MeshRenderer>().ForEach((EntityHandle entity, in MeshRenderer value) =>
        {
            ResourceGuard.Require(rows.Count < maximumRows, validate ? RenderResourceError.LimitExceeded : RenderResourceError.Conflict,
                "World renderer count exceeds the captured binding admission.");
            if (validate) value.Validate(content); rows.Add(new(entity, value));
        });
        return rows.OrderBy(static row => row.Entity.Index).ToArray();
    }
    private void CheckUnchanged(Row[] expected, RenderContentManifest content)
        => ResourceGuard.Require(expected.AsSpan().SequenceEqual(Capture(content, false, expected.Length)), RenderResourceError.Conflict, "World mesh membership/values changed while preparing.");
    private void Guard()
    {
        ResourceGuard.Require(!_closing && !_world.IsDisposed && !_world.IsFaulted, RenderResourceError.Closed, "Binding/World is closed or faulted.");
        _device.EnsureOpen();
    }
    private async Task RetainFailedCandidateAsync()
    {
        if (_candidate == null) return;
        var candidate = _candidate; _candidate = null;
        try { await candidate.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { _retired.Add(candidate); CleanupError = error; /* Actual owner remains retained. */ }
    }
    /// <summary>Cancels pending work before taking the operation gate, then retries every retained owner. World is borrowed.</summary>
    public async ValueTask DisposeAsync()
    {
        _closing = true; try { _end.Cancel(); } catch { /* Actual operation owners still drain. */ }
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return;
            if (_candidate != null) { _retired.Add(_candidate); _candidate = null; }
            if (Current != null) { _retired.Add(Current); Current = null; }
            var errors = new List<Exception>();
            for (var i = _retired.Count - 1; i >= 0; i--)
            { try { await _retired[i].DisposeAsync().ConfigureAwait(false); _retired.RemoveAt(i); } catch (Exception error) { errors.Add(error); } }
            if (errors.Count != 0) throw new AggregateException("Binding cleanup retained ownership.", errors);
            _initial.Dispose(); _end.Dispose(); IsReleased = true; CleanupError = null; _host.BindingReleased(this);
        }
        finally { _gate.Release(); }
    }
    private readonly record struct Row(EntityHandle Entity, MeshRenderer Value);
}
