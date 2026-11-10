namespace TrueMoon.Configuration;

/// <summary>Optional schema catalog for connecting a profile to the App validation stage.</summary>
public interface IConfigurationProfileSchemas
{
    /// <summary>Visits every declared schema exactly once in declaration order.</summary>
    /// <param name="visitor">Receives typed schema instances without activating resources.</param>
    void VisitSchemas(IConfigurationSchemaVisitor visitor);
}
