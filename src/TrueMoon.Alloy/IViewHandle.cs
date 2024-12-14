using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public interface IViewHandle : IDisposable
{
    void Run();
    void Close();
    
    IView? View { get; }
    IVisualTree VisualTree { get; }

    event Action Closed;
}