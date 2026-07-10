using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

public class PresentationConfiguration
{
    public Type? StartupViewType { get; set; }
    public Action<IView>? StartupViewCreationDelegate { get; set; }
}