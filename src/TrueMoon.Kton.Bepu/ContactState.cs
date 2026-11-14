using System.Numerics;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;

namespace TrueMoon.Kton.Bepu;

internal readonly record struct ContactKey(PhysicsBodyId A, PhysicsBodyId B, int SubshapeA, int SubshapeB) : IComparable<ContactKey>
{
    public int CompareTo(ContactKey other)
    {
        var order = A.Slot.CompareTo(other.A.Slot); if (order != 0) return order;
        order = A.Generation.CompareTo(other.A.Generation); if (order != 0) return order;
        order = B.Slot.CompareTo(other.B.Slot); if (order != 0) return order;
        order = B.Generation.CompareTo(other.B.Generation); if (order != 0) return order;
        order = SubshapeA.CompareTo(other.SubshapeA); return order != 0 ? order : SubshapeB.CompareTo(other.SubshapeB);
    }
}
internal readonly record struct ContactSample(ContactKey Key, bool IsSensor, Vector3 Point, Vector3 Normal, float Depth);

internal sealed class ContactState
{
    private readonly int _capacity;
    private readonly ContactSample[] _raw;
    private readonly PhysicsContactEvent[] _pending;
    private PhysicsContactEvent[] _published, _working;
    private Dictionary<ContactKey, ContactSample> _active, _current;
    private readonly ContactKey[] _invalidated;
    private int _rawCount, _pendingCount, _publishedCount, _workingCount;
    internal bool Overflow { get; private set; }
    internal int ActiveCount => _active.Count;
    internal int PendingCount => _pendingCount;
    internal ContactState(int capacity)
    {
        _capacity = capacity; _raw = new ContactSample[capacity]; _pending = new PhysicsContactEvent[capacity];
        _published = new PhysicsContactEvent[capacity]; _working = new PhysicsContactEvent[capacity]; _invalidated = new ContactKey[capacity];
        _active = new(capacity); _current = new(capacity);
    }
    internal void Record(ContactSample sample)
    {
        // One supported worker. No allocation, external callbacks, or exceptions in this callback path.
        if (_rawCount == _raw.Length) { Overflow = true; return; }
        _raw[_rawCount++] = sample;
    }
    internal void BeginStep() { _rawCount = 0; Overflow = false; _current.Clear(); _workingCount = 0; }
    internal void PreflightInvalidation(PhysicsBodyId body)
    {
        var count = 0; foreach (var key in _active.Keys) if (key.A == body || key.B == body) count++;
        PhysicsValidation.ValidateCapacity(_pendingCount, count, _capacity, "Contacts.PendingEnds").ThrowIfFailure();
    }
    internal void PreflightTeleports(PreparedBody[] prepared)
    {
        var count = 0;
        foreach (var key in _active.Keys)
            if (prepared[key.A.Slot].Teleported || prepared[key.B.Slot].Teleported) count++;
        PhysicsValidation.ValidateCapacity(_pendingCount, count, _capacity, "Contacts.PendingEnds").ThrowIfFailure();
    }
    internal void Invalidate(PhysicsBodyId body, PhysicsContactEndReason reason)
    {
        var count = 0;
        foreach (var pair in _active)
        {
            if (pair.Key.A != body && pair.Key.B != body) continue;
            _pending[_pendingCount++] = Event(pair.Value, 0, PhysicsContactKind.End, reason);
            _invalidated[count++] = pair.Key;
        }
        for (var i = 0; i < count; i++) _active.Remove(_invalidated[i]);
    }
    private static PhysicsContactEvent Event(ContactSample sample, ulong tick, PhysicsContactKind kind, PhysicsContactEndReason reason = PhysicsContactEndReason.None)
        => new(tick, kind, sample.Key.A, sample.Key.B, sample.Key.SubshapeA, sample.Key.SubshapeB, sample.IsSensor,
            kind != PhysicsContactKind.End, kind == PhysicsContactKind.End ? default : sample.Point, kind == PhysicsContactKind.End ? default : sample.Normal,
            kind == PhysicsContactKind.End ? 0 : sample.Depth, reason);
    private void AddEvent(PhysicsContactEvent value)
    {
        if (_workingCount == _capacity) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ContactOverflow, "Contacts.Events");
        _working[_workingCount++] = value;
    }
    private static bool Preferred(ContactSample a, ContactSample b)
    {
        if (a.Depth != b.Depth) return a.Depth > b.Depth;
        var order = a.Point.X.CompareTo(b.Point.X); if (order != 0) return order < 0;
        order = a.Point.Y.CompareTo(b.Point.Y); return order != 0 ? order < 0 : a.Point.Z < b.Point.Z;
    }
    internal int Publish(BepuWorld world, ulong tick)
    {
        if (Overflow) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ContactOverflow, "Contacts.CallbackSamples");
        for (var i = 0; i < _pendingCount; i++) AddEvent(_pending[i] with { Tick = tick });
        for (var i = 0; i < _rawCount; i++)
        {
            var sample = _raw[i];
            if (_current.TryGetValue(sample.Key, out var prior)) { if (Preferred(sample, prior)) _current[sample.Key] = sample; }
            else
            {
                if (_current.Count == _capacity) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ContactOverflow, "Contacts.ActivePairs");
                _current.Add(sample.Key, sample);
            }
        }
        foreach (var pair in _active)
        {
            if (_current.TryGetValue(pair.Key, out var current))
            {
                if (!world.PairSleeping(pair.Key)) AddEvent(Event(current, tick, PhysicsContactKind.Stay));
            }
            else if (world.PairSleeping(pair.Key))
            {
                if (_current.Count == _capacity) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ContactOverflow, "Contacts.SleepingPairs");
                _current.Add(pair.Key, pair.Value);
            }
            else AddEvent(Event(pair.Value, tick, PhysicsContactKind.End, PhysicsContactEndReason.Separated));
        }
        foreach (var pair in _current) if (!_active.ContainsKey(pair.Key)) AddEvent(Event(pair.Value, tick, PhysicsContactKind.Begin));
        Array.Sort(_working, 0, _workingCount, EventComparer.Instance);
        (_active, _current) = (_current, _active); (_published, _working) = (_working, _published);
        _publishedCount = _workingCount; _pendingCount = 0; return _publishedCount;
    }
    internal int Copy(Span<PhysicsContactEvent> output)
    {
        if (output.Length < _publishedCount) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.CapacityExceeded, "Contacts.Output");
        _published.AsSpan(0, _publishedCount).CopyTo(output); return _publishedCount;
    }
    internal void Clear()
    {
        _active.Clear(); _current.Clear(); Array.Clear(_raw); Array.Clear(_pending); Array.Clear(_published); Array.Clear(_working); Array.Clear(_invalidated);
        _rawCount = _pendingCount = _publishedCount = _workingCount = 0;
    }
    private sealed class EventComparer : IComparer<PhysicsContactEvent>
    {
        internal static readonly EventComparer Instance = new();
        public int Compare(PhysicsContactEvent a, PhysicsContactEvent b)
        {
            var order = new ContactKey(a.BodyA, a.BodyB, a.SubshapeA, a.SubshapeB).CompareTo(new(b.BodyA, b.BodyB, b.SubshapeA, b.SubshapeB));
            return order != 0 ? order : Rank(a.Kind).CompareTo(Rank(b.Kind));
        }
        private static int Rank(PhysicsContactKind kind) => kind == PhysicsContactKind.End ? 0 : kind == PhysicsContactKind.Begin ? 1 : 2;
    }
}
