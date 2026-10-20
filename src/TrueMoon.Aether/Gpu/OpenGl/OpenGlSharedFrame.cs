using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Gpu.Interop;

namespace TrueMoon.Aether.Gpu.OpenGl;

/// <summary>App-owned imported RGBA8 texture and binary semaphores on a borrowed GL owner context.</summary>
public sealed unsafe class OpenGlSharedFrame : IGpuResource
{
    private readonly GpuFrameOffer _offer;
    private readonly GpuOpenGlContext _context;
    private GpuFrameHandshake _handshake;
    private GpuFrameStamp _stamp;
    private readonly Dictionary<string, nint> _functions = [];
    private uint _memory, _texture, _ready, _returned, _framebuffer;
    private nint _fence;
    private bool _used, _returnSignaled;
    private int _closing;
    /// <summary>Whether all local imports/GL objects were released on their owner.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Protocol phase; cleanup does not make unconfirmed use successful.</summary>
    public string Phase => _handshake.Phase;
    internal OpenGlSharedFrame(GpuFrameOffer offer, GpuOpenGlContext context) { _offer = offer; _stamp = offer.Stamp; _context = context; _handshake = new(offer); }
    internal void Initialize(NativeRuntimeLease runtime)
    {
        Verify();
        foreach (var name in new[] { "glCreateMemoryObjectsEXT", "glMemoryObjectParameterivEXT", "glImportMemoryWin32HandleEXT", "glDeleteMemoryObjectsEXT",
                     "glGenTextures", "glBindTexture", "glTexParameteri", "glTexStorageMem2DEXT", "glDeleteTextures", "glGenSemaphoresEXT", "glImportSemaphoreWin32HandleEXT",
                     "glDeleteSemaphoresEXT", "glWaitSemaphoreEXT", "glSignalSemaphoreEXT", "glFenceSync", "glClientWaitSync", "glDeleteSync", "glFlush", "glGetError", "glGetIntegerv",
                     "glGenFramebuffers", "glBindFramebuffer", "glFramebufferTexture2D", "glCheckFramebufferStatus", "glDeleteFramebuffers", "glReadPixels", "glPixelStorei", "glBindBuffer" })
            _functions.Add(name, _context.Require(name));
        Error("before import");
        using var memory = GpuNtHandle.Duplicate(runtime.GpuProcessHandle, _offer.MemoryHandle);
        using var ready = GpuNtHandle.Duplicate(runtime.GpuProcessHandle, _offer.ReadyHandle);
        using var returned = GpuNtHandle.Duplicate(runtime.GpuProcessHandle, _offer.ReturnHandle);
        _memory = Generate("glCreateMemoryObjectsEXT"); int dedicated = 1;
        ((delegate* unmanaged<uint, uint, int*, void>)F("glMemoryObjectParameterivEXT"))(_memory, 0x9581, &dedicated);
        ((delegate* unmanaged<uint, ulong, uint, nint, void>)F("glImportMemoryWin32HandleEXT"))(_memory, _offer.AllocationBytes, 0x9587, memory.DangerousGetHandle()); Error("ImportMemory");
        _ready = Generate("glGenSemaphoresEXT"); _returned = Generate("glGenSemaphoresEXT");
        var importSemaphore = (delegate* unmanaged<uint, uint, nint, void>)F("glImportSemaphoreWin32HandleEXT");
        importSemaphore(_ready, 0x9587, ready.DangerousGetHandle()); Error("ImportSemaphore ready");
        importSemaphore(_returned, 0x9587, returned.DangerousGetHandle()); Error("ImportSemaphore return");
        var previous = Integer(0x8069); _texture = Generate("glGenTextures");
        try
        {
            ((delegate* unmanaged<uint, uint, void>)F("glBindTexture"))(0x0de1, _texture);
            var parameter = (delegate* unmanaged<uint, uint, int, void>)F("glTexParameteri");
            parameter(0x0de1, 0x9580, 0x9584); parameter(0x0de1, 0x2801, 0x2600); parameter(0x0de1, 0x2800, 0x2600);
            ((delegate* unmanaged<uint, int, uint, int, int, uint, ulong, void>)F("glTexStorageMem2DEXT"))(0x0de1, 1, 0x8058, _offer.Width, _offer.Height, _memory, 0); Error("TexStorageMem2D");
        }
        finally { ((delegate* unmanaged<uint, uint, void>)F("glBindTexture"))(0x0de1, (uint)previous); }
        _framebuffer = Generate("glGenFramebuffers"); _handshake.Imported();
        GC.KeepAlive(memory); GC.KeepAlive(ready); GC.KeepAlive(returned); // Win32 imports keep references, not ownership of these NT handles.
    }
    /// <summary>Enqueues a wait only after matching CPU-observed ready completion, reads actual imported pixels and signals return.</summary>
    internal byte[] ReadSlot(GpuFrameCompletion ready)
    {
        PrepareSlot(ready);
        return Read(ready);
    }
    private void PrepareSlot(GpuFrameCompletion ready)
    {
        Verify(); Surfaces.GpuSlotLedger.ValidateStamp(ready.Stamp, _offer.Stamp.Identity);
        if (!ready.FenceCompleted || ready.Stamp.SurfaceGeneration != _offer.Stamp.SurfaceGeneration || ready.Stamp.Slot != _offer.Stamp.Slot ||
            (_used && (_handshake.Phase != "Released" || !Completed()))) throw new InvalidOperationException("Prior GL use remains active.");
        if (_fence != 0) { ((delegate* unmanaged<nint, void>)F("glDeleteSync"))(_fence); _fence = 0; }
        _used = false; _returnSignaled = false; _stamp = ready.Stamp; _handshake = new(_offer with { Stamp = ready.Stamp }); _handshake.Imported();
    }
    internal void UseSlot(GpuFrameCompletion ready, Action<GpuOpenGlTexture> consume, Action verifySession)
    {
        PrepareSlot(ready); _handshake.Ready(ready); _used = true;
        var texture = new GpuOpenGlTexture(_context, verifySession, _texture, _offer.Width, _offer.Height, ready.Stamp);
        try
        {
            uint name = _texture, layout = 0x958d;
            ((delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void>)F("glWaitSemaphoreEXT"))(_ready, 0, null, 1, &name, &layout); Error("WaitSemaphore texture use");
            consume(texture);
            ((delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void>)F("glSignalSemaphoreEXT"))(_returned, 0, null, 1, &name, &layout); Error("SignalSemaphore texture return"); _returnSignaled = true;
        }
        catch { _handshake.Fault(); throw; }
        finally
        {
            texture.End();
            _fence = ((delegate* unmanaged<uint, uint, nint>)F("glFenceSync"))(0x9117, 0);
            ((delegate* unmanaged<void>)F("glFlush"))();
            if (_fence == 0) throw new IOException("GL texture-use fence was not created; imports retained.");
        }
    }
    /// <summary>Enqueues a wait only after matching CPU-observed ready completion, reads actual imported pixels and signals return.</summary>
    public byte[] Read(GpuFrameCompletion ready)
    {
        Verify(); _handshake.Ready(ready); var pixels = new byte[checked(_offer.Width * _offer.Height * 4)];
        var draw = Integer(0x8ca6); var read = Integer(0x8caa); var packBuffer = Integer(0x88ed);
        var stores = new[] { 0x0d05u, 0x0d02u, 0x0d03u, 0x0d04u }; var previous = stores.Select(Integer).ToArray();
        _used = true;
        try
        {
            uint texture = _texture, layout = 0x958d;
            ((delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void>)F("glWaitSemaphoreEXT"))(_ready, 0, null, 1, &texture, &layout); Error("WaitSemaphore ready");
            ((delegate* unmanaged<uint, uint, void>)F("glBindFramebuffer"))(0x8d40, _framebuffer);
            ((delegate* unmanaged<uint, uint, uint, uint, int, void>)F("glFramebufferTexture2D"))(0x8d40, 0x8ce0, 0x0de1, _texture, 0);
            if (((delegate* unmanaged<uint, uint>)F("glCheckFramebufferStatus"))(0x8d40) != 0x8cd5) throw new IOException("Imported texture framebuffer is incomplete.");
            ((delegate* unmanaged<uint, uint, void>)F("glBindBuffer"))(0x88eb, 0);
            var pixelStore = (delegate* unmanaged<uint, int, void>)F("glPixelStorei");
            for (var i = 0; i < stores.Length; i++) pixelStore(stores[i], i == 0 ? 1 : 0);
            fixed (byte* pointer = pixels) ((delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)F("glReadPixels"))(0, 0, _offer.Width, _offer.Height, 0x1908, 0x1401, pointer);
            Error("ReadPixels imported RGBA8");
            ((delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void>)F("glSignalSemaphoreEXT"))(_returned, 0, null, 1, &texture, &layout); Error("SignalSemaphore return"); _returnSignaled = true;
            return pixels;
        }
        catch { _handshake.Fault(); throw; }
        finally
        {
            var bind = (delegate* unmanaged<uint, uint, void>)F("glBindFramebuffer"); bind(0x8ca9, (uint)draw); bind(0x8ca8, (uint)read);
            ((delegate* unmanaged<uint, uint, void>)F("glBindBuffer"))(0x88eb, (uint)packBuffer);
            for (var i = 0; i < stores.Length; i++) ((delegate* unmanaged<uint, int, void>)F("glPixelStorei"))(stores[i], previous[i]);
            _fence = ((delegate* unmanaged<uint, uint, nint>)F("glFenceSync"))(0x9117, 0);
            ((delegate* unmanaged<void>)F("glFlush"))();
            if (_fence == 0) throw new IOException("GL completion fence was not created; imports retained.");
        }
    }
    /// <summary>Nonblocking owner-thread poll; null means GPU use has not completed. No client wait timeout is issued.</summary>
    public GpuFrameCompletion? PollCompletion()
    {
        Verify(); if (!_returnSignaled || _fence == 0) throw new InvalidOperationException("No GL return fence.");
        if (!Completed()) return null;
        var completion = new GpuFrameCompletion(_stamp, true);
        if (_handshake.Phase == "Ready") _handshake.Released(completion);
        return completion;
    }
    private bool Completed()
    {
        var result = ((delegate* unmanaged<nint, uint, ulong, uint>)F("glClientWaitSync"))(_fence, 0, 0);
        if (result is 0x911a or 0x911c) return true;
        if (result == 0x911b) return false;
        throw new IOException("GL fence outcome unknown: 0x" + result.ToString("X"));
    }
    private int Integer(uint name) { int value = 0; ((delegate* unmanaged<uint, int*, void>)F("glGetIntegerv"))(name, &value); return value; }
    private uint Generate(string name) { uint value = 0; ((delegate* unmanaged<int, uint*, void>)F(name))(1, &value); if (value == 0) throw new IOException(name + " returned zero."); return value; }
    private nint F(string name) => _functions[name];
    private void Error(string operation)
    { var error = ((delegate* unmanaged<uint>)F("glGetError"))(); if (error != 0) throw new IOException(operation + ": GL error 0x" + error.ToString("X")); }
    private void Verify() { _context.Verify(); ObjectDisposedException.ThrowIf(Volatile.Read(ref _closing) != 0, this); }
    /// <summary>Runs cleanup on the borrowed owner. Unconfirmed use retains imports and permits retry.</summary>
    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closing, 1);
        if (IsReleased) return ValueTask.CompletedTask;
        return new(_context.Invoke(DisposeOnOwner));
    }
    internal void DisposeOnOwner()
    {
        _context.Verify(); Interlocked.Exchange(ref _closing, 1); if (IsReleased) return;
        if (_used && (_fence == 0 || !Completed())) throw new IOException("GL GPU use remains unconfirmed; imports retained for cleanup retry.");
        Delete("glDeleteFramebuffers", ref _framebuffer); Delete("glDeleteTextures", ref _texture);
        Delete("glDeleteSemaphoresEXT", ref _returned); Delete("glDeleteSemaphoresEXT", ref _ready); Delete("glDeleteMemoryObjectsEXT", ref _memory);
        if (_fence != 0) { ((delegate* unmanaged<nint, void>)F("glDeleteSync"))(_fence); _fence = 0; }
        if (_functions.ContainsKey("glGetError")) Error("import cleanup"); IsReleased = true;
    }
    private void Delete(string name, ref uint value)
    { if (value != 0) { uint local = value; ((delegate* unmanaged<int, uint*, void>)F(name))(1, &local); value = 0; } }
}
