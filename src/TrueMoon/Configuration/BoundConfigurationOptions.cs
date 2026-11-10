namespace TrueMoon.Configuration;

internal abstract class BoundConfigurationOptions
{
    internal abstract Type ValueType { get; }
    internal abstract object Copy();
}

internal sealed class BoundConfigurationOptions<T>(IConfigurationSchema<T> schema, T value)
    : BoundConfigurationOptions where T : class
{
    internal override Type ValueType => typeof(T);
    internal override object Copy()
    {
        try
        {
            var result = schema.Copy(value);
            if (result is null || ReferenceEquals(value, result)) throw new InvalidOperationException("Options copy must be independent.");
            return result;
        }
        catch (Exception) { throw ConfigurationMetadata.Error("OptionsCopy", schema.Section); }
    }
}
