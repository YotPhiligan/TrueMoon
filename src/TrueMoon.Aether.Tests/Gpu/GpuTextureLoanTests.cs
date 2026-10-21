using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuTextureLoanTests
{
    private static GpuFrameStamp Stamp() => new(new SessionIdentity(Guid.NewGuid(), 4, new('A', 64), new('B', 64), new('C', 64)), 3, 7, 2, 2);
    [Theory]
    [InlineData("Name")] [InlineData("Width")] [InlineData("Height")] [InlineData("Stamp")]
    public void EndedLoan_RejectsAllAccessWithoutInvokingContextOrSession(string property)
    {
        var contextCalls = 0; var sessionCalls = 0;
        var context = new GpuOpenGlContext(_ => 0, () => contextCalls++, a => { a(); return Task.CompletedTask; });
        var texture = new GpuOpenGlTexture(context, () => sessionCalls++, 19, 64, 32, Stamp()); texture.End(); texture.End();
        Assert.Throws<ObjectDisposedException>(() => Read(texture, property)); Assert.Equal(1, contextCalls); Assert.Equal(0, sessionCalls);
    }
    [Fact]
    public void LiveLoan_PreservesExactDescriptorAndChecksSessionBeforeEachAccess()
    {
        var error = new IOException("session ended"); var closed = false; var checkedSession = 0;
        var context = new GpuOpenGlContext(_ => 0, () => { }, a => { a(); return Task.CompletedTask; }); var stamp = Stamp();
        var texture = new GpuOpenGlTexture(context, () => { checkedSession++; if (closed) throw error; }, 23, 128, 64, stamp);
        Assert.Equal(23u, texture.Name); Assert.Equal(128, texture.Width); Assert.Equal(64, texture.Height); Assert.Same(stamp, texture.Stamp); Assert.Equal(4, checkedSession);
        closed = true; Assert.Same(error, Assert.Throws<IOException>(() => texture.Name)); Assert.Equal(5, checkedSession);
    }
    [Theory]
    [InlineData("Name")] [InlineData("Width")] [InlineData("Height")] [InlineData("Stamp")]
    public void LiveLoan_RejectsForeignThreadBeforeContextCallback(string property)
    {
        var calls = 0; var context = new GpuOpenGlContext(_ => 0, () => calls++, a => { a(); return Task.CompletedTask; });
        var texture = new GpuOpenGlTexture(context, () => throw new Exception("must not reach session"), 23, 64, 32, Stamp());
        Exception? error = null; var thread = new Thread(() => error = Record.Exception(() => Read(texture, property))); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(3)));
        Assert.IsType<InvalidOperationException>(error); Assert.Equal(1, calls); texture.End();
    }
    private static object Read(GpuOpenGlTexture texture, string property) => property switch
    { "Name" => texture.Name, "Width" => texture.Width, "Height" => texture.Height, _ => texture.Stamp };
}
