# Aether / Editor: состояние и точка продолжения

**2026-11-16 — PHYS8.3: shared deferred command buffer оптимизирован.**
Детальная задача у [Kton](../physics/PLAN.md#phys83),
[реализация/числа](../physics/DEFERRED_COMMAND_ALLOCATIONS.md),
[команды](../physics/HISTORY.md#phys83-2026-11-16), [manifest/SHA](../physics/PHYS83_RESULTS.json).
Версия1.2611.16.4-alpha. API/schema/contact timing, full preflight, independent maps
и private-type/ALC lifetime сохранены; scheduler/transforms production code прежний.

256controllers full ECS209719–209732→6720Б/tick(~96.8%) на обоих DI.
После warmup bridge48Б, playback6224Б — detached mapping остаётся независимым.
22новых cases; полные Aether3554/Kton157/Kul103passed каждый Debug/Release,
0failed/0skipped; solution builds0errors. Первый full Debug имел diagnostic50msStop
timeout; новые forced-GC/allocation tests изолированы, D/R повторены полностью,
production deadlines прежние. Причинность конкретного timeout не установлена.

Independent ECS52×4 и104macro/attribution fixtures/62400Steps, coherent old/new
packages и final owners/pools0 проверены. [Предыдущий статус](HISTORY.md#engine-status-before-phys83-2026-11-16).
Native/SDK/TFM/dependencies прежние; full16suites/GPU/other OS не повторялись.
PHYS8 expanded workers/capacities, raw callbacks/stalls, visual/platform/release gates,
import/ED/AUD и историческая drain cause самостоятельны. Requests/Claims не менялись.
