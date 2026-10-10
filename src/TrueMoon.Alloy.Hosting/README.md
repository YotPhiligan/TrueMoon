# Подключение Alloy к TrueMoon App

Проект предоставляет единый модуль регистрации UI. Настройка выполняется до `Build()` и не создаёт окно или GPU-контекст. Целевая платформа — `net10.0`; поставляемая native Skia для Vulkan/OpenGL/raster пока проверена на Windows x64.

## Миграция alpha API: PLAN 2.1a

Связка Silk/Vulkan renderer находится в сборке и namespace `TrueMoon.Alloy.Hosting`. Для convenience API добавьте ссылку на Hosting и `using TrueMoon.Alloy.Hosting;`:

| Прежний API в Rendering.Skia | API после 2.1a |
| --- | --- |
| `VulkanHostContext.FromSilkDevice(device)` | `SilkVulkanHost.CreateContext(device)` → `VulkanHostContext` |
| `new VulkanUiTarget(device)` с `VulkanDevice` | `SilkVulkanHost.CreateTarget(device)` → `VulkanUiTarget` |
| `new VulkanInteropSurface(device, context, size)` с `VulkanDevice` | `SilkVulkanHost.CreateSurface(device, context, size)` → `VulkanInteropSurface` |
| `TrueMoon.Alloy.Rendering.Skia.VulkanWindowPresenter` | `TrueMoon.Alloy.Hosting.VulkanWindowPresenter` |
| `TrueMoon.Alloy.Rendering.Skia.VulkanPresentationResources` | `TrueMoon.Alloy.Hosting.VulkanPresentationResources` |
| `VulkanUiTarget.Device` | Свойство удалено; descriptor доступен через `VulkanUiTarget.Host`, а Silk device хранит хост |

`device` в factories — `TrueMoon.Alloy.Platform.Silk.VulkanDevice`, `context` — `SkiaSharp.GRContext`, `size` — `SkiaSharp.SKSizeI`. Presenter и его resource counters перенесены из renderer в Hosting без изменения алгоритма retirement, `Present`/`PresentImage`, владения и синхронизации. Это изменение сборочной принадлежности и namespace alpha API, не обещание binary compatibility: обновите ссылки/imports/callers и пересоберите потребителей.

Raw `VulkanHostContext` constructors, `new VulkanUiTarget(host)` и `new VulkanInteropSurface(host, context, size)` остаются в Rendering.Skia. Прямые raw Vulkan/raster/OpenGL сессии через `UiSession.Create` не требуют Platform.Silk или Hosting; App/DI и оконные adapters опциональны. После 2.1b через Alloy также больше не приходят транзитивные TrueMoon.Core/Contracts, Silk.NET.Windowing или legacy Skia/Silk/Topten зависимости. `Silk.NET.Vulkan` остаётся backend-specific зависимостью renderer, а не оконным API.

## Миграция alpha API: PLAN 2.1b

31 legacy-файл из корня Alloy и `Presenters` перенесён в `TrueMoon.Alloy.Hosting/Compatibility`: сборка теперь `TrueMoon.Alloy.Hosting`, namespace — `TrueMoon.Alloy.Hosting.Compatibility` и `TrueMoon.Alloy.Hosting.Compatibility.Presenters`. Перенесены Visual/VisualTree, IGraphicsPlatform, ViewHandle/ViewManager, legacy presenters и регистрация presentation. Это **breaking assembly/namespace migration без type forwarders**: одной замены DLL недостаточно, consumers должны обновить project references/imports/callers и пересобраться.

- `TrueMoon.Alloy` содержит только исходники `Runtime` и csproj; единственный ProjectReference — Argentis, PackageReference и ссылок на Core/Contracts нет. Обычный `UiSession` не требует App, Skia или Silk.
- Hosting явно ссылается на Alloy, Rendering.Skia, Platform.Silk, Core/Contracts и пакеты Silk.NET, SkiaSharp, Topten.RichTextKit. Platform.Silk не зависит от Skia.
- No-options `UsePresentation<TView>()` доступен только при явном opt-in `using TrueMoon.Alloy.Hosting.Compatibility;`. Для нового UI этот путь не рекомендуется: `Visual.GetEnumerator` и legacy lifetime bugs переносом **не исправлены**. Закомментированные прототипы не являются рабочими рендерерами. [Границы compatibility и миграция](Compatibility/README.md).
- Новый explicit API в `TrueMoon.Alloy.Hosting` не изменён: `UseAlloy(...)` или `UsePresentation<TView>(options => options.UseSkiaOpenGL().UseSilkWindow())`. [AlloyTest](../ManualTests/AlloyTest/README.md) уже использует explicit OpenGL/Silk; retained View1/View2 не изменены.

Native ABI, GPU lease/ownership и алгоритм retirement в 2.1b не менялись. Фактические build/tests/GPU/publish проверки переноса и оставшиеся ограничения записаны в [STATUS](../docs/alloy/STATUS.md). Завершение 2.1 не означает завершения 2.3/alpha.

## Прозрачность standalone-окна

```csharp
options.UseSkiaVulkan().UseSilkWindow(window =>
{
    window.Appearance = new WindowAppearance
    {
        Transparency = WindowTransparencyMode.PerPixel,
        Decorated = false
    };
});
```

Настройки `WindowAppearance` находятся в независимом Alloy contract и фиксируются при создании окна. По умолчанию — `Opaque`, opacity1, стандартная рамка. `PerPixel` использует premultiplied alpha UI: непрозрачный фон view по-прежнему закрывает desktop; для прозрачных областей задайте соответствующую alpha в самом UI. Vulkan требует advertised PreMultiplied composite alpha; неподдержанный framebuffer/surface даёт NotSupportedException при StartAsync, без смены backend. OpenGL использует alpha framebuffer.

`Opacity` — отдельный режим, например `new WindowAppearance { Transparency = WindowTransparencyMode.Opacity, Opacity = .5f }`. `HostedUiWindow.SetOpacity(value)` ставит изменение в очередь owner thread; `SilkWindowHost.SetOpacity(value)` выполняется на owner thread напрямую. Значение должно быть конечным и в0–1. Другие режимы допускают только initial Opacity1 и запрещают SetOpacity. Общая opacity в этом adapter заявлена для Windows; это ограничение явно отражает `AppearanceCapabilities.WindowOpacity`.

`AppearanceCapabilities` сообщает native transparent framebuffer и поддержку uniform opacity. Это не доказательство Vulkan composition: `VulkanWindowPresenter.SupportsPerPixelTransparency` отдельно проверяет surface flag, а `HostedUiWindow.SupportsPerPixelTransparency` сочетает обе проверки после успешного запуска. При прямом использовании SilkWindowHost передавайте `host.Appearance.Transparency` в `new VulkanWindowPresenter(device, mode)`; no-argument presenter сохраняет прежнюю alpha precedence для существующих HUD/opaque callers. Uniform opacity применяет оконный host, а не GPU renderer.

Windows per-pixel adapter инициализирует DWM redirection surface через PatBlt(BLACKNESS) на creation/paint/size/show/DPI/composition событиях. WM_PAINT очищается до передачи сообщения GLFW, чтобы синхронный refresh мог уже представить корректный frame. Очистки на обычном presentation frame нет. Subclass и GC root снимаются до уничтожения HWND на owner thread; native ошибки сохраняются и сообщаются из managed loop. Управляемый click-through не включён: на проверенной Windows alpha0/128/255 области по WindowFromPoint принадлежат этому окну. Отключение рамки не создаёт собственного заголовка, resize regions или Snap — это следующие подэтапы4.9c/d. DPI/monitor transitions и другие GPU/Windows конфигурации требуют отдельного native прохода; [актуальные проверки](../docs/alloy/STATUS.md).

## HUD в игре

```csharp
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

builder.Setup(app => app.UseAlloy(options => options
    .UseSkiaVulkan()
    .UseExternalHost()));

// После Build: на потоке игрового рендеринга.
var factory = (IUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
using var ui = factory.Create(hudRoot, SilkVulkanHost.CreateTarget(device),
    new UiViewport(framebufferWidth, framebufferHeight, dpiScale));

ui.HandleInput(input);                 // Координаты ввода — логические.
ui.Update();                           // Layout/рисование только при invalidation.
var lease = ui.AcquireVulkanTexture();
try
{
    // Использовать lease.Info.Image в Vulkan-композиции сцены/HUD.
    // Завершить GPU-команды и сохранить фактический конечный image layout.
}
finally
{
    lease.Return(finalImageLayout);
}
```

Игра владеет устройством, очередью, окном, swapchain и циклом кадров. Сессия владеет UI-деревом и своим Skia-контекстом/поверхностью. `VulkanUiTarget` принимает raw-handle `VulkanHostContext`; для `Platform.Silk.VulkanDevice` используется `SilkVulkanHost.CreateTarget(device)` из Hosting. Все обращения к очереди последовательны; текущая синхронизация использует ожидание GPU. Формат UI — RGBA, alpha premultiplied; внешний compositor должен использовать соответствующее blending.

`Resize`, `Update` и `Dispose` запрещены, пока image заимствован. Lease возвращают явно с фактическим layout; автоматического `Dispose` у него нет. UI-сессию создают и освобождают на одном потоке. До остановки App с другого потока игра должна освободить свои сессии на потоке рендеринга. При остановке на том же потоке registry освобождает оставшиеся сессии. Устройство освобождают после UI и compositor. `ui.Post(...)` позволяет передать изменение с другого потока для выполнения в следующем `Update`.

Подключение без App/DI также поддерживается:

```csharp
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

using var ui = UiSession.Create(hudRoot, new SkiaVulkanRenderBackend(),
    SilkVulkanHost.CreateTarget(device), new UiViewport(width, height, scale));
```

## Создание контролов и Fluent-разметка

Согласованный подход (2026-10-06): публичные конструкторы и статические фабрики на типах контролов — два равноправных способа создания элементов. Их можно смешивать в одной разметке. Фабрики упрощают запись; последующие Fluent-вызовы настраивают тот же конкретный элемент.

Фабрики доступны с 2026-10-07. Пример использует существующие `Button.Simple(...)`, `Text.Plain(...)`, `new` и Fluent helpers. Методы Apply/Cancel принадлежат пользовательскому View.

```csharp
Content(() =>
    new VStack()
        .Padding(16)
        .WithChildren(
            Text.Plain("Настройки"),

            Button.Simple("Применить")
                .Width(160)
                .OnClick(Apply),

            new Button("Отмена")
                .OnClick(Cancel)
        )
);
```

`Button.Simple("Применить")` создаёт обычную кнопку с оформлением текущей темы; `Text.Plain("Настройки")` — обычный текст без обработки разметки. Их начальные свойства и поведение соответствуют `new Button("Применить")` и `new Text("Настройки")`.

| Типы | Фабрика |
| --- | --- |
| Button / Text | `Button.Simple(string text = "")` / `Text.Plain(string value = "")` |
| CheckBox | `CheckBox.Create(string text = "")` |
| TextBox, ProgressBar, Slider, Image | Собственный `Create()` на каждом типе |
| Panel, ContentControl, Border, ScrollViewer | Собственный `Create()` на каждом типе |
| VStack, HStack, Line, Rectagle | Собственный `Create()` на каждом типе |

Create использует исходные значения конструктора. Slider/Border/ScrollViewer объявляют свой Create с конкретным возвращаемым типом. Пользовательским наследникам нужно объявлять собственную фабрику; статические фабрики не виртуальны. Shapes.Line/Rectagle сохраняются как прежние helpers; пустой Line пока не рисует линию.

Каждая фабрика возвращает новый экземпляр своего конкретного типа: после `Button.Simple(...).Width(...)` доступны методы кнопки, включая OnClick. Фабрики объявляются в Argentis и работают независимо от Hosting, App/DI, окна и renderer. Публичные конструкторы сохраняются, в том числе для наследования. Способ создания не меняет ownership, attach/detach, subscriptions или обновление постоянного дерева.

[Требования — пункт 4.8](../docs/alloy/PLAN.md#этап-4-добавить-ввод-контролы-и-оформление). [Фактическое состояние](../docs/alloy/STATUS.md). [Редактируемая форма AlloyTest](../ManualTests/AlloyTest/README.md) использует фабрики и конструкторы вместе с BindTwoWay.

## Тема, отступы и прокрутка

`UiSession.SetTheme` обновляет существующее дерево; новые дети наследуют текущую тему. Local typed value имеет приоритет над Style, Style — над Theme. Typography/Foreground и `UiProperties.Padding`/`Spacing` поддерживают этот порядок; `Clear` открывает следующий уровень.

```csharp
ui.SetTheme(Theme.Light with
{
    ButtonPadding = new Thickness(8, 4, 8, 4),
    EditorPadding = new Thickness(4),
    StackSpacing = 6
});
var button = Button.Simple("Действие"); // theme ButtonPadding
button.Style = new Style().With(UiProperties.Padding, new Thickness(10));
button.Padding = new Thickness(16);   // local overrides style
button.Clear(UiProperties.Padding);   // restores style padding
```

Button/TextBox используют свои theme tokens, CheckBox добавляет24px для индикатора. Остальные элементы по умолчанию имеют padding0; StackSpacing по умолчанию0. Исходные constructor layout defaults сохранены. Tokens валидируют конечные неотрицательные значения. Явные локальные `.Padding(...)`/`.Spacing` продолжают перекрывать тему.

`Element.ChildClipBounds` задаёт viewport детей для drawing и hit testing. ScrollViewer исключает padding, wheel bubbles при достижении границы, а `UiSession.Focus`/Tab раскрывает focused child через `BringIntoView(Rect)` от внутреннего viewport к внешнему. Rect передаётся в root coordinates после layout, Offset ограничивается текущим Extent. Прокрутка вертикальная, oversized rectangle выравнивается сверху. [AlloyTest](../ManualTests/AlloyTest/README.md) показывает тему/плотность, disabled fields, изображение и custom VolumeMeter.

## Собственный заголовок и оконные команды

Windows x64 custom frame включается отдельно от прозрачности:

```csharp
options.UseSilkWindow(window =>
{
    window.Appearance = new WindowAppearance { Decorated = false };
    window.Chrome = new WindowChromeOptions
    {
        NativeDrag = true, // явное включение системного перемещения
        NativeSnapLayouts = true, // Windows 11, собственная maximize/restore кнопка
        MinimumSize = new Size(480, 300),
        MaximumSize = new Size(1600, 1000)
    };
    window.TitleBarFactory = commands => new HStack
    {
        Height = 44, WindowRegion = WindowRegionRole.Caption
    }.WithChildren(
        new Text("Моё приложение").Width(240),
        new TextBox().Width(180),
        new Button("—").OnClick(commands.Minimize),
        new Button("□") { WindowRegion = WindowRegionRole.Maximize }.OnClick(() =>
        {
            if (commands.State == UiWindowState.Maximized) commands.Restore();
            else commands.Maximize();
        }),
        new Button("×").OnClick(commands.Close));
});
```

`TitleBarFactory` вызывается на owner thread с независимым `IWindowCommands`. Hosting оборачивает исходный view в `WindowFrame`: заголовок получает свою высоту, content — оставшуюся клиентскую область. Сессия владеет обоими элементами и освобождает их. Factory должна вернуть новый unowned live element; private allocations до исключения остаются ответственностью factory. После создания root сессии будет WindowFrame; исходный view находится в его Children. Для полностью собственного layout можно оставить factory пустой и задавать Caption/Client непосредственно в view.

`WindowRegion` наследуется дочерними элементами. Focusable controls остаются Client; исключение — явно заданная `WindowRegionRole.Maximize` на enabled maximize/restore кнопке. Disabled кнопка остаётся Client. `WindowRegionRole.Client` исключает весь subtree; такой ролью помечайте собственные интерактивные элементы, которые не принимают keyboard focus. Учитываются clipping, visibility и paint order. Геометрия копируется после UI update: native callback не вызывает UI/пользовательские delegates и не удерживает дерево. На прямом `IWindowChromeHost` после layout вызывайте `SetWindowRegions(WindowRegionMap.Create(root))`; при изменении размеров устаревшие native regions временно отключаются до новой геометрии.

`NativeDrag` по умолчанию false. `Resizable=false` отключает невидимые границы и maximize; otherwise доступны восемь resize codes. `ResizeBorder` задан в 96-DPI единицах и масштабируется по текущему DPI HWND. `MinimumSize`/`MaximumSize` заданы в логических координатах UI: native minimum округляется вверх, maximum вниз по GetDpiForWindow(HWND)/96. Native maximize учитывает текущий monitor work area и application maximum. Для custom frame используйте `SilkWindowHost.Resize`/`HostedUiWindow.Resize`: прямой GLFW Size setter предполагает стандартную рамку и не является этим контрактом.

На Windows Width/Height в UseSilkWindow и параметры Resize — логические 96-DPI размеры клиента. Например,600×400 при150% дают framebuffer900×600 и UiViewport.Scale=1.5. NativeWindow.Size/Position и Win32 client/screen coordinates — физические pixels; SilkWindowHost.Input уже нормализован, повторно делить его на Scale не нужно. Scale берётся из native HWND DPI даже при одинаковых framebuffer/client dimensions. При смене DPI сохраняется логический client size; Hosting обновляет layout до обработки input, custom frame отвергает старые regions. Создание и loop используют scoped per-monitor-v2 thread context с восстановлением caller context. Внешний HUD сам задаёт viewport/Scale и координаты input. [Контракт и границы проверок](../docs/alloy/PLAN.md#контракт-windows-dpi-49d--k3--уточнение-2026-10-09).

Через `HostedUiWindow.Minimize/Maximize/Restore/Resize/Close` команды ставятся на owner thread. `ChromeCapabilities` после startup сообщает поддержку adapter, отдельно от включённых опций и appearance/graphics capabilities. Без Chrome сохраняется прежний GLFW frame; unsupported platform/configuration сообщает исключение. HUD сохраняет оформление окна своего хоста. Windows subclass снимается до уничтожения HWND, callback errors переходят в managed loop/Completion.

`NativeSnapLayouts` default false, требует Resizable и Windows 11; unsupported configuration отклоняется до allocation. Maximize region получает HTMAXBUTTON; non-client hover/leave передаётся DWM. Windows сама показывает Snap UI согласно своим настройкам. `SnapLayouts` capability означает поддержку платформы, а не факт появления flyout при текущих пользовательских настройках. Argentis получает hover/press/release через очередь вне native callback; OnClick вызывается один раз, отпускание вне кнопки и отмена capture не активируют команду. Direct Silk hosts после `NativeWindow.DoEvents()` вызывают `DispatchPendingWindowInput()` до Update; Hosting делает это в своём цикле. Space/Enter остаются обычным routed UI input.

`SystemMenu` default true включает Alt+Space и caption right-click. Необязательный `IWindowSystemMenu.ShowSystemMenu(x,y)` открывает нативное меню в logical client coordinates на owner thread; `HostedUiWindow.ShowSystemMenu` ставит ту же операцию в очередь. Menu Move/Size/Minimize/Maximize/Restore учитывает состояние и Resizable. При SystemMenu=false сервис отклоняет вызов, обе жестовые точки входа подавляются. Double-click Caption отдаётся системной обработке maximize/restore.

Для Snap в узкие зоны выбирайте разумный minimum width и адаптивный title layout: [демонстрационный заголовок](../ManualTests/AlloyTest/CustomTitleBar.cs) сохраняет три кнопки и editor при minimum330. Адаптер снимает GLFW WS_POPUP, сохраняет resize/commands и использует HTCAPTION без WS_CAPTION; системное оформление не занимает client area. Реальные drag/resize, maximized drag restore, края/углы, Snap bar, hover и Win+Z проверяются отдельно от directed-message contract. Глобальная горячая клавиша может перехватить Alt+Space до получения сообщения окном, например PowerToys Run. Полная settings/DPI/monitor matrix остаётся открыта; фактические результаты — [STATUS](../docs/alloy/STATUS.md).

## Clipboard и ошибки ввода

Windows SilkWindowHost использует Win32 для чтения и записи текста на owner thread до Dispose. Get возвращает null без текстового формата. Set заменяет содержимое clipboard; draft HGLOBAL принадлежит адаптеру до успешной передачи Windows, после публикации не освобождается адаптером даже при Close failure. Ошибки доступа и cleanup сохраняются в [UiClipboardException](../TrueMoon.Alloy/Runtime/UiClipboardException.cs), с Read/Write и InnerException. Откат содержимого clipboard после отказа публикации не гарантируется.

При routed input `UiSession` сообщает `ClipboardFailed` на UI-потоке и оставляет окно/редактор доступными. Cut изменяет текст только после успешного Set. Хост может показать сообщение или записать диагностику без содержимого clipboard:

```csharp
session.ClipboardFailed += error =>
    ReportClipboardFailure(error.Operation, error.InnerException);
```

Без подписчика operational failure остаётся обработанным вводом. Подписчик сам отвечает за свой error handling; его исключения распространяются. Прямые `session.GetClipboardText()`/`SetClipboardText(...)` бросают UiClipboardException; programming errors провайдера не подавляются. Dispose снимает подписчиков ClipboardFailed. Контролируемый оконный recovery smoke и native OS smoke учитываются раздельно; команды, fixture и свежие результаты — в [AlloyTest](../ManualTests/AlloyTest/README.md) и [STATUS](../docs/alloy/STATUS.md).

## Односторонние привязки свойств

В Argentis доступны типизированный Bind для UiProperty и сокращение BindText для Text. Они работают с моделью, реализующей INotifyPropertyChanged, и сохраняют конкретный тип элемента в Fluent-цепочке:

```csharp
// model реализует INotifyPropertyChanged и имеет Name, Status, CanApply.
var title = new Text().BindText(model, x => x.Name).Width(200);
var status = new Text().Bind(Text.ValueProperty, model, x => x.Status);
var apply = new Button("Применить")
    .Bind(UiProperties.Enabled, model, x => x.CanApply)
    .OnClick(Apply);
```

Привязка читает начальное значение при attach; на уже attached элементе Bind синхронизирует сразу. До attach и после detach подписок на модель нет. Reattach читает актуальное значение и подключает одну новую subscription; Move внутри контейнера сохраняет её. Регистрация живёт до Dispose контрола, поэтому удалённый элемент можно переиспользовать.

Уведомления выбранного свойства, null или пустого имени ставят чтение в очередь следующего UiSession.Update. Getter выполняется на потоке-владельце и читает последнее состояние модели; обеспечьте безопасное чтение модели на этом потоке. Фоновые события, устаревшая очередь и уже захваченные source handlers после detach/transfer не меняют отсоединённый элемент. Одинаковое значение не вызывает повторный redraw.

Поддерживается прямое instance property, например `x => x.Name`, со стандартным приведением типа. Nested paths, поля и вычисляемые выражения отклоняются ArgumentException. Повторный Bind одного UiProperty отклоняется InvalidOperationException; Unbind/замена регистрации и автоматическое подключение DataContext пока не предусмотрены. Разные UiProperty можно привязывать независимо.

Направление Bind — source → control: даже Bind(TextBox.ValueProperty, ...) не записывает пользовательский ввод в модель. Для редактирования доступен BindTwoWay, для наблюдаемых коллекций — BindItems, описанные ниже. Getter/validation failure при событии передаётся из Update; после корректного нового уведомления привязка продолжает работу. Ошибки observers не откатывают уже записанное значение. [Контракт и критерии](../docs/alloy/PLAN.md#уточнение-односторонних-property-bindings--2026-10-06), [проверки и следующий шаг](../docs/alloy/STATUS.md).

## Двустороннее редактирование

BindTwoWay работает с тем же lifetime и dispatcher, но также записывает изменения attached UI property в модель. TextBox, CheckBox и Slider используют общий типизированный механизм:

```csharp
// model реализует INotifyPropertyChanged; свойства имеют public get/set.
var name = new TextBox()
    .BindTwoWay(TextBox.ValueProperty, model, x => x.Name);
var enabled = new CheckBox("Включено")
    .BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.Enabled);
var volume = new Slider()
    .BindTwoWay(ProgressBar.ValueProperty, model, x => x.Volume);
```

Селектор выбирает прямое public instance property точно того же типа, что UiProperty: string для TextBox, bool для CheckBox и float для Slider. Read-only/private-set/init-only properties, conversions, nested paths, поля и computed expressions отклоняются ArgumentException. На одном UiProperty допускается одна registration любого направления; повторный Bind или BindTwoWay отклоняется InvalidOperationException. DataContext, Unbind и converters автоматически не подключаются.

При attach и reattach побеждает значение модели, без обратной записи. Пользовательское и программное изменение attached UI property записывается сразу на потоке-владельце, только если отличается от модели. После setter привязка перечитывает canonical value: normalization видна в контроле сразу, даже если setter не поднял PropertyChanged. Эхо собственного setter и reentrant UI events не вызывают повторной записи; остальные уведомления модели читаются в следующем Update. Getter/setter должны быть безопасны на UI-потоке; синхронизацию модели с background writers обеспечивает её владелец.

При setter/getter ошибке привязка пытается восстановить контрол из фактического состояния модели, затем передаёт исключение из Set/HandleInput. Частичное изменение модели не откатывается. Если восстановление тоже не удалось, ошибки объединяются в AggregateException и сохраняется последнее committed UI value там, где его невозможно заменить. Следующее корректное изменение допускает recovery. Direct UiSession caller обрабатывает исключение; standalone Hosting использует существующий callback error → Completion/StopAsync путь.

После property commit Element выполняет invariant hook, invalidation и всех PropertyChanged observers, даже если отдельный callback бросает исключение. Это уточнение alpha failure semantics: observer failure не означает откат или отсутствие записи модели. TextBox clamps selection/caret на границы графем canonical текста до observers и после normalized input; failed Slider drag освобождает capture. Detach удаляет source и UI subscriptions, detached local edit не пишет модель; Move сохраняет connection, stale callbacks после transfer пропускаются.

[Контракт BindTwoWay](../docs/alloy/PLAN.md#контракт-двусторонних-property-bindings-35b--2026-10-06), [проверки и ограничения](../docs/alloy/STATUS.md). Примеры выше используют конструкторы; доступные фабрики можно применять в тех же цепочках, как в форме AlloyTest.

## Привязки коллекций

`BindItems` заполняет первоначально пустой ElementList из ObservableCollection<TItem> или ReadOnlyObservableCollection<TItem>, где TItem — reference type. Возвращается конкретный тип контейнера:

```csharp
var rows = VStack.Create().BindItems(model.Rows, row =>
    HStack.Create().WithChildren(
        TextBox.Create().BindTwoWay(TextBox.ValueProperty, row, x => x.Name),
        Button.Simple("Удалить").OnClick(() => model.Rows.Remove(row))));
```

Одному уникальному ненулевому объекту соответствует одна строка, сравнение — ReferenceEquals. Разные объекты, равные по Equals или имеющие одинаковый Id, создают разные строки. Повтор одной ссылки/null отклоняются до изменения дерева. Add/Remove/Replace/Move/Reset и range notifications запрашивают сверку актуального состава при Update; промежуточные состояния могут объединяться. Reset сохраняет строки для тех же объектов и переставляет без detach, включая focus/selection/subscriptions. Новый объект с прежним Id создаёт новую строку. Свойства item изменяют обычные Bind/BindTwoWay, фабрика повторно для них не вызывается.

Source фиксирован до Dispose; замена model property с коллекцией не отслеживается автоматически. После регистрации Items управляется только binding: ручные Add/Remove/Clear/Move/index replacement запрещены даже при detach. Для статических соседей используйте внешний контейнер. `rows.RefreshItems()` запрашивает повторную сверку (например, после исправления причины factory failure); detached контейнер прочитает актуальный состав при следующем attach. Unbind и keys/повторяющиеся occurrences пока не предусмотрены.

До attach source не читается и factory не вызывается. Initial sync также выполняется в следующем Update до layout, а не внутри AttachmentChanged; пока контейнер detached, source subscription отсутствует и прежние строки остаются в контейнере. Reattach восстанавливает одну subscription и сверяет актуальную коллекцию. Captured dispatcher/active guard отбрасывают stale queued/in-flight events. Фоновые уведомления только ставят sync в очередь; snapshot/factory/tree mutations выполняются на UI owner thread. ObservableCollection не становится потокобезопасной: сериализацию изменений и безопасного перечисления обеспечивает её владелец. Обычно worker результат следует применять к модели через dispatcher.

Модель/коллекция остаются caller-owned. Factory возвращает новый live unowned subtree; accepted drafts до commit принадлежат binding, после — контейнеру. При удалении строки binding получает её ownership и вызывает Dispose. Это ответственность binding как caller существующего Remove, не изменение общих правил Remove/SetContent ниже. Detach контейнера сохраняет строки; Dispose освобождает текущих детей обычным путём. Ресурсы, выделенные factory до throw и не возвращённые binding, очищает factory; чужие owned nodes binding не освобождает.

Сначала snapshot проверяется и создаются все новые строки, затем один batch commit фиксирует порядок, parent/attachment и mapping до notifications. Factory/validation failure сохраняет прежнее дерево и очищает drafts best-effort; source не откатывается. Source mutation из factory отклоняет подготовку; source changes из observers ставят отдельную синхронизацию в очередь. Notification/attachment/cleanup failure после commit не откатывает дерево: остальные observers и освобождение удалённых строк продолжаются, ошибки передаются из Update (одиночная исходная либо AggregateException). Автоматически повторять mutation нельзя. Direct UiSession caller может исправить source/factory и вызвать RefreshItems; HostedUiWindow закрывается и сообщает ошибку через Completion/StopAsync по существующей callback boundary.

[Согласованный контракт 3.5c](../docs/alloy/PLAN.md#контракт-коллекционных-привязок-35c--согласован-2026-10-07-после-93a5cae), [реализация и проверки](../docs/alloy/STATUS.md), [динамический список AlloyTest](../ManualTests/AlloyTest/README.md).

## Владение динамическим деревом

Для условной области loading/error/ready можно связать typed status property через Bind и заменять child обычным SetContent. [LoadStatusRegion в AlloyTest](../ManualTests/AlloyTest/LoadStatusRegion.cs) готовит ветвь до замены и освобождает возвращённую старую ветвь даже при postcommit notification error; factory failure сохраняет отображаемое содержимое до успешного RefreshContent. [Оконный пример](../ManualTests/AlloyTest/README.md) проверяет сохранение focus/selection/scroll соседнего списка. Это внутренний виджет примера; общий BindContent API не введён.

После успешного создания UiSession владеет корнем, оставшимися в нём детьми и render surface. Не отсоединяйте и не передавайте корень живой сессии вручную. Контейнер владеет добавленным ребёнком. `Remove`, `Clear`, замена элемента коллекции и `SetContent` **не вызывают Dispose** старого поддерева: вызывающий код получает владение и обязан переиспользовать или освободить его.

```csharp
var oldContent = content.Child;
content.SetContent(newContent);
oldContent?.Dispose(); // Или добавить oldContent в другой контейнер.

source.Items.Remove(subtree);
destination.Items.Add(subtree); // Между Remove и Add владеет вызывающий код.
```

Перед прямым Detach/Dispose ребёнка обязательно удалите его из контейнера. Контейнер/UiSession автоматически освобождают оставшихся детей. `Move` внутри одной коллекции сохраняет focus/capture и subscriptions. Remove/перенос отменяют transient input и ссылки прежней сессии; свойства, selection/scroll и экземпляры сохраняются. Замена соседней ветви сохраняет состояние неизменённых элементов. Перенос между UI-потоками выполняйте последовательно: Remove на прежнем потоке, затем Add на новом, пока поддерево detached.

`element.Own(resource)` сохраняет ресурс до Dispose, в том числе после Remove/переноса. `element.OwnAttachment(subscription)` принимает ресурс только у live attached element и освобождает при текущем detach. При повторном attach подключите новую подписку через AttachmentChanged. Bind/BindTwoWay делают это автоматически; collection bindings пока не реализованы.

Проверки ownership/cycle/disposed/index/thread отклоняют изменение до commit. Ошибки внешних notifications/cleanup передаются **после commit** и продолжения остальных уведомлений/освобождения. Не повторяйте Add/SetContent автоматически после callback exception: проверьте фактический Parent/Child. Одиночная ошибка сохраняет тип, несколько объединяются в AggregateException. Dispose элемента/сессии продолжает освобождение ресурсов и registry после ошибок; повторный вызов cleanup не повторяет.

Структурные изменения и закрытие сессии внутри tree notifications запрещены. Используйте `ui.Post(...)` для следующего Update. `element.Dispatch(...)` пропускает ранее queued action после конца attachment, даже при повторном добавлении в ту же сессию. На detached element Dispatch выполняется сразу; из detach notification откладывайте работу через `ui.Post`, не через Dispatch. При неуспешном создании UiSession все узлы и attachment subscriptions отсоединяются, владение корнем остаётся вызывающему коду.

[Правила и проверки](../docs/alloy/STATUS.md). Legacy Hosting.Compatibility использует отдельное старое дерево и этим изменением не исправлен.

## Отказ графики и явное пересоздание 2.4/K8

`UiRenderingException(backend, operation, kind, cause)` сообщает подтверждённый terminal failure: `DeviceLost`, `ContextLost` или `BackendFailure`; cause сохраняется по identity. Обычная ошибка пользовательского DrawCore остаётся retryable: direct UiSession сохраняет invalidation и допускает следующий Update. Standalone при необработанном исключении закрывает окно и передаёт ошибку хосту.

UiSession.Update/Resize и GPU export helpers перехватывают typed rendering failure. Host, обнаруживший отказ собственного presentation/engine, вызывает `ui.ReportRenderingFailure(error)` на owner thread. Первый report сохраняет RenderingFailure/IsFaulted, отменяет focus/capture/hover и queued posts; RenderingFailed вызывается один раз для каждого observer. Ошибки cancellation/observers агрегируются с original failure, остальные observers вызываются. Повторный report возвращает false. Dispose внутри notification/Update запрещён; закрывать сессию следует после выхода из отказавшей операции.

После fault Update/Resize/input/Post/theme/export/presentation запрещены. VerifyAccess допускает owner-thread cleanup; IUiRenderSurface.VerifyAvailable проверяет thread/lifetime/borrowed access, GPU health проверяется отдельно. Dispose сохраняет cleanup errors, пытается освободить остальные доступные ресурсы и уведомляет registry даже при ошибках освобождения. UiCleanup.Complete сохраняет operation error первым и выполняет все переданные безопасные actions; AggregateException может быть вложенным.

Для обычного HUD сначала завершить host GPU работу и вернуть lease с actual layout, затем Dispose UI, затем закрыть host resources. Fault не передаёт владение borrowed image. При **подтверждённом DeviceLost**, после выхода из Render/notification на owner thread:

```csharp
UiCleanup.Complete(deviceLost,
    () => ui.ReportRenderingFailure(deviceLost),
    () => lease.AbandonAfterDeviceLoss(deviceLost),
    ui.Dispose);
// Complete после cleanup бросает original error либо AggregateException.
```

AbandonAfterDeviceLoss отвергает ContextLost/BackendFailure, чужой поток и неактивный lease. Он не ждёт GPU и не возвращает image state, освобождает UI export handle и abandons Skia context. При ContextLost живого Vulkan device обычный Return по-прежнему требует успешного host completion; state update abandoned context пропускается.

Device/instance/queue/loader/feature memory должны жить до освобождения всех UI contexts **даже после abandon**. Это требование [Skia GrDirectContext](https://api.skia.org/classGrDirectContext.html). Abandon(false) предотвращает backend вызовы при разрушении Skia resources. Если GL context невозможно сделать current или Vulkan completion не подтверждён, wrappers освобождаются через abandon; это не доказывает освобождение driver allocations. Host отвечает за окончательное retirement устройства. HostedUiWindow сохраняет Vulkan presenter/device, пока session удерживает lease или предыдущий этап cleanup не завершился. Неизвестный completion failure не объявляется DeviceLost автоматически.

Standalone ловит ошибки внутри native callbacks, освобождает дерево/renderer → presenter/device → input/window, вызывает close callback один раз и передаёт все причины через Ready (creation failure), Completion и StopAsync. OpenGL Closing также перехватывает cleanup errors внутри callback. Vulkan presenter сохраняет presentation error при Return failure и discards lease при подтверждённом DeviceLost. Registry preflight запрещает stop с borrowed frame/чужого потока; после допустимого preflight ошибка одной сессии не мешает cleanup остальных.

Модель остаётся у caller. После освобождения старых controls/подписок создаётся новое дерево из прежней модели:

```csharp
// model сохраняется host; target описывает живое/пересозданное host устройство.
var replacement = UiSession.Create(new View1(model), backend, target, viewport);
replacement.Update();
```

После настоящего device/context loss новый target предоставляет host. Controlled BackendFailure probe использует прежний здоровый device. Controls, focus/capture/selection/scroll принадлежат новой сессии. Pending bindings старого faulted UI отменяются: новое дерево читает актуальную модель, даже если старый editor не успел получить её последнее значение.

[RenderingFailureProbe](../ManualTests/AlloyVulkanTest/RenderingFailureProbe.cs), режим `--settings-failure --validation`, проверяет controlled terminal draw, external report и creation rollback на реальных GPU/окнах, preservation модели/causes, registry0 и explicit new sessions/windows. Реальный device/context loss не индуцируется; accepted DeviceLost abandonment и teardown настоящего lost device остаются отдельными проверками alpha. Native ABI/packages/assets не менялись.

## OpenGL HUD и окно

```csharp
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

builder.Setup(app => app.UseAlloy(options => options.UseSkiaOpenGL().UseExternalHost()));

// Контекст игры жив и current на потоке рендеринга.
var target = new OpenGLUiTarget(engine.ResolveGlProcedure, engine.EnsureGlContextCurrent);
using var ui = factory.Create(hudRoot, target, viewport);
ui.Update();
// После сцены: borrowed RGBA8 FBO того же pixel size. Укажите фактические stencil/sample параметры.
ui.PresentOpenGL(engine.SceneFramebuffer, stencilBits: 0, sampleCount: 0, clear: false);
// Игра сама выполняет swap buffers и заново привязывает своё GL-состояние.
```

`factory` получают через `IUiSessionFactory` после Build, как в Vulkan-примере. Resolver имеет сигнатуру `nint ResolveGlProcedure(string name)`, callback — `void EnsureGlContextCurrent()`: делает этот живой контекст current или выбрасывает исключение до native GL calls. Без App: `UiSession.Create(hudRoot, new SkiaOpenGLRenderBackend(), target, viewport)`.

UI owns Skia context и offscreen RGBA/Premul GPU surface; хост owns native GL context, окно, destination FBO/attachments и цикл кадров. `PresentOpenGL` композитит retained frame на GPU без CPU copy, origin destination — BottomLeft. `clear: false` сохраняет сцену под прозрачными областями. Default FBO 0, stencil8/sample0 подходят настроенному standalone Silk окну; пользовательский FBO должен быть complete RGBA8 с указанными actual stencil/sample counts и размером session viewport. Host descriptor не получает владение native resources.

Все GL/UI операции сериализуются на creator thread с тем же current context. Skia меняет GL state; renderer делает ResetContext перед своей работой, игра заново binds свой framebuffer/program/VAO/blend/scissor/viewport и прочее перед следующей собственной работой. Автоматического сохранения всего GL-state и texture-id lease/межконтекстного handoff нет. Закройте UI **до** уничтожения контекста. Zero viewport suspended; resize требует Update перед presentation. Device/context loss recovery остаётся отдельной задачей.

`ReadbackOpenGLImage()` — diagnostic/export copy в caller-owned immutable CPU SKImage. Dispose изображения обязан вызвать хост; оно не зависит от redraw/resize/session disposal. Это readback и не часть GPU presentation. Export/presentation запрещены до успешного Update или при pending changes.

Оконное подключение: `UsePresentation<MyView>(options => options.UseSkiaOpenGL().UseSilkWindow())`. Общие дерево/layout/input/invalidation сохраняются. Hosting явно swaps buffers, освобождает session в Closing до уничтожения GL-контекста Silk. Исключения из render/input/posted window commands перехватываются внутри callbacks, после безопасного закрытия передаются через `HostedUiWindow.Completion` и `StopAsync`; ошибки не уходят через native GLFW boundary.

SilkWindowHost сохраняет native window/context после возврата Run до Dispose на owner thread. Это позволяет сначала освободить renderer и input context с GLFW callbacks, затем окно. Внешний вызов NativeWindow.Reset/Dispose до host.Dispose нарушает этот порядок. [Оконный lifetime probe](../ManualTests/AlloyTest/WindowLifetimeProbe.cs) проверяет обычное/раннее закрытие и перехваченную render error на OpenGL/Vulkan.

Проверено desktop OpenGL 3.3 на Windows x64/NVIDIA с bundled Skia 4.153.1. GLES/ANGLE/другие драйверы этим результатом не подтверждены. [Автоматические GPU/оконные/error probes](../ManualTests/AlloyVulkanTest/README.md).

## Raster UI без окна и Vulkan

```csharp
using System.IO;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

builder.Setup(app => app.UseAlloy(options => options
    .UseSkiaRaster()
    .UseExternalHost()));

// На потоке-владельце UI после Build/Start:
var factory = (IUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
using var ui = factory.Create(hudRoot, new RasterUiTarget(), new UiViewport(640, 360, 1.5f));
ui.Update();
using var image = ui.SnapshotRasterImage();  // Caller-owned immutable CPU SKImage.
using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
using var output = File.Create("hud.png");
data.SaveTo(output);
```

Без App: `UiSession.Create(hudRoot, new SkiaRasterRenderBackend(), new RasterUiTarget(), viewport)`. В обоих случаях используются те же Argentis-контролы, layout/input/focus и invalidation. Сессия владеет деревом и CPU surface; `RasterUiTarget` не содержит ресурсов хоста. Snapshot вызывают после `Update`, иначе pending changes не будут применены и метод выбросит исключение.

`SKImage` — immutable snapshot, которым владеет вызывающий код: его надо Dispose. Он остаётся читаемым после следующего render, resize или закрытия UI-сессии и не блокирует эти операции. Это CPU image, не Vulkan lease; `Return` ему не нужен. Snapshot может удерживать память предыдущего кадра, пока хост его использует. Zero-sized viewport приостанавливает рисование/export; восстановление размера требует нового `Update`. Размеры поверхности — physical pixels, ввод/layout — logical units через `UiViewport.Scale`.

Сессия и поверхность остаются привязанными к потоку создания; `Post` принимает изменения с других потоков. Raster использует native Skia, но не создаёт GPU device/окно и не вызывает Vulkan interop ABI. Bundled DLL по-прежнему Windows x64; Linux/macOS этим шагом не добавлены. `UsePresentation` поддерживает Vulkan и OpenGL/Silk; raster подключается через внешний host/direct session.

Пример: `--raster --raster-output <path.png>` в AlloyVulkanTest. `--backend-compare --validation` создаёт одно и то же представление на raster/OpenGL/Vulkan и сравнивает layout/input/выбранные RGBA pixels при scale 1/1.5/2. Это не полное pixel-exact сравнение glyph rasterization. [Проверки и команды](../ManualTests/AlloyVulkanTest/README.md).

## Устройство чужого движка

Silk-хост создавать не требуется. Передайте уже существующие dispatchable Vulkan handles как `nint`:

```csharp
using TrueMoon.Alloy;
using TrueMoon.Alloy.Rendering.Skia;

var host = new VulkanHostContext(
    instance: engine.InstanceHandle,
    physicalDevice: engine.PhysicalDeviceHandle,
    device: engine.DeviceHandle,
    queue: engine.GraphicsQueueHandle,
    queueFamily: engine.GraphicsQueueFamily,
    apiVersion: engine.RequestedVulkanApiVersion,
    getProcedureAddress: engine.ResolveVulkanProcedure,
    waitIdle: engine.WaitDeviceIdleOrThrow,
    instanceExtensions: engine.EnabledInstanceExtensions,
    deviceExtensions: engine.EnabledDeviceExtensions,
    enabledFeatures2: engine.EnabledFeatures2Pointer);

using var ui = factory.Create(hudRoot, new VulkanUiTarget(host), viewport);
// Или UiSession.Create(hudRoot, new SkiaVulkanRenderBackend(), new VulkanUiTarget(host), viewport).
```

`engine` обозначает адаптер конкретного движка. Сигнатура resolver — `nint ResolveVulkanProcedure(string name, nint instance, nint device)`: для ненулевого device используется его device loader, иначе instance/global loader. Версия — значение из `VkApplicationInfo.apiVersion`, минимум Vulkan 1.1. Передаются **включённые** extension names и feature values из создания устройства; supported properties не заменяют enabled capabilities. Списки extensions копируются и доступны только для чтения.

`enabledFeatures` — адрес `VkPhysicalDeviceFeatures`, `enabledFeatures2` — адрес `VkPhysicalDeviceFeatures2` с цепочкой pNext. Второй имеет приоритет; нулевые указатели означают, что Skia не получает optional features. Память features/chain, Vulkan handles, loader и объекты callbacks должны жить до закрытия всех UI-сессий. Не передавайте адрес структуры на стеке из метода, который возвращает живую сессию. Контракт соответствует [зафиксированному native Skia backend context](https://github.com/google/skia/blob/45afab4f1f0921f3feb97f58cf89b136fffa85e6/include/gpu/vk/VulkanBackendContext.h).

`waitIdle` проверяет, что device ещё жив, и завершает GPU-работу устройства через проверяемый `vkDeviceWaitIdle`; при ошибке выбрасывает исключение. UI вызывает его при handoff/return/disposal. Это последовательный режим одной unprotected graphics queue: игра сериализует свой submit и UI на потоке сессии. Другие queues, protected content и frames-in-flight требуют отдельного контракта и пока не поддерживаются.

`VulkanHostContext` не имеет `Dispose` и не уничтожает ресурсы движка. Закройте UI/lease и compositor перед освобождением feature memory, loader и device/instance. `VulkanUiTarget.Host` доступен для обоих путей и сохраняет identity переданного descriptor; свойства `VulkanUiTarget.Device` больше нет. При Silk convenience подключении ссылку на device хранит вызывающий хост. App-регистрация остаётся той же `UseAlloy(...UseExternalHost())`.

## Самостоятельное окно

```csharp
using TrueMoon;
using TrueMoon.Alloy.Hosting;

builder.Setup(app => app.UsePresentation<MyView>(options => options
    .UseSkiaVulkan()
    .UseSilkWindow(window =>
    {
        window.Width = 960;
        window.Height = 540;
        window.Title = "My UI";
    })));
```

`MyView` наследует `TrueMoon.Argentis.Element` (например, `Panel`). App запускает `HostedUiWindow`, который создаёт дерево, Silk-окно и ресурсы выбранного renderer на выделенном UI-потоке. Для OpenGL замените UseSkiaVulkan на UseSkiaOpenGL. Закрытие окна отменяет App lifetime. Остановка ждёт завершения UI-потока; UI/GPU ресурсы освобождаются до уничтожения принадлежащего хосту устройства или GL-контекста.

Vulkan presenter переносит UI-текстуру в swapchain через GPU blit и обрабатывает resize/out-of-date/suboptimal. OpenGL композитит GPU frame в default framebuffer и swaps buffers. Это самостоятельное окно UI; композиция игровой сцены остаётся задачей игрового хоста. `UseRenderer(factory)` предназначен для пользовательского backend в режиме внешнего хоста.

Перегрузка без настроек находится только в `TrueMoon.Alloy.Hosting.Compatibility` и сохраняет старый OpenGL/Visual путь с его известными ограничениями. Для нового UI используйте показанную перегрузку с `Action<AlloyOptions>` и импортируйте только `TrueMoon.Alloy.Hosting`, без Compatibility. Этот explicit путь не изменён переносом 2.1b.

## Пересборка native и проверки

Native DLL доставляется транзитивной ссылкой на Rendering.Skia. Публичный `SkiaNativeLibrary.Initialize(path?)` следует вызвать до первого native обращения при выборе своего файла; renderer сам инициализирует bundled DLL. Одна DLL используется обоими bindings до завершения процесса. [Хранение и пересборка native](../TrueMoon.Alloy.Rendering.Skia/README.md).

В `ManualTests/AlloyVulkanTest` доступны `--raw-host`, `--raw-hud-window`, `--runtime`, `--window`, `--interop-compose`, автоматический `--hud-window` и интерактивный `--hud-demo`. В последнем UseAlloy управляет retained HUD, а игровой хост рисует сцену, композитит UI и вызывает `VulkanWindowPresenter.PresentImage` для своей scene texture. UI lease возвращается до presentation; scene texture также остаётся собственностью хоста. [Команды и проверки raw handles/оконного HUD](../ManualTests/AlloyVulkanTest/README.md).

Автоматические оконные сценарии закрываются после проверок. Validation требует доступного `VK_LAYER_KHRONOS_validation`; в локальном окружении путь задаётся через `VK_LAYER_PATH`. Статус фактических запусков: [STATUS.md](../docs/alloy/STATUS.md).
