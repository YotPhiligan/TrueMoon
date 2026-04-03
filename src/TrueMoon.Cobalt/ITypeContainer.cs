namespace TrueMoon.Cobalt;

public interface ITypeContainer
{
    string TypeId { get; }
    
    public Type Type { get; }
    public Type EnumerableType { get; }
    
    IResolver[] GetResolvers();
    IResolver GetResolver();
}