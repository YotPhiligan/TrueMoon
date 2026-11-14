namespace TrueMoon.Kton.Bepu;

// Fixed-capacity, provider-owned storage. Backend handles are never public runtime IDs.
internal sealed class GenerationalSlots<T> where T : class
{
    private readonly T?[] _items;
    private readonly uint[] _generations;
    private readonly int[] _free;
    private int _freeCount;
    internal int Count { get; private set; }
    internal int Capacity => _items.Length;
    internal T? this[int slot] => (uint)slot < (uint)_items.Length ? _items[slot] : null;
    internal uint Generation(int slot) => (uint)slot < (uint)_items.Length ? _generations[slot] : 0;
    internal GenerationalSlots(int capacity)
    {
        _items = new T?[capacity]; _generations = new uint[capacity]; _free = new int[capacity];
        Array.Fill(_generations, 1u); _freeCount = capacity;
        for (var i = 0; i < capacity; i++) _free[i] = capacity - 1 - i;
    }
    internal int NextSlot(string field)
    {
        if (_freeCount == 0) throw new PhysicsException(new(PhysicsErrorCode.CapacityExceeded, field));
        return _free[_freeCount - 1];
    }
    internal void Publish(int slot, T item)
    {
        if (_freeCount == 0 || _free[_freeCount - 1] != slot || _items[slot] is not null) throw new InvalidOperationException("Slot publication mismatch.");
        _freeCount--; _items[slot] = item; Count++;
    }
    internal void Remove(int slot)
    {
        if (_items[slot] is null) throw new InvalidOperationException("Slot is not alive.");
        _items[slot] = null; Count--;
        if (_generations[slot] == uint.MaxValue) return; // Retired forever: no wrap or resurrection.
        _generations[slot]++; _free[_freeCount++] = slot;
    }
    internal void Clear() { Array.Clear(_items); Array.Clear(_free); _freeCount = 0; Count = 0; }
    internal void SeedFreeGenerationForTest(int slot, uint generation)
    {
        if (_items[slot] is not null || generation == 0) throw new InvalidOperationException();
        _generations[slot] = generation;
    }
}
