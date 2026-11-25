using TrueMoon.Argentis;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class ViewRegistrationTests
{
    [Theory]
    [InlineData(false, "none", 0)]
    [InlineData(true, "none", 0)]
    [InlineData(false, "separate", 11)]
    [InlineData(true, "separate", 11)]
    [InlineData(false, "default", 19)]
    [InlineData(true, "default", 19)]
    [InlineData(false, "override", 11)]
    [InlineData(true, "override", 11)]
    public async Task ViewFactory_CreatesFreshCallerOwnedViewsWithInterfaceModelAndOptionalStyle(bool microsoftDi, string style, int padding)
    {
        var model = new Model();
        var appearance = new Appearance(19);
        ActivationView second;
        await using (var app = App.Builder(builder => { if (microsoftDi) builder.UseDI(); }).Setup(context =>
            context.Services(services =>
            {
                services.Singleton<IModel>(_ => model).Singleton<Appearance>(_ => appearance);
                if (style is "separate" or "override")
                    services.Singleton<IViewStyle<ActivationView>>(_ => new ActivationStyle(new(11)));
                if (style is "default" or "override") services.View<ActivationView, ActivationStyle>();
                else services.View<ActivationView>();
            })).Build())
        {
            Assert.Null(app.Services.GetService(typeof(ActivationView)));
            var factory = (IFactory<ActivationView>)app.Services.GetService(typeof(IFactory<ActivationView>))!;
            Assert.Equal(0, model.Creations);
            using var first = factory.Create()!;
            second = factory.Create()!;
            Assert.Equal(2, model.Creations);
            Assert.NotSame(first, second);
            Assert.Same(model, first.DataContext);
            Assert.Equal("default", first.Label);
            Assert.Equal(new Thickness(padding), first.Padding);
            first.Dispose();
            Assert.False(second.IsDisposed);
        }
        using (second) Assert.False(second.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredStyleFailure_PropagatesBeforeConstructingAView(bool microsoftDi)
    {
        var failure = new InvalidOperationException("Style constructor failed.");
        var model = new Model();
        await using var app = App.Builder(builder => { if (microsoftDi) builder.UseDI(); }).Setup(context =>
            context.Services(services => services.Singleton<IModel>(_ => model)
                .View<ActivationView>()
                .Singleton<IViewStyle<ActivationView>>(_ => throw failure))).Build();
        var factory = (IFactory<ActivationView>)app.Services.GetService(typeof(IFactory<ActivationView>))!;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => factory.Create()));
        Assert.Equal(0, model.Creations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericPresentation_ActivatesTheRegisteredViewOnTheUiOwnerThread(bool microsoftDi)
    {
        var failure = new InvalidOperationException("Root construction failed before native startup.");
        var owner = -1;
        var setupThread = Environment.CurrentManagedThreadId;
        var app = App.Builder(builder => { if (microsoftDi) builder.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<IModel>(_ =>
            {
                owner = Environment.CurrentManagedThreadId;
                throw failure;
            }).View<ActivationView>());
            context.UsePresentation<ActivationView>(options => options.UseSkiaOpenGL().UseSilkWindow());
        }).Build();
        try
        {
            var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
            Assert.Equal(-1, owner);

            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => window.StartAsync()));
            Assert.Same(failure, await Record.ExceptionAsync(() => window.Completion));
            Assert.NotEqual(-1, owner);
            Assert.NotEqual(setupThread, owner);
        }
        finally
        {
            // App disposal can surface the already observed window completion failure.
            var cleanup = await Record.ExceptionAsync(() => app.DisposeAsync().AsTask());
            if (cleanup is AggregateException aggregate)
                Assert.All(aggregate.Flatten().InnerExceptions, error => Assert.Same(failure, error));
            else if (cleanup is not null) Assert.Same(failure, cleanup);
        }
    }

    internal interface IModel { int Creations { get; set; } }
    private sealed class Model : IModel { public int Creations { get; set; } }
    internal sealed record Appearance(float Padding);

    internal sealed class ActivationView : View<IModel>
    {
        public ActivationView(IModel model, string label = "default")
        {
            model.Creations++;
            DataContext = model;
            Label = label;
        }

        public string Label { get; }
    }

    internal sealed class ActivationStyle(Appearance appearance) : IViewStyle<ActivationView>
    {
        public void Configure(IViewStyleBuilder<ActivationView> styles) => styles.Root().Padding(appearance.Padding);
    }
}
