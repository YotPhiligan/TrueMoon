using MemoryPack;
using TrueMoon.Thorium;

namespace TitaniumTest.Models;

[Signal]
[MemoryPackable]
public partial class TestMessage2 : IWithResponse<TestMessageResponse2>
{
    public TimeSpan Time { get; set; }
    public string Data { get; set; }
    public bool BoolResult { get; set; }
}