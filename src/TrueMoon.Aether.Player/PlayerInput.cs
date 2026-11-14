using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Player;

/// <summary>Bounded latest input state, detached from controls/game/renderer. UI writes; the local fixed loop consumes actions/deltas.</summary>
public sealed class PlayerInput
{
    private readonly Lock _gate=new();
    private bool _captured,_interact,_eHeld;
    private GameButtons _buttons;
    private float _x,_y,_yaw,_pitch;
    /// <summary>Whether gameplay currently owns input.</summary>
    public bool IsCaptured{get{lock(_gate)return _captured;}}
    /// <summary>Begins a fresh capture without carrying keys/look from a previous focus.</summary><param name="x">Logical pointer X.</param><param name="y">Logical pointer Y.</param>
    public void Capture(float x,float y)
    {if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentOutOfRangeException(nameof(x));lock(_gate){Clear();_captured=true;_x=x;_y=y;}}
    /// <summary>Records a bounded pointer delta while captured.</summary><param name="x">Logical X.</param><param name="y">Logical Y.</param>
    public void Move(float x,float y)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y))throw new ArgumentOutOfRangeException(nameof(x));
        lock(_gate){if(!_captured)return;_yaw=Math.Clamp(_yaw+(x-_x)*.003f,-.5f,.5f);_pitch=Math.Clamp(_pitch+(y-_y)*.003f,-.5f,.5f);_x=x;_y=y;}
    }
    /// <summary>Maps WASD/E without native key handles; E repeat is coalesced until key-up.</summary><param name="key">Normalized key.</param><param name="down">Key down/up.</param><returns>Whether recognized.</returns>
    public bool Key(UiKey key,bool down)
    {
        lock(_gate)
        {
            if(!_captured)return false;
            var button=key switch{UiKey.W=>GameButtons.Forward,UiKey.S=>GameButtons.Backward,UiKey.A=>GameButtons.Left,UiKey.D=>GameButtons.Right,_=>GameButtons.None};
            if(button!=GameButtons.None){if(down)_buttons|=button;else _buttons&=~button;return true;}
            if(key!=UiKey.E)return false;
            if(down&&!_eHeld)_interact=true;_eHeld=down;return true;
        }
    }
    /// <summary>Returns held state and consumes exactly the accumulated action/look.</summary><returns>Validated bounded packet.</returns>
    public GameInputState Take()
    {lock(_gate){var result=new GameInputState(_captured,_buttons,_yaw,_pitch,_interact);_yaw=_pitch=0;_interact=false;return result;}}
    /// <summary>Focus loss/Escape/suspend/Stop clear all transient and held state.</summary>
    public void Release(){lock(_gate)Clear();}
    private void Clear(){_captured=_interact=_eHeld=false;_buttons=GameButtons.None;_yaw=_pitch=0;}
}
