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

Направление Bind — source → control: даже Bind(TextBox.ValueProperty, ...) не записывает пользовательский ввод в модель. Для редактирования доступен BindTwoWay, описанный ниже; наблюдаемые коллекции ещё предстоят. Getter/validation failure при событии передаётся из Update; после корректного нового уведомления привязка продолжает работу. Ошибки observers не откатывают уже записанное значение. [Контракт и критерии](../docs/alloy/PLAN.md#уточнение-односторонних-property-bindings--2026-10-06), [проверки и следующий шаг](../docs/alloy/STATUS.md).

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

## Владение динамическим деревом

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
