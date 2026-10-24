using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class UiFrameParticipantTests
{
    [Fact]
    public void ActiveFrames_UseInjectedMonotonicTimeOnTheOwnerThenBecomeIdle()
    {
        var time = new ManualTime(); var participant = new Participant(); var surface = new Surface();
        using var ui = Create(participant, surface, time);
        ui.Update(); Assert.Empty(participant.Ticks); Assert.False(ui.NeedsUpdate);
        participant.Start(2); ui.RequestFrame();
        time.Advance(TimeSpan.FromMilliseconds(20));
        ui.Update(); Assert.True(ui.NeedsUpdate);
        time.Advance(TimeSpan.FromMilliseconds(35));
        ui.Update();
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(35) }, participant.Ticks);
        Assert.All(participant.Threads, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
        Assert.False(ui.NeedsUpdate);
        var frames = surface.Frames;
        time.Advance(TimeSpan.FromHours(1)); Assert.False(ui.Update());
        Assert.Equal(2, participant.Ticks.Count); Assert.Equal(frames, surface.Frames);
    }

    [Fact]
    public void InputActivation_DoesNotChargeIdleTimeToTheNewAnimation()
    {
        var time = new ManualTime(); var participant = new Participant();
        using var ui = Create(participant, new Surface(), time);
        ui.Update(); time.Advance(TimeSpan.FromMinutes(10));
        Assert.True(ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)).Handled);
        time.Advance(TimeSpan.FromMilliseconds(15)); ui.Update();
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(15) }, participant.Ticks);
        Assert.Equal(1, participant.ObservedInputs);
    }

    [Fact]
    public void DetachedParticipant_IsNotTickedOrObservedAndIsNotOwnedByTheSession()
    {
        var time = new ManualTime(); var root = new Panel(); var participant = new Participant(); root.Add(participant);
        using var ui = Create(root, new Surface(), time);
        ui.Update(); participant.Start(10); ui.RequestFrame(); ui.Update();
        root.Items.Remove(participant); time.Advance(TimeSpan.FromMilliseconds(20)); ui.Update();
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10));
        Assert.Single(participant.Ticks); Assert.Equal(0, participant.ObservedInputs);
        Assert.False(ui.NeedsUpdate); Assert.False(participant.IsDisposed);
        ui.Dispose(); Assert.False(participant.IsDisposed); participant.Dispose();
    }

    [Fact]
    public void SessionDisposal_EndsFrameAndInputLifetime()
    {
        var participant = new Participant(); var surface = new Surface(); var ui = Create(participant, surface, new ManualTime());
        participant.Start(5); ui.Update(); ui.Dispose();
        Assert.True(participant.IsDisposed); Assert.Equal(1, surface.Disposals);
        Assert.Throws<ObjectDisposedException>(() => ui.Update());
        Assert.Throws<ObjectDisposedException>(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)));
        Assert.Single(participant.Ticks); ui.Dispose(); Assert.Equal(1, surface.Disposals);
    }

    [Fact]
    public void HiddenParticipant_DoesNotKeepAnOtherwiseIdleSessionAwake()
    {
        var participant = new Participant { IsVisible = false }; participant.Start(5);
        using var ui = Create(participant, new Surface(), new ManualTime());
        ui.Update();
        Assert.Empty(participant.Ticks); Assert.False(ui.NeedsUpdate);
        participant.IsVisible = true; ui.Update();
        Assert.Single(participant.Ticks); Assert.True(ui.NeedsUpdate);
    }

    private static UiSession Create(Element root, Surface surface, ManualTime time) =>
        new(root, surface, new UiViewport(240, 180), null, new UiDispatchOptions(), time);

    private sealed class ManualTime : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    private sealed class Participant : Element, IUiFrameParticipant, IUiInputObserver
    {
        private int _remaining;
        public List<TimeSpan> Ticks { get; } = [];
        public List<int> Threads { get; } = [];
        public int ObservedInputs { get; private set; }
        public bool NeedsFrame => _remaining > 0;
        public void Start(int frames) => _remaining = frames;
        public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
        { Ticks.Add(elapsed); Threads.Add(Environment.CurrentManagedThreadId); _remaining--; }
        public bool PreviewInput(UiInput input, IInputContext context)
        {
            if (input.Kind != InputKind.KeyDown || input.Key != UiKey.F10) return false;
            ObservedInputs++; Start(2); return true;
        }
    }

    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public int Frames { get; private set; }
        public int Disposals { get; private set; }
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => Frames++;
        public void Dispose() => Disposals++;
    }
}
