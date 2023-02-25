using System.Collections.ObjectModel;
using TrueMoon.Diagnostics;

namespace TrueMoon.Titanium.Units;

public class ExternalUnitHandle : ProcessUnitHandle
{
    public const string Key = "ExternalProcessFilePath";
    public ExternalUnitHandle(IUnitConfiguration configuration, IEventsSource eventsSource) : base(configuration, eventsSource)
    {
        
    }

    protected override string GetProcessFilePath()
    {
        var path = Configuration.Get<string>(Key);

        return Path.GetFullPath(path);;
    }

    protected override void ProcessLaunchArguments(Collection<string> collection)
    {
        base.ProcessLaunchArguments(collection);
        collection.Add("");
    }
}