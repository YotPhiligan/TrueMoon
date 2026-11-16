using System.Numerics;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Kton;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.ManualTests.KtonPerformance;

internal sealed record Workload(string Kind, int Count)
{
    internal bool IsCharacter => Kind == "walking-characters";
    internal bool IsSleeping => Kind == "sleeping-bodies";
    internal PhysicsWorldOptions Options => new() { Gravity = IsCharacter ? new(0, -9.81f, 0) : Vector3.Zero };
    internal Vector3 Position(int index) => new((index % 32) * 3 - 48, IsCharacter ? 1 : 3, (index / 32) * 3 - 12);
    internal Vector3 Velocity => !IsCharacter && !IsSleeping ? Vector3.UnitX : Vector3.Zero;
    internal void Populate(MeasuredWorld world)
    {
        if (IsCharacter)
            world.CreateBody(new(world.CreateShape(new PhysicsBox(new(256, 1, 256))), PhysicsBodyMotion.Static,
                new(new(0, -.5f, 0), Quaternion.Identity)) { Mass = 0 });
        var shape = world.CreateShape(IsCharacter ? new PhysicsCapsule(.5f, 1) : new PhysicsBox(Vector3.One));
        for (var index = 0; index < Count; index++)
        {
            var body = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(Position(index), Quaternion.Identity)) { LinearVelocity = Velocity });
            if (IsCharacter)
            {
                var character = world.CreateCharacter(body, new());
                world.SetCharacterInput(character, new(new(2, 0)));
            }
        }
    }
    internal void Sleep(MeasuredWorld world)
    {
        if (IsSleeping) world.ApplyCommands(world.DynamicBodies.Select(id => new PhysicsBodyCommand(PhysicsCommandKind.Sleep, id)).ToArray());
    }
    internal object Validate(MeasuredWorld world)
    {
        Program.Check(world.DynamicBodies.Count == Count, "Workload body count changed.");
        var awake = 0; var supported = 0;
        foreach (var id in world.DynamicBodies)
        {
            var state = world.GetBodyState(id); PhysicsValidation.Validate(state.Pose).ThrowIfFailure();
            if (state.IsAwake) awake++;
            if (IsCharacter) Program.Check(state.Pose.Position.Y is > .97f and < 1.03f, "Character left the flat support.");
            else Program.Check(MathF.Abs(state.LinearVelocity.X - (IsSleeping ? 0 : 1)) < .001f, "Active/sleep workload changed its motion.");
        }
        foreach (var id in world.Characters) if (world.GetCharacterState(id).IsSupported) supported++;
        Program.Check(IsSleeping ? awake == 0 : awake == Count, "Active/sleeping partition is not the requested workload.");
        Program.Check(IsCharacter ? world.Characters.Count == Count && supported == Count : world.Characters.Count == 0, "Controller workload lost support/count.");
        return new { DynamicBodies = Count, Awake = awake, Sleeping = Count - awake, Controllers = world.Characters.Count, Supported = supported };
    }
    internal SceneDocument Document()
    {
        var entities = new List<EntityInstance>(); var definitions = new List<EntityDefinition>();
        if (IsCharacter) Add(new(0, -.5f, 0), true);
        for (var index = 0; index < Count; index++) Add(Position(index), false);
        return new(new(Guid.NewGuid()), 1, entities, definitions,
            BuiltInComponents.Schemas.Concat([PhysicsComponentSchemas.Body, PhysicsComponentSchemas.Velocity, PhysicsComponentSchemas.Character]));

        void Add(Vector3 position, bool floor)
        {
            var definition = new EntityDefinitionId(Guid.NewGuid());
            var components = new List<ComponentData>
            {
                new(new(Guid.NewGuid()), BuiltInComponents.TransformId, 1, [Number("x",position.X),Number("y",position.Y),Number("z",position.Z)]),
                new(new(Guid.NewGuid()), PhysicsComponentSchemas.BodyId, 1,
                    [Integer("motion",floor ? 0 : 1),Integer("shape",IsCharacter && !floor ? 3 : 1),
                     Number("x",floor ? 256 : IsCharacter ? .5 : 1),Number("y",1),Number("z",floor ? 256 : IsCharacter ? 0 : 1),Number("mass",floor ? 0 : 1)]),
                new(new(Guid.NewGuid()), PhysicsComponentSchemas.VelocityId, 1, [Number("x",floor ? 0 : Velocity.X)])
            };
            if (IsCharacter && !floor) components.Add(new(new(Guid.NewGuid()), PhysicsComponentSchemas.CharacterId, 1));
            definitions.Add(new(definition,new(components))); entities.Add(new(new(Guid.NewGuid()),definition));
        }
    }
    private static KeyValuePair<string, PropertyValue> Number(string key, double value) => new(key, PropertyValue.From(value));
    private static KeyValuePair<string, PropertyValue> Integer(string key, long value) => new(key, PropertyValue.From(value));
}
