using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Trees;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    private int QueryCapacity(int outputLength, PhysicsQueryFilter filter)
    {
        var capacity = Math.Min(outputLength, Options.Limits.MaxQueryHits);
        if (_queryHits.Length < Math.Max(1, capacity)) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ScratchLimitExceeded, "Query.Scratch");
        long traversal = 0;
        for (var i = 0; i < Bodies.Capacity; i++)
        {
            if (Bodies[i] is not { } body || !Included(body, filter) || body.Shape.Definition is not PhysicsStaticMesh mesh) continue;
            // Exact beta.33 tree traversal starts with stackalloc int[256], then doubles a pooled int stack.
            // Bound worst-case live payload, including both buffers temporarily live during pool.Resize.
            var leaves = mesh.Indices.Length / 3; var power = 256;
            while (power < leaves) power *= 2;
            traversal = Math.Max(traversal, leaves <= 256 ? 1024 : 1024L + power * 6L);
        }
        if ((long)_queryHits.Length * Unsafe.SizeOf<PhysicsHit>() + traversal > Options.Limits.MaxQueryScratchBytes)
            throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ScratchLimitExceeded, "Query.BvhScratch");
        return capacity;
    }
    private void ValidateFilter(PhysicsQueryFilter filter)
    {
        if (filter.ExcludeBody != default) Body(filter.ExcludeBody);
    }
    private static bool Included(BodyEntry body, PhysicsQueryFilter filter) => body.Id != filter.ExcludeBody
        && (filter.LayerMask & (1u << body.Description.Filter.Layer)) != 0 && (filter.IncludeSensors || !body.Description.IsSensor);
    private static int CompareHit(PhysicsHit a, PhysicsHit b)
    {
        var order = a.Distance.CompareTo(b.Distance); if (order != 0) return order;
        order = a.Body.Slot.CompareTo(b.Body.Slot); if (order != 0) return order;
        order = a.Body.Generation.CompareTo(b.Body.Generation); return order != 0 ? order : a.Subshape.CompareTo(b.Subshape);
    }
    private void InsertHit(PhysicsHit hit, int capacity, ref int count, ref bool truncated)
    {
        var position = 0; while (position < count && CompareHit(_queryHits[position], hit) <= 0) position++;
        if (count == capacity) { truncated = true; if (position == capacity) return; }
        else count++;
        for (var i = count - 1; i > position; i--) _queryHits[i] = _queryHits[i - 1];
        _queryHits[position] = hit;
    }
    public bool RaycastNearest(PhysicsRay query, out PhysicsHit hit)
    {
        Enter();
        try
        {
            PhysicsValidation.ValidateRay(Id, query).ThrowIfFailure(); ValidateFilter(query.Filter); QueryCapacity(1, query.Filter);
            var result = CastRay(query, 1); Resources.Sample(); hit = result.WrittenCount == 0 ? default : _queryHits[0]; return result.WrittenCount != 0;
        }
        finally { Exit(); }
    }
    public PhysicsQueryResult Raycast(PhysicsRay query, Span<PhysicsHit> output)
    {
        Enter();
        try
        {
            PhysicsValidation.ValidateRay(Id, query).ThrowIfFailure(); ValidateFilter(query.Filter); var capacity = QueryCapacity(output.Length, query.Filter);
            var result = CastRay(query, capacity); Resources.Sample(); _queryHits.AsSpan(0, result.WrittenCount).CopyTo(output); return result;
        }
        finally { Exit(); }
    }
    private PhysicsQueryResult CastRay(PhysicsRay query, int capacity)
    {
        var count = 0; var truncated = false;
        for (var i = 0; i < Bodies.Capacity; i++)
        {
            if (Bodies[i] is not { } body || !Included(body, query.Filter)) continue;
            var state = Read(body); var pose = new RigidPose(state.Pose.Position, state.Pose.Orientation);
            if (body.Shape.Definition is PhysicsStaticMesh)
            {
                var handler = new MeshRayHandler(this, body, query, capacity) { Count = count, Truncated = truncated };
                var ray = new RayData { Origin = query.Origin, Direction = query.Direction }; var maximum = query.MaximumDistance;
                ref var mesh = ref Simulation.Shapes.GetShape<Mesh>(body.Shape.Backend.Index);
                mesh.RayTest(pose, ray, ref maximum, Resources.Pool, ref handler);
                count = handler.Count; truncated = handler.Truncated;
            }
            else if (ContainsPoint(body.Shape.Definition, pose, query.Origin))
                InsertHit(new(body.Id, body.Shape.Id, -1, 0, default, default, true, false), capacity, ref count, ref truncated);
            else switch (body.Shape.Definition)
            {
                case PhysicsSphere s: RayShape(new Sphere(s.Radius), pose, query, body, -1, capacity, ref count, ref truncated); break;
                case PhysicsBox b: RayShape(new Box(b.Size.X, b.Size.Y, b.Size.Z), pose, query, body, -1, capacity, ref count, ref truncated); break;
                case PhysicsCapsule c: RayShape(new Capsule(c.Radius, c.CylinderLength), pose, query, body, -1, capacity, ref count, ref truncated); break;
            }
        }
        return new(count, truncated);
    }
    private struct MeshRayHandler(BepuWorld world, BodyEntry body, PhysicsRay query, int capacity) : IShapeRayHitHandler
    {
        internal int Count;
        internal bool Truncated;
        public bool AllowTest(int childIndex) => true;
        public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, int childIndex)
        {
            // Do not shorten maximumT: truncation must still observe farther hits.
            if (t >= 0 && t <= query.MaximumDistance)
                world.InsertHit(new(body.Id, body.Shape.Id, childIndex, t, query.Origin + query.Direction * t, normal, false, true), capacity, ref Count, ref Truncated);
        }
    }
    private void RayShape<T>(T shape, RigidPose pose, PhysicsRay query, BodyEntry body, int child, int capacity, ref int count, ref bool truncated) where T : unmanaged, IConvexShape
    {
        if (shape.RayTest(pose, query.Origin, query.Direction, out var distance, out var normal) && distance >= 0 && distance <= query.MaximumDistance)
            InsertHit(new(body.Id, body.Shape.Id, child, distance, query.Origin + query.Direction * distance, normal, false, true), capacity, ref count, ref truncated);
    }
    private static bool ContainsPoint(PhysicsShapeDefinition shape, RigidPose pose, Vector3 point)
    {
        var local = Vector3.Transform(point - pose.Position, Quaternion.Inverse(pose.Orientation));
        return shape switch
        {
            PhysicsSphere s => local.LengthSquared() <= s.Radius * s.Radius,
            PhysicsBox b => MathF.Abs(local.X) <= b.Size.X * 0.5f && MathF.Abs(local.Y) <= b.Size.Y * 0.5f && MathF.Abs(local.Z) <= b.Size.Z * 0.5f,
            PhysicsCapsule c => new Vector3(local.X, local.Y - Math.Clamp(local.Y, -c.CylinderLength * 0.5f, c.CylinderLength * 0.5f), local.Z).LengthSquared() <= c.Radius * c.Radius,
            _ => false
        };
    }
    public PhysicsQueryResult Sweep(PhysicsSweep query, Span<PhysicsHit> output)
    {
        Enter();
        try
        {
            if (query is null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Query");
            var shape = Shape(query.Shape); PhysicsValidation.ValidateSweep(Id, query, shape.Definition, Options.Limits).ThrowIfFailure();
            ValidateFilter(query.Filter); var capacity = QueryCapacity(output.Length, query.Filter);
            var result = CastShape(shape, query.Pose, query.Direction, query.MaximumDistance, query.Filter, capacity);
            Resources.Sample();
            _queryHits.AsSpan(0, result.WrittenCount).CopyTo(output); return result;
        }
        finally { Exit(); }
    }
    public PhysicsQueryResult Overlap(PhysicsOverlap query, Span<PhysicsOverlapHit> output)
    {
        Enter();
        try
        {
            if (query is null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Query");
            var shape = Shape(query.Shape); PhysicsValidation.ValidateOverlap(Id, query, shape.Definition, Options.Limits).ThrowIfFailure();
            ValidateFilter(query.Filter); var capacity = QueryCapacity(output.Length, query.Filter);
            var result = CastShape(shape, query.Pose, Vector3.Zero, 1, query.Filter, capacity);
            Resources.Sample();
            for (var i = 0; i < result.WrittenCount; i++) { var hit = _queryHits[i]; output[i] = new(hit.Body, hit.Shape, hit.Subshape); }
            return result;
        }
        finally { Exit(); }
    }
    private PhysicsQueryResult CastShape(ShapeEntry source, PhysicsPose pose, Vector3 direction, float maximum, PhysicsQueryFilter filter, int capacity)
        => source.Definition switch
        {
            PhysicsSphere s => CastConvex(new Sphere(s.Radius), pose, direction, maximum, filter, capacity),
            PhysicsBox b => CastConvex(new Box(b.Size.X, b.Size.Y, b.Size.Z), pose, direction, maximum, filter, capacity),
            PhysicsCapsule c => CastConvex(new Capsule(c.Radius, c.CylinderLength), pose, direction, maximum, filter, capacity),
            _ => throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.UnsupportedGeometry, "Query.Shape")
        };
    private PhysicsQueryResult CastConvex<T>(T source, PhysicsPose pose, Vector3 direction, float maximum, PhysicsQueryFilter filter, int capacity) where T : unmanaged, IConvexShape
    {
        var count = 0; var truncated = false;
        for (var i = 0; i < Bodies.Capacity; i++)
        {
            if (Bodies[i] is not { } body || !Included(body, filter)) continue;
            var state = Read(body); var targetPose = new RigidPose(state.Pose.Position, state.Pose.Orientation);
            switch (body.Shape.Definition)
            {
                case PhysicsSphere s: SweepPair(source, pose, direction, maximum, new Sphere(s.Radius), targetPose, body, -1, capacity, ref count, ref truncated); break;
                case PhysicsBox b: SweepPair(source, pose, direction, maximum, new Box(b.Size.X, b.Size.Y, b.Size.Z), targetPose, body, -1, capacity, ref count, ref truncated); break;
                case PhysicsCapsule c: SweepPair(source, pose, direction, maximum, new Capsule(c.Radius, c.CylinderLength), targetPose, body, -1, capacity, ref count, ref truncated); break;
                case PhysicsStaticMesh mesh:
                    source.ComputeBounds(pose.Orientation, out var min, out var max);
                    min += pose.Position + Vector3.Min(Vector3.Zero, direction * maximum); max += pose.Position + Vector3.Max(Vector3.Zero, direction * maximum);
                    var inverse = Quaternion.Inverse(targetPose.Orientation); var center = Vector3.Transform((min + max) * 0.5f - targetPose.Position, inverse); var extent = (max - min) * 0.5f;
                    var localExtent = Vector3.Abs(Vector3.Transform(new(extent.X, 0, 0), inverse))
                        + Vector3.Abs(Vector3.Transform(new(0, extent.Y, 0), inverse)) + Vector3.Abs(Vector3.Transform(new(0, 0, extent.Z), inverse));
                    // Conservative broadphase padding only; exact narrowphase still determines every returned hit.
                    localExtent += new Vector3(0.001f) + (Vector3.Abs(center) + localExtent) * 1e-6f;
                    var visitor = new MeshSweepVisitor<T>(this, source, pose, direction, maximum, body, targetPose, mesh, capacity) { Count = count, Truncated = truncated };
                    ref var backend = ref Simulation.Shapes.GetShape<Mesh>(body.Shape.Backend.Index);
                    backend.Tree.GetOverlaps(center - localExtent, center + localExtent, Resources.Pool, ref visitor);
                    count = visitor.Count; truncated = visitor.Truncated;
                    break;
            }
        }
        return new(count, truncated);
    }
    private struct MeshSweepVisitor<T>(BepuWorld world, T source, PhysicsPose pose, Vector3 direction, float maximum,
        BodyEntry body, RigidPose targetPose, PhysicsStaticMesh mesh, int capacity) : BepuUtilities.IBreakableForEach<int> where T : unmanaged, IConvexShape
    {
        internal int Count;
        internal bool Truncated;
        public bool LoopBody(int child)
        {
            var vertices = mesh.Vertices.Span; var indices = mesh.Indices.Span;
            var triangle = new Triangle(vertices[indices[child * 3]], vertices[indices[child * 3 + 1]], vertices[indices[child * 3 + 2]]);
            var front = Vector3.Transform(-Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A), targetPose.Orientation);
            if (Vector3.Dot(pose.Position - targetPose.Position - Vector3.Transform(triangle.A, targetPose.Orientation), front) >= 0)
                world.SweepPair(source, pose, direction, maximum, triangle, targetPose, body, child, capacity, ref Count, ref Truncated);
            return true;
        }
    }
    private struct SweepFilter : ISweepFilter { public bool AllowTest(int childA, int childB) => true; }
    private unsafe void SweepPair<TA, TB>(TA a, PhysicsPose pose, Vector3 direction, float maximum, TB b, RigidPose targetPose, BodyEntry body, int child,
        int capacity, ref int count, ref bool truncated) where TA : unmanaged, IConvexShape where TB : unmanaged, IConvexShape
    {
        var task = Simulation.NarrowPhase.SweepTaskRegistry.GetTask(TA.TypeId, TB.TypeId);
        if (task is null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.UnsupportedGeometry, "Query.ShapePair");
        var filter = new SweepFilter(); var velocity = new BodyVelocity(direction); var stationary = new BodyVelocity();
        if (task.Sweep(Unsafe.AsPointer(ref a), TA.TypeId, pose.Orientation, velocity, Unsafe.AsPointer(ref b), TB.TypeId,
            targetPose.Position - pose.Position, targetPose.Orientation, stationary, maximum, 1e-6f, 1e-6f, 32,
            ref filter, Simulation.Shapes, Simulation.NarrowPhase.SweepTaskRegistry, Resources.Pool, out _, out var t, out var point, out var normal))
        {
            var inside = t <= 0;
            if (t > maximum) return;
            if (!float.IsFinite(t) || (!inside && (!Finite(point) || !Finite(normal))))
                throw Fault("Query.NumericResult", new InvalidOperationException("Nonfinite sweep result."));
            InsertHit(new(body.Id, body.Shape.Id, child, inside ? 0 : t, inside ? default : point + pose.Position,
                inside ? default : normal, inside, !inside), capacity, ref count, ref truncated);
        }
    }
}
