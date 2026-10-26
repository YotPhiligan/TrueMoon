using System.Text;

namespace TrueMoon.Enerit.IO.Pipes;

/// <summary>Limits for the current same-build pipes protocol.</summary>
public static class PipesProtocol
{
    private static readonly UTF8Encoding ErrorEncoding = new(false, true);

    /// <summary>Maximum request, success or error payload size, excluding the 21-byte legacy header
    /// or optional 51-byte trace request header. Responses always use 21 bytes.</summary>
    public const int MaxPayloadLength = 1024 * 1024;

    internal static string DecodeError(ReadOnlySpan<byte> payload)
    {
        try { return ErrorEncoding.GetString(payload); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException("Invalid UTF-8 invocation error payload.", error);
        }
    }

    /// <summary>Rejects invalid payload sizes before renting or writing a frame.</summary>
    /// <param name="length">Payload size in bytes.</param>
    /// <exception cref="InvalidDataException">The size is negative or exceeds the protocol limit.</exception>
    public static void ValidatePayloadLength(int length)
    {
        if (length is < 0 or > MaxPayloadLength)
            throw new InvalidDataException($"Payload length {length} is outside 0..{MaxPayloadLength}.");
    }
}
