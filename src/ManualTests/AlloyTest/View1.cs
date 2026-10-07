using TrueMoon.Argentis;

namespace AlloyTest;

/// <summary>A settings form mixing static factories and public constructors.</summary>
public sealed class View1 : View<SettingsModel>
{
    /// <summary>Creates the retained form for a model supplied by DI.</summary>
    /// <param name="model">The state preserved independently of the controls.</param>
    public View1(SettingsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Content(model, _ => VStack.Create()
            .Width(600)
            .Padding(24)
            .Configure(stack => stack.Spacing = 12)
            .WithChildren(
                Text.Plain("Настройки").Configure(text => text.FontSize = 24),
                new Text("Имя"),
                TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, x => x.Name),
                CheckBox.Create("Получать уведомления")
                    .BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.Enabled),
                Text.Plain("Громкость"),
                new Slider().BindTwoWay(ProgressBar.ValueProperty, model, x => x.Volume),
                Border.Create().Padding(12).Content(Text.Plain().BindText(model, x => x.Summary)),
                HStack.Create().Configure(stack => stack.Spacing = 12).WithChildren(
                    Button.Simple("Загрузить пример").OnClick(model.LoadExample),
                    new Button("Сбросить").OnClick(model.Reset))
            ));
    }
}