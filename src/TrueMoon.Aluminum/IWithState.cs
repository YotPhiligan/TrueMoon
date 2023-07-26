namespace TrueMoon.Aluminum;

public interface IWithState<TState>
{
    TState State { get; set; }
}