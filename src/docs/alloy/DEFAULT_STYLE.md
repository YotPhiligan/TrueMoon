# Стандартное оформление Alloy / Argentis

Принято и реализовано2026-10-22 в рамках
[PA8a](PLAN.md#pa8a-дефолтный-стиль-контролов-и-оформление-окна--2026-10-21).
Плоские цветные поверхности, прямые углы и тонкие бордеры используются обычными
конструкторами и Fluent factories. Начальная тема — `Theme.Dark`; светлая —
`Theme.Light`. Переключение через `UiSession.SetTheme` сохраняет значения,
выделение, фокус и scroll offset.

## Тема и состояния

С2026-10-24 dropdown меню используют `Theme.MenuBackdrop`: Gaussian sigma12logical
pixels, Dark tint `#12161E/B0`, Light tint `#F2F5FA/C0`, opaque fallback того же
цвета. `MenuBackdrop = null` возвращает сплошной `Surface`. Idle/disabled rows
не закрывают material, hover/pressed и focus outline сохраняются.

Материал можно назначить любому элементу через `WithBackdrop(...)`,
`Element.Backdrop`, `Style.With(UiProperties.Backdrop, ...)` или
`IViewStyle` → `rule.Backdrop(theme => theme.MenuBackdrop)`. Обычный `Background`
рисуется поверх материала: оставляйте его прозрачным для видимого эффекта.
Например, оформление части View:

```csharp
styles.Part(view => view.Surface)
    .Background(Color.Transparent)
    .Backdrop(theme => theme.MenuBackdrop);
```

Размывается уже нарисованный фон текущего окна, foreground/children остаются
чёткими. Полупрозрачность OS окна/desktop этим API не включается. Контекст без
поддержки backdrop получает непрозрачный fallback; sigma0 означает tint без blur.
[Контракт и границы](PLAN.md#pa8b-матовые-поверхности--2026-10-24).

| Назначение | Dark | Light |
| --- | --- | --- |
| Surface / Control | `#12161E` / `#202834` | `#F2F5FA` / `#FFFFFF` |
| Foreground / Border | `#E7EDF5` / `#687990` | `#182231` / `#728093` |
| ControlHover / ControlPressed | `#2D3A4C` / `#194168` | `#E2EBF7` / `#CADCF1` |
| Input / ReadOnly | `#161C26` / `#1B222D` | `#FFFFFF` / `#E9EEF5` |
| ControlDisabled / Disabled | `#191F29` / `#8E9AAA` | `#E2E7EF` / `#5C697A` |
| Focus / Error | `#FFC95C` / `#FF7080` | `#8E4800` / `#AE1E33` |
| Accent / Positive / Danger | `#1868B2` / `#18775B` / `#BE2B3D` | те же |
| OnAccent | `#FFFFFF` | `#FFFFFF` |

`CornerRadius=0`, `BorderWidth=1` в теме. Button padding остаётся12×8,
editor padding8; StackSpacing по умолчанию0. Композиция задаёт свои интервалы
через `Spacing`. Размеры — логические единицы, масштабирование выполняет renderer.

`Button.Tone` выбирает `Neutral`, `Accent`, `Positive`, `Danger`. Цветные действия
используют `OnAccent`, слегка светлеют при hover и темнеют при нажатии. CheckBox
имеет отдельный квадратный индикатор с галочкой; Slider — прямоугольный thumb.
ProgressBar заполняет внутреннюю область бордера. ScrollViewer при переполнении
резервирует справа10единиц под track, thumb шириной6 с минимальной высотой20;
поддерживает колесо и pointer capture. `ShowScrollBar=false` оставляет wheel-only
viewport без зарезервированной полосы. Скрытие полосы отменяет drag при следующем
событии указателя.

Фокус отмечен внутренним контуром толщиной2. `HasError=true` добавляет цвет ошибки
и левый маркер шириной3. `TextBox.IsReadOnly` и `RichTextEditor.Options.IsReadOnly`
имеют отдельную поверхность и правый маркер; выделение, навигация и копирование
доступны, пользовательские изменения запрещены. Программное обновление модели
остаётся доступным. Disabled использует отдельные surface/text цвета и блокирует
ввод через существующий effective-enabled contract.

```csharp
var apply = new Button("Применить") { Tone = ControlTone.Accent };
var name = new TextBox { Value = "Project", HasError = true };
var readOnly = new TextBox { Value = "Selected asset", IsReadOnly = true };
var outlined = new Style().With(UiProperties.BorderWidth, 2f)
    .With(UiProperties.BorderColor, new Color(24, 104, 178));
name.Style = outlined;
ui.SetTheme(Theme.Light);
```

Приоритет: **local → style → theme → property default**. `Clear(property)`
снимает local override; тема не перезаписывает значения стиля. Общие appearance
properties: `Background`, `Foreground`, `Tone`, `HasError`, `CornerRadius`,
`BorderWidth`, `BorderColor`. Отрицательные/нечисловые радиусы и ширины отвергаются;
диапазоны radius0–64 и border0–16. Стандартный бордер рисуется внутри Bounds,
квадратный Skia stroke — четырьмя неперекрывающимися полосами, включая translucent
corners. Rounded overrides сохраняют прежний Skia round-rect путь.

`IconButton(UiSymbol.Close, "Закрыть")` и `UiSymbols.Draw` используют геометрию,
независимую от шрифта. Доступны minimize/maximize/restore/close/check/resize grip.
`AccessibleName` — метаданные контрола; системный accessibility bridge этим API
не реализован. У собственных контролов приложение задаёт подходящие цвета текста
поверх самостоятельно нарисованных поверхностей.

## Компактная строка состояния

Общий `StatusBar` располагает primary, action, упорядоченные secondary и details.
При нехватке ширины сначала скрываются более ранние secondary; visibility самих
контролов и binding сохраняются. Скрытые сегменты исключаются из Tab/input,
расширение возвращает те же экземпляры. Перенос данных/команд в details обеспечивает
View приложения. Фокус скрытого сегмента возвращается на details на owner frame.

Editor использует высоту28, текст12, геометрические иконки14 и targets24.
Фон — `Control`, верхний разделитель — `Border`; справа24единицы оставлены под
native resize grip. Ready использует Foreground, Busy/Running — Accent,
Paused/Waiting — Focus, Error — Error. Цвет дополняет текст и иконку состояния.
Всплывающие подробности и Edit Preview используют `Theme.MenuBackdrop` с opaque
fallback; появляются над footer и сохраняют дерево/фокус/выделение при смене темы.
[Общий контракт](PLAN.md#pa4b-statusbar--2026-10-24).

## Стандартная рамка окна

Обычное `UseSilkWindow` сохраняет системное оформление. Для стандартной собственной
рамки достаточно явно выбрать borderless appearance и Chrome:

```csharp
context.UsePresentation(resolver => new MyView(), options => options
    .UseSkiaOpenGL()
    .UseSilkWindow(window =>
    {
        window.Title = "TrueMoon Editor";
        window.Appearance = new WindowAppearance { Decorated = false };
        window.Chrome = new WindowChromeOptions
        {
            NativeDrag = true, NativeSnapLayouts = true,
            MinimumSize = new Size(330, 300)
        };
    }));
```

Namespaces: `TrueMoon.Argentis`, `TrueMoon.Alloy.Runtime`,
`TrueMoon.Alloy.Hosting`. Нативных API в Argentis нет.

`UseDefaultWindowFrame=true` по умолчанию, действует при Chrome и отсутствии
`TitleBarFactory`. Пользовательская title factory имеет приоритет;
`UseDefaultWindowFrame=false` оставляет исходный root для собственной композиции.
Самостоятельно созданный WindowFrame также требует этого opt-out, чтобы host
не добавил второй заголовок.

`WindowFrame.CreateDefault(title, content)` создаёт border1, title bar36,
квадратные buttons32 и grip16 в отдельной нижней строке. Content не перекрывается
грипом. `WindowTitleBar.Icon` заимствует изображение, его освобождает вызывающая
сторона. `DefaultWindowFrameBinding` один раз связывает кнопки с borrowed
`IWindowCommands`; UI tree освобождается раньше окна. В HostedUiWindow связь
создаётся автоматически, close проходит через `RequestCloseAsync`, включая veto,
asynchronous save/drain и cleanup. Minimize/maximize/restore и доступность grip
согласуются с реальными capabilities/state. Maximized/fixed-size окно не имеет
resize grip; maximize недоступен при запрещённом resizing. Active/inactive
меняет поверхность и линию заголовка без пересоздания дерева.

Caption остаётся native drag/system menu region, enabled maximize — native Snap
region. Толщина невидимой resize-границы по умолчанию6 не связана с visual border1.
Угловой виджет использует native bottom-right hit-test. UI команды и drag/resize
проверяются на owner thread.

## Галерея и проверки

```powershell
dotnet run --project ManualTests/AlloyTest -- --style-gallery
# Для Vulkan: добавить --vulkan.
dotnet run --project ManualTests/AlloyVulkanTest -- `
  --style-gallery --style-output TestResults/DefaultStyle/new-gallery
dotnet run --project ManualTests/AlloyVulkanTest -- --default-frame --validation
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/RunSettingsGolden.ps1 `
  -ValidationLayerPath <path-to-installed-validation-layer>
```

Галерея содержит поля, error/read-only/disabled/hover, цветные actions, RTE,
progress/slider, scrollbar, theme switch и стандартную рамку. Raster proof
сохраняет Dark/Light при synthetic100/150/200%. Native proof проверяет четыре
OpenGL/Vulkan ×fixed/resizable окна, window commands/regions и close veto/drain.
SettingsSquare — отдельная серия30golden PNG; прежний Settings baseline сохранён.
[Актуальные результаты и ограничения](STATUS.md), [полный журнал](HISTORY.md),
[правила golden/font provenance](GOLDEN_TESTS.md).

Physical multi-monitor DPI/OS input, real device loss, VRAM и K7/final alpha
сохраняют отдельные критерии приёмки. Их не заменяют synthetic PNG или этот
ограниченный native run.
