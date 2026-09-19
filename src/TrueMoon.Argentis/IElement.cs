namespace TrueMoon.Argentis;

public interface IElement : IProperties
{
    IElement? Parent { get; }
}
