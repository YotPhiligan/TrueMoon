namespace TrueMoon.Aluminum;

public interface IBlueprint
{
    IElement Body { get; }
}

public interface IBlueprint<TState> : IBlueprint
{
    TState State { get; set; }
}