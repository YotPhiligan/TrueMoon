namespace TrueMoon.Aether.Gpu.OpenGl;

/// <summary>A callback-scoped RGBA8 premultiplied, stored-sRGB, top-left texture on the borrowed GL owner.</summary>
/// <remarks>Submit all rendering commands before returning, including exceptional returns. Never retain the native name
/// or defer its use. Copy into an independently owned GPU image when the UI needs to retain a frame.</remarks>
public sealed class GpuOpenGlTexture
{
    private readonly GpuOpenGlContext _context;
    private readonly Action _verifySession;
    private readonly uint _name;
    private readonly int _width, _height;
    private readonly GpuFrameStamp _stamp;
    private int _ended;
    internal GpuOpenGlTexture(GpuOpenGlContext context, Action verifySession, uint name, int width, int height, GpuFrameStamp stamp)
    { _context = context; _verifySession = verifySession; _name = name; _width = width; _height = height; _stamp = stamp; }
    private void Verify()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _ended) != 0, this);
        _context.Verify(); _verifySession();
    }
    /// <summary>Borrowed GL_TEXTURE_2D name, valid only during this callback.</summary>
    public uint Name { get { Verify(); return _name; } }
    /// <summary>Pixel width.</summary>
    public int Width { get { Verify(); return _width; } }
    /// <summary>Pixel height.</summary>
    public int Height { get { Verify(); return _height; } }
    /// <summary>Exact session, surface, slot and frame identity.</summary>
    public GpuFrameStamp Stamp { get { Verify(); return _stamp; } }
    internal void End() => Interlocked.Exchange(ref _ended, 1);
}
