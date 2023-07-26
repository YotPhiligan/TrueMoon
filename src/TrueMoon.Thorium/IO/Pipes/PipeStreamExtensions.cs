using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrueMoon.Thorium.IO.Pipes;

public static class PipeStreamExtensions
{
    public static (Guid,byte,int) GetInput(this PipeStream stream)
    {
        var guidSize = Unsafe.SizeOf<Guid>();
        Span<byte> guidBuffer = stackalloc byte[guidSize];
        stream.ReadExactly(guidBuffer);

        var method = (byte)stream.ReadByte();
        Span<byte> lenBuffer = stackalloc byte[4];
        stream.ReadExactly(lenBuffer);
            
        var len = MemoryMarshal.Read<int>(lenBuffer);
        var guidResult = MemoryMarshal.Read<Guid>(guidBuffer);
        return (guidResult,method,len);
    }
    
    public static (Guid,byte,int) GetOutput(this PipeStream stream)
    {
        var guidSize = Unsafe.SizeOf<Guid>();
        Span<byte> guidBuffer = stackalloc byte[guidSize];
        stream.ReadExactly(guidBuffer);
        var guidResult = MemoryMarshal.Read<Guid>(guidBuffer);
        var resultCode = (byte)stream.ReadByte();
        Span<byte> lenBuffer = stackalloc byte[4];
        stream.ReadExactly(lenBuffer);
        var len = MemoryMarshal.Read<int>(lenBuffer);
        return (guidResult,resultCode,len);
    }
}