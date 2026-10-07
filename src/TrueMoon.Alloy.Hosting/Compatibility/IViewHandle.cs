using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting.Compatibility;

public interface IViewHandle : IDisposable
{
    void Run();
    void Close();
    
    IView? View { get; }
    IVisualTree VisualTree { get; }

    event Action Closed;
}