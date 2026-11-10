namespace TrueMoon.Configuration;

/// <summary>Declares fields of one options type.</summary>
public interface IConfigurationSchemaBuilder<T> where T : class
{
    /// <summary>Declares a field and its default accessor.</summary>
    /// <param name="relativePath">Path relative to the section.</param><param name="get">Default/final value accessor.</param>
    /// <param name="set">Binding setter.</param><param name="field">Input metadata.</param><param name="codec">Optional explicit codec.</param>
    void Field<TValue>(string relativePath, Func<T, TValue> get, Action<T, TValue> set,
        ConfigurationField? field = null, IConfigurationValueCodec<TValue>? codec = null);
}
