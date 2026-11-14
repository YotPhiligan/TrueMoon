using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;

namespace KtonResearch;

internal struct NearestRay : IRayHitHandler
{
    internal bool Hit;
    internal float T;
    internal CollidableReference Collidable;
    internal int? OnlyBody;
    public readonly bool AllowTest(CollidableReference c) => !OnlyBody.HasValue || (c.Mobility != CollidableMobility.Static && c.BodyHandle.Value == OnlyBody.Value);
    public readonly bool AllowTest(CollidableReference c, int childIndex) => true;
    public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, CollidableReference collidable, int childIndex)
    { Hit = true; T = t; Collidable = collidable; maximumT = t; }
}

internal struct SweepHits : ISweepHitHandler
{
    internal bool Hit;
    internal float T;
    internal int ZeroCount;
    internal CollidableReference Collidable;
    public readonly bool AllowTest(CollidableReference c) => true;
    public readonly bool AllowTest(CollidableReference c, int childIndex) => true;
    public void OnHit(ref float maximumT, float t, Vector3 location, Vector3 normal, CollidableReference c)
    { Hit = true; T = t; Collidable = c; maximumT = t; }
    public void OnHitAtZeroT(ref float maximumT, CollidableReference c)
    { Hit = true; T = 0; ZeroCount++; Collidable = c; maximumT = 0; }
}
