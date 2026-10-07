namespace TrueMoon.Alloy.Hosting.Compatibility;

public interface IGraphicsPlatform : IDisposable
{
    Silk.NET.Windowing.IWindow? GetNativeWindow();
    void Initialize();
}