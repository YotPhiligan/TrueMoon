namespace TrueMoon.Cobalt;

public interface IServicesRegistrationAccessor
{
    IReadOnlyList<ServiceRegistrationHandle> GetHandles();
}