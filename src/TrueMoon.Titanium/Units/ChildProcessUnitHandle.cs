using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using TrueMoon.Diagnostics;

namespace TrueMoon.Titanium.Units;

public class ChildProcessUnitHandle : ProcessUnitHandle
{
    public ChildProcessUnitHandle(IUnitConfiguration configuration, IEventsSource eventsSource) : base(configuration, eventsSource)
    {
        
    }

    protected override void ProcessLaunchArguments(Collection<string> collection)
    {
        base.ProcessLaunchArguments(collection);
        collection.Add("");
    }
}