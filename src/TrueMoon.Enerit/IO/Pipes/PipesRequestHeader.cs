using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrueMoon.Enerit.IO.Pipes;

// Legacy Guid/method/length framing is unchanged. A negative marker is rejected by legacy receivers before dispatch.
internal readonly record struct PipesRequestHeader(Guid Id, byte Method, int Length, ActivityContext? Context, bool InvalidContext)
{
    internal const int TraceMarker = -2;
    internal const byte TraceVersion = 1;
    internal const int LegacySize = 21, TracedSize = 51;

    internal static PipesRequestHeader Read(Stream stream)
    {
        Span<byte> header = stackalloc byte[LegacySize];
        stream.ReadExactly(header);
        var id = MemoryMarshal.Read<Guid>(header);
        var method = header[16];
        var length = MemoryMarshal.Read<int>(header[17..]);
        ActivityContext? context = null;
        var invalid = false;
        if (length == TraceMarker)
        {
            var version = stream.ReadByte();
            if (version < 0) throw new EndOfStreamException("Missing pipes trace header version.");
            if (version != TraceVersion) throw new InvalidDataException("Unsupported pipes trace header version.");
            Span<byte> extension = stackalloc byte[TracedSize - LegacySize - 1];
            stream.ReadExactly(extension);
            var flags = extension[0];
            var trace = extension.Slice(1, 16);
            var span = extension.Slice(17, 8);
            invalid = IsZero(trace) || IsZero(span) || flags > (byte)ActivityTraceFlags.Recorded;
            if (!invalid) context = new(ActivityTraceId.CreateFromBytes(trace), ActivitySpanId.CreateFromBytes(span), (ActivityTraceFlags)flags, isRemote: true);
            length = MemoryMarshal.Read<int>(extension[25..]);
        }
        PipesProtocol.ValidatePayloadLength(length);
        return new(id, method, length, context, invalid);
    }

    // Returns the actual payload-length field offset, separate from the optional marker.
    internal static int Write(IBufferWriter<byte> writer, Guid id, byte method, ActivityContext? context)
    {
        var traced = context is { } value && IsValid(value);
        var size = traced ? TracedSize : LegacySize;
        var header = writer.GetSpan(size)[..size];
        MemoryMarshal.Write(header, in id); header[16] = method;
        var lengthOffset = 17;
        if (traced)
        {
            var marker = TraceMarker; MemoryMarshal.Write(header[17..], in marker);
            header[21] = TraceVersion;
            header[22] = (byte)(context!.Value.TraceFlags & ActivityTraceFlags.Recorded);
            context.Value.TraceId.CopyTo(header.Slice(23, 16));
            context.Value.SpanId.CopyTo(header.Slice(39, 8));
            lengthOffset = 47;
        }
        header.Slice(lengthOffset, sizeof(int)).Clear();
        writer.Advance(size);
        return lengthOffset;
    }

    internal static bool IsValid(ActivityContext context)
    {
        if (context.TraceId == default || context.SpanId == default) return false;
        Span<byte> ids = stackalloc byte[24];
        context.TraceId.CopyTo(ids[..16]); context.SpanId.CopyTo(ids[16..]);
        return !IsZero(ids[..16]) && !IsZero(ids[16..]);
    }
    private static bool IsZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) if (value != 0) return false;
        return true;
    }
}
