namespace TrueMoon.Argentis;

public abstract class Element : PropertiesBase, IElement
{
    public IElement? Parent { get; set; }
}