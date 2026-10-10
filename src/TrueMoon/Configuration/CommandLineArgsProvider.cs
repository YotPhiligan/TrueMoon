namespace TrueMoon.Configuration;

/// <summary>Provides a fresh snapshot of command line arguments, excluding the executable.</summary>
public class CommandLineArgsProvider : IConfigurationProvider
{
    private readonly Func<IEnumerable<string>> _arguments;

    public CommandLineArgsProvider() : this(() => Environment.GetCommandLineArgs().Skip(1)) { }

    /// <summary>Creates a provider from arguments without the executable name.</summary>
    public CommandLineArgsProvider(Func<IEnumerable<string>> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = arguments;
    }

    public string Name => ConfigurationSectionNames.CommandLineArguments;

    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (var argument in _arguments())
        {
            var separator = argument.IndexOf('=');
            if (separator < 0) dictionary[argument] = null;
            else dictionary[argument[..separator]] = argument[(separator + 1)..];
        }
        return [new CommandLineArgsSection(dictionary)];
    }
}
