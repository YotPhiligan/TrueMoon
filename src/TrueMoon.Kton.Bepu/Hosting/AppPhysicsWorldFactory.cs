namespace TrueMoon.Kton.Bepu.Hosting;

// Borrowed aliases must never implement IDisposable: both containers can capture each alias separately.
internal sealed class AppPhysicsWorldFactory(BepuPhysicsHost host) : IFactory<IPhysicsWorld, PhysicsWorldOptions>
{
    public IPhysicsWorld Create(PhysicsWorldOptions arguments) => host.Create(arguments);
    public IPhysicsWorld Create() => host.Create(null, useDefaults: true);
    public IPhysicsWorld Create<TData>(TData? data = default)
    {
        if (data is PhysicsWorldOptions arguments) return Create(arguments);
        throw new ArgumentException("Expected PhysicsWorldOptions; use Create() for configured defaults.", nameof(data));
    }
}
