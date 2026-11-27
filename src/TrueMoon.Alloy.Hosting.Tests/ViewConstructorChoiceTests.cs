using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Views;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

/// <summary>UIA4.8a: a tied view constructor marked [ViewConstructor] activates through the generated factory.</summary>
public sealed class ViewConstructorChoiceTests
{
    internal sealed class FirstMarker { public string Name => "first"; }
    internal sealed class SecondMarker { public string Name => "second"; }

    internal sealed class ChosenView : View
    {
        public ChosenView(FirstMarker marker) => Chosen = marker.Name;
        [ViewConstructor] public ChosenView(SecondMarker marker) => Chosen = marker.Name;
        public string Chosen { get; }
    }

    [Fact]
    public async Task MarkedConstructor_CreatesThroughGeneratedActivation()
    {
        await using var app = App.Builder(builder => { }).Setup(context => context.Services(services =>
        {
            services.Singleton<FirstMarker>(_ => new());
            services.Singleton<SecondMarker>(_ => new());
            services.View<ChosenView>();
        })).Build();
        var factory = (IFactory<ChosenView>)app.Services.GetService(typeof(IFactory<ChosenView>))!;
        using var view = factory.Create()!;
        Assert.Equal("second", view.Chosen);
    }
}
