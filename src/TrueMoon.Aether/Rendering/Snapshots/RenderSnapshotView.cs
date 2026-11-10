using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering.Resources;

namespace TrueMoon.Aether.Rendering.Snapshots;

/// <summary>Caller-owned one-view extraction, latest mailbox and bounded submission ledger. World access is externally serialized.</summary>
public sealed class RenderSnapshotView : IAsyncDisposable
{
    private readonly RenderResourceHost _host;
    private readonly RenderSnapshotViewRequest _request;
    private readonly RenderSnapshotOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _end = new();
    private readonly HashSet<Envelope> _envelopes = [];
    private readonly List<RenderSnapshotSubmission> _submissions = [];
    private readonly Lock _ownerGate = new();
    private readonly HashSet<IAsyncDisposable> _backendOwners = new(ReferenceEqualityComparer.Instance);
    private Envelope? _current, _pending;
    private RenderSnapshotRecording? _recording;
    private RenderSnapshotData? _published;
    private RenderViewConfiguration? _configuration;
    private RenderSnapshotCounts _counts = new(0, 0, 0, false, false, false);
    private long _bytes;
    private ulong _sequence;
    private ulong? _lastTick;
    private volatile bool _closed, _fenced;
    internal RenderSnapshotView(RenderResourceHost host, RenderSnapshotViewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Binding);
        if (request.SceneId.Value == Guid.Empty || request.SourceRevision < 0) throw new ArgumentException("A source scene and revision are required.", nameof(request));
        _host = host; _request = request; _options = (request.Options ?? new()).Snapshot();
    }
    /// <summary>Unique process-local view identity, never reused after disposal.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Last successfully published detached inspection data, null before capture/while suspended/after stop.</summary>
    public RenderSnapshotData? Current => Volatile.Read(ref _published);
    /// <summary>Last atomic counter observation at a view operation boundary.</summary>
    public RenderSnapshotCounts Counts => Volatile.Read(ref _counts) with
    { Closed = _closed || _fenced || _request.Binding.IsReleased || _request.Binding.Device.IsAdmissionClosed };
    /// <summary>Retained cleanup failure; a current publication may have succeeded before old cleanup failed.</summary>
    public Exception? CleanupError { get; private set; }
    /// <summary>Every envelope and accepted use has safely released.</summary>
    public bool IsReleased { get; private set; }

    /// <summary>Captures one complete transform boundary, publishing current/latest together. Caller must serialize World, scheduler and reload.</summary>
    /// <param name="tick">Completed transform tick, including initial zero. Equal tick requires changed view or resource binding.</param>
    /// <param name="configuration">Versioned explicit camera and extent.</param><param name="cancellationToken">Pre-publication cancellation.</param>
    /// <returns>Detached new data; null for a valid suspended view.</returns>
    public async Task<RenderSnapshotData?> CaptureAsync(ulong tick, RenderViewConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration); configuration.Validate();
        Guard();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _end.Token);
        var token = linked.Token;
        await _gate.WaitAsync(token).ConfigureAwait(false);
        Envelope? candidate = null;
        try
        {
            Guard(); token.ThrowIfCancellationRequested(); CheckConfiguration(configuration);
            await CollectCoreAsync(false).ConfigureAwait(false);
            if (configuration.Suspended)
            {
                _configuration = configuration; Drop(ref _current); Drop(ref _pending); Volatile.Write(ref _published, null);
                await RetireAsync(false).ConfigureAwait(false); return null;
            }
            ResourceGuard.Require(!_lastTick.HasValue || tick >= _lastTick && (tick > _lastTick || configuration != _configuration ||
                _current?.Data?.BindingRevision != _request.Binding.Revision), RenderResourceError.Conflict, "Capture requires a newer tick, view or resource binding.");
            ResourceGuard.Require(_envelopes.Count < _options.MaximumSnapshots, RenderResourceError.LimitExceeded, "Snapshot owner admission exhausted.");
            Reserve(MeshSnapshotExtraction.HeaderBytes);
            candidate = new(MeshSnapshotExtraction.HeaderBytes); _envelopes.Add(candidate);
            candidate.Resources = await _request.Binding.CaptureSnapshotResourcesAsync(token).ConfigureAwait(false);
            Guard();
            var resources = candidate.Resources; var world = _request.Binding.World;
            var plan = MeshSnapshotExtraction.Prepare(world, tick, resources.Manifest, configuration, _options, token);
            Reserve(plan.Bytes - candidate.Bytes); candidate.Bytes = plan.Bytes;
            var sequence = checked(_sequence + 1);
            var data = MeshSnapshotExtraction.Capture(plan, Id, _request.Binding.Device.Identity, world, _request.SceneId, _request.SourceRevision,
                tick, sequence, _request.Binding.Device.Generation, resources, configuration, token);
            Guard(); token.ThrowIfCancellationRequested();
            ResourceGuard.Require(_request.Binding.Revision == resources.BindingRevision && _request.Binding.Current?.Manifest.Digest == resources.Manifest.Digest,
                RenderResourceError.Conflict, "Resource binding changed during extraction.");
            candidate.Data = data;
            Drop(ref _current); Drop(ref _pending);
            _current = candidate; _pending = candidate; candidate.References = 2; candidate = null;
            _configuration = configuration; _lastTick = tick; _sequence = sequence; Volatile.Write(ref _published, data);
            await RetireAsync(false).ConfigureAwait(false); return data;
        }
        finally
        {
            if (candidate != null) candidate.References = 0;
            try { await RetireAsync(false).ConfigureAwait(false); }
            finally { Observe(); _gate.Release(); }
        }
    }
    /// <summary>Transfers latest pending to the sole caller-owned data reservation; no native work begins.</summary>
    /// <param name="cancellationToken">Wait cancellation.</param><returns>Latest reservation, or null when mailbox is empty/suspended.</returns>
    public async Task<RenderSnapshotRecording?> TakeLatestAsync(CancellationToken cancellationToken = default)
    {
        Guard();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Guard(); ResourceGuard.Require(_recording == null, RenderResourceError.Conflict, "A recording is already outstanding.");
            if (_pending == null) return null;
            var recording = new RenderSnapshotRecording(this, _pending); _pending = null; _recording = recording; return recording;
        }
        finally { Observe(); _gate.Release(); }
    }
    /// <summary>Transfers a live recording to a tracked backend use before invoking any backend factory/native work.</summary>
    /// <param name="recording">Reservation issued by this view.</param><param name="factory">Borrowed backend owner factory; caller retains its lifetime until all uses finish.</param>
    /// <param name="cancellationToken">Pre-admission and backend cooperative cancellation.</param><returns>View-retained accepted use.</returns>
    public async Task<RenderSnapshotSubmission> SubmitAsync(RenderSnapshotRecording recording,
        IFactory<IRenderSnapshotSubmission, RenderSnapshotData> factory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording); ArgumentNullException.ThrowIfNull(factory);
        Guard();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Guard(); cancellationToken.ThrowIfCancellationRequested(); await CollectCoreAsync(false).ConfigureAwait(false);
            ResourceGuard.Require(ReferenceEquals(_recording, recording) && !recording.IsReleased && recording.Envelope.Data!.Configuration == _configuration && _configuration?.Suspended == false,
                RenderResourceError.Conflict, "Recording is foreign, released or belongs to an obsolete view generation.");
            ResourceGuard.Require(_submissions.Count < _options.MaximumSubmissions, RenderResourceError.LimitExceeded, "Submission admission exhausted.");
            var use = new RenderSnapshotSubmission(this, recording.Envelope, factory, cancellationToken, _end.Token);
            _submissions.Add(use); _recording = null; recording.IsReleased = true;
            Observe(); use.Start(); return use;
        }
        finally { Observe(); _gate.Release(); }
    }
    /// <summary>Collects completed backend uses and retries failed retirement, leaving in-flight uses intact.</summary>
    /// <param name="cancellationToken">Gate wait cancellation; accepted work is not discarded.</param><returns>Confirmed collection or retained cleanup failure.</returns>
    public async Task CollectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await CollectCoreAsync(true).ConfigureAwait(false); }
        finally { Observe(); _gate.Release(); }
    }
    internal void Fence()
    {
        _fenced = true;
        try { _request.Binding.Device.FenceSnapshotGeneration(); }
        catch (Exception error) { CleanupError ??= error; } // Never replace the original unknown execution outcome.
    }
    internal void ClaimSubmissionOwner(IAsyncDisposable owner)
    {
        lock (_ownerGate) ResourceGuard.Require(_backendOwners.Add(owner), RenderResourceError.Conflict, "Submission factories must return exclusive owners, without aliases.");
    }
    internal void ReleaseSubmissionOwner(IAsyncDisposable owner) { lock (_ownerGate) _backendOwners.Remove(owner); }
    internal async Task ReleaseRecordingAsync(RenderSnapshotRecording recording)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (recording.IsReleased) return;
            ResourceGuard.Require(ReferenceEquals(_recording, recording), RenderResourceError.InvalidIdentity, "Foreign recording.");
            _recording = null; recording.IsReleased = true; recording.Envelope.References--;
            await RetireAsync(true).ConfigureAwait(false);
        }
        finally { Observe(); _gate.Release(); }
    }
    internal async Task ReleaseSubmissionAsync(RenderSnapshotSubmission submission)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (submission.IsReleased) return;
            ResourceGuard.Require(_submissions.Contains(submission), RenderResourceError.InvalidIdentity, "Foreign submission.");
            await ReleaseUseAsync(submission).ConfigureAwait(false); await RetireAsync(true).ConfigureAwait(false);
        }
        finally { Observe(); _gate.Release(); }
    }
    private async Task ReleaseUseAsync(RenderSnapshotSubmission use)
    {
        await use.ReleaseBackendAsync().ConfigureAwait(false);
        use.Envelope.References--; use.IsReleased = true; _submissions.Remove(use);
    }
    private async Task CollectCoreAsync(bool throwErrors)
    {
        var errors = new List<Exception>();
        foreach (var use in _submissions.ToArray())
        {
            if (!use.Completion.IsCompleted) continue;
            try { await ReleaseUseAsync(use).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        }
        try { await RetireAsync(true).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        CleanupError = errors.Count == 0 ? null : new AggregateException("Snapshot collection retained ownership.", errors);
        if (throwErrors && CleanupError != null) throw CleanupError;
    }
    private async Task RetireAsync(bool throwErrors)
    {
        var errors = new List<Exception>();
        foreach (var envelope in _envelopes.ToArray())
        {
            if (envelope.References != 0) continue;
            try
            {
                if (envelope.Resources != null) await envelope.Resources.DisposeAsync().ConfigureAwait(false);
                _envelopes.Remove(envelope); _bytes -= envelope.Bytes;
            }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count != 0)
        {
            CleanupError = new AggregateException("Snapshot retirement retained resources and budget.", errors);
            if (throwErrors) throw CleanupError;
        }
    }
    private void Reserve(long bytes)
    {
        ResourceGuard.Require(bytes >= 0 && bytes <= _options.MaximumRetainedBytes - _bytes, RenderResourceError.LimitExceeded, "Retained snapshot buffer admission exhausted.");
        _bytes += bytes;
    }
    private void CheckConfiguration(RenderViewConfiguration value)
    {
        if (_configuration is not { } old) return;
        ResourceGuard.Require(value.CameraRevision >= old.CameraRevision && value.SurfaceGeneration >= old.SurfaceGeneration &&
            (value.Camera == old.Camera || value.CameraRevision > old.CameraRevision) &&
            (value.Width == old.Width && value.Height == old.Height || value.SurfaceGeneration > old.SurfaceGeneration),
            RenderResourceError.Conflict, "View configuration changed without advancing its generation.");
    }
    private static void Drop(ref Envelope? envelope) { if (envelope != null) { envelope.References--; envelope = null; } }
    private void Guard()
    {
        ResourceGuard.Require(!_closed && !_fenced && !_request.Binding.IsReleased, RenderResourceError.Closed, "Snapshot view is stopped or fenced.");
        _request.Binding.Device.EnsureOpen();
    }
    private void Observe() => Volatile.Write(ref _counts, new(_envelopes.Count, _bytes, _submissions.Count, _pending != null, _recording != null, _closed || _fenced));
    /// <summary>Stops admission and awaits safe cleanup. Cancellation stops only this caller's wait.</summary>
    /// <param name="cancellationToken">Wait cancellation.</param><returns>Confirmed cleanup or retained failure.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default) => await DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
    /// <summary>Stops, drops unsubmitted data, drains accepted backend owners, and retains failed cleanup for explicit retry.</summary>
    public async ValueTask DisposeAsync()
    {
        _closed = true;
        try { _end.Cancel(); } catch (ObjectDisposedException) { } catch (Exception error) { CleanupError ??= error; }
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return;
            Drop(ref _current); Drop(ref _pending); Volatile.Write(ref _published, null);
            if (_recording != null) { _recording.IsReleased = true; _recording.Envelope.References--; _recording = null; }
            var errors = new List<Exception>();
            foreach (var use in _submissions.ToArray())
            { try { await ReleaseUseAsync(use).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); } }
            try { await RetireAsync(true).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) { CleanupError = new AggregateException("View stop retained owned resources.", errors); throw CleanupError; }
            IsReleased = true; CleanupError = null; _end.Dispose(); _host.ViewReleased(this);
        }
        finally { Observe(); _gate.Release(); }
    }
    internal sealed class Envelope(long bytes)
    {
        internal long Bytes = bytes;
        internal int References = 1;
        internal RenderSnapshotResources? Resources;
        internal RenderSnapshotData? Data;
    }
}
