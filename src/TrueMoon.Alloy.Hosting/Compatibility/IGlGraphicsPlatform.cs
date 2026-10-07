using Silk.NET.OpenGL;

namespace TrueMoon.Alloy.Hosting.Compatibility;

public interface IGlGraphicsPlatform : IGraphicsPlatform
{
    GL Gl { get; }
}