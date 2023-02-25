using MemoryPack;
using TrueMoon.Thorium;

namespace TitaniumTest.Models;

[Signal]
[MemoryPackable]
public partial class TestMessageLarge1 : IWithResponse<TestMessageLargeResponse1>
{
    public int Index { get; set; }
    public byte[] Data { get; set; }
}