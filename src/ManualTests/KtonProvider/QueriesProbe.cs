using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace KtonProviderProbe;

internal static partial class Program
{
    private static void QueryAcceptance()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = factory.Create(Options(limits: new() { MaxQueryHits = 2 })))
        {
            var shape = world.CreateShape(new PhysicsBox(new(2, 2, 2)));
            var far = world.CreateBody(new(shape, PhysicsBodyMotion.Static, new(new(0, -5, 0), Quaternion.Identity)) { Mass = 0 });
            var near = world.CreateBody(new(shape, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            var ray = new PhysicsRay(new(0, 5, 0), -Vector3.UnitY, 20);
            Check("ray-nearest", world.RaycastNearest(ray, out var hit) && hit.Body == near); Near("ray-distance-metres", hit.Distance, 4);
            var hits = new PhysicsHit[4]; var sentinel = new PhysicsHit(default, default, 99, 99, default, default, false, false); Array.Fill(hits, sentinel);
            var result = world.Raycast(ray, hits); Check("ray-sorted-independent-of-body-order", result.WrittenCount == 2 && !result.Truncated && hits[0].Body == near && hits[1].Body == far);
            Check("query-output-tail-untouched", hits[2] == sentinel && hits[3] == sentinel);
            var prefix = new PhysicsHit[1]; result = world.Raycast(ray, prefix); Check("nearest-prefix-and-truncation", result == new PhysicsQueryResult(1, true) && prefix[0].Body == near);
            Check("empty-output-observes-existing-hits", world.Raycast(ray, Span<PhysicsHit>.Empty) == new PhysicsQueryResult(0, true));
            Check("ray-miss-empty-output", world.Raycast(ray with { Origin = new(100, 5, 0) }, Span<PhysicsHit>.Empty) == new PhysicsQueryResult(0, false));
            Check("ray-start-inside-convex", world.RaycastNearest(ray with { Origin = Vector3.Zero }, out hit) && hit.Body == near && hit.Distance == 0 && hit.StartedInside && !hit.HasNormal);
            var queryShape = world.CreateShape(new PhysicsSphere(0.5f));
            result = world.Sweep(new(queryShape, new(new(0, 5, 0), Quaternion.Identity), -Vector3.UnitY, 20), hits);
            Check("convex-sweep-hits-both", result.WrittenCount == 2 && !result.Truncated && hits[0].Body == near); Near("convex-sweep-distance", hits[0].Distance, 3.5f, 0.001f);
            result = world.Sweep(new(queryShape, PhysicsPose.Identity, Vector3.UnitY, 1), hits);
            Check("sweep-initial-overlap", result.WrittenCount == 1 && hits[0].StartedInside && !hits[0].HasNormal && hits[0].Distance == 0);
            var overlaps = new PhysicsOverlapHit[4]; result = world.Overlap(new(queryShape, PhysicsPose.Identity), overlaps);
            Check("exact-overlap", result.WrittenCount == 1 && overlaps[0].Body == near);
            Check("overlap-rejects-aabb-corner", world.Overlap(new(queryShape, new(new(1.4f, 1.4f, 0), Quaternion.Identity)), overlaps).WrittenCount == 0);
            var filtered = ray with { Filter = new(uint.MaxValue, true, near) }; Check("live-body-query-exclusion", world.RaycastNearest(filtered, out hit) && hit.Body == far);
            world.RemoveBody(near); var replacement = world.CreateBody(new(shape, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            Reject("stale-query-exclusion-after-reuse", PhysicsErrorCode.StaleIdentifier, () => world.Raycast(filtered, hits));
            Check("query-returns-new-generation", world.RaycastNearest(ray, out hit) && hit.Body == replacement && hit.Body != near);
            using var foreign = factory.Create(Options()); var foreignBody = Ball(foreign, Vector3.Zero);
            var saved = hits.ToArray();
            Reject("foreign-query-exclusion", PhysicsErrorCode.ForeignWorld, () => world.Raycast(ray with { Filter = new(uint.MaxValue, true, foreignBody) }, hits));
            Check("query-validation-error-leaves-output-untouched", hits.SequenceEqual(saved));
        }
        using (var world = factory.Create(Options(limits: new() { MaxQueryHits = 1, MaxQueryScratchBytes = 1 })))
        {
            var output = new[] { new PhysicsHit(default, default, 77, 77, default, default, false, false) }; var before = output[0];
            Reject("scratch-budget-explicit-error", PhysicsErrorCode.ScratchLimitExceeded, () => world.Raycast(new(Vector3.Zero, Vector3.UnitX, 1), output));
            Check("scratch-error-no-output-or-world-mutation", output[0] == before && world.State == PhysicsWorldState.Ready);
        }
        QueryMatrixAndMesh(factory);
        QueryFiltersAndBounds(factory);
        MeshBvhAndBudget(factory);
        Clean("query-worlds-clean", factory);
    }
    private static void MeshBvhAndBudget(BepuPhysicsWorldFactory factory)
    {
        using (var world = factory.Create(Options(limits: new() { MaxQueryHits = 1, MaxQueryScratchBytes = System.Runtime.CompilerServices.Unsafe.SizeOf<PhysicsHit>() })))
        {
            var mesh = world.CreateShape(new PhysicsStaticMesh(new Vector3[] { new(-1, 0, -1), new(1, 0, -1), new(-1, 0, 1) }, new[] { 0, 1, 2 }));
            world.CreateBody(new(mesh, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 }); var hits = new[] { new PhysicsHit(default, default, 55, 55, default, default, false, false) };
            var saved = hits[0]; var ray = new PhysicsRay(new(-0.2f, 2, -0.2f), -Vector3.UnitY, 5);
            Reject("bvh-stack-included-in-scratch-reservation", PhysicsErrorCode.ScratchLimitExceeded, () => world.Raycast(ray, hits));
            Check("bvh-reservation-error-before-output", world.State == PhysicsWorldState.Ready && hits[0] == saved);
            Check("filtered-mesh-does-not-reserve-bvh-stack", world.Raycast(ray with { Filter = new(0, true) }, hits).WrittenCount == 0);
        }
        using (var world = factory.Create(Options()))
        {
            const int width = 17; var vertices = new Vector3[width * width]; var indices = new int[16 * 16 * 6];
            for (var z = 0; z < width; z++) for (var x = 0; x < width; x++) vertices[z * width + x] = new(x - 8, 0, z - 8);
            var cursor = 0;
            for (var z = 0; z < 16; z++) for (var x = 0; x < 16; x++)
            { var a = z * width + x; indices[cursor++] = a; indices[cursor++] = a + 1; indices[cursor++] = a + width; indices[cursor++] = a + 1; indices[cursor++] = a + width + 1; indices[cursor++] = a + width; }
            var mesh = world.CreateShape(new PhysicsStaticMesh(vertices, indices)); var body = world.CreateBody(new(mesh, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            var queryShape = world.CreateShape(new PhysicsSphere(0.25f)); var ray = new PhysicsRay(new(0.23f, 5, 0.17f), -Vector3.UnitY, 10);
            var sweep = new PhysicsSweep(queryShape, new(ray.Origin, Quaternion.Identity), ray.Direction, 10); var hits = new PhysicsHit[8];
            Check("bvh-many-triangles-ray-identity", world.RaycastNearest(ray, out var hit) && hit.Body == body && hit.Subshape == 272);
            Check("bvh-many-triangles-sweep", world.Sweep(sweep, hits).WrittenCount > 0); Near("bvh-many-triangles-sweep-distance", hits[0].Distance, 4.75f, 0.002f);
            for (var i = 0; i < 100; i++) { world.Raycast(ray, hits); world.Sweep(sweep, hits); }
            var before = ((BepuWorld)world).Resources.PoolBytes; var allocated = GC.GetTotalAllocatedBytes(true);
            for (var i = 0; i < 1000; i++) { world.Raycast(ray, hits); world.Sweep(sweep, hits); }
            allocated = GC.GetTotalAllocatedBytes(true) - allocated;
            Check("bvh-repeated-query-pool-plateau", ((BepuWorld)world).Resources.PoolBytes == before);
            Check("bvh-repeated-query-allocation-bounded", allocated <= 4096, allocated);
            Metrics["MeshQueries"] = new { Triangles = 512, Calls = 2000, ManagedAllocationDeltaBytes = allocated, PoolBytesBefore = before, PoolBytesAfter = ((BepuWorld)world).Resources.PoolBytes };
        }
    }
    private static void QueryMatrixAndMesh(BepuPhysicsWorldFactory factory)
    {
        PhysicsShapeDefinition[] definitions = [new PhysicsSphere(0.5f), new PhysicsBox(Vector3.One), new PhysicsCapsule(0.25f, 0.5f)];
        for (var target = 0; target < definitions.Length; target++)
        for (var source = 0; source < definitions.Length; source++)
        {
            using var world = factory.Create(Options()); var shape = world.CreateShape(definitions[target]);
            var body = world.CreateBody(new(shape, PhysicsBodyMotion.Static, PhysicsPose.Identity) { Mass = 0 });
            var queryShape = world.CreateShape(definitions[source]); var hits = new PhysicsHit[4];
            var result = world.Sweep(new(queryShape, new(new(0, 5, 0), Quaternion.Identity), -Vector3.UnitY, 10), hits);
            Check($"shape-pair-sweep-{source}-{target}", result.WrittenCount == 1 && hits[0].Body == body && hits[0].HasNormal && hits[0].Normal.Y > 0.9f);
            Near($"shape-pair-distance-{source}-{target}", hits[0].Distance, 4, 0.002f);
            Check($"shape-pair-overlap-{source}-{target}", world.Overlap(new(queryShape, PhysicsPose.Identity), new PhysicsOverlapHit[4]).WrittenCount == 1);
            Check($"sweep-distance-bounded-{source}-{target}", world.Sweep(new(queryShape, new(new(0, 5, 0), Quaternion.Identity), -Vector3.UnitY, 1), hits).WrittenCount == 0);
        }
        using (var world = factory.Create(Options()))
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
            world.CreateBody(new(world.CreateShape(new PhysicsBox(new(2, 1, 1))), PhysicsBodyMotion.Static, new(Vector3.Zero, rotation)) { Mass = 0 });
            Check("rotated-convex-ray", world.RaycastNearest(new(new(0, 5, 0), -Vector3.UnitY, 10), out var hit)); Near("rotated-box-full-extents", hit.Distance, 4, 0.002f);
            var shape = world.CreateShape(new PhysicsCapsule(0.25f, 1)); var hits = new PhysicsHit[2];
            Check("rotated-capsule-sweep", world.Sweep(new(shape, new(new(0, 5, 0), rotation), -Vector3.UnitY, 10), hits).WrittenCount == 1);
            Near("rotated-capsule-axis-convention", hits[0].Distance, 3.75f, 0.002f);
        }
        using (var world = factory.Create(Options(limits: new() { MaxQueryHits = 2 })))
        {
            var mesh = world.CreateShape(new PhysicsStaticMesh(new Vector3[] { new(-10, 0, -10), new(10, 0, -10), new(-10, 0, 10), new(10, 0, 10) }, new[] { 0, 1, 2, 1, 3, 2 }));
            var floor = world.CreateBody(new(mesh, PhysicsBodyMotion.Static, new(new(0, 2, 0), Quaternion.Identity)) { Mass = 0 });
            var ray = new PhysicsRay(new(-2, 5, -2), -Vector3.UnitY, 10); var hits = new PhysicsHit[4];
            Check("mesh-ray-front-triangle", world.RaycastNearest(ray, out var hit) && hit.Body == floor && hit.Subshape == 0 && hit.Normal.Y > 0.9f); Near("mesh-ray-translated-pose", hit.Distance, 3);
            Check("mesh-ray-backface-cull", !world.RaycastNearest(new(new(-2, 0, -2), Vector3.UnitY, 10), out _));
            var result = world.Raycast(ray with { Origin = new(0, 5, 0) }, hits);
            Check("mesh-shared-edge-subshape-ties", result.WrittenCount == 2 && hits[0].Subshape == 0 && hits[1].Subshape == 1);
            var queryShape = world.CreateShape(new PhysicsSphere(0.25f));
            result = world.Sweep(new(queryShape, new(new(-2, 5, -2), Quaternion.Identity), -Vector3.UnitY, 10), hits);
            Check("mesh-sweep-triangle-identity", result.WrittenCount == 1 && hits[0].Body == floor && hits[0].Subshape == 0 && hits[0].Normal.Y > 0.9f);
            Near("mesh-sweep-distance", hits[0].Distance, 2.75f, 0.002f);
            Check("mesh-sweep-backface-cull", world.Sweep(new(queryShape, new(new(-2, 0, -2), Quaternion.Identity), Vector3.UnitY, 10), hits).WrittenCount == 0);
            Check("mesh-front-overlap", world.Overlap(new(queryShape, new(new(-2, 2.1f, -2), Quaternion.Identity)), new PhysicsOverlapHit[4]).WrittenCount == 1);
            Check("mesh-back-overlap-cull", world.Overlap(new(queryShape, new(new(-2, 1.9f, -2), Quaternion.Identity)), new PhysicsOverlapHit[4]).WrittenCount == 0);
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, floor, Pose: new(new(3, 0, 0), rotation))]);
            Check("rotated-mesh-ray", world.RaycastNearest(new(new(-2, -2, -2), Vector3.UnitX, 10), out hit) && hit.Body == floor && hit.Normal.X < -0.9f);
            Near("rotated-mesh-distance", hit.Distance, 5, 0.002f);
            Reject("mesh-cannot-be-query-shape", PhysicsErrorCode.UnsupportedGeometry, () => world.Overlap(new(mesh, PhysicsPose.Identity), new PhysicsOverlapHit[1]));
        }
    }
    private static void QueryFiltersAndBounds(BepuPhysicsWorldFactory factory)
    {
        using var world = factory.Create(Options(limits: new() { MaxQueryHits = 2 })); var shape = world.CreateShape(new PhysicsSphere(1));
        var first = world.CreateBody(new(shape, PhysicsBodyMotion.Static, new(new(0, 0, -0.5f), Quaternion.Identity)) { Mass = 0, IsSensor = true, Filter = new(3, 0) });
        var second = world.CreateBody(new(shape, PhysicsBodyMotion.Static, new(new(0, 0, 0.5f), Quaternion.Identity)) { Mass = 0, Filter = new(7, 0) });
        var ray = new PhysicsRay(new(0, 5, 0), -Vector3.UnitY, 20); var hits = new PhysicsHit[4];
        world.Raycast(ray, hits); Check("equal-distance-tie-body-slot", hits[0].Body == first && hits[1].Body == second && hits[0].Distance == hits[1].Distance);
        Check("query-sensor-filter", world.RaycastNearest(ray with { Filter = new(uint.MaxValue, false) }, out var hit) && hit.Body == second);
        Check("query-layer-independent-of-body-mask", world.RaycastNearest(ray with { Filter = new(1u << 3, true) }, out hit) && hit.Body == first);
        Check("query-layer-zero-mask", world.Raycast(ray with { Filter = new(0, true) }, hits).WrittenCount == 0);
        world.CreateBody(new(shape, PhysicsBodyMotion.Static, new(new(0, -5, 0), Quaternion.Identity)) { Mass = 0 });
        var result = world.Raycast(ray, hits); Check("max-hits-ceiling-with-large-output", result == new PhysicsQueryResult(2, true));
        var queryShape = world.CreateShape(new PhysicsSphere(1)); var overlaps = new PhysicsOverlapHit[1];
        result = world.Overlap(new(queryShape, PhysicsPose.Identity), overlaps); Check("overlap-truncation-identity-order", result == new PhysicsQueryResult(1, true) && overlaps[0].Body == first);
        world.RemoveShape(queryShape); Reject("stale-query-shape", PhysicsErrorCode.StaleIdentifier, () => world.Sweep(new(queryShape, PhysicsPose.Identity, Vector3.UnitY, 1), hits));
        // Repeated queries exercise actual narrowphase tasks, bounded output and pool stability after warmup.
        queryShape = world.CreateShape(new PhysicsCapsule(0.2f, 0.4f)); var overlapQuery = new PhysicsOverlap(queryShape, PhysicsPose.Identity);
        for (var i = 0; i < 100; i++) world.Overlap(overlapQuery, overlaps);
        var before = ((BepuWorld)world).Resources.PoolBytes; var allocated = GC.GetTotalAllocatedBytes(true);
        for (var i = 0; i < 1000; i++) world.Overlap(overlapQuery, overlaps);
        allocated = GC.GetTotalAllocatedBytes(true) - allocated;
        Check("repeated-query-pools-do-not-grow", ((BepuWorld)world).Resources.PoolBytes == before);
        Check("repeated-query-managed-allocation-bounded", allocated <= 4096, allocated);
        Metrics["Queries"] = new { Calls = 1000, ManagedAllocationDeltaBytes = allocated, PoolBytesBefore = before, PoolBytesAfter = ((BepuWorld)world).Resources.PoolBytes };
    }
}
