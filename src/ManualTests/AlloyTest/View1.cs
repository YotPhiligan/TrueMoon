using TrueMoon.Argentis;
using TrueMoon.Alloy.Rendering.Skia;

namespace AlloyTest;

/// <summary>A settings form mixing static factories and public constructors.</summary>
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
    internal Image Logo { get; }
    internal VStack SettingsFields { get; }

    /// <summary>Creates the retained form for a model supplied by DI.</summary>
    /// <param name="model">The state preserved independently of the controls.</param>
    public View1(SettingsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var logoSource = new SkiaImageSource(Path.Combine(AppContext.BaseDirectory, "Assets", "alloy-mark.png"));
        Own(logoSource);
        Logo = new Image { Source = logoSource, Width = 48, Height = 48 };
        LightButton = Button.Simple("Светлая").OnClick(model.UseLight);
        DarkButton = new Button("Тёмная").OnClick(model.UseDark);
        CompactToggle = CheckBox.Create("Компактно").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.IsCompact);
        EditingToggle = new CheckBox("Ввод включён").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.ControlsEnabled);
        NameEditor = TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, x => x.Name);
        NotificationToggle = CheckBox.Create("Получать уведомления").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.Enabled);
        VolumeSlider = new Slider().BindTwoWay(ProgressBar.ValueProperty, model, x => x.Volume);
        Meter = new VolumeMeter().Bind(ProgressBar.ValueProperty, model, x => x.Volume);
        SettingsFields = VStack.Create().Bind(UiProperties.Enabled, model, x => x.ControlsEnabled).WithChildren(
            new Text("Имя"), NameEditor, NotificationToggle, Text.Plain("Громкость"), VolumeSlider, Meter);
        StatusRegion = new LoadStatusRegion(status => status.Phase switch
        {
            LoadPhase.Loading => VStack.Create().Configure(stack => stack.Spacing = 6).WithChildren(
                Text.Plain(status.Message), new ProgressBar { Value = .5f, Height = 8 }),
            LoadPhase.Error => HStack.Create().Configure(stack => stack.Spacing = 12).WithChildren(
                Text.Plain(status.Message).Width(340), Button.Simple("Повторить").OnClick(model.BeginLoading)),
            _ => Text.Plain(status.Message)
        }).Height(48).Bind(LoadStatusRegion.StatusProperty, model, x => x.LoadStatus);
        RowList = VStack.Create().Configure(stack => stack.Spacing = 8)
            .BindItems(model.Rows, row => HStack.Create().Configure(stack => stack.Spacing = 8).WithChildren(
                TextBox.Create().Width(260).BindTwoWay(TextBox.ValueProperty, row, x => x.Name),
                Button.Simple("↑").OnClick(() => model.MoveRowUp(row)),
                new Button("Заменить").OnClick(() => model.ReplaceRow(row)),
                Button.Simple("Удалить").OnClick(() => model.RemoveRow(row))));
        RowScroll = ScrollViewer.Create().Height(160).Content(RowList);
        Content(model, _ => VStack.Create()
            .Width(720)
            .Padding(24)
            .WithChildren(
                HStack.Create().WithChildren(Logo, Text.Plain("Настройки").Configure(text => text.FontSize = 24)),
                HStack.Create().WithChildren(LightButton, DarkButton, CompactToggle, EditingToggle),
                SettingsFields,
                Border.Create().Padding(12).Content(Text.Plain().BindText(model, x => x.Summary)),
                StatusRegion,
                HStack.Create().Configure(stack => stack.Spacing = 12).WithChildren(
                    Button.Simple("Загрузка").OnClick(model.BeginLoading),
                    Button.Simple("Ошибка").OnClick(model.ShowLoadError),
                    Button.Simple("Готово").OnClick(model.CompleteLoading)),
                HStack.Create().Configure(stack => stack.Spacing = 12).WithChildren(
                    Button.Simple("Загрузить пример").OnClick(model.LoadExample),
                    new Button("Сбросить").OnClick(model.Reset)),
                Text.Plain("Дела").Configure(text => text.FontSize = 20),
                HStack.Create().Configure(stack => stack.Spacing = 12).WithChildren(
                    Button.Simple("Добавить дело").OnClick(model.AddRow),
                    new Button("Развернуть (Reset)").OnClick(model.ResetRows)),
                RowScroll
            ));
        this.Bind(AppearanceProperty, model, x => x.Appearance);
    }

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
