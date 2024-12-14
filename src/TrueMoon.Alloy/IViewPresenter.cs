namespace TrueMoon.Alloy;

public interface IViewPresenter
{
    void Initialize(IGraphicsPlatform graphicsPlatform);

    void Resize(int width, int height);

    void Release();

    void Present(double t, IVisualTree visualTree);
}