using MemoryPack;

namespace TitaniumTest.Models;

[MemoryPackable]
public partial class TestMessageLargeResponse1
{
    public int Index { get; set; }
    public bool Result { get; set; }
    public byte[] Data { get; set; }
}