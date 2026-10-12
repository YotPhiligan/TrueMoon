using TrueMoon.Configuration;

namespace TrueMoon.Mithril;

public interface IUnitConfiguration : IConfigurable
{
    int Index { get; }
    
    UnitStartupPolicy? StartupPolicy { get; set; }
    UnitHostingPolicy? HostingPolicy { get; set; }
    UnitLifetimePolicy? LifetimePolicy { get; set; }
    UnitRestartPolicy? RestartPolicy { get; set; }
    /// <summary>Maximum automatic restart attempts per explicit start; default3, zero disables.</summary>
    int? RestartLimit { get => Get<int?>(nameof(RestartLimit)); set => Set(nameof(RestartLimit), value); }
    /// <summary>Delay before each automatic attempt in milliseconds; default250.</summary>
    int? RestartDelay { get => Get<int?>(nameof(RestartDelay)); set => Set(nameof(RestartDelay), value); }
    string Name { get; set; }
    
    bool? IsControlAppLifetime { get; set; }
    int? TerminationDelay { get; set; }
}