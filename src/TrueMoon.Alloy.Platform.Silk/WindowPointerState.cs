using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Platform.Silk;

// GLFW's live cursor query can already report the end of a buffered drag while
// dispatching its press. Preserve the ordered cursor events for button/wheel input.
internal sealed class WindowPointerState(float x, float y)
{
    internal void Move(float nextX, float nextY) { x = nextX; y = nextY; }

    internal UiInput Create(InputKind kind, int button = 0, float wheel = 0, bool shift = false, bool control = false)
        => new(kind, x, y, Button: button, WheelDelta: wheel, Shift: shift, Control: control);
}
