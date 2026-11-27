using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Dependencies;
using TrueMoon.Kton;
using Xunit;
using ProbeGame = Game.Logic.ProbeGame;

namespace TrueMoon.Aether.Kton.Tests;

public sealed partial class PhysicsCharacterSceneTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutoStepSession_PauseAndStepThenReplaceReloadsPolicyAndRebuildsSupport(bool microsoft)
    {
        var hostHash = new string('A', 64);
        var root = Path.Combine(Path.GetTempPath(), "kton-phys6-replace-" + Guid.NewGuid().ToString("N"));
        await using var app = PhysicsFixture.Build(microsoft, world: new() { Quality = PhysicsQualityProfile.Baseline60 });
        await app.StartAsync();
        var loader = PhysicsFixture.Loader(app);
        var simulations = app.Services.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
        var host = PhysicsFixture.Host(app);
        var data = new SceneWorldData(StepDocument(.25f, PhysicsComponentSchemas.CharacterV2, enableStep: true));
        var resolution = new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas)));
        var codec = new SceneWorldCodec(new SceneDocumentCodec(resolution));
        var leases = new List<StepRuntimeLease>();
        var factory = new Factory<IRuntimeLease, SessionLaunch>(launch =>
        {
            var lease = new StepRuntimeLease(launch, leases.Count + 101, new(loader, simulations), codec);
            leases.Add(lease);
            return lease;
        });

        try
        {
            // The probe DLL is only the candidate's managed admission payload, not the game executed by this transport.
            var probe = typeof(ProbeGame);
            var candidate = GameArtifactPublisher.Publish(Path.GetDirectoryName(probe.Assembly.Location)!, root,
                hostHash, "Game.Logic.dll", probe.FullName!, sourceRevision: 8, runtimeAccess: true);
            await using var supervisor = new SessionSupervisor(factory, hostHash, hostHash, root,
                new() { HeartbeatInterval = TimeSpan.FromSeconds(30) }, sceneWorldCodec: codec, sceneResolution: resolution);
            await using var frames = new FrameSession(supervisor);
            var replacement = new GameReplacement(supervisor, frames);
            var scenario = $"Baseline60 Replace, DI={(microsoft ? "Microsoft" : "Cobalt")}";

            await supervisor.StartWorldAsync(data);
            Assert.Equal((SessionState.Paused, 0UL), (supervisor.Status.State, supervisor.Status.Tick));
            var oldIdentity = supervisor.Status.Identity!;
            var old = Assert.Single(leases);
            var oldScene = old.Game.ActiveScene!;
            var oldWorldId = oldScene.World.Id;
            var oldCharacter = oldScene.Entities[PhysicsFixture.Entity(0)];
            var oldFloor = oldScene.Entities[PhysicsFixture.Entity(1)];

            await supervisor.SetPlayingAsync(true);
            for (var i = 0; i < 59; i++) old.Protocol.FixedTick();
            Assert.Equal(59UL, old.Game.TickNumber);
            AssertOnSurface(ObserveStep(oldScene, oldCharacter), oldFloor, -.02f, .02f, scenario, 59);

            await supervisor.SetPlayingAsync(false);
            var pausedPose = oldScene.World.GetComponent<LocalTransform>(oldCharacter);
            var paused = ObserveStep(oldScene, oldCharacter);
            old.Protocol.FixedTick();
            var status = await supervisor.GetStatusAsync();
            Assert.Equal(("Paused", 59L, 59UL), (status.Phase, status.Tick, old.Game.TickNumber));
            Assert.Equal(pausedPose, oldScene.World.GetComponent<LocalTransform>(oldCharacter));
            Assert.Equal(paused, ObserveStep(oldScene, oldCharacter));

            var step = await supervisor.StepAsync();
            Assert.Equal(("Paused", 60L, 60UL), (step.Phase, step.Tick, old.Game.TickNumber));
            AssertOnSurface(ObserveStep(oldScene, oldCharacter), oldFloor, -.02f, .02f, scenario, 60);

            var result = await replacement.ReplaceAsync(new(candidate, oldIdentity, null, candidate.SourceRevision) { World = data });
            Assert.False(result.Playing);
            Assert.Equal(oldIdentity.Generation + 1, result.Identity.Generation);
            Assert.Equal(candidate.BuildId, result.Identity.GameBuildId);
            Assert.Equal((SessionState.Paused, 60UL, result.Identity),
                (supervisor.Status.State, supervisor.Status.Tick, supervisor.Status.Identity));
            Assert.True(oldScene.IsDisposed);
            Assert.True(old.ResourcesReleased);
            Assert.Equal((1, 1), (host.Resources.PublishedWorlds, host.Resources.ActiveResourceOwners));

            Assert.Equal(2, leases.Count);
            var current = leases[1];
            var scene = current.Game.ActiveScene!;
            Assert.NotEqual(oldWorldId, scene.World.Id);
            Assert.Equal(scene.World.Id, supervisor.Status.SceneWorldId);
            Assert.Equal(60UL, current.Game.TickNumber);
            Assert.Equal<PhysicsCharacterState?>(default(PhysicsCharacterState), current.Game.SupportBeforeReplay);
            var character = scene.Entities[PhysicsFixture.Entity(0)];
            var floor = scene.Entities[PhysicsFixture.Entity(1)];
            var tread = scene.Entities[PhysicsFixture.Entity(2)];
            Assert.NotEqual(oldFloor, floor);
            var restored = ObserveStep(scene, character);
            AssertOnSurface(restored, floor, -.02f, .02f, scenario, current.Game.TickNumber);
            var restoredPosition = scene.World.GetComponent<LocalTransform>(character).Position;
            Assert.Contains(current.Game.CaptureRenderScene().Primitives,
                primitive => Vector3.Distance(primitive.World.Translation, restoredPosition) < .001f);
            var settings = scene.World.GetComponent<PhysicsCharacter>(character);
            Assert.Equal((.25f, .30f), (settings.MaximumStepHeight, settings.MinimumTreadDepth));

            scene.World.SetComponent(character, WalkTowardStep);
            var lifted = false;
            var sample = restored;
            for (var i = 0; i < 50; i++)
            {
                var reply = await supervisor.StepAsync();
                Assert.Equal(61L + i, reply.Tick);
                sample = ObserveStep(scene, character);
                if (!lifted) lifted = CheckFirstRise(sample, tread, scenario, current.Game.TickNumber);
            }
            Assert.True(lifted && sample.Feet.Z < -.50f, $"Expected arrival on replacement tread: {DescribeStep(sample, scenario, current.Game.TickNumber)}");
            AssertOnSurface(sample, tread, .23f, .27f, scenario, current.Game.TickNumber);

            await frames.StopAsync();
            Assert.True(oldScene.IsDisposed && scene.IsDisposed);
            Assert.True(current.ResourcesReleased);
            Assert.Equal((0, 0), (host.Resources.PublishedWorlds, host.Resources.ActiveResourceOwners));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CheckpointStepGame(SceneWorldLoader loader, IFactory<PrimitiveSimulation, PrimitiveSimulationRequest> simulations)
        : PrimitiveGameSession(loader, simulations), ICheckpointGameSession
    {
        internal PhysicsCharacterState? SupportBeforeReplay { get; private set; }

        public GameCheckpointState CaptureCheckpoint() => new(TickNumber, default, false);

        public void RestoreCheckpoint(GameCheckpointState state)
        {
            if (state != new GameCheckpointState(60, default, false) || TickNumber != 0 || ActiveScene is not { } scene)
                throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
            var character = scene.Entities[PhysicsFixture.Entity(0)];
            SupportBeforeReplay = scene.World.GetComponent<PhysicsCharacterState>(character);
            // Only this fixture's 60 idle ticks are reproducible; no native or ECS state is serialized.
            for (var i = 0; i < 60; i++) Tick();
        }
    }

    private sealed class StepRuntimeLease(SessionLaunch launch, int processId, CheckpointStepGame game, SceneWorldCodec codec) : IRuntimeLease
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private RuntimeProtocol? _protocol;
        internal CheckpointStepGame Game { get; } = game;
        internal RuntimeProtocol Protocol => _protocol ?? throw new InvalidOperationException("Runtime has not launched.");
        public int ProcessId => processId;
        public Task Exited => _exited.Task;
        public bool ResourcesReleased { get; private set; }

        public Task LaunchAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _protocol = new(launch.Identity, Game, ProcessId, requireRestore: launch.RequireRestore, role: launch.Role, sceneWorldCodec: codec);
            return Task.CompletedTask;
        }

        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => Protocol.SendAsync(request, token);

        public Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            _exited.TrySetResult();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _protocol?.Dispose();
            Game.Dispose();
            ResourcesReleased = true;
            return ValueTask.CompletedTask;
        }
    }
}
