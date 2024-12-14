using Silk.NET.OpenGL;

namespace TrueMoon.Alloy;

public interface IGlGraphicsPlatform : IGraphicsPlatform
{
    GL Gl { get; }
}