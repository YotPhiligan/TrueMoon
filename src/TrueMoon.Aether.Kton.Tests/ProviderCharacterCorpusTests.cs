using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Shapes;
using Xunit;
using ProviderCharacterState = TrueMoon.Kton.Characters.PhysicsCharacterState;
using static TrueMoon.Aether.Kton.Tests.ProviderCharacterTests;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class ProviderCharacterCorpusTests
{
    private const int SettleTicks = 60;
    private const float WalkSpeed = 2.4f;
    private const float TravelerDiameter = .60f;
    private const float TravelerHeight = 1.20f;
    private const float LightDiameter = .75f;
    private const float LightHeight = 1.45f;
    private const float HeavyDiameter = 1.25f;
    private const float HeavyHeight = 2f;
    private const float LowCapsuleDiameter = .50f;
    private const float LowCapsuleHeight = .55f;
    private const int Street = 0, LowerTread = 1, Ramp = 2, Landing = 3, UpperTread = 4;

    public enum Passage { LivingDoor, WideDoor, UnderDeck, Culvert }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.LivingDoor, TravelerDiameter, TravelerHeight, true)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.LivingDoor, LightDiameter, LightHeight, true)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.LivingDoor, HeavyDiameter, HeavyHeight, false)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.WideDoor, HeavyDiameter, HeavyHeight, true)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.UnderDeck, TravelerDiameter, TravelerHeight, true)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.UnderDeck, HeavyDiameter, HeavyHeight, false)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.Culvert, LowCapsuleDiameter, LowCapsuleHeight, true)]
    [InlineData(PhysicsQualityProfile.Baseline60, Passage.Culvert, TravelerDiameter, TravelerHeight, false)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.LivingDoor, TravelerDiameter, TravelerHeight, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.LivingDoor, LightDiameter, LightHeight, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.LivingDoor, HeavyDiameter, HeavyHeight, false)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.WideDoor, HeavyDiameter, HeavyHeight, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.UnderDeck, TravelerDiameter, TravelerHeight, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.UnderDeck, HeavyDiameter, HeavyHeight, false)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.Culvert, LowCapsuleDiameter, LowCapsuleHeight, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, Passage.Culvert, TravelerDiameter, TravelerHeight, false)]
    public void PassageClearance_DependsOnCapsuleDimensions(PhysicsQualityProfile profile, Passage passage,
        float diameter, float height, bool passes)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        ReadOnlySpan<PhysicsBodyId> supports = [FlatFloor(world)];
        var depth = BuildPassage(world, passage);
        var walker = CreateWalker(world, new(0, 0, 1.5f), diameter, height, out var characters);
        var context = $"{profile}, {passage}, Ø{diameter} × {height} m";
        var sample = Settle(world, characters, walker, height, supports, 0, 0, context);

        characters.SetCharacterInput(walker, new(new(0, -WalkSpeed)));
        const int ticks = 100;
        var crossed = false;
        for (var tick = 1; tick <= ticks; tick++)
        {
            sample = Advance(world, characters, walker, height, supports, context, tick);
            Require(MathF.Abs(sample.Feet.Y) <= .02f, context, tick, sample, "feet within 0.02 m of the floor");
            if (passes)
            {
                crossed |= sample.Feet.Z < 0;
                Require(MathF.Abs(sample.Feet.X) <= .03f, context, tick, sample, "centre stays within 0.03 m of the passage axis");
            }
            else
            {
                Require(sample.Feet.Z >= -.02f, context, tick, sample, "blocked centre does not cross the entrance by > 0.02 m");
            }
        }

        RequireFlatSupport(sample, 0, 0, context, ticks);
        if (passes)
        {
            var exit = -depth - diameter / 2 - .20f;
            Require(crossed && sample.Feet.Z < exit, context, ticks, sample, $"entire capsule clears the exit: Z < {exit}");
        }
        else
        {
            Require(sample.Feet.Z is >= -.02f and <= .70f && MathF.Abs(sample.Velocity.Z) <= .10f,
                context, ticks, sample, "stopped at the entrance: Z in [-0.02, 0.70], |Vz| <= 0.10 m/s");
        }
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void ThinWall_BlocksWithoutClimbingAndAllowsBacktracking(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        ReadOnlySpan<PhysicsBodyId> supports = [FlatFloor(world)];
        StaticBox(world, new(-3, 0, -.20f), new(3, 3, 0));
        var walker = CreateWalker(world, new(0, 0, 1.5f), TravelerDiameter, TravelerHeight, out var characters);
        var context = $"{profile}, thin wall";
        var sample = Settle(world, characters, walker, TravelerHeight, supports, 0, 0, context);

        characters.SetCharacterInput(walker, new(new(0, -WalkSpeed)));
        for (var tick = 1; tick <= 80; tick++)
        {
            sample = Advance(world, characters, walker, TravelerHeight, supports, context, tick);
            Require(sample.Feet.Z >= 0 && MathF.Abs(sample.Feet.Y) <= .02f,
                context, tick, sample, "feet stay on the approach side and within 0.02 m of the floor");
        }

        RequireFlatSupport(sample, 0, 0, context, 80);
        Require(sample.Feet.Z is >= .24f and <= .36f && MathF.Abs(sample.Velocity.Z) <= .10f,
            context, 80, sample, "stopped at the wall: Z in [0.24, 0.36], |Vz| <= 0.10 m/s");

        characters.SetCharacterInput(walker, new(new(0, WalkSpeed)));
        for (var tick = 81; tick <= 110; tick++)
        {
            sample = Advance(world, characters, walker, TravelerHeight, supports, context, tick);
            Require(MathF.Abs(sample.Feet.Y) <= .02f, context, tick, sample, "feet stay within 0.02 m of the floor while backtracking");
        }

        RequireFlatSupport(sample, 0, 0, context, 110);
        Require(sample.Feet.Z > 1.20f, context, 110, sample, "backtracks away from the wall to Z > 1.20");
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, false)]
    [InlineData(PhysicsQualityProfile.Baseline60, true)]
    [InlineData(PhysicsQualityProfile.FastLinear60, false)]
    [InlineData(PhysicsQualityProfile.FastLinear60, true)]
    public void MixedStepsAndRamp_TraversesInEitherDirectionAndRepeatsExactly(PhysicsQualityProfile profile, bool descending)
    {
        var context = $"{profile}, {(descending ? "descending" : "ascending")} mixed route";
        var trace = RunMixedRoute(profile, descending, context);
        var repeat = RunMixedRoute(profile, descending, context);
        for (var tick = 0; tick < trace.Length; tick++)
        {
            if (trace[tick] != repeat[tick])
                Assert.Fail($"{context}, tick {SettleTicks + tick}: first trace {trace[tick]}; repeat {repeat[tick]}.");
        }

        var direction = descending ? -1 : 1;
        var nextSupport = descending ? UpperTread : Street;
        var seen = 0;
        var contactedRamp = false;
        var becameAirborne = false;
        var firstLowerRise = false;
        var firstUpperRise = false;
        for (var tick = 0; tick < trace.Length; tick++)
        {
            var sample = trace[tick];
            if (sample.IsSupported)
            {
                var bit = 1 << sample.SupportIndex;
                if ((seen & bit) == 0)
                {
                    if (sample.SupportIndex != nextSupport)
                        Require(false, context, tick, sample, $"next first support is index {nextSupport}");
                    seen |= bit;
                    nextSupport += direction;
                }

                contactedRamp |= sample.SupportIndex == Ramp && sample.Feet.Z is >= -2.95f and <= -1.55f &&
                    sample.SupportNormal.Y is >= .94f and <= .99f;
            }
            else if (descending && nextSupport >= Street)
            {
                becameAirborne = true;
            }

            if (descending || tick == 0) continue;
            Require(sample.Feet.Y - trace[tick - 1].Feet.Y <= .27f,
                context, tick, sample, "rise per tick <= 0.27 m");
            if (!firstLowerRise && sample.Feet.Y > .15f)
            {
                Require(sample.SupportIndex == LowerTread && sample.Feet.Z < 0,
                    context, tick, sample, "first rise above 0.15 m publishes LowerTread beyond Z=0");
                firstLowerRise = true;
            }
            if (!firstUpperRise && sample.Feet.Y > .90f)
            {
                Require(sample.SupportIndex == UpperTread && sample.Feet.Z < -4.50f,
                    context, tick, sample, "first rise above 0.90 m publishes UpperTread beyond Z=-4.50");
                firstUpperRise = true;
            }
        }

        var finishTick = trace.Length - 1;
        var finish = trace[finishTick];
        Require(nextSupport == (descending ? Street - 1 : UpperTread + 1),
            context, finishTick, finish, "all five supports published in route order");
        Require(contactedRamp, context, finishTick, finish, "real ramp support in the middle with normal Y in [0.94, 0.99]");
        if (descending)
        {
            Require(becameAirborne, context, finishTick, finish, "at least one unsupported tick before reaching Street");
            RequireFlatSupport(finish, Street, 0, context, finishTick);
            Require(finish.Feet.Z is >= .50f and <= 2f, context, finishTick, finish, "finishes on Street at Z in [0.50, 2.00]");
        }
        else
        {
            Require(firstLowerRise && firstUpperRise, context, finishTick, finish, "both riser height thresholds crossed");
            RequireFlatSupport(finish, UpperTread, 1, context, finishTick);
            Require(finish.Feet.Z is >= -6.5f and <= -4.80f, context, finishTick, finish, "finishes on UpperTread at Z in [-6.50, -4.80]");
        }
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60)]
    [InlineData(PhysicsQualityProfile.FastLinear60)]
    public void OpenEdge_WithStepEnabledStillFallsAfterCrossing(PhysicsQualityProfile profile)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        ReadOnlySpan<PhysicsBodyId> supports = [StaticBox(world, new(-4, -1, 0), new(4, 0, 4))];
        var walker = CreateWalker(world, new(0, 0, 1.5f), TravelerDiameter, TravelerHeight, out var characters);
        var context = $"{profile}, open edge";
        var sample = Settle(world, characters, walker, TravelerHeight, supports, 0, 0, context);

        characters.SetCharacterInput(walker, new(new(0, -WalkSpeed)));
        var firstCrossing = 0;
        var firstUnsupported = 0;
        for (var tick = 1; tick <= 70; tick++)
        {
            sample = Advance(world, characters, walker, TravelerHeight, supports, context, tick);
            if (firstCrossing == 0 && sample.Feet.Z < 0) firstCrossing = tick;
            if (firstUnsupported == 0 && !sample.IsSupported)
            {
                firstUnsupported = tick;
                Require(firstCrossing != 0, context, tick, sample, "support is not lost before the centre crosses Z=0");
            }
        }

        Require(firstCrossing != 0 && firstUnsupported >= firstCrossing, context, 70, sample,
            $"edge crossing then lost support (movement ticks: crossing={firstCrossing}, unsupported={firstUnsupported})");
        Require(sample.Feet.Z < -.60f && sample.Feet.Y < -.30f && sample.Velocity.Y < 0 && !sample.IsSupported,
            context, 70, sample, "falls unsupported beyond the edge: Z < -0.60, Y < -0.30, Vy < 0");
    }

    private static float BuildPassage(IPhysicsWorld world, Passage passage)
    {
        if (passage == Passage.UnderDeck)
        {
            StaticBox(world, new(-3, 1.80f, -1.50f), new(3, 2f, 0));
            return 1.50f;
        }

        // Metre fixtures, not a game collision shape. The culvert uses a low upright capsule, not an elongated body.
        var (width, height, depth) = passage switch
        {
            Passage.LivingDoor => (1f, 1.6f, .23f),
            Passage.WideDoor => (1.5f, 2.3f, .30f),
            Passage.Culvert => (.88f, .73f, 1.40f),
            _ => throw new ArgumentOutOfRangeException(nameof(passage))
        };
        var halfWidth = width / 2;
        StaticBox(world, new(-3, 0, -depth), new(-halfWidth, height + .30f, 0));
        StaticBox(world, new(halfWidth, 0, -depth), new(3, height + .30f, 0));
        StaticBox(world, new(-halfWidth, height, -depth), new(halfWidth, height + .30f, 0));
        return depth;
    }

    private static Sample[] RunMixedRoute(PhysicsQualityProfile profile, bool descending, string context)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = CreateWorld(factory, profile);
        ReadOnlySpan<PhysicsBodyId> supports =
        [
            StaticBox(world, new(-2, -1, 0), new(2, 0, 4)),
            StaticBox(world, new(-2, -1, -1.25f), new(2, .25f, 0)),
            world.CreateBody(new(world.CreateShape(new PhysicsStaticMesh(
                new Vector3[] { new(-2, .75f, -3.25f), new(2, .75f, -3.25f), new(-2, .25f, -1.25f), new(2, .25f, -1.25f) },
                new[] { 0, 1, 2, 1, 3, 2 })), PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 }),
            StaticBox(world, new(-2, -1, -4.50f), new(2, .75f, -3.25f)),
            StaticBox(world, new(-2, -1, -7), new(2, 1, -4.50f))
        ];
        var start = descending ? new Vector3(0, 1, -5.35f) : new Vector3(0, 0, 1.7f);
        var walker = CreateWalker(world, start, TravelerDiameter, TravelerHeight, out var characters);
        var trace = new Sample[166];
        trace[0] = Settle(world, characters, walker, TravelerHeight, supports, descending ? UpperTread : Street, start.Y, context);
        characters.SetCharacterInput(walker, new(new(0, descending ? WalkSpeed : -WalkSpeed)));
        for (var tick = 1; tick < trace.Length; tick++)
            trace[tick] = Advance(world, characters, walker, TravelerHeight, supports, context, tick);
        return trace;
    }

    private static PhysicsBodyId FlatFloor(IPhysicsWorld world) => StaticBox(world, new(-4, -1, -6), new(4, 0, 4));

    private static PhysicsBodyId StaticBox(IPhysicsWorld world, Vector3 min, Vector3 max)
        => Box(world, max - min, (min + max) * .5f);

    private static PhysicsCharacterId CreateWalker(IPhysicsWorld world, Vector3 feet, float diameter, float height,
        out IPhysicsCharacterWorld characters)
    {
        var shape = world.CreateShape(new PhysicsCapsule(diameter * .5f, height - diameter));
        var body = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic,
            new(feet + new Vector3(0, height * .5f, 0), Quaternion.Identity)) { Mass = 1 });
        characters = (IPhysicsCharacterWorld)world;
        return characters.CreateCharacter(body, new() { MaximumStepHeight = .25f, MinimumTreadDepth = .30f });
    }

    private static Sample Settle(IPhysicsWorld world, IPhysicsCharacterWorld characters, PhysicsCharacterId walker,
        float height, ReadOnlySpan<PhysicsBodyId> supports, int expectedSupport, float expectedY, string context)
    {
        Sample sample = default;
        for (var tick = 1 - SettleTicks; tick <= 0; tick++)
            sample = Advance(world, characters, walker, height, supports, context, tick);
        RequireFlatSupport(sample, expectedSupport, expectedY, context, 0);
        return sample;
    }

    private static Sample Advance(IPhysicsWorld world, IPhysicsCharacterWorld characters, PhysicsCharacterId walker,
        float height, ReadOnlySpan<PhysicsBodyId> supports, string context, int movementTick)
    {
        Step(world, 1);
        ProviderCharacterState state = characters.GetCharacterState(walker);
        var feet = state.Pose.Position - new Vector3(0, height * .5f, 0);
        var supportIndex = state.IsSupported ? supports.IndexOf(state.SupportBody) : -1;
        var sample = new Sample(feet, state.Pose.Orientation, state.LinearVelocity, state.IsSupported, supportIndex, state.SupportNormal);
        if (state.IsSupported ? supportIndex < 0 : state.SupportBody != default || state.SupportNormal != default)
            Require(false, context, movementTick, sample, $"valid published support (reported ID: {state.SupportBody})");
        var orientation = sample.Orientation;
        Require(IsFinite(feet) && IsFinite(sample.Velocity) && IsFinite(sample.SupportNormal) &&
                float.IsFinite(orientation.X) && float.IsFinite(orientation.Y) && float.IsFinite(orientation.Z) && float.IsFinite(orientation.W),
            context, movementTick, sample, "finite pose, velocity and support normal");
        return sample;
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void RequireFlatSupport(Sample sample, int supportIndex, float height, string context, int movementTick)
    {
        if (!(sample.IsSupported && sample.SupportIndex == supportIndex && sample.SupportNormal.Y > .99f &&
              sample.Feet.Y >= height - .02f && sample.Feet.Y <= height + .02f))
            Require(false, context, movementTick, sample, $"flat support index {supportIndex}, normal Y > 0.99, feet within 0.02 m of Y={height}");
    }

    private static void Require(bool condition, string context, int movementTick, Sample sample, string expectation)
    {
        if (!condition) Assert.Fail($"{context}, tick {SettleTicks + movementTick}: expected {expectation}; observed {sample}.");
    }

    private readonly record struct Sample(Vector3 Feet, Quaternion Orientation, Vector3 Velocity, bool IsSupported,
        int SupportIndex, Vector3 SupportNormal);
}
