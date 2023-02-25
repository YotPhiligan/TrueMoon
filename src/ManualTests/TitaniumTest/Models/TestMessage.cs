using MemoryPack;
using TrueMoon.Thorium;

namespace TitaniumTest.Models;

[Signal]
[MemoryPackable]
public partial class TestMessage : IWithResponse<TestMessageResponse>
{
    public TimeSpan Time { get; set; }
    public string Data { get; set; }
}