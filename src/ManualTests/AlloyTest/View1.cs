using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;
using TrueMoon.Alloy.Rendering.Skia;

namespace AlloyTest;

/// <summary>A retained settings form assembled around its existing controls.</summary>
public sealed class View1 : View<SettingsModel>
{
    internal VStack RowList { get; }
    internal LoadStatusRegion StatusRegion { get; }
    internal ScrollViewer RowScroll { get; }
    internal static readonly UiProperty<Theme> AppearanceProperty = new("Appearance", Theme.Dark, Invalidation.Layout, value => value != null);
    internal TextBox NameEditor { get; }
    internal CheckBox NotificationToggle { get; }
    internal Slider VolumeSlider { get; }
    internal Button LightButton { get; }
    internal Button DarkButton { get; }
    internal CheckBox CompactToggle { get; }
    internal CheckBox EditingToggle { get; }
    internal VolumeMeter Meter { get; }
    internal Element Logo { get; }
    internal VStack SettingsFields { get; }

    /// <summary>Creates the retained form for a model supplied by DI.</summary>
    /// <param name="model">The state preserved independently of the controls.</param>
    public View1(SettingsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.UseSquareOpaqueAppearance)
        {
            var logoSource = new SkiaImageSource(Path.Combine(AppContext.BaseDirectory, "Assets", "alloy-mark.png"));
            Own(logoSource);
            Logo = new Image { Source = logoSource };
        }
        else Logo = new LogoMark();
        Logo.Width = Logo.Height = 48;
        LightButton = new Button("Светлая").OnClick(model.UseLight);
        DarkButton = new Button("Тёмная").OnClick(model.UseDark);
        CompactToggle = new CheckBox("Компактно").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.IsCompact);
        EditingToggle = new CheckBox("Ввод включён").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.ControlsEnabled);
        NameEditor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Name);
        NotificationToggle = new CheckBox("Получать уведомления").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.Enabled);
        VolumeSlider = new Slider().BindTwoWay(ProgressBar.ValueProperty, model, x => x.Volume);
        Meter = new VolumeMeter().Bind(ProgressBar.ValueProperty, model, x => x.Volume);
        SettingsFields = new VStack().Bind(UiProperties.Enabled, model, x => x.ControlsEnabled).WithChildren(
            new Text("Имя"), NameEditor, NotificationToggle, new Text("Громкость"), VolumeSlider, Meter);
        StatusRegion = new LoadStatusRegion(status => status.Phase switch
        {
            LoadPhase.Loading => new VStack().Configure(stack => stack.Spacing = 6).WithChildren(
                new Text(status.Message), new ProgressBar { Value = .5f, Height = 8 }),
            LoadPhase.Error => new HStack().Configure(stack => stack.Spacing = 12).WithChildren(
                new Text(status.Message).Width(340), new Button("Повторить").OnClick(model.BeginLoading)),
            _ => new Text(status.Message)
        }).Height(48).Bind(LoadStatusRegion.StatusProperty, model, x => x.LoadStatus);
        RowList = new VStack().Configure(stack => stack.Spacing = 8)
            .BindItems(model.Rows, row => new HStack().Configure(stack => stack.Spacing = 8).WithChildren(
                new TextBox().Width(260).BindTwoWay(TextBox.ValueProperty, row, x => x.Name),
                new Button("↑").Configure(button => button.FontFamily = "Segoe UI Symbol").OnClick(() => model.MoveRowUp(row)),
                new Button("Заменить").OnClick(() => model.ReplaceRow(row)),
                new Button("Удалить").OnClick(() => model.RemoveRow(row))));
        RowScroll = new ScrollViewer().Height(160).Content(RowList);
        DataContext = model;
        SetContent(new VStack()
            .Width(720)
            .Padding(24)
            .WithChildren(
                new HStack().WithChildren(Logo, new Text("Настройки").Configure(text => text.FontSize = 24)),
                new HStack().WithChildren(LightButton, DarkButton, CompactToggle, EditingToggle),
                SettingsFields,
                new Border().Padding(12).Content(new Text().BindText(model, x => x.Summary)),
                StatusRegion,
                new HStack().Configure(stack => stack.Spacing = 12).WithChildren(
                    new Button("Загрузка").OnClick(model.BeginLoading),
                    new Button("Ошибка").OnClick(model.ShowLoadError),
                    new Button("Готово").OnClick(model.CompleteLoading)),
                new HStack().Configure(stack => stack.Spacing = 12).WithChildren(
                    new Button("Загрузить пример").OnClick(model.LoadExample),
                    new Button("Сбросить").OnClick(model.Reset)),
                new Text("Дела").Configure(text => text.FontSize = 20),
                new HStack().Configure(stack => stack.Spacing = 12).WithChildren(
                    new Button("Добавить дело").OnClick(model.AddRow),
                    new Button("Развернуть (Reset)").OnClick(model.ResetRows)),
                RowScroll
            ));
        this.Bind(AppearanceProperty, model, x => x.Appearance);
    }

    internal sealed class LogoMark : Element;

    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (ReferenceEquals(property, AppearanceProperty)) Dispatch(() =>
        {
            var appearance = Get(AppearanceProperty);
            ApplyTheme(appearance);
            Background = appearance.Surface;
        });
    }
}
