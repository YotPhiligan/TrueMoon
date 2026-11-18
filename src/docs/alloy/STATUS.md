# Alloy / Argentis: состояние и точка продолжения

**2026-11-18 — SkiaSharp 4.157.0 и нативная Skia M157.**
Version **1.2611.18.9-alpha**. DLL пересобрана для Windows x64 с Vulkan
GENERAL/HOST и ABI v1. Решение собрано в Debug и Release. Пройдены
2919 тестов Alloy, Hosting, Aether Editor и Player в каждой конфигурации.
Проверены загрузка, Vulkan readback, композиция, raster/OpenGL/Vulkan и
размытие в Source Debug и published Release, validation 0/0. Независимые
потребители прошли 40 сценариев на свежих локальных пакетах.
[Ревизии и SHA](../../TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.build.json),
[приёмка](HISTORY.md#skiasharp-41570-2026-11-18).

**2026-11-15 — SkiaSharp/native4.156.1 и RichTextKit0.6.173 проверены.**
Обновление входит в [DEP1](../core/PLAN.md#dep1-dependency-refresh);
[полные результаты](../core/HISTORY.md#dep1-2026-11-15), [manifest/SHA](../core/DEPENDENCY_REFRESH_20261115.json).

Extended Windows x64 DLL9076224bytes пересобрана из exact NuGet parent/Skia gitlink;
GENERAL/HOST patch, ABI1/48bytes/ownership и C# source/API прежние. Ревизии,
toolchain и native SHA 4.156.1 указаны в [отчёте DEP1](../core/HISTORY.md#dep1-2026-11-15).
[Рецепт и consumer delivery](../../TrueMoon.Alloy.Rendering.Skia/README.md).

| Проверка15ноября | Результат |
| --- | --- |
| Native source build |exit0,127.76s с setup, MSVC14.51.36231/SDK10.0.26100.0 |
| Source Debug/published Release |ABI1/48bytes,18readbacks/36compositions/3backend compare/18captures/12comparisons каждый, validation0/0 |
| Negative gates |old155/stock156 exit2; unknown pin rejected; patch idempotent |
| Полные suites D/R |Alloy1224/Hosting171/Editor1507passed/config, failed/skipped0 |
| Solution D/R |0errors/1570warnings, diagnostic location/ID sets прежние |
| Fresh NuGet |10packages,36App+4direct cases, omitted-analyzer failure expected;435source hashes stable |

Потребителям нужно rebuild/publish с matching native asset. [Предыдущий STATUS](HISTORY.md#alloy-status-before-dep1-2026-11-15)
и прежние manifests сохраняют свои даты. UI functional queue PA8c.0/physical input/
DPI/K7 GDI soak/K8 device-loss/VRAM/release matrix не закрываются этим обновлением.

<a id="текущие-проверки--wsi-stages2026-10-10"></a>
<a id="предыдущие-проверки--hosting-dispatcher2026-10-10"></a>
<a id="предыдущие-проверки--wglthread-lifetime2026-10-10"></a>
<a id="предыдущие-проверки--lifecycleresize2026-10-10"></a>
<a id="предыдущие-проверки--textboxname-smoke2026-10-10"></a>
<a id="предыдущие-проверки--интеграция-в-main2026-10-10"></a>
<a id="предыдущие-ui-проверки--code-run2026-10-09"></a>
<a id="последнее-изменение-формата--2026-10-09"></a>
[Исторические проверки](HISTORY.md#alloy-status-before-dep1-2026-11-15).
