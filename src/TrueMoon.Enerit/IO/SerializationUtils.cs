using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TrueMoon.Enerit.IO;

public static class SerializationUtils
{
    /// <summary>Maximum count accepted by generated collection serializers.</summary>
    public const int MaxCollectionCount = 65536;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteElementsCount(int count, IBufferWriter<byte> bufferWriter)
    {
        ValidateElementsCount(count);
        Write(count, bufferWriter);
    }

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
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException("Raw serialization requires a value without managed references.");
        var size = Unsafe.SizeOf<T>();
        var target = bufferWriter.GetSpan(size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(target), value);
        bufferWriter.Advance(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(string? value, IBufferWriter<byte> bufferWriter)
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
        RequireBytes(data, size);
        if (typeof(T) == typeof(bool) && data[0] > 1)
            throw new InvalidDataException("Boolean value must be 0 or 1.");
        var value = MemoryMarshal.Read<T>(data);

        offset += size;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? ReadString(ReadOnlySpan<byte> data, ref int offset)
    {
        ValidateState(data);
        if (data[0] == 0)
        {
            offset += 1;
            return null;
        }
        RequireBytes(data, 1 + sizeof(int));
        var size = MemoryMarshal.Read<int>(data[1..]);
        var head = sizeof(int)+1;
        if (size < 0 || (size & 1) != 0) throw new InvalidDataException("Invalid UTF-16 byte length.");
        RequireBytes(data[head..], size);
        var str = Encoding.Unicode.GetString(data.Slice(head, size));
        offset += head+size;
        return str;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<byte> ReadBytes(ReadOnlySpan<byte> data, ref int offset)
    {
        RequireBytes(data, sizeof(int));
        var size = MemoryMarshal.Read<int>(data);
        RequireBytes(data[sizeof(int)..], size);
        var payload = data.Slice(sizeof(int), size);
        var mem = MemoryPoolUtils.Create(size);
        payload.CopyTo(mem.Span);
        offset += sizeof(int)+size;
        return mem;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadInstanceState(ReadOnlySpan<byte> data, ref int offset)
    {
        ValidateState(data);
        offset += 1;
        return data[0] == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadElementsCount(ReadOnlySpan<byte> data, ref int offset)
    {
        RequireBytes(data, sizeof(int));
        var count = MemoryMarshal.Read<int>(data);
        ValidateElementsCount(count);
        // Every currently generated element has at least one byte on the wire.
        if (count > data.Length - sizeof(int)) throw new InvalidDataException("Collection exceeds the remaining payload.");
        offset += sizeof(int);
        return count;
    }

    /// <summary>Rejects responses or requests that were not consumed exactly.</summary>
    /// <param name="offset">Consumed bytes from the start of the payload.</param>
    /// <param name="length">Total payload bytes.</param>
    /// <exception cref="InvalidDataException">The consumed count differs or either count is negative.</exception>
    public static void EnsureFullyConsumed(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset != length)
            throw new InvalidDataException("Unexpected trailing or incomplete invocation payload.");
    }

    private static void ValidateElementsCount(int count)
    {
        if (count is < 0 or > MaxCollectionCount) throw new InvalidDataException("Invalid collection count.");
    }

    private static void ValidateState(ReadOnlySpan<byte> data)
    {
        RequireBytes(data, 1);
        if (data[0] is not (0 or 1)) throw new InvalidDataException("Instance state must be 0 or 1.");
    }

    private static void RequireBytes(ReadOnlySpan<byte> data, int count)
    {
        if (count < 0 || count > data.Length) throw new InvalidDataException("Truncated or invalid serialized payload.");
    }
}
