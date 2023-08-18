namespace TitaniumTest.Models;

public class TestPoco
{
    public int IntValue { get; set; }
    public string Text { get; set; }
    public float FloatValue { get; set; }
    public Memory<byte> Memory { get; set; }
}

public class TestPoco2
{
    public int IntValue2 { get; set; }
    public string Text2 { get; set; }
    public TestPoco Poco { get; set; }
    public float M { get; set; }
}

public class TestPoco3
{
    public Guid GuidValue { get; set; }
    public Guid? NulGuidValue { get; set; }
    public DateTime DateTimeValue { get; set; }
    public DateTime? NulDateTimeValue { get; set; }
    public bool BoolValue { get; set; }
    public bool? NulBoolValue { get; set; }
}

public class TestPoco4
{
    public Guid GuidValue { get; set; }
    public IReadOnlyDictionary<string,TestPoco> TestDictionary { get; set; }
}


public enum TestEnum1
{
    None,
    Item1
}

public enum TestEnum2
{
    None,
    Item1
}

public record TestRecordPoco1(int IntVal, bool BoolValue, Guid? NulGuidValue, TestEnum1 TestEnum1Value, TestEnum2? NulTestEnum2Value, IReadOnlyList<string> Texts, object? NulOptionalObject = default);