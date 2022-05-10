namespace TrueMoon.Enclaves;

public static class AppParametersExtensions
{
    public static bool IsEnclave(this IAppParameters parameters) 
        => parameters.Get<string>("-eid") is { } value && !string.IsNullOrWhiteSpace(value);

    public static int? GetEnclaveId(this IAppParameters parameters)
        => parameters.Get<string>("-eid") is { } value 
           && !string.IsNullOrWhiteSpace(value) 
           && int.TryParse(value, out var id)
            ? id
            : default;
}