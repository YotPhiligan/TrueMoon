using TrueMoon.Alloy.Hosting.Generation;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis.Views;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class ViewActivationContributionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoContainers_ActivateTheirOwnContributionOfTheSameView(bool microsoftDi)
    {
        ViewActivation<ContributionView>.Register(_ => new ContributionView { Contribution = "A" }, "A");
        ViewActivation<ContributionView>.Register(_ => new ContributionView { Contribution = "B" }, "B");
        await using var appA = BuildApp(microsoftDi, services => services.View<ContributionView>("A"));
        await using var appB = BuildApp(microsoftDi, services => services.View<ContributionView>("B"));

        using var first = FactoryFor<ContributionView>(appA).Create()!;
        using var again = FactoryFor<ContributionView>(appA).Create()!;
        using var other = FactoryFor<ContributionView>(appB).Create()!;
        Assert.Equal("A", first.Contribution);
        Assert.Equal("A", again.Contribution);
        Assert.Equal("B", other.Contribution);
        Assert.NotSame(first, other);
    }

    [Fact]
    public void TwoContributions_WithoutSelection_NameEveryContribution()
    {
        ViewActivation<AmbiguousView>.Register(_ => new AmbiguousView { Contribution = "A" }, "A");
        ViewActivation<AmbiguousView>.Register(_ => new AmbiguousView { Contribution = "B" }, "B");
        var failure = Assert.Throws<InvalidOperationException>(
            () => BuildApp(false, services => services.View<AmbiguousView>()));
        Assert.Contains("More than one generated view activation for", failure.Message);
        Assert.Contains(nameof(AmbiguousView), failure.Message);
        Assert.Contains("A,", failure.Message);
        Assert.Contains("B,", failure.Message);
        Assert.Contains(typeof(ViewActivationContributionTests).Assembly.GetName().Name!, failure.Message);
    }

    [Fact]
    public void UnknownContribution_ListsRegisteredContributions()
    {
        ViewActivation<ContributionView>.Register(_ => new ContributionView { Contribution = "A" }, "A");
        ViewActivation<ContributionView>.Register(_ => new ContributionView { Contribution = "B" }, "B");
        var failure = Assert.Throws<InvalidOperationException>(
            () => BuildApp(false, services => services.View<ContributionView>("C")));
        Assert.Contains("with contribution 'C'", failure.Message);
        Assert.Contains("A,", failure.Message);
        Assert.Contains("B,", failure.Message);
    }

    [Fact]
    public async Task SingleGeneratedContribution_StillResolvesWithoutAKey()
    {
        await using var app = BuildApp(false, services => services.View<SoloView>());
        using var view = FactoryFor<SoloView>(app).Create()!;
        Assert.Equal("generated", view.Contribution);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithArgs_TwoContainers_ActivateTheirOwnContribution(bool microsoftDi)
    {
        ViewActivation<ArgRowView, RowArgument>.Register(
            (_, argument) => new ArgRowView(argument) { Contribution = "A" }, "A");
        ViewActivation<ArgRowView, RowArgument>.Register(
            (_, argument) => new ArgRowView(argument) { Contribution = "B" }, "B");
        await using var appA = BuildApp(microsoftDi, services => services.ViewWithArgs<ArgRowView, RowArgument>("A"));
        await using var appB = BuildApp(microsoftDi, services => services.ViewWithArgs<ArgRowView, RowArgument>("B"));

        using var a = Assert.IsType<ArgRowView>(FactoryFor<ArgRowView, RowArgument>(appA).Create(new RowArgument(1)));
        using var b = Assert.IsType<ArgRowView>(FactoryFor<ArgRowView, RowArgument>(appB).Create(new RowArgument(2)));
        Assert.Equal("A", a.Contribution);
        Assert.Equal(1, a.Argument.Value);
        Assert.Equal("B", b.Contribution);
        Assert.Equal(2, b.Argument.Value);
    }

    [Fact]
    public async Task SameContributionKey_ReplacesThatContributionOnly()
    {
        ViewActivation<ReplaceView>.Register(_ => new ReplaceView { Contribution = "первый" }, "свой");
        ViewActivation<ReplaceView>.Register(_ => new ReplaceView { Contribution = "второй" }, "свой");
        await using var app = BuildApp(false, services => services.View<ReplaceView>("свой"));
        using var view = FactoryFor<ReplaceView>(app).Create()!;
        Assert.Equal("второй", view.Contribution);
    }

    private static IApp BuildApp(bool microsoftDi, Action<IServicesRegistrationContext> register) =>
        App.Builder(builder => { if (microsoftDi) builder.UseDI(); }).Setup(context => context.Services(register)).Build();

    private static IFactory<T> FactoryFor<T>(IApp app) => (IFactory<T>)app.Services.GetService(typeof(IFactory<T>))!;

    private static IFactory<T, TArgument> FactoryFor<T, TArgument>(IApp app)
        => (IFactory<T, TArgument>)app.Services.GetService(typeof(IFactory<T, TArgument>))!;

    internal sealed class ContributionView : View { public string Contribution { get; set; } = "generated"; }
    internal sealed class AmbiguousView : View { public string Contribution { get; set; } = "generated"; }
    internal sealed class SoloView : View { public string Contribution { get; set; } = "generated"; }
    internal sealed class ReplaceView : View { public string Contribution { get; set; } = "generated"; }
    internal sealed record RowArgument(int Value);
    internal sealed class ArgRowView : View
    {
        public ArgRowView(RowArgument argument) => Argument = argument;
        public RowArgument Argument { get; }
        public string Contribution { get; set; } = "generated";
    }
}
