using System.Collections.ObjectModel;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class ViewWithArgsRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservableListUsesGeneratedRowsAndRetainsThemAcrossMoves(bool microsoftDi)
    {
        var a = new Product("Кофе", "CF-1", .25f, true);
        var b = new Product("Чай", "TE-2", .5f, false);
        var model = new Catalog { Items = { a, b } };
        await using var app = BuildApp(microsoftDi, services => services
            .Singleton(_ => model)
            .Singleton<IRowLabels>(_ => new RowLabels("В продаже"))
            .Singleton<IViewStyle<ProductRowView>>(_ => new ProductRowStyle())
            .ViewWithArgs<ProductRowView, Product>()
            .View<ProductsView>());
        Assert.Null(app.Services.GetService(typeof(IFactory<ProductRowView>)));
        Assert.Null(app.Services.GetService(typeof(ProductRowView)));
        var factory = Assert.IsAssignableFrom<IFactory<ProductsView>>(app.Services.GetService(typeof(IFactory<ProductsView>)));
        var root = Assert.IsType<ProductsView>(factory.Create());
        using var ui = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(640, 280));
        ui.Update();

        var list = Assert.IsType<VStack>(root.Child);
        var first = Assert.IsType<ProductRowView>(list.Items[0]);
        var second = Assert.IsType<ProductRowView>(list.Items[1]);
        Assert.Same(a, first.DataContext);
        Assert.Same(b, second.DataContext);
        Assert.Equal(new Thickness(7), first.Padding);
        var fields = Assert.IsType<HStack>(first.Child);
        Assert.Equal(a.Name, Assert.IsType<Text>(fields.Items[0]).Value);
        Assert.Equal(a.Sku, Assert.IsType<Text>(fields.Items[1]).Value);
        var slider = Assert.IsType<Slider>(fields.Items[2]);
        var checkBox = Assert.IsType<CheckBox>(fields.Items[3]);
        Assert.Equal(a.Level, slider.Value);
        Assert.True(checkBox.IsChecked);
        Assert.Equal("В продаже", checkBox.Value);
        slider.Value = .75f;
        checkBox.IsChecked = false;
        Assert.Equal(.75f, a.Level);
        Assert.False(a.Enabled);

        model.Items.Move(0, 1);
        ui.Update();
        Assert.Same(second, list.Items[0]);
        Assert.Same(first, list.Items[1]);

        var c = new Product("Какао", "CO-3", 1, true);
        model.Items[1] = c;
        ui.Update();
        Assert.True(first.IsDisposed);
        Assert.Same(second, list.Items[0]);
        var replacement = Assert.IsType<ProductRowView>(list.Items[1]);
        Assert.Same(c, replacement.DataContext);
        model.Items.RemoveAt(0);
        ui.Update();
        Assert.True(second.IsDisposed);
        Assert.Same(replacement, Assert.Single(list.Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedConstructionDisposesInjectedChildButDoesNotOwnRuntimeArgument(bool microsoftDi)
    {
        var error = new InvalidOperationException("Row rejected the item.");
        InjectedChild? child = null;
        await using var app = BuildApp(microsoftDi, services => services
            .Singleton<IFactory<InjectedChild>>(_ => new Factory<InjectedChild>(() => child = new InjectedChild()))
            .Singleton(_ => new RowFailure(error))
            .ViewWithArgs<RejectingRow, SuppliedView>());
        using var supplied = new SuppliedView();
        var factory = Assert.IsAssignableFrom<IFactory<RejectingRow, SuppliedView>>(
            app.Services.GetService(typeof(IFactory<RejectingRow, SuppliedView>)));

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => factory.Create(supplied)));
        Assert.True(Assert.IsType<InjectedChild>(child).IsDisposed);
        Assert.False(supplied.IsDisposed);
    }

    private static IApp BuildApp(bool microsoftDi, Action<IServicesRegistrationContext> register) =>
        App.Builder(builder => { if (microsoftDi) builder.UseDI(); }).Setup(context => context.Services(register)).Build();

    internal sealed class Catalog
    {
        public ObservableCollection<Product> Items { get; } = [];
    }

    internal sealed class Product(string name, string sku, float level, bool enabled) : Bindable
    {
        private float _level = level;
        private bool _enabled = enabled;
        public string Name { get; } = name;
        public string Sku { get; } = sku;
        public float Level { get => _level; set => SetProperty(ref _level, value); }
        public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
    }

    internal interface IRowLabels { string Enabled { get; } }
    private sealed record RowLabels(string Enabled) : IRowLabels;

    internal sealed class ProductsView : View<Catalog>
    {
        public ProductsView(Catalog model, IFactory<ProductRowView, Product> rows) =>
            Content(model, ui => ui.VStack().BindItems(model.Items, rows.Create));
    }

    internal sealed class ProductRowView : View<Product>
    {
        public ProductRowView(IRowLabels labels, Product item) => Content(item, ui => ui.HStack(
            ui.Text(item.Name),
            ui.Text(item.Sku),
            ui.Create<Slider>().BindTwoWay(ProgressBar.ValueProperty, x => x.Level),
            ui.CheckBox(labels.Enabled).BindTwoWay(CheckBox.IsCheckedProperty, x => x.Enabled)));
    }

    internal sealed class ProductRowStyle : IViewStyle<ProductRowView>
    {
        public void Configure(IViewStyleBuilder<ProductRowView> styles) => styles.Root().Padding(7);
    }

    internal sealed class InjectedChild : View;
    internal sealed class SuppliedView : View;
    internal sealed record RowFailure(Exception Error);
    internal sealed class RejectingRow : View
    {
        public RejectingRow(InjectedChild child, SuppliedView item, RowFailure failure) => throw failure.Error;
    }
}
