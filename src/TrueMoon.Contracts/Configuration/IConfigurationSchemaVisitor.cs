namespace TrueMoon.Configuration;

/// <summary>Receives schema types without reflection or a DI provider dependency.</summary>
public interface IConfigurationSchemaVisitor
{
    /// <summary>Visits one schema instance.</summary><param name="schema">Declared schema.</param>
    void Visit<T>(IConfigurationSchema<T> schema) where T : class;
}
