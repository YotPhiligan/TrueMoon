# Нативная Skia для TrueMoon.Alloy.Rendering.Skia

Проект хранит проверенную Windows x64 библиотеку с Vulkan interop extension:

- `NativeAssets/win-x64/libSkiaSharp.dll` — SkiaSharp 4.153.1, ABI v1, около 9 MB.
- `NativeAssets/win-x64/libSkiaSharp.build.json` — точные revisions и SHA-256 артефакта.
- Исходники extension и инструменты: [NativeInterop](../ManualTests/AlloyVulkanTest/NativeInterop/README.md).

DLL предназначена для хранения в Git вместе с проектом; исходники Skia и промежуточная native-сборка остаются в игнорируемом TestResults. Новый проект не требуется: native asset относится к существующему Skia renderer.

## Использование

Ссылка на renderer автоматически копирует DLL при build и publish в `truemoon-native/win-x64/libSkiaSharp.dll` относительно выходного каталога приложения. При pack библиотека включается как contentFiles с copy-to-output. Отдельный путь предотвращает конфликт со штатными SkiaSharp.NativeAssets.

Публичный `SkiaNativeLibrary.Initialize(path?)` до первого native-вызова загружает DLL и устанавливает DllImportResolver для SkiaSharp и interop bindings. Обе сборки используют один handle до завершения процесса. Vulkan/OpenGL/raster renderers автоматически инициализируют bundled DLL; явный вызов нужен для своего пути до первого использования Skia. Аргумент `--native-skia <путь>` в AlloyVulkanTest позволяет проверить альтернативный файл. На других платформах встроенный Windows asset не загружается; interop там не проверен. Runtime и подключение App: [Hosting](../TrueMoon.Alloy.Hosting/README.md).

```powershell
dotnet build ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --interop-check
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --interop
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --interop-compose
```

Для существующего устройства игрового движка renderer принимает `VulkanHostContext` с borrowed instance/physical device/device/queue handles, family, API version, enabled extensions/features и host loader/completion callbacks. Он не создаёт и не уничтожает device/instance и не требует `VulkanDevice` в raw target. Подробный [пример и lifetime contract](../TrueMoon.Alloy.Hosting/README.md#устройство-чужого-движка); проверки — `--raw-host --validation` и `--raw-hud-window --validation`. В PLAN 2.1a прямая зависимость renderer-сборки от Platform.Silk удалена: прямые raw Vulkan/raster/OpenGL пути через `UiSession.Create` не требуют Platform.Silk или Hosting. После 2.1b нет и транзитивных TrueMoon.Core/Contracts/Windowing зависимостей через Alloy. `Silk.NET.Vulkan` bindings остаются явной backend-specific зависимостью Vulkan renderer, а не оконным API.

## Миграция alpha API: PLAN 2.1a

`VulkanWindowPresenter` и `VulkanPresentationResources` теперь принадлежат сборке и namespace `TrueMoon.Alloy.Hosting`, а не Rendering.Skia. Их retirement, `Present`/`PresentImage`, ownership и синхронизация сохраняются. Silk convenience overloads и `VulkanUiTarget.Device` удалены из renderer; явный adapter — публичный static `SilkVulkanHost` в Hosting:

- `VulkanHostContext.FromSilkDevice(device)` → `SilkVulkanHost.CreateContext(device)`.
- `new VulkanUiTarget(device)` с Silk `VulkanDevice` → `SilkVulkanHost.CreateTarget(device)`.
- `new VulkanInteropSurface(device, context, size)` с Silk `VulkanDevice` → `SilkVulkanHost.CreateSurface(device, context, size)`.

Factories возвращают соответственно `VulkanHostContext`, `VulkanUiTarget` и `VulkanInteropSurface`; параметры surface — `SkiaSharp.GRContext` и `SkiaSharp.SKSizeI`. Silk consumers добавляют ссылку/import `TrueMoon.Alloy.Hosting` и сохраняют свой device отдельно; `target.Host` предоставляет borrowed descriptor. Raw constructors `VulkanHostContext`, `new VulkanUiTarget(host)` и `new VulkanInteropSurface(host, context, size)` остаются в Rendering.Skia и не требуют этого adapter. Это alpha API assembly/namespace migration: существующие consumers обновляют и пересобирают, совместимость уже собранных приложений не гарантируется. [Примеры подключения](../TrueMoon.Alloy.Hosting/README.md#миграция-alpha-api-plan-21a).

## Границы после PLAN 2.1b

`TrueMoon.Alloy` теперь содержит только `Runtime` и csproj: единственный ProjectReference — Argentis, PackageReference и ссылок на Core/Contracts нет. Rendering.Skia явно зависит от Alloy и пакетов SkiaSharp/Silk.NET.Vulkan; Platform.Silk остаётся без Skia. Silk.NET, SkiaSharp, Topten.RichTextKit и Core/Contracts для legacy presentation явно подключены в опциональном Hosting, а не в общем runtime.

31 legacy-файл из Alloy root/Presenters перенесён в сборку Hosting, namespace `TrueMoon.Alloy.Hosting.Compatibility` и `.Compatibility.Presenters`. No-options `UsePresentation<TView>()` теперь требует opt-in Compatibility; explicit Hosting API не изменён. Это breaking alpha assembly/namespace migration **без type forwarders**: обновите ссылки/imports/callers и пересоберите consumers. Compatibility не рекомендуется для нового UI; старые `Visual.GetEnumerator` и lifetime bugs не исправлены, закомментированные прототипы не являются рендерерами. [Подробности](../TrueMoon.Alloy.Hosting/Compatibility/README.md), [explicit AlloyTest](../ManualTests/AlloyTest/README.md).

Native asset/ABI, lease/ownership и retirement в 2.1b не изменялись. Фактические build/tests/GPU/publish проверки переноса и их ограничения — в [STATUS](../docs/alloy/STATUS.md); исторические результаты ниже сохранены отдельно.

## OpenGL backend

`SkiaOpenGLRenderBackend`/`OpenGLUiTarget` работают через общие UiSession и Skia drawing/text adapter. Target заимствует существующий desktop GL context через resolver/current-context guard; Skia owns offscreen RGBA/Premul GPU surface. `PresentOpenGL` композитит кадр в host-owned RGBA8 framebuffer без CPU copy/swap; `clear: false` сохраняет scene. `ReadbackOpenGLImage` возвращает независимую CPU копию для assertions/export. Thread affinity, pending frame guard, zero-size suspend/restore и draw retry сохраняются.

Хост owns context/FBO/attachments, обеспечивает current context и заново binds своё GL state после Skia. UI закрывают до уничтожения context. Standalone доступен через `UsePresentation(...UseSkiaOpenGL().UseSilkWindow())`; explicit swap и Closing disposal выполняет hosting. Native asset/ABI не менялся: та же bundled 4.153.1 DLL поддерживает проверенный GL путь. [Подключение и границы](../TrueMoon.Alloy.Hosting/README.md#opengl-hud-и-окно), [probes](../ManualTests/AlloyVulkanTest/README.md). Проверено Windows x64 desktop GL3.3/NVIDIA, не GLES/ANGLE или cross-context texture handoff.

## CPU raster backend

`SkiaRasterRenderBackend` создаёт session-owned прозрачную RGBA8888/Premul поверхность в памяти. `RasterUiTarget` не требует window/device. Layout/text/drawing выполняются тем же Skia adapter, что и Vulkan; `UiSession.Update()` сохраняет перерисовку только по invalidation. `SnapshotRasterImage()` возвращает caller-owned immutable `SKImage`, независимый от последующих render/resize/disposal. Поверхность/сессия thread-affine; нулевой viewport suspended, stale/unrendered export запрещён. Native Skia и его загрузчик нужны, Vulkan context/interop ABI для raster не используются.

Подключение через `UseAlloy(options => options.UseSkiaRaster().UseExternalHost())` и напрямую: [пример Hosting](../TrueMoon.Alloy.Hosting/README.md#raster-ui-без-окна-и-vulkan). `--raster --raster-output TestResults/AlloyRaster/hud.png` запускает CPU App/DI-пример, `--backend-compare --validation` проверяет один Argentis view на raster/OpenGL/Vulkan. Текущая native поставка ограничена Windows x64.

## Ресурсы рисования и текста

Каждая raster/OpenGL/Vulkan UI-поверхность владеет одним SKPaint и ограниченным FIFO-кэшем до16 SKFont/metrics по точным FontFamily/FontSize. Measure и Draw используют одни ресурсы; cache переживает resize/scale/suspend этой поверхности, но не переносится между сессиями. При вытеснении и Dispose освобождаются native fonts; paint освобождается при Dispose. SKFont удерживает native typeface reference, поэтому временный managed SKTypeface освобождается сразу после создания font и не хранится в cache. Canvas/context/device не принадлежат cache.

Цвет/style/stroke width устанавливаются перед каждой операцией, чтобы stroke не менял последующие fill/text. Ресурсы и TextLayout требуют creator thread; ранее полученный text service после surface.Dispose отклоняет Measure. Cleanup cache включён в constructor rollback и best-effort surface teardown, в том числе при графическом отказе. Новых public API, packages или native ABI нет; InternalsVisibleTo предоставляет доступ только тестовой сборке.

Это ограниченная оптимизация по trace2026-10-08, без cache строк/text blobs, dirty-subtree layout или partial redraw. [До/после, профиль и проверки](../docs/alloy/PERFORMANCE_BASELINE.md#профиль-и-оптимизация-2026-10-08).

## Пересборка и обновление DLL

Команды выполняются из src. Требуются .NET 10, Git, настоящий Python 3, Ninja, MSVC C++ x64, Windows SDK и x64 Spectre libraries. Native build не запускается при обычном dotnet build.

При первом запуске:

```powershell
git clone --depth 1 --branch v4.153.1 https://github.com/mono/SkiaSharp.git TestResults/VulkanInterop/SkiaSharp-4.153.1
```

Повторная сборка с используемыми в этой машине инструментами:

```powershell
$env:PYTHONHOME = 'C:/Program Files/LibreOffice/program/python-core-3.13.15'
$env:PATH = 'C:/Program Files/LibreOffice/program;' + $env:PATH
pwsh -NoProfile -File ManualTests/AlloyVulkanTest/NativeInterop/BuildNativeInterop.ps1 `
  -SkiaSharpSourcePath TestResults/VulkanInterop/SkiaSharp-4.153.1 `
  -VisualStudioPath C:/BuildTools/TrueMoonVS2022 `
  -PythonPath 'C:/Program Files/LibreOffice/program/python-core-3.13.15/bin/python.exe' `
  -NinjaPath TestResults/VulkanInterop/tools/ninja/ninja.exe -InstallToRenderer
```

Для другого Python/компилятора замените пути; PYTHONHOME нужен только для данного bundled Python. `-CheckOnly` проверяет prerequisites и revisions без сборки/копирования.

`-InstallToRenderer` после успешной native-сборки сверяет pinned версию с SkiaSharp в Directory.Packages.props, копирует DLL в NativeAssets и обновляет build.json. SkiaSharp и native DLL обновляются согласованно. Скрипт не подменяет NuGet cache и не меняет глобальные Git/Windows настройки.

После обновления выполнить build, ABI probe и GPU probe из раздела выше. Для core/synchronization validation:

```powershell
$env:VK_LAYER_PATH = (Resolve-Path TestResults/VulkanInterop/tools/validation-1.4.363.0/Bin).Path
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --interop --validation
dotnet ManualTests/AlloyVulkanTest/bin/Debug/net10.0/AlloyVulkanTest.dll --interop-compose --validation
```

Validation layer устанавливается/извлекается отдельно; подробности toolchain, pins и предыдущих проверок находятся в [README эксперимента](../ManualTests/AlloyVulkanTest/NativeInterop/README.md). После изменения extension на диске ранее применённая копия в source checkout должна быть обновлена явно: ApplyNativeInterop намеренно отказывается перезаписывать отличающийся extension. Проще использовать новый чистый checkout pinned исходников.

Независимая Vulkan-композиция добавлена в manual probe; shaders встроены в managed сборку, native DLL менять для этого не требуется. Пересборка shaders: [инструкция](../ManualTests/AlloyVulkanTest/Shaders/README.md).
