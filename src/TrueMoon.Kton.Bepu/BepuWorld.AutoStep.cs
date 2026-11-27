using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    private void TryAutoStep(CharacterEntry entry)
    {
        var body = entry.Body;
        var reference = Simulation.Bodies[body.Handle];
        ref var controller = ref Resources.Characters!.GetCharacterByBodyHandle(body.Handle);
        var target = controller.TargetVelocity;
        if (controller.TryJump || target == Vector2.Zero || !reference.Awake || !controller.Supported ||
            controller.SupportNormal.Y < .99f || MathF.Abs(reference.Velocity.Linear.Y) > .25f ||
            !ByBackend.TryGetValue(controller.Support.Packed, out var support) ||
            !TryGetStepBox(support, out var supportMin, out var supportMax)) return;

        var capsule = (PhysicsCapsule)body.Shape.Definition;
        var halfHeight = capsule.Radius + capsule.CylinderLength * .5f;
        var pose = reference.Pose;
        var start = pose.Position;
        if (start.X < supportMin.X || start.X > supportMax.X || start.Z < supportMin.Z || start.Z > supportMax.Z ||
            MathF.Abs(start.Y - halfHeight - supportMax.Y) > .005f) return;

        var speed = target.Length();
        if (speed == 0) return;
        var direction = new Vector3(target.X, 0, -target.Y) / speed;
        var forwardSpeed = Vector3.Dot(reference.Velocity.Linear, direction);
        if (forwardSpeed >= speed * .9f || forwardSpeed < -.10f) return;

        var travel = speed * Profile.FixedDeltaSeconds;
        var approach = MathF.Min(travel, capsule.Radius);
        var skin = MathF.Min(.001f, capsule.Radius * .002f);
        var shape = new Capsule(capsule.Radius, capsule.CylinderLength);
        var probe = new RigidPose(new(start.X, supportMax.Y + halfHeight + skin, start.Z), pose.Orientation);
        var nearest = new StepSweep(this, body, support);
        Simulation.Sweep(shape, probe, new BodyVelocity(direction), approach, Resources.Pool, ref nearest, 1e-6f, 1e-6f, 32);
        if (nearest.Hit is not { } tread || !TryGetStepBox(tread, out var treadMin, out var treadMax)) return;

        var rise = treadMax.Y - supportMax.Y;
        if (treadMin.Y > supportMax.Y + skin || rise <= skin || rise > entry.Options.MaximumStepHeight + .0001f ||
            !IntersectStepFootprint(start, direction, supportMin, supportMax, 0, out _, out var supportExit) ||
            !IntersectStepFootprint(start, direction, treadMin, treadMax, 0, out var enter, out var exit) ||
            enter < 0 || enter > supportExit + skin ||
            !IntersectStepFootprint(start, direction, treadMin, treadMax, MathF.Max(.002f, 2 * skin), out var landingDistance, out _) ||
            landingDistance <= 0 || landingDistance > capsule.Radius + approach + .002f ||
            exit - landingDistance < MathF.Max(entry.Options.MinimumTreadDepth, travel + skin)) return;

        var landing = start + direction * landingDistance;
        landing.Y = treadMax.Y + halfHeight + skin;
        var lift = landing.Y - start.Y;
        var gap = landing.Y - halfHeight - treadMax.Y;
        if (landing.X <= treadMin.X || landing.X >= treadMax.X || landing.Z <= treadMin.Z || landing.Z >= treadMax.Z ||
            !float.IsFinite(lift) || lift <= 0 || gap < 0 || gap > capsule.Radius * .005f ||
            PhysicsValidation.Validate(new PhysicsPose(landing, pose.Orientation)).IsFailure) return;

        // The lower box is receding. The tread starts outside the centre's path up, and is below the path across.
        var blocker = new StepSweep(this, body, support, tread);
        Simulation.Sweep(shape, pose, new BodyVelocity(Vector3.UnitY), lift, Resources.Pool, ref blocker, 1e-6f, 1e-6f, 32);
        if (blocker.Hit is not null) return;
        var raised = new RigidPose(new(start.X, landing.Y, start.Z), pose.Orientation);
        Simulation.Sweep(shape, raised, new BodyVelocity(direction), landingDistance, Resources.Pool, ref blocker, 1e-6f, 1e-6f, 32);
        if (blocker.Hit is not null) return;

        // Keep the persistent target; only the normal collision analysis can publish the next support.
        ResetCharacterSupport(entry);
        reference.Pose = new RigidPose(landing, pose.Orientation);
        reference.Velocity.Linear.Y = 0;
        reference.UpdateBounds();
    }

    private bool TryGetStepBox(BodyEntry entry, out Vector3 min, out Vector3 max)
    {
        if (entry.Description is { Motion: PhysicsBodyMotion.Static, IsSensor: false } &&
            entry.Shape.Definition is PhysicsBox box)
        {
            var pose = Simulation.Statics[entry.StaticHandle].Pose;
            if (pose.Orientation == Quaternion.Identity || pose.Orientation == new Quaternion(0, 0, 0, -1))
            {
                var half = box.Size * .5f;
                min = pose.Position - half;
                max = pose.Position + half;
                return true;
            }
        }
        min = max = default;
        return false;
    }

    private static bool IntersectStepFootprint(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, float inset,
        out float enter, out float exit)
    {
        enter = float.NegativeInfinity;
        exit = float.PositiveInfinity;
        return ClipStepAxis(origin.X, direction.X, min.X + inset, max.X - inset, ref enter, ref exit) &&
            ClipStepAxis(origin.Z, direction.Z, min.Z + inset, max.Z - inset, ref enter, ref exit);
    }

    private static bool ClipStepAxis(float origin, float direction, float min, float max, ref float enter, ref float exit)
    {
        if (min >= max) return false;
        if (direction == 0) return origin >= min && origin <= max;
        var a = (min - origin) / direction;
        var b = (max - origin) / direction;
        enter = MathF.Max(enter, MathF.Min(a, b));
        exit = MathF.Min(exit, MathF.Max(a, b));
        return enter <= exit;
    }

    private struct StepSweep(BepuWorld world, BodyEntry body, BodyEntry support, BodyEntry? tread = null) : ISweepHitHandler
    {
        internal BodyEntry? Hit;
        private float _distance;

        public readonly bool AllowTest(CollidableReference c) => (tread is null || Hit is null) &&
            world.ByBackend.TryGetValue(c.Packed, out var other) && other != body && other != support && other != tread &&
            !other.Description.IsSensor && body.Description.Filter.Allows(other.Description.Filter) &&
            !world.DisabledPairs.ContainsKey(Pair(body.Id.Slot, other.Id.Slot));

        public readonly bool AllowTest(CollidableReference c, int childIndex) => true;

        public void OnHit(ref float maximumT, float t, Vector3 location, Vector3 normal, CollidableReference c)
            => Record(ref maximumT, t, c);

        public void OnHitAtZeroT(ref float maximumT, CollidableReference c) => Record(ref maximumT, 0, c);

        private void Record(ref float maximumT, float t, CollidableReference c)
        {
            var other = world.ByBackend[c.Packed];
            if (tread is not null)
            {
                Hit = other;
                maximumT = 0;
            }
            // Keep the original traversal distance for a stable winner across equal distances, including zero.
            else if (Hit is null || t < _distance || t == _distance &&
                (other.Id.Slot < Hit.Id.Slot || other.Id.Slot == Hit.Id.Slot && other.Id.Generation < Hit.Id.Generation))
            {
                Hit = other;
                _distance = t;
            }
        }
    }
}
