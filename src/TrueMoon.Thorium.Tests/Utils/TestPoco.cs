using MemoryPack;

namespace TrueMoon.Thorium.Tests.Utils;

[MemoryPackable]
public partial class TestPoco
{
    public int Number { get; set; }
    public byte ByteValue { get; set; }
    public string TestString { get; set; }
    public byte[] Data { get; set; }
}