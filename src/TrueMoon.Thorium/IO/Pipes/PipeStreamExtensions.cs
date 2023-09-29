using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrueMoon.Thorium.IO.Pipes;

public static class PipeStreamExtensions
{
    public static (Guid guid, byte method, int payloadLenght) GetRequestHeader(this PipeStream stream)
    {
        var offset = 0;
        Span<byte> responseHeaderBuffer = stackalloc byte[_headerSize];
        stream.Read(responseHeaderBuffer);
        var guidResult = MemoryMarshal.Read<Guid>(responseHeaderBuffer[offset..]);
        offset += _guidSize;
        var resultCode = MemoryMarshal.Read<byte>(responseHeaderBuffer[offset..]);
        offset++;
        var len = MemoryMarshal.Read<int>(responseHeaderBuffer[offset..]);
        return (guidResult,resultCode,len);
    }
    
    private static readonly int _guidSize = Unsafe.SizeOf<Guid>();
    private static readonly int _headerSize = _guidSize + 1 + sizeof(int);
    
    public static (Guid guid, byte statusCode, int payloadLenght) GetResponseHeader(this PipeStream stream)
    {
        var offset = 0;
        Span<byte> responseHeaderBuffer = stackalloc byte[_headerSize];
        stream.Read(responseHeaderBuffer);
        var guidResult = MemoryMarshal.Read<Guid>(responseHeaderBuffer[offset..]);
        offset += _guidSize;
        var resultCode = MemoryMarshal.Read<byte>(responseHeaderBuffer[offset..]);
        offset++;
        var len = MemoryMarshal.Read<int>(responseHeaderBuffer[offset..]);
        return (guidResult,resultCode,len);
    }
    
    public static void ReadFullBuffer(this PipeStream stream, Memory<byte> memory)
    {
        var offset = 0;
        while (offset < memory.Length)
        {
            var r = stream.Read(memory.Span[offset..]);
            offset += r;
        }
    }
}