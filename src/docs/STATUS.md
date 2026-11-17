# TrueMoon: общий статус

**2026-11-17 — PHYS8.4: визуальный тестовый уровень и автоматическая приёмка реализованы.**
[Единственная задача](physics/PLAN.md#phys84), [скриншот/управление/границы](physics/VISUAL_PLAYGROUND.md),
[команды](physics/HISTORY.md#phys84-2026-11-17), [manifest/SHA](physics/PHYS84_RESULTS.json).

ManualTests/KtonVisual: actual game session/Kton, slope/platform/jump/ceiling,
consumer pose interpolation и Vulkan/OpenGL. Source Debug/published Release/fresh
NuGet Debug/Release, Cobalt/MS DI, CPU/native — 16 профилей, 96 CPU-групп/96 PNG.
Validation 0/0, final GPU/physics/UI owners/handles/pools 0; 19 package archives,
isolated graph/cache и loaded DLL SHA проверены. Unit/full-solution suites не повторялись.

Production/native/dependencies не менялись, версия 1.2611.16.4-alpha сохранена.
Ручной Win32 input/close не подтверждён из-за ограничения Computer Use; отдельный
пробный процесс завершён адресно, не считается passing интерактивного прогона.
Exact capsule mesh, expanded physics workload/workers, physical/DPI/device-loss/
platform/release и import/ED/AUD остаются открытыми. UIQA-DOC и входящая Enerit-заявка
не изменялись. [Предыдущая сводка](HISTORY.md#common-status-before-phys84-2026-11-17).
