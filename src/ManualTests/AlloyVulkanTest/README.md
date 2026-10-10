# Skia / Vulkan / OpenGL / raster / retained HUD

Windows x64, .NET 10. Native Skia с interop доставляется через Rendering.Skia; ручное копирование DLL не требуется. [Пересборка native](../../TrueMoon.Alloy.Rendering.Skia/README.md).

## Custom frame и native Snap: 4.9c/d

`--window-chrome --validation` проверяет production Windows adapter: OpenGL/Vulkan × native drag on/off × resize on/off; directed WM_NCHITTEST для заголовка, disabled Button/TextBox, всех восьми границ/углов, отрицательных screen coordinates и устаревшего layout. Native limits, fractional rounding, exact client resize, bounded/unbounded maximize/work area, minimize/restore/close и hooks проверяются отдельно. Hosting proof активирует Argentis maximize button через UiSession input, затем queued команды; title factory failure освобождает root/device/window и сохраняет исходную ошибку. Native mouse pointer не перемещается; directed messages не заменяют physical drag/Snap/DPI matrix.

`--window-dpi --validation --dpi-output <JSON>` проверяет production native DPI → UiViewport.Scale на OpenGL/Vulkan × обычное/custom окно × Opaque/Opacity/PerPixel. Размер600×400 задаётся логически; framebuffer, native limits и input сверяются с HWND DPI. Проверяются focus/selection после resize/перемещения, exactly-once client click, minimize/restore и teardown hooks/validation. Окно перемещается на каждый доступный монитор и обратно; ActualDpis/ActualDpiTransitions/UnavailableRequiredDpis показывают физическое покрытие. Directed WM_GETDPISCALEDSIZE для96/144/192 проверяет ответ hook, отдельно от physical transitions. Probe также создаёт per-monitor-v2 HWND из DPI-unaware caller и проверяет восстановление thread context. Настройки Windows не меняются; курсор временно перемещается на кнопку собственного окна и восстанавливается. Для полной K3 matrix нужны доступные100/150/200% и разные DPI мониторов.

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --window-dpi --validation --dpi-output TestResults/AlloyDpi/dpi.json
```

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-chrome --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-appearance --custom-chrome --validation --appearance-output TestResults/AlloyTransparency/chrome-debug-alpha.json
```

`--custom-chrome` добавляет production custom frame к существующей desktop matrix и оставляет6undecorated combinations: оба backend × Opaque/Opacity/PerPixel,80samples/240RGB,48per-pixel hit targets/configuration. JSON содержит CustomChrome/ChromeHooks; нужны видимый desktop и контрольный фон. [API](../../TrueMoon.Alloy.Hosting/README.md#собственный-заголовок-и-оконные-команды), [проверки](../../docs/alloy/STATUS.md).

`--window-snap --validation` выполняет8 directed-message cases (OpenGL/Vulkan × Snap on/off × SystemMenu on/off): HTMAXBUTTON, queued UI hover/press, отпускание вне кнопки, exactly-once click, capture cancel/teardown, disabled exclusion, Space/Enter, caption double-click и четыре входа native system menu. GUI_INMENUMODE проверяется на потоке собственного HWND, затем меню закрывается адресным сообщением. Реальный указатель не перемещается; результат не доказывает системный Snap flyout.

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-snap --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-snap-input --validation --snap-output TestResults/AlloyTransparency/snap-input.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-appearance --custom-chrome --native-snap --validation --appearance-output TestResults/AlloyTransparency/snap-alpha.json
```

`--window-snap-input` использует настоящий SendInput на разблокированном свободном desktop: system move/resize loop, editor exclusion, exactly-once maximize и work area, drag restore, double-click maximize/restore, системное меню по правому клику, edge/corner Snap, hover и Win+Z после каждого этапа. Покрывает OpenGL/Vulkan × Opaque/Opacity/PerPixel. До ввода проверяет foreground и принадлежность стартовой точки своему HWND; отказ guard останавливает дальнейший ввод. Читает Windows arranging/docking/drag-restore settings, не меняет их; отключённые возможности пропускает явно. JSON содержит Checks, Skipped, фактический native DPI и UiScale, Shell hit targets и Failure. Alt+Space, перехваченный PowerToys Run, явно отмечается skipped; `--snap-keyboard-control` сравнивает его с обычным decorated окном.

Flyout проверяется через WindowFromPoint ниже Maximize region: Xaml_WindowedPopupClass принадлежит explorer/ShellHost/ShellExperienceHost. Shell может возвращать HWND отдельной цифровой подсказки, поэтому её rectangle является hit target geometry, а не обязательно границей всего меню. `--capture-snap` сохраняет отдельные PNG для каждого backend/mode: full UI context и target. При закрытии hover указатель уводится в свободную область собственного окна, принадлежность точки проверяется: перемещение внутрь Shell popup искажает последующий Win+Z.

`--window-snap-bar --validation --capture-snap --snap-output <JSON>` удерживает реальный drag у верхнего края, выбирает зону и проверяет результирующую native Snap geometry. PNG показывают системную bar до отпускания; требуется визуальная сверка. Bar может пропускать hit testing, а thumbnail перекрывает указатель, поэтому WindowFromPoint не используется как доказательство её видимости. Оба physical probes освобождают только введённые keys/buttons и восстанавливают курсор. Settings off, Aero Shake и переходы между DPI/мониторами требуют отдельной матрицы; один physical150% монитор не заменяет её. `--native-snap` с `--custom-chrome` проверяет desktop alpha при установленном adapter path.

## Публичная конфигурация окна: 4.9b

`--window-appearance --validation --appearance-output <JSON>` проверяет production `SilkWindowHost`, независимый `WindowAppearance` и публичный Vulkan alpha-selecting presenter. Матрица: OpenGL/Vulkan × decorated/borderless × Opaque/Opacity/PerPixel. На каждом окне: два фона, initial/forced WM_PAINT/resize/minimize-zero-size-restore, alpha0/128/255 либо uniform opacity0/0.5/1. Проверяются реальный Win32 WS_CAPTION, roundtrip opacity, source RGBA, alpha/format, window owner thread/disposed guards, native WindowFromPoint для per-pixel client областей, отсутствие очистки на каждом обычном frame и нулевые hooks/resources после Dispose. Это desktop RGB proof, а не fixed-font full golden или native DPI/monitor matrix. Нужен свободный от перекрытий видимый Windows desktop.

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-appearance --validation --appearance-output TestResults/AlloyTransparency/appearance-debug.json
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyTransparency/appearance-publish
./TestResults/AlloyTransparency/appearance-publish/AlloyVulkanTest.exe --window-appearance --validation --appearance-output TestResults/AlloyTransparency/appearance-release.json
```

Каждый sample содержит expected/actual RGB; вся новая матрица требует Matches=true, в отличие от исторической capability matrix ниже. Запуск даёт 160 samples/480 RGB точек на configuration, 12 create/dispose foreground окон; для per-pixel — 96 native hit target checks/configuration. При RGB mismatch diagnostics показывают оба набора RGB, native foreground/background targets, visibility и opacity. [Публичный API](../../TrueMoon.Alloy.Hosting/README.md#прозрачность-standalone-окна), [текущие результаты и ограничения](../../docs/alloy/STATUS.md).

## Оконная прозрачность: prototype 4.9a

`--window-transparency` проверяет actual desktop RGB поверх другого окна, а также GPU-source premultiplied alpha. По 7 путей OpenGL/Vulkan: opaque, общая opacity 0/0.5/1, per-pixel alpha 0/128/255; дополнительный internal Vulkan PreMultiplied candidate. Каждый путь проверяется после resize и minimize/zero-size/restore. Три неверных Vulkan alpha requests отвергаются без потери старой сессии и retained presenter resources. Capture использует только центры полос собственных окон; внешние окна не снимаются. Нужен видимый Windows x64 desktop без перекрытия контрольных окон.

```powershell
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj --no-restore
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-transparency --validation --transparency-output TestResults/AlloyTransparency/debug.json
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyTransparency/publish
./TestResults/AlloyTransparency/publish/AlloyVulkanTest.exe --window-transparency --validation --transparency-output TestResults/AlloyTransparency/release.json
```

Exit 0 означает, что матрица собрана и assertions/control/lifecycle/validation прошли; конкретный режим может иметь `Supported=false`. В JSON сохраняются expected/desktop RGB, logical/framebuffer sizes, advertised/selected Vulkan alpha/format, GL alpha bits, GPU/GLFW/runtime и cleanup. Whole-window opacity никогда не включается вместе с transparent framebuffer. Неподдержанный PreMultiplied flag даёт явный unsupported result, без создания несовместимого swapchain и backend fallback.

Исходная матрица на RTX 5070 Ti / Windows build 26300 / NVIDIA 617.42: OpenGL прошёл per-pixel desktop alpha; Vulkan Opaque и PreMultiplied без очистки не прошли. Дополнительная проверка ниже подтвердила рабочий Vulkan путь. Публичный default presenter сохраняет Opaque-first; внутренний experiment не является оконным API. [Фактические проверки и следующий шаг](../../docs/alloy/STATUS.md). Input/click-through, title bar/Snap, DPI 150/200%/monitor transitions и GPU interop этим prototype не проверяются.

## Изоляция Vulkan / GLFW / DWM: prototype 4.9a.1

`--vulkan-transparency-isolation` запускается до загрузки native Skia. Фон создаётся raw OpenGL, foreground — `vkCmdClearColorImage` в owned GPU image и существующий Vulkan presenter. Readback используется только для проверки source RGBA. Сравниваются Opaque negative control, PreMultiplied baseline и PreMultiplied с `PatBlt(BLACKNESS)` клиентского DC после обработки show/resize, с рамкой и без неё. Alpha 0/128/255 проверяется над двумя фонами, после resize и minimize/restore; opaque endpoint подтверждает, что Vulkan изображение видно. JSON включает RGB samples, alpha/format, validation/lifecycle и проверку отсутствия native libSkiaSharp.dll.

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --vulkan-transparency-isolation --validation --isolation-output TestResults/AlloyTransparency/isolation-debug.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --window-transparency --redirection-clear --validation --transparency-output TestResults/AlloyTransparency/skia-clear-debug.json
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyTransparency/isolation-publish
./TestResults/AlloyTransparency/isolation-publish/AlloyVulkanTest.exe --vulkan-transparency-isolation --validation --isolation-output TestResults/AlloyTransparency/isolation-release.json
./TestResults/AlloyTransparency/isolation-publish/AlloyVulkanTest.exe --window-transparency --redirection-clear --validation --transparency-output TestResults/AlloyTransparency/skia-clear-release.json
```

Raw и Skia candidates с очисткой прошли Debug/published Release на указанной машине. Decorated PreMultiplied baseline повторил смешивание с белым; borderless raw baseline уже работал без очистки. Это проверка существующего GPU пути, а не готовая оконная функция. Нужна интеграция инициализации redirection surface в production paint/resize lifecycle и отдельный capability/input contract; CPU/layered-window transport пока не нужен. [GLFW PR #2815](https://github.com/glfw/glfw/pull/2815) содержит соответствующую гипотезу. Exit0 означает успешное выполнение controls/lifecycle и сбор матрицы; неподдержанные baseline cases могут оставаться в отчёте.

## Отказы и явное пересоздание 2.4/K8

Режим `--settings-failure --validation` проверяет общую View1/SettingsModel: controlled terminal draw на raster/OpenGL/Vulkan, запрет дальнейших Update/input/export, Dispose и explicit new tree/session с прежней моделью и новыми transient states. Vulkan lease при fault остаётся borrowed: преждевременный Dispose и abandonment с BackendFailure отвергаются, normal Return разблокирует cleanup. Check(ErrorDeviceLost) проверяется synthetic result без потери GPU. Registry пытается освободить все сессии при ошибке одной.

Для OpenGL/Vulkan окон проверяются draw failure, external ReportRenderingFailure без throw и создание surface с ошибкой; исходные operation/tree/close errors сохраняются, Ready/Completion/StopAsync их передают, close/notification выполняются один раз, registry0. После draw/report создаётся новое окно из прежней модели. Vulkan core/synchronization validation включает teardown. Probe не обращается к OS clipboard и не индуцирует физический device/context loss; accepted DeviceLost abandonment/реальный lost-device teardown этим результатом не подтверждены. [Контракт владения и восстановления](../../TrueMoon.Alloy.Hosting/README.md#отказ-графики-и-явное-пересоздание-24k8).

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --settings-failure --validation
```

## Миграция alpha API: PLAN 2.1a

Manual Silk callers используют `SilkVulkanHost` из сборки/namespace `TrueMoon.Alloy.Hosting` (`using TrueMoon.Alloy.Hosting;`):

- `SilkVulkanHost.CreateContext(device)` возвращает `VulkanHostContext` вместо `VulkanHostContext.FromSilkDevice(device)`; raw probe проверяет отказ factory для disposed device.
- `SilkVulkanHost.CreateTarget(device)` возвращает `VulkanUiTarget` вместо удалённого Silk constructor.
- `SilkVulkanHost.CreateSurface(device, context, size)` возвращает `VulkanInteropSurface` вместо удалённого Silk constructor (`GRContext`, `SKSizeI`).

`VulkanWindowPresenter` и `VulkanPresentationResources` перенесены из сборки/namespace Rendering.Skia в Hosting. Обновите project references/imports/callers и пересоберите alpha consumers: прежняя binary compatibility не гарантируется. Алгоритмы probes, GPU handoff/retirement и владение ресурсами не меняются. Raw `VulkanHostContext` constructors и `new VulkanUiTarget(host)` сохраняются; вместо удалённого `target.Device` raw probe проверяет `ReferenceEquals(target.Host, host)`.

Прямой renderer для raw Vulkan/raster/OpenGL через `UiSession.Create` не требует Platform.Silk/Hosting; после 2.1b нет и транзитивных TrueMoon.Core/Contracts/Windowing зависимостей через Alloy. Этот многоцелевой manual host использует оба модуля для Silk device/window и App-интеграции. Явные Vulkan bindings в renderer не равнозначны зависимости от Platform.Silk. [Контракты и примеры](../../TrueMoon.Alloy.Hosting/README.md#миграция-alpha-api-plan-21a).

## Миграция alpha API: PLAN 2.1b

31 legacy-файл из корня Alloy/Presenters перенесён в `TrueMoon.Alloy.Hosting/Compatibility`, namespace `TrueMoon.Alloy.Hosting.Compatibility` и `.Compatibility.Presenters`. Это breaking assembly/namespace migration **без type forwarders**: legacy consumers должны обновить ссылки/imports/callers и пересобраться. No-options `UsePresentation<TView>()` доступен только с явным импортом Compatibility; для нового UI он не рекомендуется. `Visual.GetEnumerator` и legacy lifetime bugs не исправлены; закомментированные прототипы не являются рабочими рендерерами. [Compatibility](../../TrueMoon.Alloy.Hosting/Compatibility/README.md).

Alloy содержит только Runtime/csproj, единственный ProjectReference — Argentis; PackageReference и Core/Contracts удалены. Hosting явно подключает Silk.NET/SkiaSharp/Topten.RichTextKit, Core/Contracts и UI adapters. Platform.Silk остаётся без Skia. Новый explicit Hosting API, native ABI, lease/ownership и retirement не менялись. [AlloyTest](../AlloyTest/README.md) использует Hosting и `UsePresentation<View1>(options => options.UseSkiaOpenGL().UseSilkWindow())`; его `--smoke` закрывает окно после >=30 кадров с 30-секундным cancellation bound, обычный запуск остаётся оконным, retained View1/View2 не изменены.

Все результаты ниже — **исторические**; фактические повторные build/tests/GPU/publish запуски после переноса 2.1b и оставшиеся ограничения записаны отдельно в [STATUS](../../docs/alloy/STATUS.md).

## Общая форма raster/OpenGL/Vulkan и HUD

Новая контрольная сцена — исходный [View1](../AlloyTest/View1.cs)/[SettingsModel](../AlloyTest/SettingsModel.cs) из AlloyTest. Проект compile-link те же sources и [SettingsFormSmoke](../AlloyTest/SettingsFormSmoke.cs), доставляет PNG через PreserveNewest; не ссылается на standalone executable и не содержит копии controls/model. Прежние маленькие probes ниже сохраняются отдельно.

Команды из `src`; VK_LAYER_PATH задавайте только запускаемому процессу, используя существующий validation manifest:

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --settings-compare --validation
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --settings-hud --validation
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --settings-hud-demo --validation
```

[SettingsBackendProbe](SettingsBackendProbe.cs) выполняет40 шагов исходной формы на raster/OpenGL/Vulkan при logical900×1100, scales1/1.5/2. Framebuffer масштабируется соответственно. Сравнивает bounds всего дерева каждого шага и model/rows/focus/selection/scroll/theme/disabled state, количество redraw и5 selected RGBA samples (фон/button/PNG/meter/scroll) с допуском1 на канал. Каждый backend подтверждает5 static Update без draw, zero-size suspend, один draw после restore, Dispose дерева/branches/generated rows и прекращение model subscriptions. Snapshot/GL/Vulkan readback нужен только assertions; production композиция остаётся на GPU. Segoe UI/system font configuration не фиксируется доставляемым font asset; selected pixels избегают glyph/AA edges. Это **не полный fixed-font golden K2/K4**.

[SettingsHudProbe](SettingsHudProbe.cs) — реальное game-owned Vulkan окно1100×1100, borrowed raw device descriptor, host-owned scene/compositor/presenter. Та же форма ограничена720×1000 и включена в прозрачный Panel рядом с alpha overlay показателей и кнопкой сцены. UI обрабатывает форму/список/условные области/темы/scroll по тому же40-step script; необработанный и незахваченный ввод передаётся сцене. Хост изменяет фон и композитит retained texture каждый кадр, UI.Update рисует только при invalidation. Lease возвращается с фактическим layout в finally.

Bounded `--settings-hud` выполняет90 presentations, scene passthrough/button capture, selected scene transparency/alpha readbacks, actual resize/minimize/restore и zero viewport. Явно освобождает первую UI-сессию, создаёт новое дерево из прежней SettingsModel, сохраняет введённый Unicode Name/Rows и отменяет old focus; новое дерево продолжает scene routing/metrics. После teardown registry0 и host device.WaitIdle остаётся доступен. Managed callback errors перехватываются до выхода через GLFW и после Run передаются вызывающему коду. Timeout30s. `--settings-hud-demo` оставляет тот же HUD интерактивным до Escape/закрытия, scripted assertions при этом не выполняются.

Свежие результаты2026-10-07: `--settings-compare --validation` и `--settings-hud --validation` прошли Debug/published Release. Comparison:3 scales ×3 backend,40 steps и29 script draws на сессию +1 restore draw. HUD каждый90 GPU compositions/36 UI draws/2 sessions/5 readbacks, validation0errors/0warnings включая teardown. Standalone AlloyTest OpenGL/Vulkan каждый40 frames в обоих режимах. UI386/386, solution build0errors/1339warnings, оба publish exit0; PNG/native hashes совпадают. [STATUS](../../docs/alloy/STATUS.md) содержит команды, logs и границы.

Новых renderer/runtime/native ABI/packages нет. OS clipboard в этих probes не используется, UI input инъецирован программно; native mouse/keyboard/DPI переходы, fixed-font full golden, long resource/VRAM soak и device-loss остаются отдельным alpha проходом. В историческом запуске2026-10-07 HUD resize использовал scale1; comparison scales — logical DPI simulation. После native DPI contract2026-10-09 HUD принимает window.Viewport.Scale, нормализованный input, делает logical Resize и масштабирует readback coordinates; текущие результаты — STATUS. Контракт backend failure/recreate проверяется отдельно через --settings-failure; успешная смена UI-сессии не является device-loss тестом.

## OpenGL на общих контрактах

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --opengl
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --opengl-window
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --opengl-failure
```

`--opengl` создаёт host-owned GL context и регистрирует `UseAlloy(...UseSkiaOpenGL().UseExternalHost())` без UI-owned окна. Проверяет App/DI, retained redraw/input/capture, posted changes, thread/reentrancy/target/viewport/loader/output guards, failed draw/retry, suspend/restore, CPU readback неизменный после redraw/resize/disposal, registry/tree disposal и повторное использование контекста новой сессией. Три shared-view кадра композитятся в game-owned RGBA8 FBO: фон, premultiplied overlay и opaque button проверяются через assertion-only readback с учётом bottom-left origin. Ownership host texture/FBO сохраняется, GL GetError — NoError. Для реального HUD используйте `PresentOpenGL(...clear: false)`, CPU readback не нужен. [Подключение и GL-state contract](../../TrueMoon.Alloy.Hosting/README.md#opengl-hud-и-окно).

`--opengl-window` использует тот же `ProbeView` и HostedUiWindow, что Vulkan: >=30 frames, actual framebuffer resize, minimize/restore, динамический Text и завершение App. GL errors проверяются между кадрами. Skia закрывается в Closing до уничтожения native context Silk; для GL нет Vulkan swapchain generations.

`--opengl-failure` намеренно бросает исключение из DrawCore. Ошибка перехватывается внутри GLFW callback, затем передаётся через Completion/StopAsync после безопасного закрытия, registry count0. Probe подтверждает, что process не завершается из-за managed unwind через native refresh callback.

В предыдущем OpenGL шаге все три прошли Debug и published Release на Windows x64/OpenGL3.3 NVIDIA RTX5070Ti (driver616.92). GL GetError не заменяет полноценный GL debug validation/driver profiling; GLES/ANGLE/другие GPU, context loss и cross-context texture exchange не проверялись. Logs в `TestResults/AlloyOpenGL`, native DLL/ABI/packages не менялись.

| Требование | Историческое доказательство OpenGL шага |
| --- | --- |
| Explicit external backend и lifecycle | `OpenGLProbe.Run`, `--opengl`, Debug/Release |
| Shared Argentis view/layout/input/selected pixels | `RasterProbe.Compare`, `--backend-compare --validation`, raster/OpenGL/Vulkan, scales1/1.5/2 |
| GPU HUD alpha/host framebuffer ownership | `OpenGLProbe.VerifyComposition`, 3 FBO compositions per --opengl/comparison |
| Standalone resize/minimize/close | `RuntimeProbe.RunWindowAsync`, `--opengl-window`, Debug/Release >=30 frames |
| Error propagation/cleanup | `OpenGLProbe.RunFailureAsync`, `--opengl-failure`, Debug/Release |
| UI regression | `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --logger trx --results-directory TestResults/AlloyOpenGL`: Passed53/Failed0/Skipped0 |

## CPU raster и сравнение backend

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --raster --raster-output TestResults/AlloyRaster/hud.png
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --backend-compare --validation
```

`--raster` регистрирует `UseAlloy(...UseSkiaRaster().UseExternalHost())`, создаёт retained Argentis HUD без окна/Vulkan device, проверяет input/invalidation, CPU snapshot и App/registry disposal. PNG export опционален. Изображение остаётся читаемым после закрытия сессии. Этот путь не требует Vulkan validation layer; остаётся зависимость от native Skia Windows x64.

`--backend-compare` создаёт один класс `BackendHudView` без изменения контролов на raster, OpenGL и Vulkan. Проверяет 3 viewports/scale 1/1.5/2, одинаковые bounds/input/capture, click/invalidation и выбранные transparency/premultiplied overlay/button RGBA pixels с допуском 1. GPU readback — assertion-only. OpenGL также проверяет blending поверх сцены в borrowed FBO. Это не полное glyph/screenshot сравнение; fixed-font golden fixtures ещё не добавлены. В предыдущем OpenGL шаге тройное сравнение прошло Debug и published Release; Vulkan validation 0 errors/0 warnings, включая teardown.

Целевые xUnit:

```powershell
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --filter FullyQualifiedName~RasterRenderingTests
```

В предыдущем OpenGL шаге 24 raster cases и все 53 UI cases прошли. [RasterRenderingTests](../../TrueMoon.Alloy.Tests/RasterRenderingTests.cs) содержат assertions для alpha/blending/clipping/clear, DPI/resize/initial-empty/snapshot lifetime, stale export/invalid viewport/thread/reentrant failures, retained redraw, Latin/Cyrillic text, pointer/keyboard/focus/capture/detach/disabled/theme.

| Требование | Доказательство |
| --- | --- |
| CPU image/прозрачность | `Snapshot_PreservesTransparencyAndPremultipliedAlpha`, `Snapshot_BlendsPremultipliedLayersInPaintOrder` |
| Clipping/clear | `Render_ClipsOverflowAndClearsHiddenContent` |
| DPI/layout | `Dpi_UsesLogicalLayoutAndPhysicalPixels`, `ScaleChange_WithSamePixelSizeRedrawsLogicalGeometry` |
| Resize/suspend/disposal | `Resize_SuspendsAndRestoresWhileSnapshotsOutliveSession`, `InitialEmptyViewport_CanRestoreWithoutReplacingSession` |
| Retained redraw | `Update_DrawsOnlyOnInvalidationAndKeepsPreviousSnapshotImmutable` |
| Input/focus/capture | `Input_PointerCaptureAndKeyboardNavigationUseSameSession`, `Input_DetachCancelsFocusAndCapture`, `Input_OverlapHitsTopmostAndDisabledChildPassesThrough` |
| Text/theme | `Text_MeasuresAndDrawsLatinAndCyrillic`, `ThemeAndPressedState_ChangeRasterButtonPixels` |
| Errors/thread | `InvalidViewport_IsRejectedBeforeSurfaceMutation`, `Backend_RejectsForeignTargetAndUnrenderedOrMismatchedSurface`, `Drawing_ReentrantOperationsAreRejectedAndFailureCanBeRetried`, `Thread_ForeignAccessIsRejectedAndPostRunsAtNextUpdate` |
| UseAlloy/shared view | `RasterProbe.Run` / `--raster`; `RasterProbe.Compare` / `--backend-compare --validation`, Debug/published logs в `TestResults/AlloyRaster` |

Исторический test run OpenGL шага: `dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --no-restore --verbosity quiet --logger trx --results-directory TestResults/AlloyOpenGL` — **Passed: 53, Failed: 0, Skipped: 0**, включая 24 raster cases. Отдельный filtered raster run24/0/0 в TestResults/AlloyRaster относится к предыдущему шагу. Это выполнение на Windows x64, не заявление о поддержке иных ОС.

## Оконный игровой HUD

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --hud-demo
```

Игра создаёт Silk-окно, VulkanDevice, scene compositor, swapchain presenter и цикл кадров. `UseAlloy(...UseExternalHost())` регистрирует UI renderer/factory; HUD создаётся как retained Argentis-дерево через `IUiSessionFactory`.

Полупрозрачная панель показывает счётчик действий, кнопку и список. Кнопка меняет сцену; необработанный клик вне HUD также попадает в сцену. Escape закрывает окно. Ввод сначала получает UiSession, после чего хост учитывает Handled, PointerCaptured и KeyboardFocused. UI-текстура рисуется только при invalidation; сцена и её композиция продолжаются каждый кадр.

Цепочка GPU: `UiSession.Update` → `AcquireVulkanTexture` → Vulkan scene/HUD draw → возврат UI lease с ShaderReadOnlyOptimal → `VulkanWindowPresenter.PresentImage` готовой сцены → swapchain. Все операции последовательны на одном устройстве и очереди; источник presentation остаётся собственностью игрового compositor. Callback PresentImage фиксирует layout источника после завершённых GPU-команд, включая случай out-of-date. Native DLL/ABI не менялись.

В интерактивном режиме CPU readback отсутствует. Автоматический вариант закрывается после проверок:

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --hud-window --validation
```

Проверяются не менее 90 presented frames, сцена/alpha/text/button pixels, динамическое добавление/удаление элемента и сохранение фокуса, pointer/keyboard активация, ввод в игровую сцену, создание второй UI-сессии с отменой capture первой, два resize, minimize/zero-size/restore и логические масштабы 100/150/200%. CPU readback выполняется только на кадрах assertions. Это проверка логического scale, а не переноса между мониторами с разным системным DPI.

## Raw handles внешнего хоста

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --raw-host --validation
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --raw-hud-window --validation
```

`--raw-host` создаёт **3 отдельных logical devices на стороне тестового engine**. Они не обёрнуты в VulkanDevice: UI получает только `VulkanHostContext` с raw handles и callbacks. Instance/validation предоставляются общим тестовым хостом; тестовый engine явно создаёт и уничтожает каждое дополнительное устройство после освобождения UI.

Проверяются три реально согласованных режима device creation: optional features выключены; enabled core `SamplerAnisotropy`/`DualSrcBlend` (если поддерживаются GPU); features2 с core features и pNext `PhysicalDevice16BitStorageFeatures`. Память structs/chain остаётся живой до UI teardown. В историческом запуске на проверенном GPU эти два core features и `StorageBuffer16BitAccess` были включены. В сумме — 6 hosted UI-сессий, 54 GPU readbacks и ещё 3 direct UI-сессии после App.Stop. Проверяются retained button pixels/alpha, input, resize/scale/suspend, lease guards, registry disposal, использование host resolver/wait callbacks и живое устройство после остановки UI/App. Отрицательные cases: invalid handles/family/version/extension names, immutable snapshots, отсутствующая обязательная loader function, исключение resolver, failed completion callback и отказ host callback после окончания lifetime. Readback нужен только для assertions.

`--raw-hud-window` повторяет оконный retained HUD через raw target: не менее 90 кадров, 2 UI-сессии, resize/minimize/restore, scene/alpha/text assertions, ввод и GPU sampling. Оконный тестовый хост по-прежнему использует Silk convenience device для своей сцены/swapchain; UI видит raw descriptor. На этом device для Skia не рекламируются optional features; maintenance1 относится к host presenter. `TrueMoon.Alloy.Hosting.VulkanWindowPresenter` остаётся convenience presenter Silk-хоста — внешний игровой движок управляет собственным swapchain.

В предыдущем raw-host шаге оба сценария проверены в Debug и из Release publish с validation 0 errors/0 warnings, включая teardown. Это самостоятельный тестовый host, не интеграция с конкретным сторонним движком. Native DLL/ABI не менялись. [Публичный контракт и пример](../../TrueMoon.Alloy.Hosting/README.md#устройство-чужого-движка).

## Lifecycle окон и resize soak

`--window-lifecycle-soak` по умолчанию создаёт последовательно240 owned HWNDs:20 повторов matrix OpenGL/Vulkan ×Opaque/Opacity/PerPixel ×standard/custom frame. Каждый цикл выполняет3logical client resize, native minimize/restore и explicit zero-size UI suspension, проверяет editor value/grapheme selection/focus после resize и cancellation активного pointer capture при close. Custom frame включает настоящий Argentis title bar и Win32 adapter. Это программные команды на одном текущем physical DPI; physical monitor transitions/Snap/input matrix этим не заменяются.

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --window-lifecycle-soak --soak-cycles 240 --soak-output TestResults/AlloyLifecycle/lifecycle.json --validation
```

Для короткого полного прохода matrix используйте `--soak-cycles 12`. Cycles ограничены1–10000; timeout фиксируется до запуска. После каждого окна проверяются HWND destruction, registry/subscriptions и все3native hooks; каждые12окон и в конце — GC/WeakReference для host/window/session/tree/controls, включая последнюю сессию. Внешний publisher остаётся живым во всём прогоне; owned Update handler снимается при disposal root. Probe сохраняет JSON также при отказе; ошибка записи отчёта не подменяет исходную ошибку.

Счётчики USER/GDI после warm checkpoint не должны превышать его более чем на2/4 соответственно. Process handles/threads/private/working-set/managed bytes сохраняются как наблюдения, без memory/VRAM budget. Ноль WeakReference/registry/hooks не доказывает освобождение всех driver/GPU allocations; для owned swapchain/synchronization handles используйте отдельный retirement probe ниже. Debug и published Release запускаются отдельно с validation layer через process-local `VK_LAYER_PATH`; отчёт записывает фактические cycles/backend/appearance/scale.

При превышении USER/GDI probe завершает оставшиеся заранее заданные cycles, сохраняет весь тренд и возвращает failure; превышение любого checkpoint остаётся отказом. Для отдельной локализации доступен `--native-window-resource-control`:24windows без Skia/UI/VulkanDevice, по умолчанию OpenGL и новый поток для каждого окна. `--native-single-thread` использует один поток; `--native-vulkan-window` выбирает окно без GL context; `--native-direct-silk` обходит TrueMoon host/adapters/input полностью. `--soak-output` сохраняет наблюдения. Успешный exit этого diagnostic control не является passing UI/resource soak.

### Native thread lifetime и shared owner loop controls

`--native-window-resource-control` принимает `--soak-cycles`1–10000 (default24). `--native-raw-wgl` обходит GLFW/Silk: скрытый Win32 STATIC HWND, owned HDC и legacy WGL context,3swaps, unbind/delete/release/destroy на owner thread. `--native-raw-window` — тот же control без DC/GL context. Raw modes исключают Silk/Vulkan/UI flags. Raw WGL context отличается от GLFW context profile; JSON сохраняет фактические vendor/renderer/version.

`--native-workers 1..8` использует фиксированные workers serial round-robin; `--native-single-thread` — вызывающий поток; без обоих flags каждое окно получает новый Thread. Worker reuse здесь — диагностическое сравнение, не реализация production scheduler и не concurrent-window proof. GLFW window creation/event processing ограничены [main thread](https://www.glfw.org/docs/latest/intro.html#thread_safety); worker pool не является portable заменой owner event loop.

`--native-ui-control` включает Skia/OpenGL session, editor focus/selection, не менее12frames и3logical resizes на каждое окно; проверяет disposal/registry, WeakReferences и native hooks. Только этот вариант загружает Skia. JSON всех controls содержит native owner IDs, top-level/message-only own HWND census до/после disposal, process thread IDs, private/managed bytes, handles, USER/GDI. После join workers записывается дополнительная фиксированная1s quiescence sample. HWND census не перечисляет все USER objects; private bytes не равны VRAM. Exit0 означает завершённую диагностику, а не passing full K7.

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --native-window-resource-control --native-raw-wgl --soak-cycles 240 --soak-output TestResults/AlloyNativeThreads/raw-wgl.json
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --native-window-resource-control --native-ui-control --native-workers 2 --soak-cycles 240 --soak-output TestResults/AlloyNativeThreads/ui-reuse.json
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --window-shared-loop-control --soak-output TestResults/AlloyNativeThreads/shared-loop.json
```

Shared-loop control выполняет20rounds на одном owner thread: два live OpenGL HWND/session, закрытие первого и создание replacement при продолжающемся sibling; всего60HWND/1200frames/180resizes. Context делается current перед input/update/render/dispose. GPU session освобождается до HWND/context. Проверяются независимые HWND, registry и DPI hooks2→1→2→0, value/selection,300WeakReferences/retained0, USER/GDI warm round4+2/+4. Focus одновременно двух native окон не утверждается. Это prototype без HostedUiWindow scheduling, Vulkan/alpha/chrome/minimize/cancellation/fault matrix и cross-platform proof; исходный полный lifecycle soak остаётся отдельным критерием. Глобальная GLFW termination не вызывается.

## Swapchain retirement и ресурсный soak

VulkanDevice автоматически включает доступные instance dependencies и feature swapchainMaintenance1. При доступности предпочитается VK_KHR_swapchain_maintenance1, затем EXT. Presenter создаёт present fence для каждого image; перед reuse/reset и уничтожением поколения ждёт соответствующие presentation fences. Старые swapchain, semaphores и fences освобождаются при resize, а не копятся до закрытия окна.

На legacy устройстве прошлые поколения сохраняются вместе со swapchain. Завершение acquire-semaphore wait для ранее показанного image нового поколения подтверждает возможность освободить его предшественников. Если новые поколения непрерывно заменяются без такой reacquisition, retirement откладывается до возобновления presentation. Legacy shutdown по-прежнему использует WaitIdle: без maintenance1 это не такая же формальная гарантия, как present fence. Основание: [Khronos guide](https://docs.vulkan.org/guide/latest/swapchain_semaphore_reuse.html), [swapchain recreation sample](https://docs.vulkan.org/samples/latest/samples/api/swapchain_recreation/README.html), [present fence specification](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html).

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --retirement-soak --validation
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --retirement-soak --legacy-presentation --validation
```

По умолчанию каждый запуск проверяет 200 UI-сессий/2000 кадров, два resize/recreation на цикл (400 swapchains), 10 minimize/restore и масштабы 1/1.5/2. Для короткого smoke добавьте `--soak-cycles 5`. Окно закрывается автоматически. Checks проверяют steady-state счётчики presenter, created == destroyed и ноль owned Vulkan objects после Dispose. Также проверяются registry count, реальные подписки внешнего publisher через Element.Own, cancellation capture и отсутствие удерживаемых сессий/деревьев через WeakReference после GC. Интерактивный режим отсутствует; CPU readback не используется в этом нагрузочном сценарии.

`VulkanWindowPresenter.Resources` возвращает `TrueMoon.Alloy.Hosting.VulkanPresentationResources` и учитывает только принадлежащие presenter Vulkan handles/pools, а `HostedUiSessionFactory.ActiveSessionCount` — зарегистрированные UI-сессии. Это не счётчики всех GPU/Skia/driver allocations и не профиль VRAM. В историческом hardware запуске поддержан KHR fence path; EXT negotiation реализован, но отдельно на EXT-only устройстве не проверен. Принудительный legacy путь проверяется на том же устройстве с отключённым feature.

## Performance baseline Settings K9

```powershell
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release --no-restore -o TestResults/AlloyPerformance/publish -v quiet
./ManualTests/AlloyVulkanTest/MeasureSettings.ps1 -Executable TestResults/AlloyPerformance/publish/AlloyVulkanTest.exe -OutputDirectory TestResults/AlloyPerformance/baseline
```

Runner выполняет два независимых Release-процесса:32 строки,12 cases (raster/OpenGL/Vulkan/Vulkan HUD × static/property-edit/list-edit), warmup≥100frames и≥500ms,500samples/case. `Rows`, `Samples` (чётное), `Warmup`, `WarmupMilliseconds`, `Runs`, `OutputDirectory` настраиваются параметрами. Прямой режим exe — `--settings-baseline` с `--baseline-rows`, `--baseline-samples`, `--baseline-warmup`, `--baseline-warmup-ms`, `--baseline-output` и optional `--baseline-environment`. Все вызовы — с owner thread без изменения runtime API.

JSON содержит raw samples/counters/GC collections/точные границы/Windows+GPU+driver+SDK metadata и source/binary/native/font/PNG hashes; CSV и Markdown содержат summary. Update включает вложенные layout/render phases, model mutation измеряется отдельно. Managed allocations относятся к owner thread; CPU wall time Render не является GPU timestamp duration. GL completion/Vulkan export-return/HUD composition/presentation вынесены отдельно. Validation выключен в измерениях; для короткой отдельной correctness проверки после настройки VK_LAYER_PATH добавить `-Samples 4 -Warmup 2 -WarmupMilliseconds 0 -Runs 1 -Validation`. System fallback fonts и отсутствие readback не заменяют golden/VRAM проверки. [Методика, текущие результаты и ограничения](../../docs/alloy/PERFORMANCE_BASELINE.md).

## Остальные сценарии

- `--runtime` — UiSession/UseAlloy/DI, input, DPI layout, Post, lease guards и device ownership без окна.
- `--window` — UsePresentation/App/DI, standalone UI, resize/minimize/restore.
- `--interop-compose` — 36 независимых offscreen GPU-композиций, premultiplied alpha и redraw после возврата lease.
- `--interop` — raw Vulkan readback заимствованного UI image.
- `--interop-check` — preflight native ABI; без флагов запускается исходный Skia GPU smoke.

К любому GPU-сценарию добавляется `--validation`. Требуется доступный VK_LAYER_KHRONOS_validation. В локальном окружении с извлечённым SDK путь задаётся так:

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
```

Фактические результаты запусков и следующие задачи — [STATUS.md](../../docs/alloy/STATUS.md). Исходники GLSL/SPIR-V и инструкция пересборки — [Shaders](Shaders/README.md).

### Callback fault/teardown proof

`--window-callback-failure --validation` проверяет deferred errors native-origin mouse/key/text/focus handlers, managed Render dispatch и actual DPI subclass fault (directed WM_GETDPISCALEDSIZE dpi0/valid SIZE). Последующие handlers подавляются, ошибка передаётся через VerifyWindowAccess/Run; direct hosts должны проверять VerifyWindowAccess после manual DoEvents/DoRender. NativeWindow/GLFW callbacks никогда не являются местом для unwind managed exceptions.

Для hosted OpenGL/Vulkan×Opaque/Opacity/PerPixel проверяются original+cleanup errors, Completion/StopAsync identity, tree/focus/registry/HWND/DPI/chrome/transparency hooks cleanup и явная здоровая новая session/window. `--callback-output <path>` сохраняет JSON scope/counters. Нужен существующий validation manifest, переданный через process-local VK_LAYER_PATH. Directed faults не являются physical DPI/device loss или long resource soak. [Точные команды и результаты2026-10-10](../../docs/alloy/HISTORY.md#49--k7-callback-fault-boundary-и-cleanup-2026-10-10).
