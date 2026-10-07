using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting.Compatibility;

public class PresentationConfiguration
{
    public Type? StartupViewType { get; set; }
    public Action<IView>? StartupViewCreationDelegate { get; set; }
}