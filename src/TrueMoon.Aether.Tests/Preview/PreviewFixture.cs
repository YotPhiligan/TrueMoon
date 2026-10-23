using System.Collections.Concurrent;
using System.Threading.Channels;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Tests.Scenes.Authoring;

namespace TrueMoon.Aether.Tests.Preview;

internal sealed class PreviewFixture : IAsyncDisposable
{
    internal const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    internal readonly RuntimeFactory Runtimes = new();
    internal readonly Transport Viewport;
    internal readonly SessionSupervisor Supervisor;
    internal readonly FrameSession Frames;
    internal PreviewFixture(ViewportTransportKind kind = ViewportTransportKind.Gpu)
    {
        Viewport = new(kind);
        Supervisor = new(Runtimes, Hash, Hash, "preview-test", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
        Frames = new(Supervisor, Viewport);
    }
    internal static ResolvedSceneSnapshot Scene(long revision = 7, int count = 2)
        => SceneResolver.Resolve(AuthoringTestData.Document(revision, count));
    internal static FrameMetadata Metadata(ulong frame = 1) => new(0, frame, 0, 0, 1);
    internal async Task<ViewportPresentation> ConfiguredAsync() => await Viewport.Configurations.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
    internal async Task<ViewportFrame> PresentAsync()
    {
        var presentation = await ConfiguredAsync();
        var frame = presentation.Issue(Metadata());
        if (!Frames.Presented(frame)) throw new InvalidOperationException("Fixture presentation was not current.");
        return frame;
    }
    public async ValueTask DisposeAsync()
    {
        await Frames.DisposeAsync().AsTask().WaitAsync(Deadline);
        await Supervisor.DisposeAsync().AsTask().WaitAsync(Deadline);
    }
    internal sealed class RuntimeFactory : IRuntimeFactory
    {
        internal readonly List<Lease> Leases = [];
        internal Exception? LaunchError;
        internal Func<ControlRequest, CancellationToken, Task>? BeforeSend;
        internal int MaxLive;
        public IRuntimeLease Create(SessionLaunch launch)
        {
            var lease = new Lease(this, launch); Leases.Add(lease);
            MaxLive = Math.Max(MaxLive, Leases.Count(l => !l.ResourcesReleased));
            return lease;
        }
    }
    internal sealed class Lease(RuntimeFactory owner, SessionLaunch launch) : IRuntimeLease
    {
        internal readonly SessionLaunch Launch = launch;
        internal readonly PreviewGameSession Game = new();
        internal readonly ConcurrentQueue<CommandKind> Commands = [];
        internal int Stops, Disposals;
        internal RuntimeProtocol Protocol = null!;
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 900 + (int)Launch.Identity.Generation;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (owner.LaunchError is { } error) throw error;
            Protocol = new(Launch.Identity, Game, ProcessId, role: Launch.Role);
            return Task.CompletedTask;
        }
        public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        {
            Commands.Enqueue((CommandKind)request.Kind);
            if (owner.BeforeSend is { } before) await before(request, token);
            return await Protocol.SendAsync(request, token);
        }
        internal void Crash() => _exit.TrySetResult();
        public Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            Stops++;
            _exit.TrySetResult();
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        { Disposals++; Game.Dispose(); ResourcesReleased = Exited.IsCompletedSuccessfully; return ValueTask.CompletedTask; }
    }
    internal sealed class Transport(ViewportTransportKind kind) : IGameViewportTransport, IAsyncDisposable
    {
        internal readonly List<Connection> Connections = [];
        internal readonly Channel<ViewportPresentation> Configurations = Channel.CreateUnbounded<ViewportPresentation>();
        internal Func<ViewportPresentation, CancellationToken, Task>? BeforeConfigure;
        internal Exception? ConnectError, ConfigureError;
        internal int Disposals;
        public ViewportTransportKind Kind => kind;
        public Task<IGameViewportConnection> ConnectAsync(IRuntimeLease runtime, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ConnectError is { } error) throw error;
            var connection = new Connection(this); Connections.Add(connection);
            return Task.FromResult<IGameViewportConnection>(connection);
        }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
    internal sealed class Connection(Transport owner) : IGameViewportConnection
    {
        private TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Disposed;
        internal bool FailDispose;
        internal int Disposals;
        public Task Completion => _completion.Task;
        public async Task ConfigureAsync(ViewportPresentation presentation, CancellationToken token)
        {
            _completion.TrySetCanceled(); _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (owner.ConfigureError is { } error) throw error;
            owner.Configurations.Writer.TryWrite(presentation);
            if (owner.BeforeConfigure is { } before) await before(presentation, token);
        }
        internal void Fail(Exception error) => _completion.TrySetException(error);
        public ValueTask DisposeAsync()
        {
            Disposals++;
            if (FailDispose) throw new IOException("preview fixture connection cleanup failed");
            Disposed = true; _completion.TrySetCanceled(); return ValueTask.CompletedTask;
        }
    }
}
