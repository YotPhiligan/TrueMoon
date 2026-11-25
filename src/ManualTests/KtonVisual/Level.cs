using System.Numerics;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Kton;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.ManualTests.KtonVisual;

internal sealed record Actor(string Name, Vector3 Position, Vector3 Size, Vector3 Color,
    PhysicsBodyMotion Motion = PhysicsBodyMotion.Static, float Roll = 0, bool Character = false);

internal static class Level
{
    internal const int Character = 2, Platform = 3;
    internal static readonly Actor[] Actors =
    [
        new("Floor",new(0,-.5f,0),new(36,1,18),new(.18f,.25f,.30f)),
        new("Slope",new(0,.7f,-2),new(8,.5f,4),new(.50f,.65f,.78f),Roll:.25f),
        new("Capsule proxy",new(-6,1.1f,-2),new(.5f,1,0),new(1,.25f,.12f),PhysicsBodyMotion.Dynamic,Character:true),
        new("Moving platform",new(7,1.25f,-2),new(4,.5f,3),new(.95f,.60f,.12f),PhysicsBodyMotion.Kinematic),
        new("Dynamic A",new(-3,3,3),Vector3.One,new(.30f,.80f,.75f),PhysicsBodyMotion.Dynamic),
        new("Dynamic B",new(0,5,4),new(1.5f,1.5f,1.5f),new(.65f,.30f,.85f),PhysicsBodyMotion.Dynamic),
        new("Low ceiling",new(-6,2.8f,5),new(5,.4f,2),new(.6f,.7f,.75f)),
        new("Stop wall",new(12,1.5f,-2),new(.5f,3,6),new(.5f,.6f,.65f))
    ];
    internal static EntityId Id(int index) => new(new Guid(index + 1, 0, 0, new byte[8]));
    internal static SceneDocument Document()
    {
        var instances = new List<EntityInstance>(); var definitions = new List<EntityDefinition>();
        for (var i = 0; i < Actors.Length; i++)
        {
            var actor = Actors[i]; var id = new EntityDefinitionId(Guid.NewGuid());
            var values = new List<ComponentData>
            {
                new(new(Guid.NewGuid()),BuiltInComponents.TransformId,1,
                    [N("x",actor.Position.X),N("y",actor.Position.Y),N("z",actor.Position.Z),N("roll",actor.Roll)]),
                new(new(Guid.NewGuid()),PhysicsComponentSchemas.BodyId,1,
                    [I("motion",(long)actor.Motion),I("shape",actor.Character ? 3 : 1),N("x",actor.Size.X),N("y",actor.Size.Y),
                     N("z",actor.Size.Z),N("mass",actor.Motion == PhysicsBodyMotion.Dynamic ? 1 : 0)]),
                new(new(Guid.NewGuid()),PhysicsComponentSchemas.VelocityId,1)
            };
            if (actor.Character) values.Add(new(new(Guid.NewGuid()),PhysicsComponentSchemas.CharacterId,1));
            definitions.Add(new(id,new(values))); instances.Add(new(Id(i),id));
        }
        return new(new(Guid.NewGuid()),1,instances,definitions,
            BuiltInComponents.Schemas.Concat([PhysicsComponentSchemas.Body,PhysicsComponentSchemas.Velocity,PhysicsComponentSchemas.Character]));
    }
    private static KeyValuePair<string,PropertyValue> N(string name,double value) => new(name,PropertyValue.From(value));
    private static KeyValuePair<string,PropertyValue> I(string name,long value) => new(name,PropertyValue.From(value));
}
