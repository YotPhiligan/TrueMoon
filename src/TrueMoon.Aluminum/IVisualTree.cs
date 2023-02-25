namespace TrueMoon.Aluminum;

public interface IVisualTree
{
    IElement Root { get; }
    void Build();

    event EventHandler Refresh;
    event EventHandler Rebuild;
}