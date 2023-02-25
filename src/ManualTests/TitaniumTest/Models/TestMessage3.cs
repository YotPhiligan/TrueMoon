using MemoryPack;
using TrueMoon.Thorium;

namespace TitaniumTest.Models;

[Signal]
[MemoryPackable]
public partial class TestMessage3 : IWithResponse<TestMessageLargeResponse3>
{
    public int Index { get; set; }
}