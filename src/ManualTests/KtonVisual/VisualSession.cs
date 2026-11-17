using System.Numerics;
using TrueMoon.Aether.Abstractions.Checkpoints;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Kton;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;
using TrueMoon.Kton.Contacts;

namespace TrueMoon.ManualTests.KtonVisual;

internal readonly record struct Pose(Vector3 Position, Quaternion Rotation)
{
    internal static Pose Read(LocalTransform value) => new(value.Position,value.Rotation);
    internal static Pose Between(Pose a,Pose b,float alpha) => new(Vector3.Lerp(a.Position,b.Position,alpha),Quaternion.Slerp(a.Rotation,b.Rotation,alpha));
    internal Matrix4x4 Matrix => Matrix4x4.CreateFromQuaternion(Rotation)*Matrix4x4.CreateTranslation(Position);
}

// Caller owns the factory product. The game owns its SceneWorld, scheduler and Kton world.
// Render interpolation reads copied poses and never writes back into simulation components.
internal sealed class VisualSession(IFactory<PrimitiveGameSession> games) : IDisposable
{
    private PrimitiveGameSession? _game;
    private readonly Pose[] _previous = new Pose[Level.Actors.Length], _current = new Pose[Level.Actors.Length];
    private ulong _jump, _teleport;
    internal ulong Tick => _game?.TickNumber ?? 0;
    internal bool Playing { get; private set; }
    internal bool Stopped => _game == null;
    internal World World => _game?.ActiveScene?.World ?? throw new InvalidOperationException("Start a session first.");
    internal EntityHandle Entity(int index) => _game!.ActiveScene!.Entities[Level.Id(index)];
    internal PhysicsCharacterState CharacterState => World.GetComponent<PhysicsCharacterState>(Entity(Level.Character));
    internal Vector3 Position(int index) => _current[index].Position;
    internal int LastContacts { get; private set; }
    internal int PriorTickContacts { get; private set; }
    internal ulong Generation { get; private set; }
    internal static readonly CameraTransform Camera = new(2,10,19,-.38f,0,0);

    internal void Reset()
    {
        Stop(); _game = games.Create() ?? throw new InvalidOperationException("Null game factory result.");
        try
        {
            _game.LoadSceneWorld(new(Level.Document())); _jump=0; _teleport=0; LastContacts=0; PriorTickContacts=0;
            ReadPoses(); Array.Copy(_current,_previous,_current.Length); Generation++;
        }
        catch { Stop(); throw; }
    }
    internal void SetPlaying(bool value)
    {
        if (_game == null) return;
        _game.SetPlaying(value); Playing=value;
        // Pausing snaps to the completed tick; resume starts from the same pose pair.
        Array.Copy(_current,_previous,_current.Length);
    }
    internal void Advance(Vector2 velocity,bool jump=false)
    {
        if (_game == null) return;
        if (jump) _jump=checked(_jump+1);
        World.SetComponent(Entity(Level.Character),new PhysicsCharacterInput(velocity,_jump));
        var platform = World.GetComponent<LocalTransform>(Entity(Level.Platform)).Position;
        var next = -2 + MathF.Sin((Tick+1)/60f)*1.5f;
        World.SetComponent(Entity(Level.Platform),new PhysicsVelocity(new(0,0,(next-platform.Z)*60)));
        var prior=0;
        World.Query<PhysicsContact>().ForEach((EntityHandle _,in PhysicsContact contact) =>
        { if(contact.Contact.Tick!=Tick)throw new InvalidDataException("Contact tick does not match the previous completed tick."); prior++; });
        PriorTickContacts=prior;
        Array.Copy(_current,_previous,_current.Length); var before=Tick; _game.Tick();
        if(Tick!=before+1)throw new InvalidDataException("Step did not advance exactly one tick.");
        ReadPoses(); LastContacts=0;
        World.Query<PhysicsContact>().ForEach((EntityHandle _,in PhysicsContact contact) =>
        { if(contact.Contact.Tick!=Tick)throw new InvalidDataException("Published contact tick mismatch."); LastContacts++; });
    }
    internal void Teleport(Vector3 position)
    {
        var entity=Entity(Level.Character); var command=new PhysicsTeleport(++_teleport,new(position,Quaternion.Identity),true);
        if(World.HasComponent<PhysicsTeleport>(entity)) World.SetComponent(entity,command); else World.AddComponent(entity,command);
        Advance(Vector2.Zero); Array.Copy(_current,_previous,_current.Length);
    }
    private void ReadPoses()
    {
        for(var i=0;i<_current.Length;i++)
        {
            var entity=Entity(i); var local=World.GetComponent<LocalTransform>(entity); var world=World.GetComponent<WorldTransform>(entity);
            if(world.Tick!=Tick||Vector3.Distance(world.Matrix.Translation,local.Position)>1e-4f)
                throw new InvalidDataException("Physical and extracted World poses differ.");
            _current[i]=Pose.Read(local);
        }
    }
    internal Pose Sample(int index,float alpha) => Pose.Between(_previous[index],_current[index],CheckedAlpha(alpha));
    private float CheckedAlpha(float alpha)
    {
        if(!float.IsFinite(alpha)||alpha<0||alpha>1)throw new ArgumentOutOfRangeException(nameof(alpha));
        return Playing?alpha:1;
    }
    internal PrimitiveScene Render(float alpha)
    {
        var primitives=new List<RenderPrimitive>();
        for(var i=0;i<Level.Actors.Length;i++)
        {
            var actor=Level.Actors[i]; var pose=Sample(i,alpha);
            if(!actor.Character) primitives.Add(new(Matrix4x4.CreateScale(actor.Size)*pose.Matrix,actor.Color));
            else
            {
                // Stepped debug proxy, not an exact capsule mesh. Its centre and total height
                // agree with the radius .5 / cylinder length 1 physics descriptor.
                var color=CharacterState.IsSupported?new Vector3(.25f,1,.45f):new Vector3(1,.25f,.12f);
                for(var slice=0;slice<16;slice++)
                {
                    var y=-1+(slice+.5f)/8; var cap=MathF.Max(0,MathF.Abs(y)-.5f);
                    var r=MathF.Sqrt(.25f-cap*cap);
                    primitives.Add(new(Matrix4x4.CreateScale(r*2,.125f,r*2)*Matrix4x4.CreateTranslation(0,y,0)*pose.Matrix,color));
                }
            }
        }
        return new(Camera,primitives){Far=80};
    }
    internal void Stop()
    {
        Playing=false;
        if(_game != null){_game.Dispose();_game=null;}
    }
    public void Dispose()=>Stop();
}
