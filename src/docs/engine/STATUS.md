# Aether / Editor: состояние и точка продолжения

**2026-11-17 — PHYS8.4: локальный Kton visual consumer и автоматическая приёмка готовы.**
Подробная задача у [Kton](../physics/PLAN.md#phys84), [скриншот/API/границы](../physics/VISUAL_PLAYGROUND.md),
[команды](../physics/HISTORY.md#phys84-2026-11-17), [manifest/SHA](../physics/PHYS84_RESULTS.json).

Настоящая PrimitiveGameSession/Kton + existing Vulkan/OpenGL primitive surface;
копии поз интерполируются consumer без мутации Physics World. Pause/Step/Stop/Reset,
склон/прыжок/потолок/платформа и routed input проверены в 16 профилях: source D,
published R, fresh NuGet D/R, оба DI, CPU/native отдельно. 96 CPU-групп/96 PNG,
validation 0/0 и финальные resources/handles/UI sessions 0; 19 package archives
и fresh graph/cache/loaded SHA подтверждены. Полные unit/solution suites не повторялись.

ManualTests consumer не добавляет production API/native/dependency изменений;
Version 1.2611.16.4-alpha сохранена. Капсула — stepped proxy; это локальный consumer,
не новая Editor/Player IPC/ALC/reload приёмка. Ручной Win32 input/close не подтверждён:
Computer Use не смог надёжно активировать/наблюдать окно, наш probe завершён адресно.
[Предыдущий статус](HISTORY.md#engine-status-before-phys84-2026-11-17).

Далее PHYS8 expanded capacity/workers/raw callbacks/stalls; физический input/DPI,
device loss/other OS, import/ED/AUD и историческая drain cause самостоятельны.
Чужие UIQA-DOC изменения и заявка Enerit сохранены.
