namespace TrueMoon.Enerit.IO;

/// <summary>Owns byte rentals until a complete decode transfers them to the caller or service.</summary>
/// <remarks>A scope belongs to one synchronous decode. It is not thread-safe.</remarks>
public sealed class PayloadDecodeScope : IDisposable
{
    private List<Memory<byte>>? _rentals;
    private bool _completed;

    /// <summary>Reads and tracks an independently owned byte rental.</summary>
    /// <param name="data">The unread payload beginning with its byte-length header.</param>
    /// <param name="offset">The total consumed byte count.</param>
    /// <returns>The whole rental. Successful decoding transfers its ownership on Commit.</returns>
    public Memory<byte> ReadBytes(ReadOnlySpan<byte> data, ref int offset)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        var memory = SerializationUtils.ReadBytes(data, ref offset);
        try
        {
            (_rentals ??= []).Add(memory);
            return memory;
        }
        catch
        {
            memory.TryReturn();
            throw;
        }
    }

    /// <summary>Transfers all decoded rentals to the caller or service after validation succeeds.</summary>
    /// <remarks>The recipient must return each whole rental after use. No more reads are allowed.</remarks>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;
        _rentals = null;
    }

    /// <summary>Returns all uncommitted rentals, including those held by partially constructed values.</summary>
    public void Dispose()
    {
        if (_completed) return;
        _completed = true;
        if (_rentals is not null)
        {
            foreach (var rental in _rentals) rental.TryReturn();
            _rentals = null;
        }
    }
}
