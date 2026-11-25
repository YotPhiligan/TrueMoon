# Kton visual playground — PHYS8.4

Самостоятельный непакетируемый App/DI consumer. Настоящие `PrimitiveGameSession`,
SceneWorld/scheduler и `AddKtonPhysics`; физика фиксирована на 60 Hz. Vulkan рисует
primitive scene в локальный surface, OpenGL/Skia копирует его в окно Alloy.
Изображение подтверждается после того, как презентер нарисовал соответствующую версию и окно опубликовало `HostedUiWindow.FramePresented`.

Сцена: пол, плавный вход на склон, движущаяся платформа, два динамических куба,
низкий потолок и стена. Персонаж — реальный PHYS6 capsule controller; изображение
капсулы — составной ступенчатый debug-proxy из кубов, **не точная геометрия коллайдера**.
Положение центра, полный рост, ориентация и высота относительно опоры согласованы.
Зелёный цвет означает опору, красный — отсутствие опоры.

## Запуск

Из `src`, PowerShell 7, Windows x64 с подходящим Vulkan/OpenGL adapter:

```powershell
./ManualTests/KtonVisual/Play.ps1
# Microsoft DI adapter вместо Cobalt:
./ManualTests/KtonVisual/Play.ps1 -Microsoft
```

SDK/TFM/Version/LangVersion/features читаются через общий `ManualTests/BuildProfile.ps1`.
Каждое приложение запускается с новым каталогом evidence. Скрипт не устанавливает
SDK, не публикует пакеты и не меняет общую Version.

Щёлкните по изображению для фокуса:

| Клавиши | Действие |
|---|---|
| WASD | движение в world XZ |
| Space | одна попытка прыжка; повтор key-down не создаёт новые запросы |
| Enter | Play/Pause |
| F | один фиксированный Step на паузе |
| Home | Reset: новый World и очищенная история интерполяции |
| End | Stop: release physics/GPU, очистка изображения |
| Escape / потеря фокуса | сброс удерживаемого ввода |

Loop использует accumulator, максимум 100 ms catch-up за итерацию. Между соседними
завершёнными ticks отображение интерполирует копии поз. Physics World не меняется
при рендеринге; metadata сохраняет физический tick. Pause и ручной Step показывают
полную последнюю позу, teleport/reset сбрасывают историю. Это consumer policy,
а не новая interpolation API физического модуля.

## Автоматическая приёмка

```powershell
./ManualTests/KtonVisual/Verify.ps1 -EvidenceDirectory TestResults/PHYS84-new `
    -ValidationDirectory <каталог-с-VkLayer_khronos_validation.json-и-DLL>
```

Validation layer должен существовать заранее; script не скачивает и не устанавливает
его. `VK_LAYER_PATH` задаётся только дочерним процессам. В `verify` Vulkan core и
synchronization validation обязательны, автоматического fallback нет.

Матрица: source Debug, published Release, fresh isolated NuGet Debug/published Release;
каждый профиль на Cobalt/MS DI, CPU и native отдельно. Private offline feed, пустой
NuGet cache и совпадение загруженных DLL с cache проверяются. Все собственные пакеты
используют persisted Version, архивы из других запусков не перезаписываются.

CPU: настоящий UiSession/routed input и Skia raster интерфейса, положение/опора,
склон, прыжок/приземление, низкий потолок, перенос платформой, contact ticks, соответствие центров
physical/render, endpoints/midpoint интерполяции, отсутствие мутации World, Pause,
один Step, teleport/reset и idempotent Stop. Native: 12 PNG, разные дробные фазы
одного tick, побайтово одинаковые кадры на паузе, Pause/Step/Stop/Reset через routed
UI, stale ticket rejection, новая World identity, validation 0/0 и cleanup.

Синтетический routed input **не доказывает физическую клавиатуру/мышь**, held-button
Win32/DPI matrix или полное ручное прохождение уровня. Consumer работает локально;
это не новая Editor/Player IPC/ALC/reload приёмка. Exact capsule mesh, materials,
10k bodies/multiple workers, device loss, other OS/AOT и общая PHYS8 readiness
остаются отдельными. Production API, native sources и зависимости не меняются.

Game/session factory product принадлежит coordinator/caller; GPU products caller-owned
с App fallback. Surface закрывается до game/World и window/GL context; заимствованные
сервисы не освобождаются View. GPU image в Canvas независима от borrowed texture и
освобождается на UI owner при замене/Stop/Dispose. App singleton aliases не получают
второго владельца native ресурсов.
