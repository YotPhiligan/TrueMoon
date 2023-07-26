namespace TrueMoon.Aluminum;

public interface IElement : IWithProperties
{
    IElement? Parent { get; set; }
}