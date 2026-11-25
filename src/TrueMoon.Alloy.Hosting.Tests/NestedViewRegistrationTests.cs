using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class NestedViewRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConstructorViewsUseTheirFactoriesAndTransferOwnershipToTheTree(bool microsoftDi)
    {
        var model = new Message("Отчёт");
        await using var app = BuildApp(microsoftDi, services => services.Singleton<IMessage>(_ => model)
            .View<StyledChild, ChildStyle>()
            .Singleton<IFactory<TrackedChild>>(_ => new Factory<TrackedChild>(() => new TrackedChild()))
            .View<ParentView>());
        using var root = FactoryFor<ParentView>(app).Create()!;
        Assert.Same(model, root.First.DataContext);
        Assert.NotSame(root.First, root.Second);
        Assert.NotNull(root.Custom);

        root.Attach(static () => { }, static work => work(), null, new UiControlFactories());
        var layout = Assert.IsType<VStack>(root.Child);
        Assert.Equal(new Element[] { root.First, root.Second, root.Custom }, layout.Children);
        Assert.Equal(new Thickness(26), root.First.Padding);
        Assert.Equal(model.Caption, Assert.IsType<Text>(root.Second.Child).Value);

        var removed = root.First;
        Assert.True(layout.Items.Remove(removed));
        using (removed)
        {
            root.Dispose();
            Assert.False(removed.IsDisposed);
            Assert.True(root.Second.IsDisposed);
            Assert.Equal(1, root.Custom.Disposals);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FailureDisposesAnEarlierChildWithoutReplacingTheOriginalError(bool microsoftDi, bool constructorFails)
    {
        var error = new InvalidOperationException("Creation stopped.");
        TrackedChild? created = null;
        await using var app = BuildApp(microsoftDi, services => services
            .Singleton<IFactory<TrackedChild>>(_ => new Factory<TrackedChild>(() => created = new TrackedChild()))
            .Singleton<IFactory<OtherChild>>(_ => new Factory<OtherChild>(() => throw error))
            .Singleton<ConstructionFailure>(_ => new(error))
            .View<TwoChildParent>().View<RejectingParent>());

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
        {
            if (constructorFails) FactoryFor<RejectingParent>(app).Create();
            else FactoryFor<TwoChildParent>(app).Create();
        }));
        Assert.Equal(1, Assert.IsType<TrackedChild>(created).Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposingAnUnattachedParentDisposesItsUnadoptedChild(bool microsoftDi)
    {
        await using var app = BuildApp(microsoftDi, services => services.View<TrackedChild>().View<OptionalParent>());
        using var parent = FactoryFor<OptionalParent>(app).Create()!;
        var child = Assert.IsType<TrackedChild>(parent.ChildView);
        Assert.Null(child.Parent);
        parent.Dispose();
        Assert.Equal(1, child.Disposals);
    }

    [Fact]
    public async Task OptionalChildCanBeAbsent()
    {
        await using var app = BuildApp(false, services => services.View<OptionalParent>());
        using var root = FactoryFor<OptionalParent>(app).Create()!;
        Assert.Null(root.ChildView);
    }

    [Fact]
    public async Task AFactoryCannotGiveTheParentAViewAlreadyOwnedByATree()
    {
        var child = new TrackedChild();
        using var tree = new VStack().WithChildren(child);
        await using var app = BuildApp(false, services => services
            .Singleton<IFactory<TrackedChild>>(_ => new Factory<TrackedChild>(() => child)).View<OptionalParent>());

        Assert.Contains("fresh, unowned view", Assert.Throws<InvalidOperationException>(() => FactoryFor<OptionalParent>(app).Create()).Message);
        Assert.Same(tree, child.Parent);
        Assert.False(child.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConstructorViewCyclesAreReportedWithoutRecursiveConstruction(bool microsoftDi)
    {
        await using var app = BuildApp(microsoftDi, services => services.View<FirstInCycle>().View<SecondInCycle>());
        var error = Assert.Throws<InvalidOperationException>(() => FactoryFor<FirstInCycle>(app).Create());
        Assert.Contains("Circular View activation", error.Message);
        Assert.Contains(nameof(FirstInCycle), error.Message);
    }

    private static IApp BuildApp(bool microsoftDi, Action<IServicesRegistrationContext> register) =>
        App.Builder(builder => { if (microsoftDi) builder.UseDI(); }).Setup(context => context.Services(register)).Build();

    private static IFactory<T> FactoryFor<T>(IApp app) => (IFactory<T>)app.Services.GetService(typeof(IFactory<T>))!;

    internal interface IMessage { string Caption { get; } }
    private sealed record Message(string Caption) : IMessage;
    internal sealed record ConstructionFailure(Exception Error);

    internal sealed class StyledChild : View<IMessage>
    {
        public StyledChild(IMessage model) => Content(model, ui => ui.Text(model.Caption));
    }

    internal sealed class ChildStyle : IViewStyle<StyledChild>
    {
        public void Configure(IViewStyleBuilder<StyledChild> styles) => styles.Root().Padding(26);
    }

    internal sealed class TrackedChild : View
    {
        public int Disposals { get; private set; }
        public override void Dispose() { Disposals++; base.Dispose(); }
    }

    internal sealed class ParentView : View
    {
        public ParentView(StyledChild first, StyledChild second, TrackedChild custom)
        {
            First = first; Second = second; Custom = custom;
            Content(ui => ui.VStack(first, second, custom));
        }

        public StyledChild First { get; }
        public StyledChild Second { get; }
        public TrackedChild Custom { get; }
    }

    internal sealed class OtherChild : View;
    internal sealed class TwoChildParent : View { public TwoChildParent(TrackedChild first, OtherChild second) { } }
    internal sealed class RejectingParent : View { public RejectingParent(TrackedChild child, ConstructionFailure failure) => throw failure.Error; }
    internal sealed class OptionalParent(TrackedChild? child = null) : View { public TrackedChild? ChildView { get; } = child; }
    internal sealed class FirstInCycle : View { public FirstInCycle(SecondInCycle child) { } }
    internal sealed class SecondInCycle : View { public SecondInCycle(FirstInCycle child) { } }
}
