using System.Numerics;
using BepuPhysics.CollisionDetection;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    internal bool PairSleeping(ContactKey key)
    {
        var a = Bodies[key.A.Slot]; var b = Bodies[key.B.Slot];
        return a is not null && b is not null && a.Id == key.A && b.Id == key.B
            && a.Description.Filter.Allows(b.Description.Filter) && !DisabledPairs.ContainsKey(Pair(a.Id.Slot, b.Id.Slot))
            && !Read(a).IsAwake && !Read(b).IsAwake;
    }
    internal void RecordContact<T>(CollidablePair pair, ref T manifold, int childA = -1, int childB = -1) where T : unmanaged, IContactManifold<T>
    {
        if (!ByBackend.TryGetValue(pair.A.Packed, out var a) || !ByBackend.TryGetValue(pair.B.Packed, out var b)) { CallbackFailed = true; return; }
        var meshA = a.Shape.Definition is PhysicsStaticMesh; var meshB = b.Shape.Definition is PhysicsStaticMesh;
        if ((meshA || meshB) && childA == -1 && childB == -1) return; // Mesh contacts use the child callback, where triangle identity is explicit.
        if (!meshA) childA = -1; if (!meshB) childB = -1;
        var flipped = a.Id.Slot > b.Id.Slot || (a.Id.Slot == b.Id.Slot && a.Id.Generation > b.Id.Generation);
        var key = flipped ? new ContactKey(b.Id, a.Id, childB, childA) : new ContactKey(a.Id, b.Id, childA, childB);
        var position = Read(a).Pose.Position;
        var found = false; var bestDepth = float.NegativeInfinity; var bestPoint = Vector3.Zero; var bestNormal = Vector3.Zero;
        for (var i = 0; i < manifold.Count; i++)
        {
            manifold.GetContact(i, out var offset, out var normal, out var depth, out _);
            if (!float.IsFinite(depth) || !Finite(offset) || !Finite(normal)) { CallbackFailed = true; continue; }
            if (depth < 0) continue; // Speculative-only contacts never create gameplay Begin.
            var point = position + offset;
            if (!found || depth > bestDepth || (depth == bestDepth && PointBefore(point, bestPoint)))
            { found = true; bestDepth = depth; bestPoint = point; bestNormal = flipped ? normal : -normal; }
        }
        if (found) Contacts.Record(new(key, a.Description.IsSensor || b.Description.IsSensor, bestPoint, bestNormal, bestDepth));
    }
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool PointBefore(Vector3 a, Vector3 b) => a.X < b.X || (a.X == b.X && (a.Y < b.Y || (a.Y == b.Y && a.Z < b.Z)));
    public int CopyContactEvents(Span<PhysicsContactEvent> output)
    {
        Enter(); try { return Contacts.Copy(output); } finally { Exit(); }
    }
}
