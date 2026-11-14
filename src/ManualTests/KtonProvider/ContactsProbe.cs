using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;

namespace KtonProviderProbe;

internal static partial class Program
{
    private static PhysicsContactEvent[] Events(IPhysicsWorld world)
    {
        var buffer = new PhysicsContactEvent[world.Options.Limits.MaxContactEvents]; var count = world.CopyContactEvents(buffer); return buffer[..count];
    }
    private static void ContactAcceptance()
    {
        using var factory = new BepuPhysicsWorldFactory();
        using (var world = factory.Create(Options()))
        {
            var floor = Floor(world); var body = Ball(world, new(0, 0.1f, 0)); Step(world, 1); var begin = Events(world);
            Check("contact-begin", begin.Length == 1 && begin[0].Kind == PhysicsContactKind.Begin && begin[0].BodyA == floor && begin[0].BodyB == body && begin[0].HasContact && begin[0].Depth >= 0);
            Check("canonical-contact-normal", begin[0].Normal.Y > 0.9f, begin[0]);
            Reject("contact-output-must-fit-whole-snapshot", PhysicsErrorCode.CapacityExceeded, () => world.CopyContactEvents(Array.Empty<PhysicsContactEvent>()));
            Check("contact-copy-repeatable-owned", Events(world).SequenceEqual(begin));
            Step(world, 1); Check("contact-stay", Events(world).Single().Kind == PhysicsContactKind.Stay);
            world.ApplyCommands([new(PhysicsCommandKind.Sleep, body)]); Step(world, 1);
            Check("sleep-retains-pair-without-stay", Events(world).Length == 0 && ((BepuWorld)world).Contacts.ActiveCount == 1);
            world.ApplyCommands([new(PhysicsCommandKind.Wake, body)]); Step(world, 1); Check("wake-resumes-stay", Events(world).Single().Kind == PhysicsContactKind.Stay);
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: new(new(0, 5, 0), Quaternion.Identity))]); Step(world, 1); var end = Events(world);
            Check("teleport-end-old-pair", end.Length == 1 && end[0].Kind == PhysicsContactKind.End && end[0].EndReason == PhysicsContactEndReason.Teleported && end[0].BodyB == body && !end[0].HasContact);
            Check("owned-old-begin-survives-next-step", begin[0].Kind == PhysicsContactKind.Begin && begin[0].BodyB == body);
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: new(new(0, 0.1f, 0), Quaternion.Identity))]); Step(world, 1);
            Check("teleport-back-new-begin", Events(world).Single().Kind == PhysicsContactKind.Begin);
            world.RemoveBody(body); var replacement = Ball(world, new(0, 0.1f, 0)); Step(world, 1); var changed = Events(world);
            Check("delete-reuse-publishes-old-end-new-begin", changed.Length == 2
                && changed.Any(e => e.Kind == PhysicsContactKind.End && e.EndReason == PhysicsContactEndReason.BodyRemoved && e.BodyB == body)
                && changed.Any(e => e.Kind == PhysicsContactKind.Begin && e.BodyB == replacement && e.BodyB.Generation != body.Generation));
        }
        using (var world = factory.Create(Options(limits: new() { MaxContactEvents = 1 })))
        {
            Floor(world); Ball(world, new(-2, 0.1f, 0)); Ball(world, new(2, 0.1f, 0));
            Reject("callback-overflow-faults-with-explicit-code", PhysicsErrorCode.ContactOverflow, () => world.Step(1));
            Check("overflow-does-not-publish-partial-tick", world.State == PhysicsWorldState.Faulted && world.LastCompletedTick == 0);
            world.Dispose(); Clean("contact-overflow-clean", factory);
        }
        MoreContactTransitions(factory);
        Clean("contact-worlds-clean", factory);
    }
    private static void MoreContactTransitions(BepuPhysicsWorldFactory factory)
    {
        using (var world = factory.Create(Options()))
        {
            Floor(world); var body = Ball(world, new(0, 0.1f, 0)); Step(world, 1); var saved = Events(world);
            world.RemoveBody(body);
            try { world.Step(2, new CancellationToken(true)); Check("pending-end-cancel", false); } catch (OperationCanceledException) { Check("pending-end-cancel", true); }
            Check("cancel-keeps-published-snapshot-and-pending-end", Events(world).SequenceEqual(saved) && ((BepuWorld)world).Contacts.PendingCount == 1 && world.LastCompletedTick == 1);
            Step(world, 1); Check("pending-end-published-on-retry", Events(world).Single().EndReason == PhysicsContactEndReason.BodyRemoved);
        }
        using (var world = factory.Create(Options(new(0, -9.81f, 0))))
        {
            Floor(world); var shape = world.CreateShape(new TrueMoon.Kton.Shapes.PhysicsBox(Vector3.One));
            var body = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(new(0, 0.5f, 0), Quaternion.Identity)));
            var buffer = new PhysicsContactEvent[world.Options.Limits.MaxContactEvents]; var begins = 0; var ends = 0; var stays = 0;
            for (var i = 0; i < 600; i++)
            {
                Step(world, 1); var count = world.CopyContactEvents(buffer);
                for (var e = 0; e < count; e++) { if (buffer[e].Kind == PhysicsContactKind.Begin) begins++; else if (buffer[e].Kind == PhysicsContactKind.End) ends++; else stays++; }
            }
            Metrics["AutomaticSleepContacts"] = new { Begins = begins, Ends = ends, Stays = stays, IsAwake = world.GetBodyState(body).IsAwake, ActivePairs = ((BepuWorld)world).Contacts.ActiveCount };
            Check("automatic-sleep-retains-contact-identity", !world.GetBodyState(body).IsAwake && ((BepuWorld)world).Contacts.ActiveCount == 1 && begins == 1 && ends == 0);
            world.ApplyCommands([new(PhysicsCommandKind.Wake, body)]); Step(world, 1);
            Check("automatic-sleep-wake-stay", Events(world).Single().Kind == PhysicsContactKind.Stay);
        }
        using (var world = factory.Create(Options()))
        {
            Floor(world); var shape = world.CreateShape(new TrueMoon.Kton.Shapes.PhysicsSphere(0.2f));
            var sensor = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(new(0, 0.1f, 0), Quaternion.Identity)) { IsSensor = true });
            Step(world, 1); Check("sensor-begin-without-solver-response", Events(world).Single().IsSensor && Events(world).Single().Kind == PhysicsContactKind.Begin);
            world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, sensor, new(0, 5, 0))]); var separated = false;
            for (var i = 0; i < 10; i++) { Step(world, 1); separated |= Events(world).Any(e => e.Kind == PhysicsContactKind.End && e.EndReason == PhysicsContactEndReason.Separated); }
            Check("physical-separation-end", separated);
        }
        using (var world = factory.Create(Options()))
        {
            var floor = Floor(world); var body = Ball(world, new(0, 0.1f, 0)); Step(world, 1);
            world.ApplyCommands([new(PhysicsCommandKind.Sleep, body)]); Step(world, 1); world.RemoveBody(floor); Step(world, 1);
            Check("delete-support-while-sleeping-retains-old-ids", Events(world).Single() is { Kind: PhysicsContactKind.End, EndReason: PhysicsContactEndReason.BodyRemoved } end && end.BodyA == floor && end.BodyB == body);
        }
        using (var world = factory.Create(Options(limits: new() { MaxContactEvents = 2 })))
        {
            Floor(world); var body = Ball(world, new(0, 0.1f, 0)); Step(world, 1); var pose = world.GetBodyState(body).Pose;
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: pose), new(PhysicsCommandKind.Teleport, body, Pose: pose)]); Step(world, 1); var events = Events(world);
            Check("same-pair-teleport-end-before-new-begin", events.Length == 2 && events[0].Kind == PhysicsContactKind.End && events[0].EndReason == PhysicsContactEndReason.Teleported && events[1].Kind == PhysicsContactKind.Begin);
            Check("repeated-teleport-does-not-duplicate-pending-end", ((BepuWorld)world).Contacts.PendingCount == 0);
        }
        using (var world = factory.Create(Options(limits: new() { MaxContactEvents = 1 })))
        {
            Floor(world); var body = Ball(world, new(0, 0.1f, 0)); Step(world, 1); var saved = Events(world);
            world.ApplyCommands([new(PhysicsCommandKind.Teleport, body, Pose: world.GetBodyState(body).Pose)]);
            Reject("end-plus-begin-event-overflow", PhysicsErrorCode.ContactOverflow, () => world.Step(2));
            Check("event-overflow-keeps-last-completed-tick", world.LastCompletedTick == 1 && world.State == PhysicsWorldState.Faulted && saved[0].Kind == PhysicsContactKind.Begin);
            world.Dispose(); Clean("event-overflow-clean", factory);
        }
        using (var world = factory.Create(Options(limits: new() { MaxContactEvents = 1 })))
        {
            Floor(world); var body = Ball(world, new(-2, 0.1f, 0)); Step(world, 1);
            world.ApplyCommands([new(PhysicsCommandKind.Sleep, body)]); Step(world, 1);
            Ball(world, new(2, 0.1f, 0));
            Reject("sleeping-cache-plus-new-pair-overflow", PhysicsErrorCode.ContactOverflow, () => world.Step(3));
            Check("sleeping-cache-overflow-keeps-completed-tick", world.State == PhysicsWorldState.Faulted && world.LastCompletedTick == 2);
            world.Dispose(); Clean("sleeping-cache-overflow-clean", factory);
        }
        using (var world = factory.Create(Options()))
        {
            Floor(world); var shape = world.CreateShape(new TrueMoon.Kton.Shapes.PhysicsSphere(0.2f));
            world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic, new(new(0, 0.1f, 0), Quaternion.Identity)) { Filter = new(0, 0), IsSensor = true });
            Step(world, 1); Check("contact-filter-applies-before-sensor-events", Events(world).Length == 0);
        }
        using (var world = factory.Create(Options()))
        {
            Floor(world); var body = Ball(world, new(0, 0.3f, 0), velocity: new(0, -20, 0)); Step(world, 1);
            Check("speculative-only-contact-does-not-begin", Events(world).Length == 0);
        }
        using (var world = factory.Create(Options()))
        {
            var mesh = world.CreateShape(new TrueMoon.Kton.Shapes.PhysicsStaticMesh(new Vector3[] { new(-5, 0, -5), new(5, 0, -5), new(-5, 0, 5) }, new[] { 0, 1, 2 }));
            var floor = world.CreateBody(new(mesh, PhysicsBodyMotion.Static, new(new(10, 2, 5), Quaternion.Identity)) { Mass = 0 });
            var body = Ball(world, new(9, 2.1f, 4)); Step(world, 1); var contact = Events(world).Single();
            Check("mesh-contact-child-generation-identity", contact.BodyA == floor && contact.BodyB == body && contact.SubshapeA == 0 && contact.SubshapeB == -1);
            Check("mesh-contact-parent-world-point-normal", MathF.Abs(contact.Point.X - 9) < 0.4f && MathF.Abs(contact.Point.Y - 2) < 0.3f && MathF.Abs(contact.Point.Z - 4) < 0.4f && contact.Normal.Y > 0.9f, contact);
        }
    }
}
