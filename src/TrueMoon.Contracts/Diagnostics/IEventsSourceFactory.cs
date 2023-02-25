namespace TrueMoon.Diagnostics;

public interface IEventsSourceFactory
{
    IEventsSource Create(string name);
    IEventsSource<T> Create<T>();
}