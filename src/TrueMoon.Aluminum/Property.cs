namespace TrueMoon.Aluminum;

public class Property : IProperty
{
    public Property(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public class Property<T> : Property, IProperty<T>
{
    public T? Value { get; set; }

    public Property() : base(typeof(T).Name)
    {
    }
    
    public Property(T? value) : base(typeof(T).Name)
    {
        Value = value;
    }
    
    public Property(string name) : base(name)
    {
    }
    
    public Property(string name, T? value) : base(name)
    {
        Value = value;
    }
}