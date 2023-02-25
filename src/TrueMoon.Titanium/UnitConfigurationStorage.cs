namespace TrueMoon.Titanium;

public class UnitConfigurationStorage
{
    public UnitConfigurationStorage(List<IUnitConfiguration> unitConfigurations)
    {
        UnitConfigurations = unitConfigurations;
    }

    public IReadOnlyList<IUnitConfiguration> UnitConfigurations { get; }
}