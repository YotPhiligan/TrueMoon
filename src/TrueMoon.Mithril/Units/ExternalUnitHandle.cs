using System.Collections.ObjectModel;
using TrueMoon.Diagnostics;

namespace TrueMoon.Mithril.Units;

public class ExternalUnitHandle : ProcessUnitHandle
{
    public const string Key = "ExternalProcessFilePath";
    public ExternalUnitHandle(IUnitConfiguration configuration, IEventsSource eventsSource) : base(configuration, eventsSource)
    {
        
    }

    protected override string GetProcessFilePath()
    {
        var path = Configuration.Get<string>(Key);

        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("External hosting requires an executable path.");
        return Path.GetFullPath(path);
    }

    protected override void ProcessLaunchArguments(Collection<string> collection)
    {
        base.ProcessLaunchArguments(collection);
    }
}
