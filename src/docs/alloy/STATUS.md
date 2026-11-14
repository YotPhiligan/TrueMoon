# Alloy / Argentis: состояние и точка продолжения

**2026-11-12 — SkiaSharp/native обновлены до4.156.0.**
Exact source build SkiaM156, bundled Windows x64 DLL9075712bytes и metadata
согласованы с уже выбранным managed package. ABI1/48bytes/ownership и C# API прежние.
GENERAL/HOST patch сохранён; explicit source pins, upstream NuGet isolation,
VS default MSVC/Spectre и installed SDK selection проверены.
[Native/delivery guide](../../TrueMoon.Alloy.Rendering.Skia/README.md),
[manifest/команды/SHA](SKIASHARP41560_RESULTS.json),
[история и предыдущий STATUS](HISTORY.md#skiasharp-41560-2026-11-12).

| Фактическая проверка | Результат |
| --- | --- |
| Native source build |1475steps/104.27s/exit0;5exports; actual ABI1/48bytes |
| Source Debug/published Release |по18readbacks,36GPU compositions,3backend compare и18shadow/typography captures/12comparisons; Vulkan validation0/0 |
| Alloy/Hosting/Editor Debug и Release |по1224/171/1496passed; failed/skipped0 |
| Solution non-incremental Debug |0errors/1556warnings |
| Fresh NuGet |10packages,36App+4direct cases; omitted-analyzer failure ожидаем;435source hashes stable |
| Negative gates |old native155 и stock156безexports exit2; unknown source rejected; patch idempotent155/156 |

Потребителям нужно пересобрать/перепубликовать outputs с matching native DLL.
Native producer использует upstream SDK10.0.404; consumers — общий net11/SDK11
profile. Исторические CORE8/Player результаты сохранены в HISTORY со своими датами;
нового native Player/full Aether acceptance здесь нет. UI functional queue прежняя:
PA8c.0 visual baseline, physical input/DPI/OS capture, K7 GDI soak,
K8/device-loss/VRAM и release matrix сохраняют отдельные критерии.

<a id="текущие-проверки--wsi-stages2026-10-10"></a>
<a id="предыдущие-проверки--hosting-dispatcher2026-10-10"></a>
<a id="предыдущие-проверки--wglthread-lifetime2026-10-10"></a>
<a id="предыдущие-проверки--lifecycleresize2026-10-10"></a>
<a id="предыдущие-проверки--textboxname-smoke2026-10-10"></a>
<a id="предыдущие-проверки--интеграция-в-main2026-10-10"></a>
<a id="предыдущие-ui-проверки--code-run2026-10-09"></a>
<a id="последнее-изменение-формата--2026-10-09"></a>
