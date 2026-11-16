using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Shapes;
using Xunit;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class ProviderCharacterValidationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Options_ValidateFinitePositiveValuesAndInclusiveCeilings(int field)
    {
        var maximum = field switch { 0 or 4 => 50f, 1 => 1.5f, _ => 100000f };
        Assert.False(Options(field, maximum).Validate().IsFailure);
        Assert.False(Options(field, maximum / 2).Validate().IsFailure);
        foreach (var invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity, float.NegativeInfinity, MathF.BitIncrement(maximum) })
        {
            var error = Options(field, invalid).Validate();
            Assert.Equal(PhysicsErrorCode.InvalidArgument, error.Code);
            Assert.Equal("Character.Options", error.Field);
            Assert.Equal(error, Options(field, invalid).ValidateInput(default));
        }
    }

    [Fact]
    public void Input_UsesVectorMagnitudeAndIncludesExactMaximum()
    {
        var options = new PhysicsCharacterOptions { MaximumSpeed = 5 };
        Assert.False(options.ValidateInput(default).IsFailure);
        Assert.False(options.ValidateInput(new(new(3, 4), true)).IsFailure);
        Assert.False(options.ValidateInput(new(new(-5, 0))).IsFailure);
        foreach (var velocity in new[] { new Vector2(4, 4), new(0, MathF.BitIncrement(5)), new(float.NaN, 0), new(0, float.PositiveInfinity) })
            Assert.Equal(PhysicsErrorCode.InvalidArgument, options.ValidateInput(new(velocity)).Code);
    }

    [Fact]
    public void InvalidOptionsOrInput_DoesNotPublishControllerOrReplaceValidGoal()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = ProviderCharacterTests.CreateWorld(factory);
        ProviderCharacterTests.Box(world, new(20, 1, 20), new(0, -.5f, 0));
        var body = ProviderCharacterTests.Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        Assert.Equal(PhysicsErrorCode.InvalidArgument, Assert.Throws<PhysicsException>(() => characters.CreateCharacter(body, null!)).Error.Code);
        Assert.Equal(PhysicsErrorCode.InvalidArgument, Assert.Throws<PhysicsException>(() => characters.CreateCharacter(body, new() { MaximumSpeed = float.NaN })).Error.Code);
        var character = characters.CreateCharacter(body, new());
        ProviderCharacterTests.Step(world, 60);
        characters.SetCharacterInput(character, new(new(2, 0)));
        Assert.Equal(PhysicsErrorCode.InvalidArgument, Assert.Throws<PhysicsException>(() => characters.SetCharacterInput(character, new(new(6, 6)))).Error.Code);
        Assert.Equal(60UL, world.LastCompletedTick);
        Assert.Equal(PhysicsWorldState.Ready, world.State);
        ProviderCharacterTests.Step(world, 60);
        Assert.InRange(characters.GetCharacterState(character).Pose.Position.X, 1.8f, 2.1f);
        Assert.InRange(characters.GetCharacterState(character).LinearVelocity.X, 1.95f, 2.05f);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void UnsupportedBody_IsRejectedBeforeMutation(int invalid)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = ProviderCharacterTests.CreateWorld(factory);
        PhysicsShapeDefinition definition = invalid switch
        {
            3 => new PhysicsBox(Vector3.One),
            4 => new PhysicsSphere(.5f),
            _ => new PhysicsCapsule(.5f, 1)
        };
        var motion = invalid switch { 0 => PhysicsBodyMotion.Static, 1 => PhysicsBodyMotion.Kinematic, _ => PhysicsBodyMotion.Dynamic };
        var body = world.CreateBody(new(world.CreateShape(definition), motion,
            new(new(0, 2, 0), invalid == 5 ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, .1f) : Quaternion.Identity))
        { Mass = motion == PhysicsBodyMotion.Dynamic ? 1 : 0, IsSensor = invalid == 2, AngularVelocity = invalid == 6 ? Vector3.UnitY : Vector3.Zero });
        var before = world.GetBodyState(body);
        Assert.Throws<PhysicsException>(() => ((IPhysicsCharacterWorld)world).CreateCharacter(body, new()));
        Assert.Equal(before, world.GetBodyState(body));
        Assert.Equal(0UL, world.LastCompletedTick);
        Assert.Equal(PhysicsWorldState.Ready, world.State);
        world.RemoveBody(body);
    }

    [Fact]
    public void CharacterIds_RejectDefaultForeignStaleAndNeverAliasReusedSlot()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var first = ProviderCharacterTests.CreateWorld(factory);
        using var second = ProviderCharacterTests.CreateWorld(factory);
        var body = ProviderCharacterTests.Capsule(first, new(0, 2, 0));
        var otherBody = ProviderCharacterTests.Capsule(second, new(0, 2, 0));
        var characters = (IPhysicsCharacterWorld)first;
        var otherCharacters = (IPhysicsCharacterWorld)second;
        var character = characters.CreateCharacter(body, new());
        var foreign = otherCharacters.CreateCharacter(otherBody, new());
        Assert.Equal(PhysicsErrorCode.InvalidIdentifier, Assert.Throws<PhysicsException>(() => characters.GetCharacterState(default)).Error.Code);
        Assert.Equal(PhysicsErrorCode.ForeignWorld, Assert.Throws<PhysicsException>(() => characters.GetCharacterState(foreign)).Error.Code);
        Assert.Equal(PhysicsErrorCode.ResourceInUse, Assert.Throws<PhysicsException>(() => characters.CreateCharacter(body, new())).Error.Code);
        characters.RemoveCharacter(character);
        var replacement = characters.CreateCharacter(body, new());
        Assert.Equal(character.Slot, replacement.Slot);
        Assert.True(replacement.Generation > character.Generation);
        Assert.Equal(PhysicsErrorCode.StaleIdentifier, Assert.Throws<PhysicsException>(() => characters.GetCharacterState(character)).Error.Code);
        Assert.Equal(PhysicsErrorCode.StaleIdentifier, Assert.Throws<PhysicsException>(() => characters.SetCharacterInput(character, default)).Error.Code);
        Assert.Equal(PhysicsErrorCode.StaleIdentifier, Assert.Throws<PhysicsException>(() => characters.RemoveCharacter(character)).Error.Code);
        Assert.Equal(replacement, characters.GetCharacterState(replacement).Id);
        Assert.Equal(body, characters.GetCharacterState(replacement).Body);
    }

    [Fact]
    public void Controller_BorrowsBodyAndRequiresExplicitRemovalBeforeBodyDestruction()
    {
        using var factory = new BepuPhysicsWorldFactory();
        var world = ProviderCharacterTests.CreateWorld(factory);
        var body = ProviderCharacterTests.Capsule(world, new(0, 3, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        Assert.Equal(PhysicsErrorCode.ResourceInUse, Assert.Throws<PhysicsException>(() => world.RemoveBody(body)).Error.Code);
        Assert.Equal(PhysicsWorldState.Ready, world.State);
        characters.RemoveCharacter(character);
        Assert.Equal(body, world.GetBodyState(body).Body);
        world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, body, Angular: Vector3.UnitZ)]);
        ProviderCharacterTests.Step(world, 12);
        Assert.True(MathF.Abs(world.GetBodyState(body).Pose.Orientation.Z) > .05f);
        world.RemoveBody(body);
        world.Dispose();
        Assert.Equal(0, factory.Resources.ActiveResourceOwners);
        Assert.Equal(0, factory.Resources.PublishedWorlds);
        Assert.Equal(0, factory.Resources.MaximumPoolBytesAfterCleanup);
        Assert.Equal(PhysicsErrorCode.Disposed, Assert.Throws<PhysicsException>(() => characters.GetCharacterState(character)).Error.Code);
    }

    [Fact]
    public void CharacterAndBallSocket_ExcludeEachOtherInEitherCreationOrder()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = ProviderCharacterTests.CreateWorld(factory);
        var body = ProviderCharacterTests.Capsule(world, new(0, 3, 0));
        var anchor = ProviderCharacterTests.Box(world, Vector3.One, new(0, 5, 0), PhysicsBodyMotion.Kinematic);
        var description = new PhysicsBallSocket(body, anchor, Vector3.UnitY, -Vector3.UnitY);
        var joint = world.CreateBallSocket(description);
        var characters = (IPhysicsCharacterWorld)world;
        Assert.Equal(PhysicsErrorCode.ResourceInUse, Assert.Throws<PhysicsException>(() => characters.CreateCharacter(body, new())).Error.Code);
        world.RemoveJoint(joint);
        var character = characters.CreateCharacter(body, new());
        Assert.Equal(PhysicsErrorCode.ResourceInUse, Assert.Throws<PhysicsException>(() => world.CreateBallSocket(description)).Error.Code);
        Assert.Equal(PhysicsWorldState.Ready, world.State);
        characters.RemoveCharacter(character);
        var replacement = world.CreateBallSocket(description);
        world.RemoveJoint(replacement);
        world.RemoveBody(body);
        Assert.Equal(PhysicsWorldState.Ready, world.State);
    }

    [Fact]
    public void DetachedController_RestoresRotationalResponseToOffCentreContact()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = ProviderCharacterTests.CreateWorld(factory);
        ProviderCharacterTests.Box(world, new(20, 1, 20), new(0, -.5f, 0));
        var body = ProviderCharacterTests.Capsule(world, new(0, 1, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        ProviderCharacterTests.Step(world, 60);
        characters.RemoveCharacter(character);
        var ram = ProviderCharacterTests.Box(world, new(.5f, .5f, 2), new(-2, 1.7f, 0), PhysicsBodyMotion.Kinematic);
        world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, ram, new(3, 0, 0))]);
        var maximumAngularSpeed = 0f;
        for (var tick = 0; tick < 60; tick++)
        {
            ProviderCharacterTests.Step(world, 1);
            maximumAngularSpeed = MathF.Max(maximumAngularSpeed, world.GetBodyState(body).AngularVelocity.Length());
        }
        Assert.True(maximumAngularSpeed > .2f, $"Detached body retained locked inertia: peak angular speed {maximumAngularSpeed}.");
        Assert.True(MathF.Abs(world.GetBodyState(body).Pose.Orientation.Z) > .05f);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CharacterCommandRestrictions_RejectEntireBatchBeforeOtherBodyMutation(int invalid)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = ProviderCharacterTests.CreateWorld(factory);
        var body = ProviderCharacterTests.Capsule(world, new(0, 3, 0));
        var other = ProviderCharacterTests.Box(world, Vector3.One, new(5, 3, 0), PhysicsBodyMotion.Dynamic);
        ((IPhysicsCharacterWorld)world).CreateCharacter(body, new());
        var bad = invalid switch
        {
            0 => new PhysicsBodyCommand(PhysicsCommandKind.SetVelocity, body, Angular: Vector3.UnitY),
            1 => new PhysicsBodyCommand(PhysicsCommandKind.Teleport, body, Pose: new(new(0, 3, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .1f))),
            _ => new PhysicsBodyCommand(PhysicsCommandKind.Sleep, body)
        };
        Assert.Equal(PhysicsErrorCode.InvalidArgument, Assert.Throws<PhysicsException>(() => world.ApplyCommands(
            [new(PhysicsCommandKind.SetVelocity, other, new(3, 0, 0)), bad])).Error.Code);
        Assert.Equal(Vector3.Zero, world.GetBodyState(other).LinearVelocity);
        Assert.Equal(new Vector3(0, 3, 0), world.GetBodyState(body).Pose.Position);
        Assert.Equal(0UL, world.LastCompletedTick);
        Assert.Equal(PhysicsWorldState.Ready, world.State);
    }

    [Fact]
    public void Character_ExternalLinearImpulseRemainsPhysicalWhileAirborne()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = ProviderCharacterTests.CreateWorld(factory);
        var body = ProviderCharacterTests.Capsule(world, new(0, 3, 0));
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(body, new());
        world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, body, new(2, 0, 0))]);
        characters.SetCharacterInput(character, new(new(-3, 0)));
        ProviderCharacterTests.Step(world, 30);
        var state = characters.GetCharacterState(character);
        Assert.False(state.IsSupported);
        Assert.InRange(state.Pose.Position.X, .98f, 1.02f);
        Assert.InRange(state.LinearVelocity.X, 1.99f, 2.01f);
        Assert.True(state.LinearVelocity.Y < -4);
    }

    private static PhysicsCharacterOptions Options(int field, float value) => field switch
    {
        0 => new() { MaximumSpeed = value },
        1 => new() { MaximumSlopeRadians = value },
        2 => new() { MaximumHorizontalForce = value },
        3 => new() { MaximumVerticalForce = value },
        _ => new() { JumpSpeed = value }
    };
}
