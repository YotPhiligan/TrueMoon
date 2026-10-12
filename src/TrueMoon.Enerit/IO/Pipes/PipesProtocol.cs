namespace TrueMoon.Enerit.IO.Pipes;

/// <summary>Limits for the current same-build pipes protocol.</summary>
public static class PipesProtocol
{
    /// <summary>Maximum request, success or error payload size, excluding the 21-byte header.</summary>
    public const int MaxPayloadLength = 1024 * 1024;

    /// <summary>Rejects invalid payload sizes before renting or writing a frame.</summary>
    /// <param name="length">Payload size in bytes.</param>
    /// <exception cref="InvalidDataException">The size is negative or exceeds the protocol limit.</exception>
    public static void ValidatePayloadLength(int length)
    {
        if (length is < 0 or > MaxPayloadLength)
            throw new InvalidDataException($"Payload length {length} is outside 0..{MaxPayloadLength}.");
    }
}
