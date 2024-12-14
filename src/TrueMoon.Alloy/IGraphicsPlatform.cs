namespace TrueMoon.Alloy;

public interface IGraphicsPlatform : IDisposable
{
    Silk.NET.Windowing.IWindow? GetNativeWindow();
    void Initialize();
}