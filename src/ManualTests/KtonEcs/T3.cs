using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Kton;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bepu.Hosting;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Shapes;
using EcsInput = TrueMoon.Aether.Kton.PhysicsCharacterInput;
using EcsState = TrueMoon.Aether.Kton.PhysicsCharacterState;

namespace TrueMoon.ManualTests.KtonEcs;

internal static partial class Program
{
    private const float T3Speed = 2.4f;
    private const string T3ArchiveSha256 = "CB03D329829DC33FDCA344E1B2ED1607CC1CDCD8816DB66DC49FC6B402240AA1";
    private static readonly Guid RampAssetId = new("a92347ef-7384-4f4d-8f0f-6a33dcab9e01");
    private static readonly string[] RouteNames = ["Street", "Step1", "Step2", "Step3", "Step4", "Ramp", "Porch", "Room", "DoorPostLeft", "DoorPostRight", "DoorLintel"];
    private static readonly JsonSerializerOptions T3Json = new()
    {
        IncludeFields = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    private static async Task<int> RunT3(string fixturePath, string reportPath)
    {
        if (!Path.IsPathFullyQualified(reportPath) || Path.Exists(reportPath))
        {
            Console.Error.WriteLine("T3 requires an absolute path to a new report.");
            return 1;
        }

        await using var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var report = new T3Report();
        T3Row? row = null;
        try
        {
            Require(Path.IsPathFullyQualified(fixturePath), "T3 requires an absolute fixture path.");
            var bytes = await File.ReadAllBytesAsync(fixturePath);
            report.FixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes));
            var fixture = JsonSerializer.Deserialize<T3Fixture>(bytes, T3Json) ?? throw new InvalidDataException("Empty T3 fixture.");
            report.ArchiveSha256 = fixture.ArchiveSha256;
            ValidateFixture(fixture);
            report.ArchiveRead = true;
            var ramp = fixture.RouteColliders.Single(collider => collider.Name == "Ramp").Mesh!;

            foreach (var microsoft in new[] { false, true })
            foreach (var profile in new[] { PhysicsQualityProfile.Baseline60, PhysicsQualityProfile.FastLinear60 })
            {
                var provider = microsoft ? "Microsoft" : "Cobalt";
                await using var app = BuildApp(microsoft, profile, witnesses: null,
                    configure: options => options.AddStaticMesh(RampAssetId, ramp.Vertices, ramp.Indices));
                var host = app.Services.Resolve<BepuPhysicsHost>()!;
                await app.StartAsync();
                var games = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!;
                RequireNoT3World(host);
                foreach (var scenario in fixture.Scenarios)
                {
                    var colliders = scenario.LedgeColliders ?? fixture.RouteColliders;
                    T3Sample[] off = [];
                    foreach (var autoStep in new[] { false, true })
                    {
                        row = new(provider, profile, scenario.Id, autoStep);
                        report.Rows.Add(row);
                        var document = T3Document(fixture.Body, scenario, colliders, autoStep);
                        row.Trace = RunT3Ecs(games, host, fixture, scenario, colliders, document, autoStep);
                        var repeat = RunT3Ecs(games, host, fixture, scenario, colliders, document, autoStep);
                        CompareT3(row, row.Trace, repeat, "Fresh ECS repeat");
                        row.RepeatExact = true;
                        var control = RunT3Control(fixture, scenario, colliders, profile, autoStep);
                        CompareT3(row, row.Trace, control, "Direct provider control");
                        row.ProviderExact = true;
                        row.Landmarks = CheckT3Outcome(fixture, scenario, autoStep, row.Trace);
                        if (!autoStep) off = row.Trace;
                        else if (scenario.Id is T3Case.StairsUp or T3Case.Ledge25)
                            Require(FirstDifference(off, row.Trace) is not null, "Opt-in did not change the trace.");
                        else
                        {
                            CompareT3(row, row.Trace, off, "Opt-out ECS trace");
                            report.OffOnExactCount++;
                        }
                        row.Passed = true;
                        row = null;
                    }
                }
                await app.StopAsync();
                var resources = host.Resources;
                Require(host.IsReleased && resources.MaximumPoolBytesAfterCleanup == 0, $"{provider}/{profile}: host or pools not released.");
                RequireNoT3World(host);
                report.Owners.Add(new(provider, profile, resources));
            }
            report.Assemblies = ReadLoadedAssemblies();
            Require(report.Rows.Count == 88 && report.Owners.Count == 4 && report.OffOnExactCount == 36, "Incomplete T3 matrix.");
            report.Completed = true;
        }
        catch (Exception error)
        {
            report.Failure = error is T3ComparisonException comparison ? comparison.Failure
                : new(row?.Provider, row?.Profile, row?.Case, row?.AutoStep, null, null, null, null, error.Message);
            Console.Error.WriteLine($"T3 {row?.Provider}/{row?.Profile}/{row?.Case}/auto-step={row?.AutoStep}: {error}");
        }
        await JsonSerializer.SerializeAsync(output, report, T3Json);
        return report.Completed ? 0 : 1;
    }

    private static T3Sample[] RunT3Ecs(IFactory<PrimitiveGameSession> games, BepuPhysicsHost host, T3Fixture fixture,
        T3Scenario scenario, T3Collider[] colliders, SceneDocument document, bool autoStep)
    {
        T3Sample[] trace;
        using (var game = games.Create() ?? throw new InvalidOperationException("Null game factory result."))
        {
            game.LoadSceneWorld(new(document));
            var scene = game.ActiveScene!;
            var world = scene.World;
            Require(game.TickNumber == 0 && host.Resources.PublishedWorlds == 1 && host.Resources.ActiveResourceOwners == 1,
                "Expected one newly published ECS physics world at tick zero.");
            Require(scene.Scene.HierarchyOrder.SequenceEqual(document.Entities.Select(entity => entity.Id)), "Authored root order changed on import.");
            var names = new Dictionary<EntityHandle, string>(colliders.Length);
            var previousIndex = -1;
            for (var i = 0; i < colliders.Length; i++)
            {
                var handle = scene.Entities[document.Entities[i].Id];
                var collider = colliders[i];
                var expected = collider.Box is { } box
                    ? new PhysicsBody(PhysicsBodyMotion.Static, PhysicsColliderShape.Box, box.Max - box.Min, 0)
                    : new PhysicsBody(PhysicsBodyMotion.Static, PhysicsColliderShape.StaticMesh, Vector3.Zero, 0) { MeshAssetId = RampAssetId };
                Require(handle.Index > previousIndex && world.GetComponent<PhysicsBody>(handle) == expected, $"Imported collider/order differs: {collider.Name}.");
                previousIndex = handle.Index;
                names.Add(handle, collider.Name);
            }

            var character = scene.Entities[document.Entities[^1].Id];
            var body = fixture.Body;
            Require(character.Index > previousIndex && world.GetComponent<PhysicsBody>(character) ==
                new PhysicsBody(PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Capsule, new(body.Radius, body.CylinderLength, 0), body.Mass),
                "Imported capsule/order differs.");
            Require(world.GetComponent<PhysicsCharacter>(character).CaptureOptions() == T3CharacterOptions(autoStep), "Imported controller settings differ.");

            for (var i = 0; i < fixture.WarmupTicks; i++) game.Tick();
            trace = new T3Sample[scenario.TickCount + 1];
            trace[0] = Read(0);
            var tick = 0;
            foreach (var segment in scenario.Input)
            {
                var input = new EcsInput(T3Velocity(segment.Direction));
                for (var i = 0; i < segment.Ticks; i++)
                {
                    world.SetComponent(character, input);
                    game.Tick();
                    trace[++tick] = Read(tick);
                }
            }

            T3Sample Read(int index)
            {
                if (game.TickNumber != (ulong)(fixture.WarmupTicks + index))
                    throw new InvalidDataException($"Incorrect ECS tick for sample {index}: {game.TickNumber}.");
                var feet = world.GetComponent<LocalTransform>(character).Position - body.CenterOffset;
                var velocity = world.GetComponent<PhysicsVelocity>(character).Linear;
                var support = world.GetComponent<EcsState>(character);
                return CaptureT3Sample(index, feet, velocity, support.IsSupported, support.SupportNormal, support.SupportEntity, names);
            }
        }
        RequireNoT3World(host);
        return trace;
    }

    private static T3Sample[] RunT3Control(T3Fixture fixture, T3Scenario scenario, T3Collider[] colliders,
        PhysicsQualityProfile profile, bool autoStep)
    {
        using var factory = new BepuPhysicsWorldFactory();
        using var world = factory.Create(new PhysicsWorldOptions { Quality = profile });
        var names = new Dictionary<PhysicsBodyId, string>(colliders.Length);
        foreach (var collider in colliders)
        {
            PhysicsShapeDefinition shape;
            Vector3 position;
            if (collider.Box is { } box)
            {
                shape = new PhysicsBox(box.Max - box.Min);
                position = (box.Min + box.Max) / 2;
            }
            else
            {
                var mesh = collider.Mesh!;
                shape = new PhysicsStaticMesh(mesh.Vertices, mesh.Indices);
                position = Vector3.Zero;
            }
            var id = world.CreateBody(new(world.CreateShape(shape), PhysicsBodyMotion.Static, new(position, Quaternion.Identity)) { Mass = 0 });
            names.Add(id, collider.Name);
        }
        var body = fixture.Body;
        var capsule = world.CreateShape(new PhysicsCapsule(body.Radius, body.CylinderLength));
        var capsuleBody = world.CreateBody(new(capsule, PhysicsBodyMotion.Dynamic,
            new(scenario.RequestedStart + body.CenterOffset, Quaternion.Identity)) { Mass = body.Mass });
        var characters = (IPhysicsCharacterWorld)world;
        var character = characters.CreateCharacter(capsuleBody, T3CharacterOptions(autoStep));
        characters.SetCharacterInput(character, new(Vector2.Zero));
        for (var i = 0; i < fixture.WarmupTicks; i++) world.Step(world.LastCompletedTick + 1);
        var trace = new T3Sample[scenario.TickCount + 1];
        trace[0] = Read(0);
        var tick = 0;
        foreach (var segment in scenario.Input)
        {
            var input = new TrueMoon.Kton.Characters.PhysicsCharacterInput(T3Velocity(segment.Direction));
            for (var i = 0; i < segment.Ticks; i++)
            {
                characters.SetCharacterInput(character, input);
                world.Step(world.LastCompletedTick + 1);
                trace[++tick] = Read(tick);
            }
        }
        return trace;

        T3Sample Read(int index)
        {
            var state = characters.GetCharacterState(character);
            return CaptureT3Sample(index, state.Pose.Position - body.CenterOffset, state.LinearVelocity,
                state.IsSupported, state.SupportNormal, state.SupportBody, names);
        }
    }

    private static SceneDocument T3Document(T3Body body, T3Scenario scenario, T3Collider[] colliders, bool autoStep)
    {
        var entities = new EntityInstance[colliders.Length + 1];
        var definitions = new EntityDefinition[entities.Length];
        for (var i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            if (collider.Box is { } box)
                Add(i, collider.Name, (box.Min + box.Max) / 2, box.Max - box.Min, PhysicsBodyMotion.Static, PhysicsColliderShape.Box, 0);
            else
                Add(i, collider.Name, Vector3.Zero, Vector3.Zero, PhysicsBodyMotion.Static, PhysicsColliderShape.StaticMesh, 0);
        }
        Add(colliders.Length, "Capsule", scenario.RequestedStart + body.CenterOffset, new(body.Radius, body.CylinderLength, 0),
            PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Capsule, body.Mass);
        var schemas = BuiltInComponents.Schemas.Concat(scenario.LedgeColliders is null
            ? [PhysicsComponentSchemas.Body, PhysicsComponentSchemas.BodyV2, PhysicsComponentSchemas.CharacterV2]
            : [PhysicsComponentSchemas.Body, PhysicsComponentSchemas.CharacterV2]);
        return new(new(Id(1, (int)scenario.Id + 1)), 1, entities, definitions, schemas);

        void Add(int order, string name, Vector3 position, Vector3 size, PhysicsBodyMotion motion, PhysicsColliderShape shape, float mass)
        {
            // Descending GUID tails ensure that explicit sibling order, not GUID sorting, drives attachment order.
            var tail = entities.Length - order;
            var definition = new EntityDefinitionId(Id(3, tail));
            var mesh = shape == PhysicsColliderShape.StaticMesh;
            List<KeyValuePair<string, PropertyValue>> descriptor =
                [Integer("motion", (long)motion), Integer("shape", (long)shape), Number("x", size.X), Number("y", size.Y), Number("z", size.Z), Number("mass", mass)];
            if (mesh) descriptor.Add(new("meshAsset", PropertyValue.From(RampAssetId.ToString("D"))));
            List<ComponentData> components =
            [
                new(new(Id(4, tail)), BuiltInComponents.TransformId, 1, [Number("x", position.X), Number("y", position.Y), Number("z", position.Z)]),
                new(new(Id(5, tail)), BuiltInComponents.AppearanceId, 1),
                new(new(Id(6, tail)), PhysicsComponentSchemas.BodyId, mesh ? 2 : 1, descriptor)
            ];
            if (shape == PhysicsColliderShape.Capsule)
                components.Add(new(new(Id(7, tail)), PhysicsComponentSchemas.CharacterId, 2,
                    [Number("speed", T3Speed), Number("maximumStepHeight", autoStep ? .25 : 0), Number("minimumTreadDepth", .30)]));
            entities[order] = new(new(Id(2, tail)), definition, null, null, name, order);
            definitions[order] = new(definition, new(components));
        }

        static Guid Id(int domain, int tail) => new(0x3723ec00 + domain, 0x6473, 0x4cdb, 0xb7, 0xda, 0, 0, 0, 0, 0, (byte)tail);
    }

    private static T3Landmarks CheckT3Outcome(T3Fixture fixture, T3Scenario scenario, bool autoStep, T3Sample[] trace)
    {
        var start = trace[0];
        var finish = trace[^1];
        var unsupported = trace.Where(sample => !sample.IsSupported).ToArray();
        var firstUnsupported = unsupported.Length == 0 ? (int?)null : unsupported[0].Tick;
        var requested = scenario.RequestedStart;
        Require(start.IsSupported && Vector2.Distance(new(start.Feet.X, start.Feet.Z), new(requested.X, requested.Z)) <= .01f &&
            MathF.Abs(start.Feet.Y - requested.Y) <= .05f, $"Settled start differs: {start} vs {requested}.");

        var ramp = First(trace, sample => sample.IsSupported && sample.Support == "Ramp" && sample.SupportNormal.Y is > .85f and < .95f && sample.SupportNormal.Z > .4f);
        var room = First(trace, sample => sample.Support == "Room");
        var steps = new List<T3Sample>(4);
        T3Sample? ledge = null;
        T3DoorCrossing? door = null;
        T3Sample? edge = null;
        var doorZ = fixture.Door.DoorPostLeft.Y;
        for (var i = 1; i < trace.Length; i++)
        {
            var a = trace[i - 1].Feet;
            var b = trace[i].Feet;
            if (a.Z < doorZ || b.Z >= doorZ) continue;
            var x = a.X + (b.X - a.X) * ((doorZ - a.Z) / (b.Z - a.Z));
            door = new(trace[i].Tick, x);
            break;
        }

        switch (scenario.Id)
        {
            case T3Case.Flat:
                FinishAt("Street", 0); Continuous();
                Require(MathF.Abs((start.Feet.Z - finish.Feet.Z) - 1.20f) <= .10f, "Unexpected flat northward travel.");
                break;
            case T3Case.StairsUp when !autoStep:
                Blocked("Street", 1);
                break;
            case T3Case.StairsUp:
                for (var i = 1; i <= 4; i++)
                {
                    var threshold = (i - 1) * .25f + .15f;
                    var sample = First(trace, sample => sample.Feet.Y > threshold)
                        ?? throw new InvalidDataException($"No rise at step {i}.");
                    Require(sample.IsSupported && sample.Support == $"Step{i}" && sample.SupportNormal.Y > .99f &&
                        MathF.Abs(sample.Feet.Y - i * .25f) <= .025f && (steps.Count == 0 || sample.Tick > steps[^1].Tick),
                        $"First rise did not publish Step{i} pose and support together: {sample}.");
                    steps.Add(sample);
                }
                FinishAt("Room", 1); Continuous(); PassDoor();
                Require(room is { } firstRoom && firstRoom.Tick > steps[^1].Tick, "Room support must follow the fourth step.");
                break;
            case T3Case.StairsDown:
                FinishAt("Street", 0);
                Require(finish.Feet.Z > 1, "Descent did not reach the street.");
                break;
            case T3Case.RampUp:
                FinishAt("Porch", 1); TraverseRamp();
                break;
            case T3Case.RampDown:
                FinishAt("Street", 0); TraverseRamp();
                break;
            case T3Case.RampToRoom:
                FinishAt("Room", 1); TraverseRamp(); PassDoor();
                Require(MathF.Abs(finish.Feet.X) <= .20f, "Ramp-to-room finish is off centre.");
                break;
            case T3Case.DoorCenter:
                FinishAt("Room", 1); Continuous(); PassDoor();
                break;
            case T3Case.DoorPost:
                FinishAt("Porch", 1); Continuous();
                Require(door is null && finish.Feet.Z < start.Feet.Z && finish.Feet.Z >= doorZ + fixture.Door.DoorPostWidth / 2,
                    "Door-post case did not approach and stop at the post.");
                break;
            case T3Case.Ledge25 when autoStep:
                ledge = First(trace, sample => sample.Feet.Y > .15f);
                Require(ledge is { } lift && lift.Feet.Z < 29 && MathF.Abs(lift.Feet.Y - .25f) <= .025f &&
                    lift.IsSupported && lift.Support == "LedgeNorth" && lift.SupportNormal.Y > .99f,
                    $"First ledge rise did not publish new support: {ledge}.");
                FinishAt("LedgeNorth", .25f);
                break;
            case T3Case.Ledge25:
            case T3Case.Ledge30:
                Blocked("LedgeSouth", 29);
                break;
            case T3Case.BackEdge:
                edge = First(trace, sample => sample.Feet.Z < -5.25f);
                Require(edge is { } crossed && firstUnsupported > crossed.Tick && !finish.IsSupported && finish.Feet.Y < 1,
                    $"Expected edge crossing followed by loss of support: {edge}, unsupported={firstUnsupported}, finish={finish}.");
                break;
            default:
                throw new InvalidDataException($"Unknown T3 case {scenario.Id}.");
        }
        return new(start, finish, firstUnsupported, unsupported.Length, ramp, room, steps.ToArray(), ledge, door, edge);

        void FinishAt(string support, float height) => Require(finish.IsSupported && finish.Support == support && MathF.Abs(finish.Feet.Y - height) <= .05f,
            $"Expected finish on {support} at {height} m, got {finish}.");
        void Continuous() => Require(unsupported.Length == 0, $"Unexpected loss of support at tick {firstUnsupported}.");
        void TraverseRamp()
        {
            Continuous();
            Require(ramp is not null, "No support from the triangular ramp with its inclined normal.");
        }
        void PassDoor()
        {
            var margin = fixture.Door.DoorPostWidth / 2 + fixture.Body.Radius;
            Require(door is { } crossing && crossing.X >= fixture.Door.DoorPostLeft.X + margin && crossing.X <= fixture.Door.DoorPostRight.X - margin,
                $"Capsule did not clear the doorway: {door}.");
        }
        void Blocked(string support, float boundary)
        {
            FinishAt(support, 0);
            Require(trace.All(sample => sample.Feet.Z >= boundary && MathF.Abs(sample.Feet.Y) <= .02f) &&
                finish.Feet.Z >= boundary + .24f && finish.Feet.Z <= boundary + .36f && finish.Feet.Z < start.Feet.Z && MathF.Abs(finish.Velocity.Z) <= .10f,
                $"Expected motion up to a blocked riser, got {finish}.");
        }
    }

    private static T3Sample? First(T3Sample[] trace, Predicate<T3Sample> predicate)
    {
        foreach (var sample in trace) if (predicate(sample)) return sample;
        return null;
    }

    private static void CompareT3(T3Row row, T3Sample[] actual, T3Sample[] expected, string comparison)
    {
        if (FirstDifference(actual, expected) is { } difference)
            throw new T3ComparisonException(new(row.Provider, row.Profile, row.Case, row.AutoStep, difference.Tick,
                difference.Field, difference.Actual, difference.Expected, comparison));
    }

    private static (int? Tick, string Field, object? Actual, object? Expected)? FirstDifference(T3Sample[] actual, T3Sample[] expected)
    {
        if (actual.Length != expected.Length) return (null, "Trace.Length", actual.Length, expected.Length);
        for (var tick = 0; tick < actual.Length; tick++)
        {
            var a = actual[tick];
            var b = expected[tick];
            if (a.Tick != b.Tick) return (tick, nameof(a.Tick), a.Tick, b.Tick);
            if (a.Feet != b.Feet) return Difference(tick, nameof(a.Feet), a.Feet, b.Feet);
            if (a.Velocity != b.Velocity) return Difference(tick, nameof(a.Velocity), a.Velocity, b.Velocity);
            if (a.IsSupported != b.IsSupported) return (tick, nameof(a.IsSupported), a.IsSupported, b.IsSupported);
            if (a.SupportNormal != b.SupportNormal) return Difference(tick, nameof(a.SupportNormal), a.SupportNormal, b.SupportNormal);
            if (a.Support != b.Support) return (tick, nameof(a.Support), a.Support, b.Support);
        }
        return null;

        static (int, string, object, object) Difference(int tick, string field, Vector3 a, Vector3 b) => a.X != b.X ? (tick, field + ".X", a.X, b.X)
            : a.Y != b.Y ? (tick, field + ".Y", a.Y, b.Y) : (tick, field + ".Z", a.Z, b.Z);
    }

    private static T3Sample CaptureT3Sample<T>(int tick, Vector3 feet, Vector3 velocity, bool supported, Vector3 normal,
        T handle, Dictionary<T, string> names) where T : struct, IEquatable<T>
    {
        if (!Finite(feet) || !Finite(velocity) || !Finite(normal)) throw new InvalidDataException($"Nonfinite physics sample at tick {tick}.");
        string? name = null;
        if (supported)
        {
            if (!names.TryGetValue(handle, out name)) throw new InvalidDataException($"Unmapped support at tick {tick}: {handle}.");
        }
        else if (!handle.Equals(default) || normal != default) throw new InvalidDataException($"Airborne sample has support at tick {tick}.");
        return new(tick, feet, velocity, supported, normal, name);
    }

    private static PhysicsCharacterOptions T3CharacterOptions(bool autoStep) => new()
    { MaximumSpeed = T3Speed, MaximumStepHeight = autoStep ? .25f : 0, MinimumTreadDepth = .30f };

    private static Vector2 T3Velocity(T3Direction direction) => direction switch
    {
        T3Direction.N => new(0, -T3Speed), T3Direction.S => new(0, T3Speed),
        T3Direction.E => new(T3Speed, 0), T3Direction.W => new(-T3Speed, 0),
        _ => throw new InvalidDataException($"Invalid T3 direction: {direction}.")
    };

    private static void ValidateFixture(T3Fixture fixture)
    {
        Require(fixture.LayoutVersion == 1 && fixture.ClockHz == 60 && fixture.WarmupTicks == 60 && fixture.ArchiveSha256 == T3ArchiveSha256,
            "Unexpected T3 fixture metadata.");
        Require(fixture.Body is { Radius: .30f, CylinderLength: .60f, Mass: 1 } body && body.CenterOffset == new Vector3(0, .60f, 0),
            "Unexpected T3 capsule.");
        Require(fixture.Door is { DoorPostWidth: .23f, DoorUnderside: 2.6f } door &&
            door.DoorPostLeft == new Vector2(-.615f, -2.28f) && door.DoorPostRight == new Vector2(.615f, -2.28f), "Unexpected T3 doorway.");
        Require(fixture.RouteColliders.Select(collider => collider.Name).SequenceEqual(RouteNames), "Unexpected T3 collider order.");
        foreach (var collider in fixture.RouteColliders)
        {
            if (collider.Name == "Ramp")
            {
                Require(collider.Box is null && collider.Mesh is { Vertices.Length: 4, Indices.Length: 6 }, "Ramp must be a two-triangle mesh.");
                continue;
            }
            CheckBox(collider);
        }
        Require(fixture.Scenarios.Select(scenario => scenario.Id).SequenceEqual(Enum.GetValues<T3Case>()), "Unexpected T3 scenario order.");
        foreach (var scenario in fixture.Scenarios)
        {
            Require(Finite(scenario.RequestedStart) && scenario.Input.Length is > 0 and <= 4 &&
                scenario.Input.All(segment => segment.Ticks is > 0 and <= 160), $"Invalid start/input for {scenario.Id}.");
            if (scenario.Id is T3Case.Ledge25 or T3Case.Ledge30)
            {
                Require(scenario.LedgeRise == (scenario.Id == T3Case.Ledge25 ? .25f : .30f) && scenario.LedgeColliders is { Length: 2 } &&
                    scenario.LedgeColliders[0].Name == "LedgeSouth" && scenario.LedgeColliders[1].Name == "LedgeNorth", $"Invalid geometry for {scenario.Id}.");
                foreach (var collider in scenario.LedgeColliders!) CheckBox(collider);
            }
            else Require(scenario.LedgeRise is null && scenario.LedgeColliders is null, $"Unexpected ledge for {scenario.Id}.");
        }

        static void CheckBox(T3Collider collider) => Require(collider.Mesh is null && collider.Box is { } box && Finite(box.Min) && Finite(box.Max) &&
            box.Max.X > box.Min.X && box.Max.Y > box.Min.Y && box.Max.Z > box.Min.Z, $"Invalid box for {collider.Name}.");
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static void RequireNoT3World(BepuPhysicsHost host)
    {
        var resources = host.Resources;
        if (resources.PublishedWorlds != 0 || resources.ActiveResourceOwners != 0 || resources.PendingFailedCreationCleanup)
            throw new InvalidDataException($"ECS world cleanup incomplete: {resources}.");
    }

    private enum T3Case { Flat, StairsUp, StairsDown, RampUp, RampDown, RampToRoom, DoorCenter, DoorPost, BackEdge, Ledge25, Ledge30 }
    private enum T3Direction { N, S, E, W }
    private sealed record T3Fixture(int LayoutVersion, int ClockHz, int WarmupTicks, string ArchiveSha256, T3Body Body,
        T3Door Door, T3Collider[] RouteColliders, T3Scenario[] Scenarios);
    private sealed record T3Body(float Radius, float CylinderLength, Vector3 CenterOffset, float Mass);
    private sealed record T3Door(Vector2 DoorPostLeft, Vector2 DoorPostRight, float DoorPostWidth, float DoorUnderside);
    private sealed record T3Collider(string Name, T3Bounds? Box, T3Triangles? Mesh);
    private sealed record T3Bounds(Vector3 Min, Vector3 Max);
    private sealed record T3Triangles(Vector3[] Vertices, int[] Indices);
    private sealed record T3Scenario(T3Case Id, Vector3 RequestedStart, T3Segment[] Input, float? LedgeRise, T3Collider[]? LedgeColliders)
    { internal int TickCount => Input.Sum(segment => segment.Ticks); }
    private readonly record struct T3Segment(T3Direction Direction, int Ticks);
    private readonly record struct T3Sample(int Tick, Vector3 Feet, Vector3 Velocity, bool IsSupported, Vector3 SupportNormal, string? Support);
    private sealed record T3DoorCrossing(int Tick, float X);
    private sealed record T3Landmarks(T3Sample Start, T3Sample Finish, int? FirstUnsupportedTick, int UnsupportedTicks,
        T3Sample? FirstRampSupport, T3Sample? FirstRoomSupport, T3Sample[] Steps, T3Sample? FirstLedgeRise, T3DoorCrossing? DoorCrossing, T3Sample? EdgeCrossing);
    private sealed record T3Owner(string Provider, PhysicsQualityProfile Profile, BepuResourceSnapshot Resources);
    private sealed record T3Failure(string? Provider, PhysicsQualityProfile? Profile, T3Case? Case, bool? AutoStep,
        int? Tick, string? Field, object? Actual, object? Expected, string Reason);
    private sealed class T3ComparisonException(T3Failure failure)
        : Exception($"{failure.Reason}: tick {failure.Tick}, {failure.Field}: actual={failure.Actual}, expected={failure.Expected}.")
    { internal T3Failure Failure { get; } = failure; }
    private sealed record T3Row(string Provider, PhysicsQualityProfile Profile, T3Case Case, bool AutoStep)
    {
        public bool Passed { get; set; }
        public bool RepeatExact { get; set; }
        public bool ProviderExact { get; set; }
        public T3Sample[] Trace { get; set; } = [];
        public T3Landmarks? Landmarks { get; set; }
    }
    private sealed class T3Report
    {
        public string Stage => "KTON-ECS-T3";
        public bool NativeRendering => false;
        public bool Completed { get; set; }
        public string? FixtureSha256 { get; set; }
        public string? ArchiveSha256 { get; set; }
        public bool ArchiveRead { get; set; }
        public List<T3Row> Rows { get; } = new(88);
        public List<T3Owner> Owners { get; } = new(4);
        public int OffOnExactCount { get; set; }
        public LoadedAssembly[] Assemblies { get; set; } = [];
        public T3Failure? Failure { get; set; }
    }
}
