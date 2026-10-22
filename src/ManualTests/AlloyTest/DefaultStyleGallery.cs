using TrueMoon.Argentis;

namespace AlloyTest;

/// <summary>Interactive inventory of the built-in square style; all widgets use public APIs.</summary>
public sealed class DefaultStyleGallery : ContentControl
{
    /// <summary>Editable field used by the directed focus proof.</summary>
    public TextBox NameEditor { get; }
    /// <summary>Interactive overflowing list.</summary>
    public ScrollViewer Scroller { get; }
    /// <summary>Creates an independent gallery with theme-switching actions.</summary>
    public DefaultStyleGallery()
    {
        NameEditor = new TextBox { Value = "TrueMoon — новый проект", AccessibleName = "Название проекта" };
        var status = new Text("Готово к работе");
        var slider = new Slider { Value = .62f }; var progress = new ProgressBar { Value = .62f };
        slider.PropertyChanged += (_, property) => { if (ReferenceEquals(property, ProgressBar.ValueProperty)) progress.Value = slider.Value; };
        var normal = new Button("Обычная");
        var accent = new Button("Применить") { Tone = ControlTone.Accent }.OnClick(() => status.Value = "Изменения применены");
        var positive = new Button("Создать") { Tone = ControlTone.Positive }.OnClick(() => status.Value = "Проект создан");
        var danger = new Button("Удалить") { Tone = ControlTone.Danger }.OnClick(() => status.Value = "Пример действия удаления");
        var items = new VStack { Spacing = 6 };
        for (var i = 1; i <= 12; i++) items.Add(new Button($"Сцена {i:00}") { Height = 32 });
        Scroller = new ScrollViewer { Height = 116 }.Content(items);
        var left = new VStack { Spacing = 10, Width = 440 }.WithChildren(
            Heading("Поля и состояния"), new Text("Название проекта · фокус с клавиатуры"), NameEditor,
            new TextBox { Value = "Только чтение · можно выделить и скопировать", IsReadOnly = true },
            new TextBox { Value = "Укажите корректное имя", HasError = true, AccessibleName = "Имя с ошибкой" },
            new ErrorLabel("Ошибка: имя должно быть уникальным"),
            new TextBox { Value = "Недоступное поле", IsEnabled = false },
            new HStack { Spacing = 12 }.WithChildren(new CheckBox("Сетка") { IsChecked = true }, new CheckBox("Привязка"), new CheckBox("Заблокировано") { IsEnabled = false }),
            Heading("Многострочный редактор"),
            new RichTextEditor { Text = "Редактирование текста без лишнего оформления.\nCtrl+B · жирный, Ctrl+I · курсив, Ctrl+Z · отмена.", Height = 100 },
            new HStack { Spacing = 6 }.WithChildren(new IconButton(UiSymbol.Check, "Применить форматирование"), new Button("Формат") { Tone = ControlTone.Accent }),
            status);
        var right = new VStack { Spacing = 10, Width = 440 }.WithChildren(
            Heading("Цветные действия"), new HStack { Spacing = 8 }.WithChildren(normal, accent, positive, danger),
            new HStack { Spacing = 8 }.WithChildren(new Button("Hover") { IsHovered = true }, new Button("Недоступно") { IsEnabled = false }),
            new Text("Прямые углы · бордер 1 · фокус 2 · маркер ошибки 3"),
            Heading("Значения"), slider, progress,
            Heading("Список и полоса прокрутки"), Scroller,
            new Border { Padding = new Thickness(12) }.Content(new VStack { Spacing = 4 }.WithChildren(
                new Text("Tab — переход между контролами"), new Text("Enter / Space — активация кнопки"), new Text("Колесо / перетаскивание — прокрутка"))),
            new HStack { Spacing = 8 }.WithChildren(new Button("Светлая тема").OnClick(() => Switch(Theme.Light)), new Button("Тёмная тема").OnClick(() => Switch(Theme.Dark))));
        SetContent(new VStack { Padding = new Thickness(24), Spacing = 18 }.WithChildren(
            new Text("TrueMoon Alloy") { FontSize = 28 }, new Text("Стандартное оформление контролов и окна"),
            new HStack { Spacing = 24 }.WithChildren(left, right)));
    }
    /// <inheritdoc />
    protected override Color ThemeBackground => Theme.Surface;
    private static Text Heading(string value) => new(value) { FontSize = 20 };
    private sealed class ErrorLabel(string value) : Text(value) { protected override Color ThemeForeground => Theme.Error; }
    private void Switch(Theme theme)
    {
        Element root = this; while (root.Parent != null) root = root.Parent;
        root.ApplyTheme(theme with { FontFamily = Theme.FontFamily });
    }
}
