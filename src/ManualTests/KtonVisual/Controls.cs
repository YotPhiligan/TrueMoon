using System.Numerics;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.KtonVisual;

internal enum VisualCommand { Toggle, Step, Reset, Stop }
internal sealed class Controls
{
    private readonly Lock _gate=new();
    private readonly Queue<VisualCommand> _commands=[];
    private readonly HashSet<UiKey> _held=[];
    private bool _jump;
    private long _epoch;
    internal bool Key(UiKey key,bool down)
    {
        lock(_gate)
        {
            if(!down){_held.Remove(key);return true;}
            if(!_held.Add(key))return true;
            switch(key)
            {
                case UiKey.Enter:_commands.Enqueue(VisualCommand.Toggle);break;
                case UiKey.F:_commands.Enqueue(VisualCommand.Step);break;
                case UiKey.Home:_commands.Enqueue(VisualCommand.Reset);break;
                case UiKey.End:_commands.Enqueue(VisualCommand.Stop);break;
                case UiKey.Space:_jump=true;break;
                case UiKey.Escape:_held.Clear();_jump=false;break;
            }
            return true;
        }
    }
    internal (Vector2 Move,bool Jump,VisualCommand[] Commands,long Epoch) Take()
    {
        lock(_gate)
        {
            var move=new Vector2((_held.Contains(UiKey.D)?1:0)-(_held.Contains(UiKey.A)?1:0),(_held.Contains(UiKey.S)?1:0)-(_held.Contains(UiKey.W)?1:0));
            if(move.LengthSquared()>1)move=Vector2.Normalize(move);
            var commands=_commands.ToArray();_commands.Clear();var jump=_jump;_jump=false;
            return(move*2.5f,jump,commands,_epoch);
        }
    }
    internal void Release(){lock(_gate){_held.Clear();_jump=false;_commands.Clear();_epoch++;}}
    internal void ResetMotion()
    {
        lock(_gate)
        {
            // Keep command/Space key edges until real key-up, even across mode changes.
            _held.Remove(UiKey.W);_held.Remove(UiKey.A);_held.Remove(UiKey.S);_held.Remove(UiKey.D);
            _jump=false;_commands.Clear();_epoch++;
        }
    }
}
