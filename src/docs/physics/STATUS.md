# TrueMoon.Kton: состояние и следующий шаг

**2026-11-17 — PHYS8.4: визуальный debug-consumer реализован, автоматическая приёмка прошла.**
[Задача](PLAN.md#phys84), [уровень/скриншот/границы](VISUAL_PLAYGROUND.md),
[команды](HISTORY.md#phys84-2026-11-17), [manifest/SHA](PHYS84_RESULTS.json).

ManualTests/KtonVisual использует настоящую PrimitiveGameSession, SceneWorld/scheduler
и Kton. Пол, склон, движущаяся платформа, прыжок, низкий потолок и dynamic cubes;
отображение через Vulkan/OpenGL surface. Капсула показана ступенчатым proxy.
Consumer интерполирует копии поз; Physics World и metadata tick сохраняются.
Pause показывает завершённую позу, Step выполняет один tick, Stop/Reset закрывают
старые tickets и освобождают World/surface. Есть helper Play.ps1 с WASD/Space и командами.

Фактически прошли 16 профилей: source Debug/published Release/fresh NuGet Debug/
published Release, каждый CPU/native на Cobalt/MS DI. 96 CPU-групп и 96 PNG;
validation 0/0, финальные GPU owners/handles/UI sessions/physics worlds/pools — 0.
19 собственных архивов одной версии, isolated graph/cache и loaded DLL SHA проверены;
extended Skia/font snapshots совпадают в четырёх outputs. Full unit/solution suites
в этом срезе не повторялись. [Предыдущий статус](HISTORY.md#physics-status-before-phys84-2026-11-17).

Ручной интерактивный Win32-прогон не подтверждён: Computer Use не смог надёжно
активировать/наблюдать тестовое окно; отдельный пробный процесс завершён адресно.
Это не failure scripted-матрицы и не proof штатного закрытия interactive loop.
Общая Version 1.2611.16.4-alpha сохранена: production/native/dependencies прежние.
Далее — explicit 10k-body/worker profiles и raw callbacks/stalls; ручной input/close,
точная collider mesh, DPI/device loss/other OS/AOT и общая PHYS8 readiness открыты.
