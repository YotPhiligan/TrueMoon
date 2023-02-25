using System.Diagnostics;

namespace TrueMoon.Diagnostics;

public readonly struct EventsSourceActivityHandler : IDisposable
{
    private readonly IEventsSource _eventsSource;
    private readonly Activity _activity;

    public EventsSourceActivityHandler(IEventsSource eventsSource, Activity activity)
    {
        _eventsSource = eventsSource;
        _activity = activity;
    }

    public void Close()
    {
        _eventsSource.StopActivity(_activity);
    }
    
    public void Dispose()
    {
        Close();
    }
}