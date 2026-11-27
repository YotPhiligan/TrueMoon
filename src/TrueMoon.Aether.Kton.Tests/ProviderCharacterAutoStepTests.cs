using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Shapes;
using Xunit;
using ProviderCharacterState = TrueMoon.Kton.Characters.PhysicsCharacterState;
using static TrueMoon.Aether.Kton.Tests.ProviderCharacterTests;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class ProviderCharacterAutoStepTests
{
    private const float Height = .25f;
    private static readonly Vector2 North = new(0, -2.4f);

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, false)]
    [InlineData(PhysicsQualityProfile.FastLinear60, false)]
    [InlineData(PhysicsQualityProfile.Baseline60, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, true)]
    public void StepAtHeightLimit_ReachesTreadAndPublishesRealSupport(PhysicsQualityProfile profile, bool diagonal)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        var south = South(world);
        var north = NorthBox(world);
        var walker = CreateWalker(world, new(0, 0, 1));
        Step(world, 60);
        AssertSupport(walker.State, south);
        walker.Input(diagonal ? Vector2.Normalize(new(.5f, -1)) * 2.4f : North);

        Span<PhysicsContactEvent> events = stackalloc PhysicsContactEvent[8];
        var firstRise = false;
        var separated = false;
        var previousY = Feet(walker.State).Y;
        for (var i = 0; i < 50; i++)
        {
            world.Step(world.LastCompletedTick + 1);
            var state = walker.State;
            var feet = Feet(state);
            Assert.True(feet.Y - previousY <= Height + .02f, $"Feet rose from {previousY} to {feet.Y}.");
            previousY = feet.Y;
            if (!firstRise && feet.Y > .15f)
            {
                firstRise = true;
                Assert.True(feet.Z < 0);
                AssertSupport(state, north);
            }
            foreach (var contact in events[..world.CopyContactEvents(events)])
            {
                if (contact.Kind != PhysicsContactKind.End) continue;
                Assert.NotEqual(PhysicsContactEndReason.Teleported, contact.EndReason);
                if (contact.BodyA == south && contact.BodyB == state.Body || contact.BodyA == state.Body && contact.BodyB == south)
                {
                    Assert.Equal(PhysicsContactEndReason.Separated, contact.EndReason);
                    separated = true;
                }
            }
        }

        Assert.True(firstRise);
        Assert.True(separated);
        var finish = walker.State;
        Assert.True(Feet(finish).Z < 0);
        if (diagonal) Assert.True(Feet(finish).X > .4f);
        Assert.InRange(Feet(finish).Y, Height - .02f, Height + .02f);
        AssertSupport(finish, north);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, .30f, Height)]
    [InlineData(PhysicsQualityProfile.FastLinear60, .30f, Height)]
    [InlineData(PhysicsQualityProfile.Baseline60, Height, 0f)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Height, 0f)]
    public void HigherRiserAndDisabledPolicy_DoNotAdvanceOrBoost(PhysicsQualityProfile profile, float rise, float maximum)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        var south = South(world);
        NorthBox(world, rise);
        var walker = CreateWalker(world, new(0, 0, 1), maximum);
        Step(world, 60);
        AssertBlocked(world, walker, south, Feet(walker.State).Y + .01f);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, Obstacle.ShallowTread)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Obstacle.ShallowTread)]
    [InlineData(PhysicsQualityProfile.Baseline60, Obstacle.CeilingAboveLift)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Obstacle.CeilingAboveLift)]
    [InlineData(PhysicsQualityProfile.Baseline60, Obstacle.CeilingAboveTransfer)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Obstacle.CeilingAboveTransfer)]
    public void ShallowTreadOrLowCeiling_RejectsWholeStep(PhysicsQualityProfile profile, Obstacle obstacle)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        var south = South(world);
        if (obstacle == Obstacle.ShallowTread)
        {
            NorthBox(world, minZ: -.20f);
            NorthBox(world, rise: 0, maxZ: -.20f);
        }
        else
        {
            NorthBox(world);
            Beam(world, obstacle == Obstacle.CeilingAboveLift ? .75f : -.05f);
        }
        var walker = CreateWalker(world, new(0, 0, 1));
        Step(world, 60);
        AssertBlocked(world, walker, south, .02f);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, true)]
    [InlineData(PhysicsQualityProfile.Baseline60, false)]
    [InlineData(PhysicsQualityProfile.FastLinear60, false)]
    public void SensorsAndExcludedLayers_DoNotProvideOrBlockStep(PhysicsQualityProfile profile, bool sensor)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        South(world);
        NorthBox(world, sensor: sensor, layer: sensor ? (byte)0 : (byte)1);
        var flat = NorthBox(world, rise: 0);
        var walker = CreateWalker(world, new(0, 0, 1));
        Step(world, 60);
        walker.Input(North);
        for (var i = 0; i < 50; i++)
        {
            Step(world, 1);
            Assert.True(Feet(walker.State).Y <= .02f);
        }
        var finish = walker.State;
        Assert.True(Feet(finish).Z < -.5f);
        AssertSupport(finish, flat);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void SensorAndExcludedBeams_DoNotBlockClearTread(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        South(world);
        var north = NorthBox(world);
        Beam(world, .75f, sensor: true);
        Beam(world, -.05f, layer: 1);
        var walker = CreateWalker(world, new(0, 0, 1));
        Step(world, 60);
        walker.Input(North);
        Step(world, 50);
        var finish = walker.State;
        Assert.True(Feet(finish).Z < 0);
        Assert.InRange(Feet(finish).Y, Height - .02f, Height + .02f);
        AssertSupport(finish, north);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void GapAndAirborneApproach_DoNotSnapToTread(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = CreateWorld(factory, profile))
        {
            South(world);
            var north = NorthBox(world, maxZ: -.10f);
            var walker = CreateWalker(world, new(0, 0, 1));
            Step(world, 60);
            walker.Input(North);
            for (var i = 0; i < 50; i++)
            {
                Step(world, 1);
                var state = walker.State;
                Assert.True(Feet(state).Y <= .02f);
                Assert.NotEqual(north, state.SupportBody);
            }
        }
        using (var world = CreateWorld(factory, profile))
        {
            South(world);
            var north = NorthBox(world);
            // Within 2 cm of the blocked centre (~0.296 m from the riser).
            var walker = CreateWalker(world, new(0, .10f, .31f));
            Assert.False(walker.State.IsSupported);
            walker.Input(North);
            Step(world, 1);
            var state = walker.State;
            Assert.InRange(Feet(state).Y, .08f, .101f);
            Assert.NotEqual(north, state.SupportBody);
        }
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void JumpPulseTakesPrecedenceAtBlockedRiser(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        var south = South(world);
        var north = NorthBox(world);
        var walker = CreateWalker(world, new(0, 0, .31f));
        Step(world, 60);
        AssertSupport(walker.State, south);
        walker.Input(North, jump: true);
        Step(world, 1);
        var state = walker.State;
        Assert.True(state.LinearVelocity.Y > 1);
        Assert.InRange(Feet(state).Y, .01f, .15f);
        Assert.NotEqual(north, state.SupportBody);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void DescentStillReachesStreetWithSolverGravity(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        var south = South(world);
        var north = NorthBox(world);
        var walker = CreateWalker(world, new(0, Height, -1));
        Step(world, 60);
        AssertSupport(walker.State, north);
        walker.Input(-North);
        var lostSupport = false;
        for (var i = 0; i < 65; i++)
        {
            Step(world, 1);
            lostSupport |= !walker.State.IsSupported;
        }
        Assert.True(lostSupport);
        var finish = walker.State;
        Assert.True(Feet(finish).Z > 0);
        Assert.InRange(Feet(finish).Y, -.02f, .02f);
        AssertSupport(finish, south);
    }

    private static void AssertBlocked(IPhysicsWorld world, Walker walker, PhysicsBodyId south, float maxY)
    {
        AssertSupport(walker.State, south);
        walker.Input(North);
        for (var i = 0; i < 50; i++)
        {
            Step(world, 1);
            var feet = Feet(walker.State);
            Assert.True(feet.Z >= 0 && feet.Y <= maxY, $"Feet crossed the blocked limits: {feet} (max Y {maxY}).");
        }
        var finish = walker.State;
        Assert.InRange(Feet(finish).Z, 0, .35f);
        AssertSupport(finish, south);
    }

    private static void AssertSupport(ProviderCharacterState state, PhysicsBodyId expected)
    {
        Assert.True(state.IsSupported);
        Assert.Equal(expected, state.SupportBody);
        Assert.True(state.SupportNormal.Y > .99f);
    }

    private static Vector3 Feet(ProviderCharacterState state) => state.Pose.Position - new Vector3(0, .6f, 0);

    private static Walker CreateWalker(IPhysicsWorld world, Vector3 feet, float maximum = Height)
    {
        var body = world.CreateBody(new(world.CreateShape(new PhysicsCapsule(.3f, .6f)), PhysicsBodyMotion.Dynamic,
            new(feet + new Vector3(0, .6f, 0), Quaternion.Identity)) { Mass = 1, Filter = new(0, ~(1u << 1)) });
        var characters = (IPhysicsCharacterWorld)world;
        return new(characters, characters.CreateCharacter(body, new() { MaximumStepHeight = maximum }));
    }

    private static PhysicsBodyId South(IPhysicsWorld world) => Box(world, new(4, 1, 3), new(0, -.5f, 1.5f));

    private static PhysicsBodyId NorthBox(IPhysicsWorld world, float rise = Height, float minZ = -3, float maxZ = 0,
        bool sensor = false, byte layer = 0)
        => StaticBox(world, new(4, rise + 1, maxZ - minZ), new(0, (rise - 1) * .5f, (minZ + maxZ) * .5f), sensor, layer);

    private static void Beam(IPhysicsWorld world, float maxZ, bool sensor = false, byte layer = 0)
        => StaticBox(world, new(4, .5f, maxZ + 1), new(0, 1.6f, (maxZ - 1) * .5f), sensor, layer);

    private static PhysicsBodyId StaticBox(IPhysicsWorld world, Vector3 size, Vector3 centre, bool sensor, byte layer)
        => world.CreateBody(new(world.CreateShape(new PhysicsBox(size)), PhysicsBodyMotion.Static, new(centre, Quaternion.Identity))
        { Mass = 0, IsSensor = sensor, Filter = new(layer, uint.MaxValue) });

    private readonly record struct Walker(IPhysicsCharacterWorld World, PhysicsCharacterId Id)
    {
        internal ProviderCharacterState State => World.GetCharacterState(Id);
        internal void Input(Vector2 velocity, bool jump = false) => World.SetCharacterInput(Id, new(velocity, jump));
    }

    public enum Obstacle { ShallowTread, CeilingAboveLift, CeilingAboveTransfer }
}
