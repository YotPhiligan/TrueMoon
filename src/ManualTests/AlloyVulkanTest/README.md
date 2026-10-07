# Skia / Vulkan / OpenGL / raster / retained HUD

Windows x64, .NET 10. Native Skia с interop доставляется через Rendering.Skia; ручное копирование DLL не требуется. [Пересборка native](../../TrueMoon.Alloy.Rendering.Skia/README.md).

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

## Swapchain retirement и ресурсный soak

VulkanDevice автоматически включает доступные instance dependencies и feature swapchainMaintenance1. При доступности предпочитается VK_KHR_swapchain_maintenance1, затем EXT. Presenter создаёт present fence для каждого image; перед reuse/reset и уничтожением поколения ждёт соответствующие presentation fences. Старые swapchain, semaphores и fences освобождаются при resize, а не копятся до закрытия окна.

На legacy устройстве прошлые поколения сохраняются вместе со swapchain. Завершение acquire-semaphore wait для ранее показанного image нового поколения подтверждает возможность освободить его предшественников. Если новые поколения непрерывно заменяются без такой reacquisition, retirement откладывается до возобновления presentation. Legacy shutdown по-прежнему использует WaitIdle: без maintenance1 это не такая же формальная гарантия, как present fence. Основание: [Khronos guide](https://docs.vulkan.org/guide/latest/swapchain_semaphore_reuse.html), [swapchain recreation sample](https://docs.vulkan.org/samples/latest/samples/api/swapchain_recreation/README.html), [present fence specification](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html).

```powershell
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --retirement-soak --validation
dotnet run --project ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj -- --retirement-soak --legacy-presentation --validation
```

По умолчанию каждый запуск проверяет 200 UI-сессий/2000 кадров, два resize/recreation на цикл (400 swapchains), 10 minimize/restore и масштабы 1/1.5/2. Для короткого smoke добавьте `--soak-cycles 5`. Окно закрывается автоматически. Checks проверяют steady-state счётчики presenter, created == destroyed и ноль owned Vulkan objects после Dispose. Также проверяются registry count, реальные подписки внешнего publisher через Element.Own, cancellation capture и отсутствие удерживаемых сессий/деревьев через WeakReference после GC. Интерактивный режим отсутствует; CPU readback не используется в этом нагрузочном сценарии.

`VulkanWindowPresenter.Resources` возвращает `TrueMoon.Alloy.Hosting.VulkanPresentationResources` и учитывает только принадлежащие presenter Vulkan handles/pools, а `HostedUiSessionFactory.ActiveSessionCount` — зарегистрированные UI-сессии. Это не счётчики всех GPU/Skia/driver allocations и не профиль VRAM. В историческом hardware запуске поддержан KHR fence path; EXT negotiation реализован, но отдельно на EXT-only устройстве не проверен. Принудительный legacy путь проверяется на том же устройстве с отключённым feature.

## Другие проверки

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
