using System.Numerics;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    public PhysicsStepResult Step(ulong tick, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            PhysicsValidation.ValidateTick(_tick, tick).ThrowIfFailure(); cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _state, (int)PhysicsWorldState.Stepping);
            try
            {
                Faults.Visit(Checkpoint.StepBegun); CallbackFailed = false; Contacts.BeginStep();
                for (var i = 0; i < Bodies.Capacity; i++)
                {
                    if (Bodies[i] is not { } body) continue;
                    if (body.PendingImpulse != Vector3.Zero)
                    {
                        var reference = Simulation.Bodies[body.Handle]; reference.Awake = true;
                        reference.ApplyLinearImpulse(body.PendingImpulse); body.PendingImpulse = Vector3.Zero;
                    }
                    if (body.Character is { Options.MaximumStepHeight: > 0 } character) TryAutoStep(character);
                }
                // Synchronous BEPU call joins the owned dispatcher. Cancellation after this admission boundary does not abort a step.
                Simulation.Timestep(Profile.FixedDeltaSeconds, Resources.Dispatcher);
                if (CallbackFailed) throw new InvalidOperationException("Backend contact callback reported a failure.");
                for (var i = 0; i < Bodies.Capacity; i++)
                {
                    if (Bodies[i] is not { } body) continue;
                    var state = Read(body); ValidateMotion(state.Pose, state.LinearVelocity, state.AngularVelocity);
                }
                var events = Contacts.Publish(this, tick);
                Resources.Sample(); _commandsAdmitted = 0; Volatile.Write(ref _tick, tick);
                Volatile.Write(ref _state, (int)PhysicsWorldState.Ready);
                return new(tick, events);
            }
            catch (Exception error) { throw Fault("Step", error); }
        }
        finally { Exit(); }
    }
}
