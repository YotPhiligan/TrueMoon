namespace TrueMoon.Cobalt;

public interface IServicesRegistrationAccessor
{
    /// <summary>Returns detached registration metadata in registration order; instances/factories keep their original references.</summary>
    IReadOnlyList<ServiceRegistrationHandle> GetHandles();
}