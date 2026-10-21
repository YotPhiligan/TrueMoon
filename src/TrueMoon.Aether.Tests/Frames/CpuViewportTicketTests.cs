using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Replacements;
using Xunit;

namespace TrueMoon.Aether.Tests.Frames;
public sealed class CpuViewportTicketTests
{
    [Fact]
    public async Task CpuReceiverTicket_CompletesSharedReadinessOnlyAfterActualPresentation()
    {
        const string hash="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var factory=new ReplacementRuntimeFixture{Produce=true};await using var supervisor=new SessionSupervisor(factory,hash,hash,"cpu-ticket-tests");
        await using var frames=new FrameSession(supervisor);var start=frames.StartAsync(new SceneSnapshot("room",7,[]),32,16);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(frames.Display==null)await Task.Delay(5,deadline.Token);
        var display=frames.Display!;await display.Receiver.FirstFrame.WaitAsync(deadline.Token);var presentation=frames.Presentation!;ViewportFrame ticket;
        using(var image=display.Receiver.TakeLatest()){Assert.NotNull(image);ticket=presentation.Issue(image.Metadata);}
        Assert.Equal(ViewportTransportKind.Cpu,presentation.Surface.Transport);Assert.False(start.IsCompleted);Assert.False(presentation.FirstPresented.IsCompleted);Assert.False(display.Presented.Task.IsCompleted);
        Assert.True(frames.Presented(ticket));await start.WaitAsync(deadline.Token);Assert.True(presentation.FirstPresented.IsCompletedSuccessfully);Assert.True(display.Presented.Task.IsCompletedSuccessfully);
        await frames.ResizeAsync(0,0);Assert.True(frames.Presentation!.Surface.Suspended);Assert.False(frames.Presented(ticket));Assert.Equal(0,frames.MappingCount);
        await frames.StopAsync();Assert.True(factory.Leases[0].ResourcesReleased);
    }
}