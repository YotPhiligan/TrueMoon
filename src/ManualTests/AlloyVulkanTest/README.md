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

**Fixed-font full golden K2/K4:** `--settings-golden` использует immutable font catalog, доставляемые OFL fonts и30reference PNG. [Метод, frame matrix, AA допуски и baseline update](../../docs/alloy/GOLDEN_TESTS.md). `RunSettingsGolden.ps1` проверяет Debug/published Release, по180полных сравнений, SHA256/provenance и отсутствие перезаписи fixtures. Diff PNG и report JSON сохраняются при ошибке. Прежний `--settings-compare` ниже остаётся отдельным selected-pixel smoke с системными шрифтами.

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

`--opengl-failure` намеренно бросает исключение из презентера во время рисования. Ошибка перехватывается внутри GLFW callback, затем передаётся через Completion/StopAsync после безопасного закрытия, registry count0. Probe подтверждает, что process не завершается из-за managed unwind через native refresh callback.

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

### Production dispatcher и Vulkan resource controls — 2026-10-10

`--hosted-dispatcher --validation` проверяет реальные HostedUiWindow/HostedUiDispatcher: GL/GL, Vulkan/Vulkan и GL/Vulkan пары; один owner через независимые app scopes, разные HWND, resize/selection, native WM_PAINT своего и соседнего окна, close/recreate при продолжающемся sibling, command/startup faults и независимый shutdown. Startup cancellation отменяет ожидание, повтор Start не создаёт второй root; canceled shutdown wait не отменяет cleanup. Проверяются close-before-start, rejected starts/posts, registry/hooks0 и validation0. Это bounded functional proof, не полный K7, physical drag/DPI или VRAM.

`--partial-window-startup` проверяет OpenGL/OpenGL и Vulkan/Vulkan в одном
`HostedUiDispatcher` с общим реестром сессий. После отказа фабрики содержимого
при первом attach проверяются освобождение дерева, HWND и hooks неудавшегося окна.
Выжившее окно получает адресный `WM_CHAR`; проверка ждёт `FramePresented` после
запроса кадра с новым значением `TextBox`. `--validation` требует доступного
слоя Vulkan. Сценарий не имитирует физический ввод.

`--window-lifecycle-soak --soak-resource-trace` сохраняет дополнительные samples после каждого окна для привязки native роста к backend. Основные warm checkpoint и USER/GDI +2/+4 лимиты не меняются.

Для изоляции Vulkan на одном потоке без HostedUiWindow:

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --soak-cycles 24 --soak-output TestResults/AlloyDispatcher/vulkan-device.json
# Добавить один из --native-vulkan-skia / --native-vulkan-swapchain / --native-vulkan-presenter.
```

Device control не создаёт Skia/presenter/swapchain. Skia control добавляет direct UI session/draw без export/presentation. Swapchain control выполняет native capabilities/formats queries и create/destroy без acquire/submit/present. Presenter control использует raw vkCmdClearColorImage image и существующий presenter без Skia/UI. `--retain-vulkan-loader-control` удерживает Vk.GetApi до окончания программы для проверки loader-lifetime гипотезы. Flags отражаются в JSON; Completed означает завершение diagnostic, а не passing resource stability. No-window/device-loss/VRAM/точный native allocator этими controls не подтверждаются.

### WSI stage и raw Win32 controls — 2026-10-10

`--native-vulkan-wsi-stage capabilities|formats-count|formats|present-modes|swapchain` требует `--native-vulkan-window --native-vulkan-device`; не совмещается с Skia/presenter/старым swapchain flag. Samples отмечают вызовы queries, create/destroy и завершение device/window disposal. `--native-wsi-repeat 1..32` повторяет stage на той же surface, по умолчанию1. Controls не включают validation и не выполняют acquire/submit/present.

`--native-raw-vulkan` с теми же flags создаёт hidden Win32 STATIC HWND, instance с KHR_surface/Win32_surface и device с KHR_swapchain через raw Vulkan bindings. GLFW, Silk Window, production VulkanDevice и Skia не используются. Дополнительный `--native-surface-first` меняет порядок raw teardown на surface→device; оба варианта сначала уничтожают swapchains, затем surface/device, instance и HWND. Изменение native ownership production не требуется.

`--native-vulkan-vendor <hex>` выбирает raw GPU, например0x10DE/NVIDIA или0x1002/AMD; system preferences не меняются, production GPU policy не затронута. Omit `--native-single-thread` для serial new-thread diagnostic. Последний очищает текущий GDI trend после thread exit, но не решает прежний GL/WGL thread lifetime и не принимается как Hosting scheduling workaround.

```powershell
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-vulkan-wsi-stage formats --soak-cycles 24 --soak-output TestResults/AlloyWsi/formats.json
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --native-window-resource-control --native-single-thread --native-vulkan-window --native-vulkan-device --native-vulkan-wsi-stage swapchain --native-raw-vulkan --soak-cycles 24 --soak-output TestResults/AlloyWsi/raw.json
# Добавить --native-surface-first для другого teardown order или --native-wsi-repeat 4 для4generations/surface.
```

Debug/published Release controls локализовали GDI trend до первого swapchain на новой surface/window. На текущем NVIDIA ICD native debugger наблюдает `GetDC(NULL)` внутри vkCreateSwapchain и отсутствие последующего ReleaseDC для3конкретных HDC в bounded lifecycle; formats-only control этого callsite не достигает. Raw24cycles на NVIDIA дают GDI6→29, на установленном AMD —5→5 в обоих configurations. Это evidence конкретных ICD, не full UI rendering/K7 или verdict обо всех drivers; private/handles growth отдельно не объяснён. [Команды, hashes, stack и ограничения](../../docs/alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Completed в JSON означает завершение diagnostic; K7 не закрыт, прежние warm limits сохранены.

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
## Window component

`--window-component --component-output <fresh-directory>` проверяет настоящий
Argentis Window<TData> через HostedUiWindow на OpenGL и Vulkan. Initial component
Title/ClientSize имеют приоритет над host defaults; проверяются одна рамка,
native/painted caption, resize/state commands, retained focused draft/theme,
component close veto → async drain → actual cleanup и session/native hooks0.
App profiles дополнительно проверяют UsePresentationFactory, borrowed VM/style,
Cobalt/MS DI, lazy Build и единственное создание/disposal продукта и модели.
OpenGL PNG снимается после presentation. `--validation` требует установленного
VK_LAYER_KHRONOS_validation и корректного VK_LAYER_PATH; plain run без него не
является Vulkan validation proof.

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Debug -- --window-component --component-output TestResults/WindowComponent/native-debug
dotnet publish ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -c Release -o TestResults/WindowComponent/publish
dotnet TestResults/WindowComponent/publish/AlloyVulkanTest.dll --window-component --component-output TestResults/WindowComponent/native-release
```
## System font typography

`dotnet run --project ManualTests/AlloyVulkanTest -- --font-typography --font-output TestResults/Fonts-new --validation`
uses a fresh output directory and writes18dark/light raster/OpenGL/Vulkan PNGs at
synthetic100/150/200%, full-frame comparisons and font/native/hardware metadata.
Configure process-local `VK_LAYER_PATH` to an available validation manifest directory.
The fixture covers weights400/600, italic, ordered family fallback, plain/rich text,
selection retention, idle and disposal. Optional Segoe UI Variable families fall
back explicitly; this is not physical-DPI or automatic-optical-sizing acceptance.

## Validation icons and shadows

For the default frosted control style, run
`--control-glass --design-output fresh-directory --validation`. It covers
Light and Dark at 100%, 125%, and 175% on raster, OpenGL, and Vulkan. The
fixture captures rounded validation badges, their tooltip and focus, controls
within a frosted panel, and a standalone field over a black/white split.
The report verifies the standalone blur by sampling each side of the split
and records full-frame pixel comparisons and diff PNGs.

`--icon-catalog --design-output fresh-directory --validation` captures every
built-in symbol at 12, 16, and 24 logical pixels in Light and Dark, at 100%,
125%, and 175% on raster, OpenGL, and Vulkan. The report verifies visible
contours, both the silver disc and the rim of Argentis, matching layout and
idle. Full-frame differences and diff PNGs are recorded separately.

`--window-underlay --design-output fresh-directory --validation` captures the
Light and Dark window gradient and a frosted panel over the empty Editor viewport
at 100%, 125%, and 175% on raster, OpenGL, and Vulkan. The report includes pixel
samples before and after the glass tint. The fixture also checks idle, the solid
theme fallback, an explicit frame background, and the validation badge.

`--validation-shadows --design-output fresh-directory --validation` runs the shared
gallery scaffold with TextBox/RichTextEditor/NumberBox validation badges, an actual500ms hover
tooltip, semantic panel and a MenuPopup sample. Raster/OpenGL/Vulkan, Dark/Light,
synthetic100/150/200%,18PNG/12comparisons; exact logical bounds, flat tolerance2 and
symmetric1px edge envelope with coverage32 (typography retains24). Uses default
system fonts, preserves input/selection, verifies idle and owned tree disposal.
The menu sample now uses the default frosted backdrop over a black/white split.
BlurWitness pins actual Gaussian mixing, so a tint/opaque/no-blur substitute fails.
Pinned native4.156.0 includes the GENERAL source-stage HOST fix for VUID02819.
`--design-frosted-menu` remains an explicit alias; `--design-opaque-menu` and
`--design-no-shadows` are positive controls. `--design-no-blur` must fail the semantic
pixel assertion. A pre-fix `--native-skia` DLL must reproduce VUID02819.
Configure VK_LAYER_PATH to the available validation layer and use fresh output paths.
`VerifyBackdropPackages.ps1 -ValidationDirectory <layer> -EvidenceDirectory <fresh>`
packs5fresh packages, restores an external PackageReference-only Vulkan consumer,
then checks6Dark/Light/DPI cases and teardown validation in Debug/published Release.
It also verifies solid square badges, Theme.OnError, NumberBox parent validation and
the exact live tooltip without losing selection/focus; screenshots precede the tooltip.
[Badge audit and current checks](../../docs/alloy/HISTORY.md#validation-badge-2026-11-08).
See [current native fix evidence](../../docs/alloy/HISTORY.md#vulkan-backdrop-2026-11-08).
[Current commands/results/limits](../../docs/alloy/HISTORY.md#validation-shadows-2026-11-08).

Add `--design-lower-richtext-hint` to reproduce the PA8c.0 shadow overlap case.
It opens the lower RichTextEditor hint in Light at synthetic scale 2 and
saves all three 1480×1300 frames before comparing the GPU captures to
raster. The report keeps the original tolerances and records exact logical
bounds, full-frame statistics, and up to 16 out-of-tolerance pixel samples
per comparison. The output includes full-frame diff PNGs and 8× nearest-pixel
crops centered on the historical pixel at (1077, 457). A completed comparison
outside tolerance writes the report and exits with code 1. Use fresh output
paths for the `--design-no-shadows` and `--design-opaque-menu` controls.
[PA8c.0 capture results](../../docs/alloy/HISTORY.md#pa8c0-shadow-overlap-2026-11-20).

Use `--design-no-panel-shadow` to suppress only the panel shadow or
`--design-no-hint-shadow` to suppress only the validation hint shadow.
Pass both flags to suppress both. Each control keeps the original shadow
geometry and sets its paint alpha to zero. The menu keeps its usual shadow
and backdrop in all four cases. Capture reports include the effective
shadow settings of the panel, hint and menu.

## Fractional typography and thin lines

`--fractional-typography --font-output fresh-directory --validation`
captures the same Light scene at synthetic 125% and 175% on raster, OpenGL,
and Vulkan. The scene uses the default font family at sizes 11, 12, and 14,
with regular and semibold Latin and Cyrillic samples. It draws horizontal
and vertical lines and rectangular outlines with a width of one logical pixel.
Each shape group includes integer and fractional coordinates.

Routed keyboard input places the caret after the first word, then selects
that word with the caret at the same index. The probe checks focus, the
selection range and the painted bounds of both changes. Each saved 1:1
frame has 4× nearest-pixel crops for text, editor and lines. Full-frame diff
PNGs and numeric results for each region accompany the crops.

The CPU/GPU comparison uses flat tolerance 2 and the existing symmetric
one-pixel edge envelope with tolerance 24. OpenGL/Vulkan uses the strict
per-channel tolerance 2. Region comparisons use neighboring pixels from the
full frame, including at crop boundaries. A completed comparison outside
tolerance saves the report and returns exit code 1.

## Small text composition

`--typography-composition --font-output fresh-directory --validation`
uses the same six text samples at synthetic 125% and 175% in three cases.

| Case | Drawing and composition |
| --- | --- |
| `opaque` | Text drawn on the Light theme's opaque input background |
| `translucent` | The same text and panel rendered as one 60% opacity group over a two-color background |
| `offscreen` | Text rendered to a transparent premultiplied target and sampled 1:1 over the same two-color background |

The offscreen case uses a Skia raster surface, a borrowed OpenGL framebuffer,
or the Vulkan host compositor. GPU output is read after composition. The probe
also captures the source and checks every final pixel against the premultiplied
blend with tolerance 1. Saved output includes 1:1 frames, 4× nearest-pixel
text crops, offscreen sources with alpha, and full-frame backend differences.

The report separates raw backend comparisons from same-backend comparisons
of glyph coverage against the opaque case. The latter use source alpha for
offscreen and estimate coverage from the red-channel contrast for opaque and
translucent output. The report records each sample's ink bounds and peak
coverage. All cases use the current Alloy text settings.

## Small text rendering modes

`--typography-rendering-modes --font-output fresh-directory --validation`
uses the small text composition matrix with nine Skia font modes. Each mode
changes one property from the current default. The probe compares aliased and
grayscale edges, requests `SubpixelAntialias`, varies hinting, and toggles
`SKFont.Subpixel` for glyph positioning. It draws each sample at its original
integer device X and again 0.5 device pixels to the right. Layout and baseline
coordinates come from the default font metrics.

The probe draws directly into Skia raster, OpenGL and Vulkan surfaces so that
it can vary the font properties independently of Alloy's text adapter. An
unmodified `--typography-composition` run provides a check of the default
mode at zero offset. OpenGL composites the intermediate texture in the same
GPU context. Vulkan composites the leased image in the host pipeline. The
LCD request is measured on the default targets of these backends.

The report distinguishes backend parity from intentional mode and phase
differences. It includes glyph bounds, partial alpha counts and per-sample
horizontal centroids weighted by the alpha channel of the intermediate
texture. Each configuration writes 324 frames at 1:1 with 4× text crops
and 108 intermediate textures with alpha.

## Isolated glyph positions and clipping

`--typography-glyphs --font-output fresh-directory --validation` captures
`A V i j Ё ф` individually at 125% and 175%, with `SKFont.Subpixel` off and on.
Columns follow that glyph order. Rows are 11/400, 11/600, 12/400, 12/600,
14/400, and 14/600. Each cell uses the same baseline and origin for its
unclipped and clipped captures. The `x50` variant moves the origin right by
0.5 device pixels. Grayscale antialiasing and normal hinting stay fixed.

The report includes `SKFont` advances and run origins before shaping, their
scaled equivalents, and the actual pixel bounds and alpha centroids from
each backend. The `advance` clip uses the exact horizontal advance rectangle.
The `halfadvance` clip cuts at a known device-pixel edge inside the glyph and
checks which pixels survive. Both checks preserve the RGBA of surviving
pixels and count removed coverage. Each configuration saves 72 composed
frames, 72 source textures with alpha, and 72 first-row crops at 4×.

## Fractional scrolling in TextBox

`--typography-textbox-positioning --font-output fresh-directory --validation`
renders a focused, single-line `TextBox` at synthetic 125% and 175% on
raster, OpenGL and Vulkan. The Light theme fixture uses Segoe UI Variable
Text at logical size 14. With the caret at End, the probe changes the field
width so the text origin moves left by 0, 0.25, 0.5 and 0.75 device pixels.
It uses the stock layout service and varies `SKFont.Subpixel` in a diagnostic
drawing context. The stock raster renderer provides a pixel-exact comparison
for the off mode within the text row.

At each phase the probe checks that clipped text leaves the left padding clear,
clicks the boundary before the final glyph, and verifies the painted caret and
selection of that glyph with Shift+Left. End must restore the scroll position. The
report records the contrast-weighted horizontal center of the final glyph
and compares the geometry and text-row pixels across backends. Each run
writes 48 frames at 1:1 and 48 field crops at 4×. File suffixes
`x0`, `x25`, `x50` and `x75` identify the four scroll phases.

Add `--dark-bidirectional` to run the same probe in Dark with three strings
ending in `W`, `Д`, and `7` before trailing spaces. The spaces keep the
measured glyph clear of the caret. The probe narrows the field to capture
`toward-end`, then widens it to capture `toward-start`. On the return pass
it renders at Home and then End at each width so that TextBox recalculates
its retained scroll offset. It saves the End frame at each phase. Reported
motion is relative to the first captured phase of each direction.

The Dark run writes 288 frames at 1:1 and 288 field crops at 4×. It checks
equality of complete frames at the same phase in both directions on each
backend. Backend comparisons and stock raster checks cover the text row
as in the Light run.

## Fractional positioning in the Skia renderer

`--typography-textbox-renderer-positioning --font-output fresh-directory --validation`
repeats the Dark matrix with actual raster, OpenGL, and Vulkan UI backends.
For each string, it exercises plain text and `TextBox.UseShapedText` at
125% and 175% in both directions. The probe runs the renderer defaults,
explicit `off`, and explicit `on`. It configures the surface before the first
layout. Plain text uses `SKFont.Subpixel` and shaped text uses RichTextKit's
`TextPaintOptions.SubpixelPositioning`. Application backends keep their
defaults.

The report records the origin, glyph coverage, and interaction geometry for
all 864 frames. Each captured phase verifies the caret move and selection in
the readback, then checks that End restores the frame exactly. Forward and
reverse frames must match at the same phase. Explicit settings that match a
renderer default must match its pixels exactly. Other mode comparisons
record measured pixel differences. Each run also saves 864 field crops at 4×.

`--typography-editable-fields --font-output fresh-directory --validation`
compares a standalone `TextBox` and a `NumberBox` with its own prefix and editor.
It resizes the **outer** control, focuses the editor and scrolls the same numeric
draft to End. Spaces isolate the last digit for measuring its ink; this is
not a test of number parsing. Only plain text positioning is overridden.
Each `Light|Dark` / `TextBox|NumberBox` subdirectory contains a `report.json`
and captures for all three backends at logical 100%, 125% and 175%.
To measure tracking error, take `abs(GlyphMotionPx - ScrollMotionPx)` for
nonzero scroll motion and compare `default`, `off` and `on` within each theme.
These are pixel and input checks, not timing, cache or physical DPI measurements.

## TextBox positioning performance

`--typography-textbox-performance --font-output fresh-directory` compares
`Subpixel=false` and `true` on the stock raster, OpenGL, and Vulkan renderers
at 125% and 175%. The Dark fixture contains 12 plain `TextBox` controls using
the same three strings. Stationary frames redraw the retained tree at the
same scroll offset. Scrolling frames narrow the fields in steps of 0.25
device pixels. Home and End restart each four-phase sweep. The intermediate
Home frame and its completion run outside the timed sample.

Use a published Release executable for timing. The default order within each
backend, scale and scenario is off–on–on–off. Run a second process with
`--subpixel-on-first` to reverse it. Each batch has at least 192 warmup
frames and 200 ms of warmup, then 512 measured frames. A separate
`--validation` run uses 16 warmup and 32 measured frames per batch.

The report records CPU wall time and managed allocations for `UiSession.Update`
and its nested `Render` call. GPU completion and Vulkan texture return have
separate measurements. The probe samples the process-wide Skia font cache
after purge, after warmup, and after measurement. The counts refer to cache
entries and bytes. The final pixel readback is outside the timed frames and
checks identical repeated frames and a pixel difference between modes.

## Editor inspector positioning performance

`--typography-inspector-performance --font-output fresh-directory` uses the
same timing protocol and order flags with the production `EditorInspectorView`
and style. It creates an isolated Editor project and opens an in-memory scene
through the document services. The 336×600 logical pixel fixture shows
Transform, Appearance, Interaction and a schema-defined section with numeric,
boolean and multiline fields. The scrolling case anchors the caret at the
end of a long Transform X value and narrows that field by 0.25 device pixels
per phase. The render surface and document are retained throughout each batch.

The toggle applies to the plain text path of the surface. RichTextKit keeps
its default positioning, including for the note with colored emoji. OpenGL and
Vulkan report the Skia context's cached resource count and bytes before drawing,
after the first anchored frame, after warmup, after measurement, and at each
phase of an untimed verification pass. The counters cover Skia's context cache.
Raster has no GPU context. Short validation runs save representative PNGs.

## Mixed text and color emoji

`--typography-emoji --font-output fresh-directory --validation` draws
`Готово ✅ · Автор 👩‍💻` and a text-only control in `TextBox` with
`UseShapedText = true`. It uses installed Segoe UI Variable Text and Segoe UI
Emoji at logical size 18 and synthetic 125% and 175% on raster, OpenGL and
Vulkan. The report records font revisions and hashes, fallback runs, line
heights and baselines, color-pixel counts and caret positions. A reference
render from the recorded RichTextKit runs checks the pixels inside the text
region. Routed arrows visit all grapheme boundaries in both directions.

The probe saves 1:1 frames, a 4× crop, and frames with the caret on either side
of each emoji. It exercises toggling shaped layout off and back on within the
same session.

## Editor vector toolbar icons

`--editor-vector-icons --icons-output fresh-directory --validation`
captures the actual Editor header with controlled command models. The upper
row draws vector paths and the lower row shows previous geometry at the same
logical icon size of 16. The matrix covers Light and Dark at synthetic 100%,
125%, and 175% on raster, OpenGL, and Vulkan. It saves normal, pressed Play,
and disabled frames, 4× crops, and crops of pixel differences outside the
existing comparison tolerances.

The probe verifies keyboard activation of each toolbar action and the compact
layout. The report records both geometry and pixel parity. `Passed` requires
successful interaction, matching layout, matching icon centers within half a
physical pixel, matching ink bounds within one pixel, and clean Vulkan
validation. `PixelParityPassed` reports the existing pixel tolerances
independently and retains all recorded edge differences.
