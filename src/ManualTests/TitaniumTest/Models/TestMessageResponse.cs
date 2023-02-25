using MemoryPack;

namespace TitaniumTest.Models;

[MemoryPackable]
public partial class TestMessageResponse
{
    public TimeSpan Time { get; set; }
    public string Data { get; set; }
}

[MemoryPackable]
public partial class TestMessageResponse2
{
    public TimeSpan Time { get; set; }
    public string Data { get; set; }
    public int IntResult { get; set; }
}