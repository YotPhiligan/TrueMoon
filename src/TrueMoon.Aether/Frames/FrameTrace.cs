using System.Diagnostics;
using System.Diagnostics.Tracing;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Frames;

// Opt-in timings only. No pixels/native handles or history are retained by the runtime.
[EventSource(Name = "TrueMoon-Aether-Frames")]
internal sealed class FrameTrace : EventSource
{
    internal static readonly FrameTrace Log = new();

    [NonEvent]
    internal long Begin() => IsEnabled() ? Stopwatch.GetTimestamp() : 0;

    [NonEvent]
    internal void Complete(string phase, SessionIdentity identity, ulong surface, ulong frame, long started)
    {
        if (started == 0 || !IsEnabled()) return;
        Frame(phase, identity.SessionId.ToString("N"), unchecked((long)identity.Generation), unchecked((long)surface),
            unchecked((long)frame), started, Stopwatch.GetTimestamp());
    }

    [Event(1, Level = EventLevel.Informational)]
    public void Frame(string phase, string session, long runtime, long surface, long frame, long started, long ended)
        => WriteEvent(1, phase, session, runtime, surface, frame, started, ended);
}
