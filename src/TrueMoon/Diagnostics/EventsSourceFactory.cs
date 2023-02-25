namespace TrueMoon.Diagnostics;

public class EventsSourceFactory : IEventsSourceFactory
{
    public IEventsSource Create(string name)
    {
        var source = new EventsSource(name);
        
        return source;
    }
    
    public IEventsSource<T> Create<T>()
    {
        var source = new EventsSource<T>();
        
        return source;
    }
}