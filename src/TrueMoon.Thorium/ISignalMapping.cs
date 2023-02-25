namespace TrueMoon.Thorium;

public interface ISignalMapping
{
    string AssemblyName { get; }

    IReadOnlyList<SignalMappingItem> GetValues();
}