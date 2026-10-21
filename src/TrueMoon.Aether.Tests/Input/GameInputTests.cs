using System.Numerics;
using EngineSessionProbe;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Tests.Input;

public sealed class GameInputTests
{
    [Fact]
    public void State_ValidatesNeutralAndExactLookBoundaries()
    {
        default(GameInputState).Validate();
        new GameInputState(true, GameButtons.Forward | GameButtons.Backward | GameButtons.Left | GameButtons.Right, .5f, -.5f, true).Validate();
        new GameInputTick(GameButtons.None, -1, 1, true).Validate();
        var state = new GameInputAccumulator(); state.Set(new(true, GameButtons.Right, .5f, -.5f, true));
        Assert.Equal(new GameInputTick(GameButtons.Right, .5f, -.5f, true), state.Take());
        state.Set(default); Assert.Equal(default, state.Take());
    }
    [Theory]
    [InlineData("flags")] [InlineData("yaw-nan")] [InlineData("pitch-infinite")]
    [InlineData("yaw-high")] [InlineData("pitch-low")] [InlineData("release-held")]
    [InlineData("release-look")] [InlineData("release-action")]
    public void State_RejectsMalformedPacketsBeforeMutation(string invalid)
    {
        var input = invalid switch
        {
            "flags" => new(true, (GameButtons)16), "yaw-nan" => new(true, Yaw: float.NaN),
            "pitch-infinite" => new(true, Pitch: float.PositiveInfinity), "yaw-high" => new(true, Yaw: .5001f),
            "pitch-low" => new(true, Pitch: -.5001f), "release-held" => new(false, GameButtons.Forward),
            "release-look" => new(false, Yaw: .1f), _ => new GameInputState(false, InteractPressed: true)
        };
        var accumulator = new GameInputAccumulator(); accumulator.Set(new(true, GameButtons.Left, .2f, -.1f, true));
        Assert.Throws<InvalidDataException>(input.Validate);
        Assert.Throws<InvalidDataException>(() => accumulator.Set(input));
        Assert.Equal(new GameInputTick(GameButtons.Left, .2f, -.1f, true), accumulator.Take());
    }
    [Theory]
    [InlineData(16, 0, 0)] [InlineData(0, float.NaN, 0)] [InlineData(0, 0, float.PositiveInfinity)]
    [InlineData(0, 1.001f, 0)] [InlineData(0, 0, -1.001f)]
    public void Tick_RejectsInvalidFlagsAndLook(int flags, float yaw, float pitch)
    { Assert.Throws<InvalidDataException>(() => new GameInputTick((GameButtons)flags, yaw, pitch, false).Validate()); }

    [Fact]
    public void Accumulator_CoalescesLookAndActionsOnceAndExpiresAfterTwelveTicks()
    {
        var state = new GameInputAccumulator();
        for (var i = 0; i < 4; i++) state.Set(new(true, GameButtons.Forward, .4f, -.4f, true));
        Assert.Equal(new GameInputTick(GameButtons.Forward, 1, -1, true), state.Take());
        for (var i = 1; i < 12; i++) Assert.Equal(new GameInputTick(GameButtons.Forward, 0, 0, false), state.Take());
        Assert.Equal(default, state.Take()); Assert.Equal(default, state.Take());
    }
    [Fact]
    public void Accumulator_RefreshExtendsLeaseReleaseClearsPendingAndInvalidPacketDoesNotRefresh()
    {
        var state = new GameInputAccumulator(); state.Set(new(true, GameButtons.Right));
        for (var i = 0; i < 11; i++) state.Take();
        Assert.Throws<InvalidDataException>(() => state.Set(new(true, Pitch: float.NaN)));
        Assert.Equal(GameButtons.Right, state.Take().Buttons); Assert.Equal(default, state.Take());
        state.Set(new(true, GameButtons.Left)); for (var i = 0; i < 11; i++) state.Take();
        state.Set(new(true, GameButtons.Backward, .25f, -.3f, true));
        Assert.Equal(new GameInputTick(GameButtons.Backward, .25f, -.3f, true), state.Take());
        state.Set(default); Assert.Equal(default, state.Take());
        state.Set(new(true, GameButtons.Forward, .2f, .1f, true)); state.Clear(); Assert.Equal(default, state.Take());
    }
    [Theory]
    [InlineData(GameButtons.Forward, 0, -.05f)] [InlineData(GameButtons.Backward, 0, .05f)]
    [InlineData(GameButtons.Left, -.05f, 0)] [InlineData(GameButtons.Right, .05f, 0)]
    [InlineData(GameButtons.Forward | GameButtons.Backward | GameButtons.Left | GameButtons.Right, 0, 0)]
    [InlineData(GameButtons.None, 0, 0)]
    public void Camera_UsesFixedSpeedAndOppositeKeysCancel(GameButtons held, float x, float z)
    {
        var result = FirstPersonCamera.Step(new(0, 1, 3, 0, 0, .1f), new(held, 0, 0, false));
        Assert.Equal(x, result.X, 5); Assert.Equal(3 + z, result.Z, 5); Assert.Equal(1, result.Y); Assert.Equal(.1f, result.Roll);
    }
    [Fact]
    public void Camera_NormalizesDiagonalAndTurnsMovementWithYaw()
    {
        var camera = new CameraTransform(0, 1, 3, 0, 0, 0);
        var diagonal = FirstPersonCamera.Step(camera, new(GameButtons.Forward | GameButtons.Right, 0, 0, false));
        Assert.Equal(.05f, Vector2.Distance(new(0, 3), new(diagonal.X, diagonal.Z)), 5);
        Assert.Equal(.05f / MathF.Sqrt(2), diagonal.X, 5); Assert.True(diagonal.Z < 3);
        var right = FirstPersonCamera.Step(camera with { Yaw = -MathF.PI / 2 }, new(GameButtons.Forward, 0, 0, false));
        Assert.Equal(.05f, right.X, 5); Assert.Equal(3, right.Z, 5);
        var ahead = camera; for (var i = 0; i < 60; i++) ahead = FirstPersonCamera.Step(ahead, new(GameButtons.Forward, 0, 0, false));
        Assert.Equal(0, ahead.Z, 4);
    }
    [Fact]
    public void Camera_ClampsPitchWrapsYawAndRejectsInvalidPose()
    {
        var result = FirstPersonCamera.Step(new(0, 1, 3, 1.5f, MathF.PI, 0), new(GameButtons.None, .2f, .5f, false));
        Assert.Equal(FirstPersonCamera.MaximumPitch, result.Pitch); Assert.Equal(-MathF.PI + .2f, result.Yaw, 5);
        result = FirstPersonCamera.Step(result with { Pitch = -1.5f }, new(GameButtons.None, 0, -.5f, false));
        Assert.Equal(-FirstPersonCamera.MaximumPitch, result.Pitch);
        Assert.Throws<InvalidDataException>(() => FirstPersonCamera.Step(new(float.NaN, 1, 3, 0, 0, 0), default));
    }
    [Fact]
    public void UiBuffer_PreservesQuickEKeyRepeatAndConsumesLookOnce()
    {
        var state = new SceneInputBuffer();
        state.Key(UiKey.W, true); state.Key(UiKey.D, true); state.Key(UiKey.E, true); state.Key(UiKey.E, true); state.Key(UiKey.E, false);
        state.Look(12, -6);
        Assert.Equal(new GameInputState(true, GameButtons.Forward | GameButtons.Right, -.03f, .015f, true), state.Take());
        Assert.Equal(new GameInputState(true, GameButtons.Forward | GameButtons.Right), state.Take());
        state.Key(UiKey.E, true); Assert.True(state.Take().InteractPressed);
        state.Key(UiKey.E, true); Assert.False(state.Take().InteractPressed);
        state.Key(UiKey.W, false); state.Key(UiKey.D, false); Assert.Equal(GameButtons.None, state.Take().Buttons);
    }
    [Fact]
    public void UiBuffer_BoundsMouseIgnoresInvalidCoordinatesAndResetDropsAllTransientState()
    {
        var state = new SceneInputBuffer(); state.Look(100000, -100000); state.Look(float.NaN, 0);
        Assert.Equal(new GameInputState(true, Yaw: -.5f, Pitch: .5f), state.Take());
        state.Key(UiKey.A, true); state.Key(UiKey.S, true); state.Key(UiKey.E, true); state.Look(10, 2); state.Clear();
        Assert.Equal(new GameInputState(true), state.Take());
        state.Key(UiKey.E, true); Assert.True(state.Take().InteractPressed);
    }
    [Fact]
    public void CameraGame_CheckpointRetainsPoseInteractionAndNeutralTicksStayStill()
    {
        using var game = new CameraGame(); Assert.Throws<InvalidOperationException>(game.CaptureCheckpoint);
        game.Load(new("room", 7, [new() { Id = "switch", Interactive = true }])); var initial = game.CaptureCheckpoint();
        game.Tick(new(GameButtons.Forward, .1f, .2f, true)); var moved = game.CaptureCheckpoint();
        Assert.Equal(1UL, moved.Tick); Assert.True(moved.Interaction); Assert.Equal(0xffc06020u, game.PatternColor);
        game.Tick(); Assert.Equal(moved.Camera, game.CaptureCheckpoint().Camera); Assert.True(game.CaptureCheckpoint().Interaction);
        Assert.Throws<InvalidDataException>(() => game.Tick(new(GameButtons.None, float.NaN, 0, false)));
        Assert.Equal(2UL, game.TickNumber);
        var beforeInvalidRestore = game.CaptureCheckpoint();
        Assert.Throws<InvalidDataException>(() => game.RestoreCheckpoint(new(99, beforeInvalidRestore.Camera with { X = float.NaN }, false)));
        Assert.Equal(beforeInvalidRestore, game.CaptureCheckpoint());
        game.RestoreCheckpoint(initial); Assert.Equal(initial, game.CaptureCheckpoint()); Assert.Equal(0xff2080c0u, game.PatternColor);
        game.Tick(new(GameButtons.None, 0, 0, true)); Assert.True(game.CaptureCheckpoint().Interaction);
        game.Tick(new(GameButtons.None, 0, 0, true)); Assert.False(game.CaptureCheckpoint().Interaction);
        game.Load(new("empty", 8, [])); game.Tick(new(GameButtons.None, 0, 0, true)); Assert.False(game.CaptureCheckpoint().Interaction);
        game.Dispose(); Assert.Throws<ObjectDisposedException>(() => game.Tick());
    }
}
