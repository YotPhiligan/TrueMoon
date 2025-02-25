﻿using TrueMoon.Configuration;

namespace TrueMoon.Titanium;

public interface IUnitConfiguration : IConfigurable
{
    int Index { get; }

    Action<IAppConfigurationContext> ConfigurationDelegate { get; set; }
    UnitStartupPolicy? StartupPolicy { get; set; }
    UnitHostingPolicy? HostingPolicy { get; set; }
    UnitLifetimePolicy? LifetimePolicy { get; set; }
    UnitRestartPolicy? RestartPolicy { get; set; }
    string Name { get; set; }
    
    bool? IsControlAppLifetime { get; set; }
    int? TerminationDelay { get; set; }
}