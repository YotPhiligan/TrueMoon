
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Importing.Assimp;

// ABI: assimp v6.0.5 cfileio.h. No wrapper IOStream temporary byte[count] allocation.
internal sealed unsafe class CapturedIo : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileIo
    {
        internal delegate* unmanaged[Cdecl]<FileIo*, byte*, byte*, NativeFile*> Open;
        internal delegate* unmanaged[Cdecl]<FileIo*, NativeFile*, void> Close;
        internal nint User;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeFile
    {
        internal delegate* unmanaged[Cdecl]<NativeFile*, byte*, nuint, nuint, nuint> Read;
        internal delegate* unmanaged[Cdecl]<NativeFile*, byte*, nuint, nuint, nuint> Write;
        internal delegate* unmanaged[Cdecl]<NativeFile*, nuint> Tell;
        internal delegate* unmanaged[Cdecl]<NativeFile*, nuint> Size;
        internal delegate* unmanaged[Cdecl]<NativeFile*, nuint, int, int> Seek;
        internal delegate* unmanaged[Cdecl]<NativeFile*, void> Flush;
        internal nint User;
    }
    private sealed class Cursor(CapturedIo owner, ReadOnlyMemory<byte> bytes)
    {
        internal readonly CapturedIo Owner = owner;
        internal readonly ReadOnlyMemory<byte> Bytes = bytes;
        internal int Position;
    }
    private readonly CapturedModelSource input;
    private readonly HashSet<nint> streams = [];
    private GCHandle self;
    private FileIo* io;
    internal nint Pointer => (nint)io;
    internal int LiveStreams => streams.Count;
    internal int OpenCalls { get; private set; }
    internal long ReadBytes { get; private set; }
    internal Exception? Error { get; private set; }
    internal readonly HashSet<string> Used = new(StringComparer.Ordinal);

    internal CapturedIo(CapturedModelSource input)
    {
        this.input = input;
        self = GCHandle.Alloc(this);
        try
        {
            io = (FileIo*)NativeMemory.AllocZeroed((nuint)sizeof(FileIo));
            if (io == null) throw new OutOfMemoryException();
            *io = new() { Open = &Open, Close = &Close, User = GCHandle.ToIntPtr(self) };
        }
        catch { self.Free(); throw; }
    }
    private static CapturedIo Owner(FileIo* io) => (CapturedIo)GCHandle.FromIntPtr(io->User).Target!;
    private static Cursor State(NativeFile* file) => (Cursor)GCHandle.FromIntPtr(file->User).Target!;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static NativeFile* Open(FileIo* io, byte* path, byte* mode)
    {
        var owner = Owner(io);
        try
        {
            if (++owner.OpenCalls > 512 || owner.streams.Count >= 16)
                throw new InvalidDataException("Native I/O open quota.");
            var access = Text(mode);
            if (access is not ("r" or "rb" or "rt")) throw new InvalidDataException("Write access denied.");
            var name = ModelSourcePath.Normalize(Text(path));
            var state = new Cursor(owner, owner.input.GetFile(name).Bytes);
            var handle = GCHandle.Alloc(state);
            NativeFile* file = null;
            try
            {
                file = (NativeFile*)NativeMemory.AllocZeroed((nuint)sizeof(NativeFile));
                if (file == null) throw new OutOfMemoryException();
                *file = new() { Read = &Read, Write = &Write, Tell = &Tell, Size = &Size, Seek = &Seek, Flush = &Flush, User = GCHandle.ToIntPtr(handle) };
                owner.Used.Add(name); owner.streams.Add((nint)file);
                return file;
            }
            catch { if (file != null) NativeMemory.Free(file); handle.Free(); throw; }
        }
        catch (Exception ex) { owner.Error ??= ex; return null; }
    }
    private static string Text(byte* text)
    {
        if (text == null) throw new InvalidDataException("Null callback string.");
        var length = 0;
        while (length <= 256 && text[length] != 0) length++;
        if (length > 256) throw new InvalidDataException("Callback path quota.");
        return new System.Text.UTF8Encoding(false, true).GetString(new ReadOnlySpan<byte>(text, length));
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Close(FileIo* io, NativeFile* file)
    {
        var owner = Owner(io);
        try { owner.Close((nint)file); } catch (Exception ex) { owner.Error ??= ex; }
    }
    private void Close(nint pointer)
    {
        if (!streams.Remove(pointer)) return;
        GCHandle.FromIntPtr(((NativeFile*)pointer)->User).Free(); NativeMemory.Free((void*)pointer);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint Read(NativeFile* file, byte* destination, nuint size, nuint count)
    {
        var state = State(file); var owner = state.Owner;
        try
        {
            if (size == 0 || count == 0) return 0;
            var requested = checked(size * count); // Reject overflow, never allocate from this size.
            var bytes = (int)Math.Min((ulong)requested, (ulong)(state.Bytes.Length - state.Position));
            if (owner.ReadBytes > 128 * 1024 * 1024 - bytes) throw new InvalidDataException("Native I/O read quota.");
            state.Bytes.Span.Slice(state.Position, bytes).CopyTo(new Span<byte>(destination, bytes));
            state.Position += bytes; owner.ReadBytes += bytes;
            return (nuint)bytes / size; // fread returns whole items, but consumes the partial final item.
        }
        catch (Exception ex) { owner.Error ??= ex; return 0; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint Write(NativeFile* file, byte* data, nuint size, nuint count)
    { State(file).Owner.Error ??= new InvalidDataException("Native write denied."); return 0; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint Tell(NativeFile* file) => (nuint)State(file).Position;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint Size(NativeFile* file) => (nuint)State(file).Bytes.Length;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Seek(NativeFile* file, nuint offset, int origin)
    {
        var state = State(file);
        try
        {
            var delta = unchecked((long)offset); // C API carries signed relative offsets through size_t.
            var position = origin switch { 0 => delta, 1 => checked(state.Position + delta), 2 => checked(state.Bytes.Length + delta), _ => -1 };
            if (position < 0 || position > state.Bytes.Length) return -1;
            state.Position = (int)position; return 0;
        }
        catch (Exception ex) { state.Owner.Error ??= ex; return -1; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Flush(NativeFile* file) { }

    internal void ThrowIfFailed() { if (Error != null) throw new InvalidDataException("Captured I/O failed.", Error); }
    public void Dispose()
    {
        if (io == null) return;
        foreach (var pointer in streams.ToArray()) Close(pointer);
        NativeMemory.Free(io); io = null; self.Free();
    }
}

