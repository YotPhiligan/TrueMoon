namespace TrueMoon.Aluminum;

public interface ITrigger
{
    string Name { get; }
    void Subscribe(object o, Action action);
    void Unsubscribe(object o);
}

public interface ITriggerSubscription
{
    void Invoke();
}