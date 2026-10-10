using System.Buffers;
using System.Runtime.CompilerServices;

namespace TrueMoon;

/// <summary>A growable pooled buffer writer. Its views remain valid only until growth or disposal.</summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>Writing and disposal must not run concurrently.</remarks>
public class ArrayPoolBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    private const int DefaultInitialBufferSize = 256;
    private readonly ArrayPool<T> _pool;
    private T[]? _buffer;
    private int _index;

    /// <summary>Creates a writer with a default capacity.</summary>
    public ArrayPoolBufferWriter() : this(DefaultInitialBufferSize) { }

    /// <summary>Creates a writer with at least the requested capacity.</summary>
    /// <param name="initialCapacity">Positive minimum initial capacity.</param>
    public ArrayPoolBufferWriter(int initialCapacity) : this(initialCapacity, ArrayPool<T>.Shared) { }

    /// <summary>Creates a writer using the specified pool.</summary>
    /// <param name="initialCapacity">Positive minimum initial capacity.</param>
    /// <param name="pool">Pool that owns all buffers used by this writer.</param>
    public ArrayPoolBufferWriter(int initialCapacity, ArrayPool<T> pool)
    {
        if (initialCapacity <= 0)
        {
            throw new ArgumentException("Capacity must be positive.", nameof(initialCapacity));
        }
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        _buffer = pool.Rent(initialCapacity);
    }

    private T[] GetBuffer() => _buffer ?? throw new ObjectDisposedException(GetType().Name);

    /// <summary>Gets the written data.</summary>
    public ReadOnlyMemory<T> WrittenMemory => GetBuffer().AsMemory(0, _index);
    /// <summary>Gets the written data.</summary>
    public ReadOnlySpan<T> WrittenSpan => GetBuffer().AsSpan(0, _index);
    /// <summary>Gets a writable view of the written data.</summary>
    public Memory<T> Buffer => GetBuffer().AsMemory(0, _index);
    /// <summary>Gets a writable span of the written data.</summary>
    public Span<T> WrittenSpanWritable => GetBuffer().AsSpan(0, _index);
    /// <summary>Gets the number of written elements.</summary>
    public int WrittenCount { get { _ = GetBuffer(); return _index; } }
    /// <summary>Gets total current capacity.</summary>
    public int Capacity => GetBuffer().Length;
    /// <summary>Gets remaining capacity without growth.</summary>
    public int FreeCapacity => GetBuffer().Length - _index;

    /// <summary>Clears the written region and resets the write position.</summary>
    public void Clear()
    {
        GetBuffer().AsSpan(0, _index).Clear();
        _index = 0;
    }

    /// <inheritdoc />
    public void Advance(int count)
    {
        var buffer = GetBuffer();
        if (count < 0)
        {
            throw new ArgumentException("Count must not be negative.", nameof(count));
        }
        if (count > buffer.Length - _index)
        {
            throw new InvalidOperationException("Cannot advance beyond the buffer capacity.");
        }
        _index += count;
    }

    /// <inheritdoc />
    public Memory<T> GetMemory(int sizeHint = 0) => CheckAndResizeBuffer(sizeHint).AsMemory(_index);
    /// <inheritdoc />
    public Span<T> GetSpan(int sizeHint = 0) => CheckAndResizeBuffer(sizeHint).AsSpan(_index);

    private T[] CheckAndResizeBuffer(int sizeHint)
    {
        var buffer = GetBuffer();
        if (sizeHint < 0)
        {
            throw new ArgumentException("Size hint must not be negative.", nameof(sizeHint));
        }
        sizeHint = Math.Max(1, sizeHint);
        if (sizeHint <= buffer.Length - _index)
        {
            return buffer;
        }
        var required = (long)_index + sizeHint;
        if (required > Array.MaxLength)
        {
            throw new OutOfMemoryException($"Requested capacity {required} exceeds the array limit.");
        }
        var newSize = (int)Math.Min(Array.MaxLength, Math.Max(required, (long)buffer.Length * 2));
        var newBuffer = _pool.Rent(newSize);
        buffer.AsSpan(0, _index).CopyTo(newBuffer);
        _buffer = newBuffer;
        _pool.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        return newBuffer;
    }

    /// <summary>Returns the buffer once, clearing references. Later operations throw.</summary>
    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
        {
            _index = 0;
            _pool.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}
