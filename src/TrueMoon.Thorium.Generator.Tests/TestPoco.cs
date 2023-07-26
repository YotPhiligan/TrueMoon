namespace TrueMoon.Thorium.Generator.Tests;

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