using System.Diagnostics;

namespace TrueMoon.Aether.Diagnostics;

internal sealed class ChildDiagnosticRate(ChildDiagnosticDeliveryOptions options)
{
    private long _window = Stopwatch.GetTimestamp();
    private int _records, _bytes;
    internal bool Enter(int records, int bytes)
    {
        if (Stopwatch.GetElapsedTime(_window) >= TimeSpan.FromSeconds(1))
        { _window = Stopwatch.GetTimestamp(); _records = _bytes = 0; }
        bytes += ChildDiagnosticCodec.EnvelopeBytes;
        if (records > options.RecordsPerSecond - _records || bytes > options.BytesPerSecond - _bytes) return false;
        _records += records; _bytes += bytes; return true;
    }
}
