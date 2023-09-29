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
        var target = bufferWriter.GetSpan(1);

        target[0] = (byte)(state ? 1 : 0);
        
        bufferWriter.Advance(1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(T value, IBufferWriter<byte> bufferWriter)
    {
        var size = Unsafe.SizeOf<T>();
        var target = bufferWriter.GetSpan(size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(target), value);
        bufferWriter.Advance(size);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(string value, IBufferWriter<byte> bufferWriter)
    {
        byte state;
        if (value is null)
        {
            var target1 = bufferWriter.GetSpan(1);
            state = 0;
            MemoryMarshal.Write(target1, ref state);
            bufferWriter.Advance(1);
            return;
        }
        var target = bufferWriter.GetSpan(1);
        state = 1;
        MemoryMarshal.Write(target, ref state);
        bufferWriter.Advance(1);
        var size = Encoding.Unicode.GetByteCount(value);
        target = bufferWriter.GetSpan(sizeof(int));
        MemoryMarshal.Write(target, ref size);
        bufferWriter.Advance(sizeof(int));
        target = bufferWriter.GetSpan(size);
        size = Encoding.Unicode.GetBytes(value, target);
        bufferWriter.Advance(size);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(ReadOnlyMemory<byte> data, IBufferWriter<byte> bufferWriter)
    {
        var target = bufferWriter.GetSpan(sizeof(int));
        var dataLength = data.Length;
        MemoryMarshal.Write(target, ref dataLength);
        bufferWriter.Advance(sizeof(int));
        target = bufferWriter.GetSpan(dataLength);
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
            offset += 1;
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
        var mem = ArrayPool<byte>.Shared.Rent(size).AsMemory(0, size);
        data.Slice(sizeof(int),size).CopyTo(mem.Span);
        offset += sizeof(int)+size;
        return mem;
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