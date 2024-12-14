namespace TrueMoon.Alloy;

public class GlGraphicsPlatformFactory : IFactory<IGraphicsPlatform>
{
    public IGraphicsPlatform Create()
    {
        return new GlGraphicsPlatform();
    }

    public IGraphicsPlatform? Create<TData>(TData? data = default) => Create();
}