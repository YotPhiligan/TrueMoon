namespace TrueMoon.Dependencies;

public interface ICustomDiContainerAccessor
{
    bool TryGetTypedContainer<T>(out T container);
}