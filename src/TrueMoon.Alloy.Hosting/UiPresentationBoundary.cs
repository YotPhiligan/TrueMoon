using TrueMoon.Alloy.Runtime;
using System.Diagnostics;

namespace TrueMoon.Alloy.Hosting;

internal static class UiPresentationBoundary
{
    internal static bool Run(UiSession session, Func<bool> output)
    {
        var diagnostics = session.Diagnostics;
        if (diagnostics == null) return output();
        var started = Stopwatch.GetTimestamp();
        var outcome = UiPresentationOutcome.Failed;
        try
        {
            var presented = output();
            outcome = presented ? UiPresentationOutcome.Presented : UiPresentationOutcome.Skipped;
            return presented;
        }
        finally { diagnostics.RecordPresentation(Stopwatch.GetElapsedTime(started), outcome); }
    }
}
