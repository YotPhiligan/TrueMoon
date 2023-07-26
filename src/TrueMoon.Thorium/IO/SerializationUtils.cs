using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TrueMoon.Thorium.IO;

public static class SerializationUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteElementsCount(int count, IBufferWriter<byte> bufferWriter) => Write(count, bufferWriter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInstanceState(bool state, IBufferWriter<byte> bufferWriter)
    {
        var target = bufferWriter.GetSpan();

        target[0] = (byte)(state ? 1 : 0);
        
        bufferWriter.Advance(1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(T value, IBufferWriter<byte> bufferWriter)
    {
        var size = Unsafe.SizeOf<T>();
        var target = bufferWriter.GetSpan();
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(target), value);
        bufferWriter.Advance(size);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(string value, IBufferWriter<byte> bufferWriter)
    {
        byte state;
        if (value is null)
        {
            var target1 = bufferWriter.GetSpan();
            state = 0;
            MemoryMarshal.Write(target1, ref state);
            return;
        }
        var target = bufferWriter.GetSpan();
        state = 1;
        MemoryMarshal.Write(target, ref state);
        bufferWriter.Advance(1);
        target = bufferWriter.GetSpan();
        var size = Encoding.Unicode.GetByteCount(value);
        MemoryMarshal.Write(target, ref size);
        bufferWriter.Advance(sizeof(int));
        target = bufferWriter.GetSpan();
        size = Encoding.Unicode.GetBytes(value, target);
        bufferWriter.Advance(size);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(ReadOnlyMemory<byte> data, IBufferWriter<byte> bufferWriter)
    {
        var target = bufferWriter.GetSpan();
        var dataLength = data.Length;
        MemoryMarshal.Write(target, ref dataLength);
        bufferWriter.Advance(sizeof(int));
        target = bufferWriter.GetSpan();
        data.Span.CopyTo(target);
        bufferWriter.Advance(dataLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(ReadOnlySpan<byte> data, ref int offset) 
        where T : struct
    {
        var size = Unsafe.SizeOf<T>();
        var value = MemoryMarshal.Read<T>(data);

        offset += size;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ReadOnlySpan<byte> data, ref int offset)
    {
        if (data[0] == 0)
        {
            return null;
        }
        var size = MemoryMarshal.Read<int>(data[1..]);
        var head = sizeof(int)+1;
        var str = Encoding.Unicode.GetString(data.Slice(head, size));
        offset += head+size;
        return str;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<byte> ReadBytes(ReadOnlySpan<byte> data, ref int offset)
    {
        var size = MemoryMarshal.Read<int>(data);
        var array = ArrayPool<byte>.Shared.Rent(size);
        data[sizeof(int)..].CopyTo(array);
        offset += sizeof(int)+size;
        return array.AsMemory(0, size);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadInstanceState(ReadOnlySpan<byte> data, ref int offset)
    {
        offset += 1;
        return data[0] == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadElementsCount(ReadOnlySpan<byte> data, ref int offset) => Read<int>(data, ref offset);
}