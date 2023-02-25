using MemoryPack;

namespace TrueMoon.Thorium.Tests.Utils;

[MemoryPackable]
public partial class TestResponsePoco
{
    public bool Result { get; set; }
    public string ResultString { get; set; }
}