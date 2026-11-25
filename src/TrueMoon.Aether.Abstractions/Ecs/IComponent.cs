using System.Runtime.CompilerServices;

namespace TrueMoon.Aether.Ecs;

/// <summary>Optional marker for reference-free typed value components. It is never a storage or dispatch interface.</summary>
public interface IComponent;

/// <summary>Shared component admission, independent of any pool, World or registry.</summary>
public static class ComponentStorage
{
    /// <summary>Rejects managed references; unmarked struct components remain supported.</summary>
    /// <typeparam name="T">Typed component.</typeparam>
    public static void Validate<T>() where T : struct
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException("ECS components must not contain managed references.");
    }
}
