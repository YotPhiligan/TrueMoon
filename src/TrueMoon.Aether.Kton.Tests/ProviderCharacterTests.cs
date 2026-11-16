using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Shapes;
using Xunit;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class ProviderCharacterTests
{
    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void Capsule_FindsFloorMovesAtRequestedVelocityAndBrakes(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        var floor = Box(world, new(100, 1, 20), new(0, -.5f, 0));
        var body = Capsule(world, new(0, 2, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 90);
        var resting = characters.GetCharacterState(character);
        Assert.True(resting.IsSupported);
        Assert.Equal(floor, resting.SupportBody);
        Assert.InRange(resting.Pose.Position.Y, .97f, 1.03f);
        Assert.InRange(Vector3.Dot(resting.SupportNormal, Vector3.UnitY), .999f, 1.001f);

        characters.SetCharacterInput(character, new(new(3, 0)));
        Step(world, 120);
        var moving = characters.GetCharacterState(character);
        Assert.InRange(moving.Pose.Position.X, 5.5f, 6.2f);
        Assert.InRange(moving.LinearVelocity.X, 2.95f, 3.05f);
        Assert.InRange(moving.Pose.Position.Y, .97f, 1.03f);
        Assert.True(moving.IsSupported);

        characters.SetCharacterInput(character, default);
        Step(world, 30);
        var stopped = characters.GetCharacterState(character);
        Assert.InRange(MathF.Abs(stopped.LinearVelocity.X), 0, .03f);
        Assert.InRange(stopped.Pose.Position.X - moving.Pose.Position.X, -.02f, .4f);
        Assert.Equal(Vector3.Zero, world.GetBodyState(body).AngularVelocity);
    }

    [Fact]
    public void VelocityInput_UsesWorldXZRegardlessOfCapsuleYaw()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        Box(world, new(100, 1, 100), new(0, -.5f, 0));
        var body = Capsule(world, new(0, 1, 0));
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, .8f);
        world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: new(new(0, 1, 0), rotation))]);
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 90);
        characters.SetCharacterInput(character, new(new(2, -3)));
        Step(world, 60);
        var state = characters.GetCharacterState(character);
        Assert.InRange(state.Pose.Position.X, 1.8f, 2.1f);
        Assert.InRange(state.Pose.Position.Z, -3.1f, -2.7f);
        Assert.InRange(state.LinearVelocity.X, 1.95f, 2.05f);
        Assert.InRange(state.LinearVelocity.Z, -3.05f, -2.95f);
        Assert.True(MathF.Abs(Quaternion.Dot(rotation, state.Pose.Orientation)) > .9999f);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void Jump_IsOnePulseAndAnAirborneAttemptDoesNotBoostOrQueue(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        Box(world, new(20, 1, 20), new(0, -.5f, 0));
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        characters.SetCharacterInput(character, new(Vector2.Zero, true));
        Step(world, 1);
        Assert.False(characters.GetCharacterState(character).IsSupported);
        Assert.InRange(world.GetBodyState(body).LinearVelocity.Y, 4.7f, 5.2f);
        Step(world, 8);
        var before = world.GetBodyState(body);
        characters.SetCharacterInput(character, new(Vector2.Zero, true));
        Step(world, 1);
        var after = world.GetBodyState(body);
        Assert.InRange(after.LinearVelocity.Y - before.LinearVelocity.Y, -.3f, -.05f);
        Step(world, 140);
        var landed = characters.GetCharacterState(character);
        Assert.True(landed.IsSupported);
        Assert.InRange(landed.Pose.Position.Y, .97f, 1.03f);
        Assert.InRange(MathF.Abs(landed.LinearVelocity.Y), 0, .05f);
        Step(world, 30);
        Assert.InRange(characters.GetCharacterState(character).Pose.Position.Y, .97f, 1.03f);
    }

    [Fact]
    public void MeshSeams_AcrossTriangleAndBodyBoundariesKeepSupport()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        MeshFloor(world, -8, 0);
        MeshFloor(world, 0, 8);
        var body = Capsule(world, new(-5, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        characters.SetCharacterInput(character, new(new(2, 0)));
        var unsupported = 0;
        for (var tick = 0; tick < 300; tick++)
        {
            Step(world, 1);
            var state = characters.GetCharacterState(character);
            if (!state.IsSupported) unsupported++;
            Assert.InRange(state.Pose.Position.Y, .94f, 1.06f);
        }
        Assert.InRange(unsupported, 0, 2);
        Assert.InRange(characters.GetCharacterState(character).Pose.Position.X, 4.7f, 5.1f);
    }

    [Theory]
    [InlineData(20f, true)]
    [InlineData(60f, false)]
    public void SlopeLimit_AcceptsWalkableAndRejectsSteepContact(float degrees, bool supported)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var tangent = MathF.Tan(degrees * MathF.PI / 180);
        Mesh(world, [new(-10, -10 * tangent, -5), new(10, 10 * tangent, -5), new(-10, -10 * tangent, 5), new(10, 10 * tangent, 5)], [0, 1, 2, 1, 3, 2]);
        var body = Capsule(world, new(0, 1.2f, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new() { MaximumSlopeRadians = MathF.PI / 4 });
        Step(world, 45);
        var state = characters.GetCharacterState(character);
        Assert.Equal(supported, state.IsSupported);
        if (supported)
            Assert.InRange(state.SupportNormal.Y, .93f, .95f);
        else
        {
            Assert.Equal(default, state.SupportBody);
            Assert.True(state.Pose.Position.X < -.2f, $"Steep contact should slide: {state.Pose.Position}.");
        }
    }

    [Fact]
    public void SlopeCrest_AscendsAndDescendsWithoutLosingTheSurface()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var height = 6 * MathF.Tan(MathF.PI / 9);
        Mesh(world,
            [new(-6, 0, -5), new(0, height, -5), new(6, 0, -5), new(-6, 0, 5), new(0, height, 5), new(6, 0, 5)],
            [0, 1, 3, 1, 4, 3, 1, 2, 4, 2, 5, 4]);
        var body = Capsule(world, new(-5, height / 6 + 1.1f, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        characters.SetCharacterInput(character, new(new(2, 0)));
        var maximumY = float.MinValue;
        var unsupported = 0;
        for (var tick = 0; tick < 300; tick++)
        {
            Step(world, 1);
            var state = characters.GetCharacterState(character);
            maximumY = MathF.Max(maximumY, state.Pose.Position.Y);
            if (!state.IsSupported) unsupported++;
        }
        var final = characters.GetCharacterState(character);
        Assert.InRange(maximumY, height + .9f, height + 1.15f);
        Assert.InRange(final.Pose.Position.X, 3.8f, 4.6f);
        Assert.True(final.Pose.Position.Y < maximumY - 1.2f);
        Assert.True(final.IsSupported);
        Assert.InRange(unsupported, 0, 8);
    }

    [Fact]
    public void MovingPlatform_TransfersLinearVelocityAndKeepsRelativePosition()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var platform = Box(world, new(8, 1, 8), new(0, -.5f, 0), PhysicsBodyMotion.Kinematic);
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, platform, new(2, 0, 0))]);
        Step(world, 120);
        var state = characters.GetCharacterState(character);
        var platformState = world.GetBodyState(platform);
        Assert.True(state.IsSupported);
        Assert.Equal(platform, state.SupportBody);
        Assert.InRange(state.Pose.Position.X, 3.7f, 4.1f);
        Assert.InRange(MathF.Abs(state.Pose.Position.X - platformState.Pose.Position.X), 0, .25f);
        Assert.InRange(state.LinearVelocity.X, 1.95f, 2.05f);
    }

    [Fact]
    public void JumpFromDynamicSupport_AppliesOpposingImpulseToSupport()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = factory.Create(new() { Gravity = Vector3.Zero });
        var platform = world.CreateBody(new(world.CreateShape(new PhysicsBox(new(8, 1, 8))), PhysicsBodyMotion.Dynamic,
            PhysicsPose.Identity) { Mass = 10 });
        var body = Capsule(world, new(0, 1.5f, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 30);
        Assert.True(characters.GetCharacterState(character).IsSupported);
        Assert.Equal(platform, characters.GetCharacterState(character).SupportBody);
        characters.SetCharacterInput(character, new(Vector2.Zero, true));
        Step(world, 1);
        var characterState = characters.GetCharacterState(character);
        var supportVelocity = world.GetBodyState(platform).LinearVelocity;
        Assert.False(characterState.IsSupported);
        Assert.InRange(characterState.LinearVelocity.Y, 4.9f, 5.1f);
        Assert.InRange(supportVelocity.Y, -.51f, -.49f);
        Assert.InRange(MathF.Abs(characterState.LinearVelocity.Y + 10 * supportVelocity.Y), 0, .01f);
    }

    [Fact]
    public void RotatingPlatform_TransfersContactPointVelocity()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var platform = Box(world, new(8, 1, 8), new(0, -.5f, 0), PhysicsBodyMotion.Kinematic);
        var body = Capsule(world, new(2, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, platform, Angular: new(0, .3f, 0))]);
        Step(world, 120);
        var state = characters.GetCharacterState(character);
        Assert.True(state.IsSupported);
        Assert.Equal(platform, state.SupportBody);
        Assert.InRange(state.Pose.Position.X, 1.4f, 1.9f);
        Assert.InRange(state.Pose.Position.Z, -1.4f, -.8f);
        Assert.InRange(new Vector2(state.Pose.Position.X, state.Pose.Position.Z).Length(), 1.85f, 2.15f);
        Assert.True(state.LinearVelocity.Z < -.35f);
    }

    [Fact]
    public void LowCeiling_BlocksJumpWithoutPenetrationAndAllowsLanding()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        Box(world, new(8, 1, 8), new(0, -.5f, 0));
        Box(world, new(8, .5f, 8), new(0, 2.5f, 0));
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        characters.SetCharacterInput(character, new(Vector2.Zero, true));
        var maximumY = 1f;
        for (var tick = 0; tick < 120; tick++)
        {
            Step(world, 1);
            maximumY = MathF.Max(maximumY, characters.GetCharacterState(character).Pose.Position.Y);
        }
        Assert.InRange(maximumY, 1.12f, 1.31f);
        Assert.True(characters.GetCharacterState(character).IsSupported);
        Assert.InRange(characters.GetCharacterState(character).Pose.Position.Y, .97f, 1.03f);
    }

    [Fact]
    public void Character_PushesDynamicBodyWithSolverResponse()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        Box(world, new(40, 1, 20), new(0, -.5f, 0));
        var pushed = Box(world, Vector3.One, new(2, .5f, 0), PhysicsBodyMotion.Dynamic);
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        characters.SetCharacterInput(character, new(new(3, 0)));
        Step(world, 120);
        Assert.True(world.GetBodyState(pushed).Pose.Position.X > 3.5f);
        Assert.True(characters.GetCharacterState(character).Pose.Position.X > 3);
        Assert.True(characters.GetCharacterState(character).IsSupported);
        Assert.Equal(Vector3.Zero, world.GetBodyState(body).AngularVelocity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedOrTeleportedSupport_IsInvalidatedBeforeNextStepAndCharacterFalls(bool teleport)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var floor = Box(world, new(8, 1, 8), new(0, -.5f, 0));
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        Assert.True(characters.GetCharacterState(character).IsSupported);
        if (teleport)
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, floor, Pose: new(new(50, -.5f, 0), Quaternion.Identity))]);
        else
            world.RemoveBody(floor);
        var invalidated = characters.GetCharacterState(character);
        Assert.False(invalidated.IsSupported);
        Assert.Equal(default, invalidated.SupportBody);
        Step(world, 30);
        Assert.True(characters.GetCharacterState(character).Pose.Position.Y < .1f);
        Assert.True(characters.GetCharacterState(character).LinearVelocity.Y < -4);
    }

    [Fact]
    public void CharacterTeleport_DropsOldSupportAndReacquiresOnlyAtDestination()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var floor = Box(world, new(40, 1, 20), new(0, -.5f, 0));
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 60);
        Assert.True(characters.GetCharacterState(character).IsSupported);
        world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: new(new(10, 4, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, .8f)))]);
        Assert.False(characters.GetCharacterState(character).IsSupported);
        Assert.Equal(default, characters.GetCharacterState(character).SupportBody);
        Step(world, 10);
        Assert.False(characters.GetCharacterState(character).IsSupported);
        Assert.InRange(world.GetBodyState(body).Pose.Position.X, 9.99f, 10.01f);
        Step(world, 100);
        var landed = characters.GetCharacterState(character);
        Assert.True(landed.IsSupported);
        Assert.Equal(floor, landed.SupportBody);
        Assert.InRange(landed.Pose.Position.X, 9.99f, 10.01f);
        Assert.InRange(landed.Pose.Position.Y, .97f, 1.03f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SensorOrFilteredFloor_NeverBecomesSupport(bool sensor)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory);
        var shape = world.CreateShape(new PhysicsBox(new(8, 1, 8)));
        world.CreateBody(new(shape, PhysicsBodyMotion.Static, new(new(0, -.5f, 0), Quaternion.Identity))
        { Mass = 0, IsSensor = sensor, Filter = sensor ? PhysicsCollisionFilter.All : new(0, 0) });
        var body = Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Step(world, 45);
        var state = characters.GetCharacterState(character);
        Assert.False(state.IsSupported);
        Assert.Equal(default, state.SupportBody);
        Assert.True(state.Pose.Position.Y < -1);
    }

    internal static IPhysicsWorld CreateWorld(BepuPhysicsWorldFactory factory, PhysicsQualityProfile profile = PhysicsQualityProfile.Baseline60)
        => factory.Create(new() { Quality = profile });

    internal static PhysicsBodyId Capsule(IPhysicsWorld world, Vector3 position)
        => world.CreateBody(new(world.CreateShape(new PhysicsCapsule(.5f, 1)), PhysicsBodyMotion.Dynamic, new(position, Quaternion.Identity)) { Mass = 1 });

    internal static PhysicsBodyId Box(IPhysicsWorld world, Vector3 size, Vector3 position, PhysicsBodyMotion motion = PhysicsBodyMotion.Static)
        => world.CreateBody(new(world.CreateShape(new PhysicsBox(size)), motion, new(position, Quaternion.Identity)) { Mass = motion == PhysicsBodyMotion.Dynamic ? 1 : 0 });

    internal static void Step(IPhysicsWorld world, int count)
    {
        for (var tick = 0; tick < count; tick++) world.Step(world.LastCompletedTick + 1);
    }

    private static void MeshFloor(IPhysicsWorld world, float minimumX, float maximumX)
        => Mesh(world, [new(minimumX, 0, -5), new(maximumX, 0, -5), new(minimumX, 0, 5), new(maximumX, 0, 5)], [0, 1, 2, 1, 3, 2]);

    private static void Mesh(IPhysicsWorld world, Vector3[] vertices, int[] indices)
        => world.CreateBody(new(world.CreateShape(new PhysicsStaticMesh(vertices, indices)), PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
}
