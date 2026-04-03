namespace TrueMoon.Aluminum;

public interface IElement : IProperties
{
    IElement? Parent { get; set; }
}