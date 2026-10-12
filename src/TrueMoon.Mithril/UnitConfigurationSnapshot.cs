namespace TrueMoon.Mithril;

internal static class UnitConfigurationSnapshot
{
    internal static IReadOnlyList<IUnitConfiguration> Create(IEnumerable<IUnitConfiguration> configurations)
    {
        var result = configurations.Select(Copy).ToArray();
        var indexes = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in result)
        {
            if (unit.Index <= 0 || !indexes.Add(unit.Index)) throw new ArgumentException("Unit indexes must be positive and unique.");
            if (string.IsNullOrWhiteSpace(unit.Name) || !names.Add(unit.Name)) throw new ArgumentException("Unit names must be nonempty and unique.");
            if (unit.TerminationDelay is < 0) throw new ArgumentOutOfRangeException(nameof(unit.TerminationDelay));
            if (unit.RestartLimit is < 0) throw new ArgumentOutOfRangeException(nameof(unit.RestartLimit));
            if (unit.RestartDelay is < 0) throw new ArgumentOutOfRangeException(nameof(unit.RestartDelay));
            if (unit.HostingPolicy is not (UnitHostingPolicy.ChildProcess or UnitHostingPolicy.MainProcess or UnitHostingPolicy.External))
                throw new ArgumentException("Select child, main or external unit hosting.");
            if (unit.StartupPolicy is not (UnitStartupPolicy.Immediate or UnitStartupPolicy.Delayed or UnitStartupPolicy.None))
                throw new ArgumentException("Unknown unit startup policy.");
            if (unit.LifetimePolicy != UnitLifetimePolicy.App) throw new NotSupportedException("Only app-owned unit lifetime is supported.");
            if (unit.RestartPolicy is not (UnitRestartPolicy.Never or UnitRestartPolicy.Always or UnitRestartPolicy.Once or UnitRestartPolicy.OnCrash))
                throw new ArgumentException("Unknown unit restart policy.");
            if (unit.HostingPolicy == UnitHostingPolicy.MainProcess && unit.StartupPolicy != UnitStartupPolicy.Immediate)
                throw new NotSupportedException("Main-process services start through the App lifecycle; delayed local hosting is unsupported.");
            if (unit.HostingPolicy == UnitHostingPolicy.External)
            {
                var path = unit.Get<string>(Units.ExternalUnitHandle.Key);
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("External hosting requires an executable path.");
                unit.Set(Units.ExternalUnitHandle.Key, Path.GetFullPath(path));
            }
        }
        return result;
    }

    private static IUnitConfiguration Copy(IUnitConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = new UnitConfiguration(source.Index);
        foreach (var (key, value) in source.GetList()) copy.Set(key, value);
        copy.HostingPolicy ??= UnitHostingPolicy.ChildProcess;
        copy.LifetimePolicy ??= UnitLifetimePolicy.App;
        copy.StartupPolicy ??= UnitStartupPolicy.Immediate;
        copy.RestartPolicy ??= UnitRestartPolicy.Always;
        return copy;
    }
}
