using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrueMoon;

/// <summary>Rents logical memory regions whose pool ownership can be validated on return.</summary>
public static class MemoryPoolUtils
{
    /// <summary>Returns the exact whole memory from a rental, rejecting foreign or already returned memory.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">The whole logical memory returned by Create or Rent.</param>
    public static void Return<T>(Memory<T> memory) => Return((ReadOnlyMemory<T>)memory);

    /// <summary>Returns the exact whole memory from a rental, rejecting slices and repeated returns.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">The whole logical memory returned by Create or Rent.</param>
    public static void Return<T>(ReadOnlyMemory<T> memory)
    {
        if (!TryReturn(memory))
        {
            throw new InvalidOperationException("Memory is not an active whole TrueMoon pool rental.");
        }
    }

    /// <summary>Attempts to return a whole logical rental exactly once.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">Memory to return.</param>
    /// <returns>True when the caller owned an active whole rental and returned it.</returns>
    public static bool TryReturn<T>(Memory<T> memory) => TryReturn((ReadOnlyMemory<T>)memory);

    /// <summary>Attempts to return a whole logical rental exactly once.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="memory">Memory to return.</param>
    /// <returns>False for foreign memory, slices, or a previously returned rental.</returns>
    public static bool TryReturn<T>(ReadOnlyMemory<T> memory) =>
        MemoryMarshal.TryGetMemoryManager<T, Rental<T>>(memory, out var rental, out var start, out var length)
        && start == 0 && length == rental.Length && rental.TryReturn();

    /// <summary>Rents a byte region of the requested logical length. Return the whole region after use.</summary>
    /// <param name="len">Nonnegative logical length.</param>
    /// <returns>Memory with independently tracked ownership even when its array is later reused.</returns>
    public static Memory<byte> Create(int len) => new Rental<byte>(len).Memory;

    /// <summary>Rents memory with an explicit idempotent owner.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="len">Nonnegative logical length.</param>
    /// <returns>An owner that returns its memory on disposal.</returns>
    public static IMemoryOwner<T> Rent<T>(int len) => new Rental<T>(len);

    private sealed class Rental<T> : MemoryManager<T>
    {
        private T[]? _buffer;
        public int Length { get; }

        public Rental(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            Length = length;
            _buffer = ArrayPool<T>.Shared.Rent(length);
        }

        public override Memory<T> Memory
        {
            get
            {
                _ = GetBuffer();
                return CreateMemory(Length);
            }
        }

        private T[] GetBuffer() => _buffer ?? throw new ObjectDisposedException(nameof(Rental<T>));
        public override Span<T> GetSpan() => GetBuffer().AsSpan(0, Length);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            var buffer = GetBuffer();
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, Length);
            return buffer.AsMemory(elementIndex, Length - elementIndex).Pin();
        }

        // The handle returned by Pin owns the underlying array's pin directly.
        public override void Unpin() { }

        public bool TryReturn()
        {
            var buffer = Interlocked.Exchange(ref _buffer, null);
            if (buffer is null)
            {
                return false;
            }
            ArrayPool<T>.Shared.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            return true;
        }

        protected override void Dispose(bool disposing) => TryReturn();
    }
}
