namespace TrueMoon.Aluminum;

public abstract class Element : PropertiesBase, IElement
{
    public IElement? Parent { get; set; }
}