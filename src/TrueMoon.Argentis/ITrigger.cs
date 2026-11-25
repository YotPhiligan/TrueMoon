namespace TrueMoon.Argentis;

public interface ITrigger
{
    string Name { get; }
    void Subscribe(object o, Action action);
    void Unsubscribe(object o);
}