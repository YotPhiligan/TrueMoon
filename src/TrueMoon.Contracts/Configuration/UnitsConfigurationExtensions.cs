namespace TrueMoon.Configuration;

public static class UnitsConfigurationExtensions
{
    public const string UnitIdArg = "-u";
    public const string UnitParentIdArg = "-p";
    
    public static bool IsProcessingUnit(this IConfiguration parameters) 
        => parameters.Get<string>(UnitIdArg, ConfigurationSectionNames.CommandLineArguments) is { } value && !string.IsNullOrWhiteSpace(value);

    public static int? GetProcessingUnitId(this IConfiguration parameters)
        => parameters.Get<string>(UnitIdArg, ConfigurationSectionNames.CommandLineArguments) is { } value 
           && !string.IsNullOrWhiteSpace(value) 
           && int.TryParse(value, out var id)
            ? id
            : null;
    
    public static int? GetProcessingUnitParentId(this IConfiguration parameters)
        => parameters.Get<string>(UnitParentIdArg, ConfigurationSectionNames.CommandLineArguments) is { } value 
           && !string.IsNullOrWhiteSpace(value) 
           && int.TryParse(value, out var id)
            ? id
            : null;
}