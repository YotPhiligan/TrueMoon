# TrueMoon: общий статус

**2026-11-16 — PHYS8.3: allocations deferred-команд контактов уменьшены.**
Версия `1.2611.16.4-alpha`; [задача](physics/PLAN.md#phys83),
[изменения/измерения](physics/DEFERRED_COMMAND_ALLOCATIONS.md),
[команды](physics/HISTORY.md#phys83-2026-11-16), [manifest/SHA](physics/PHYS83_RESULTS.json).

256controllers full ECS209719–209732→6720Б/tick(~96.8%) на Cobalt/MS DI.
Compact records/copied payload blocks/reused validation сохраняют full preflight,
immutable maps, tokens/generations/private-type/ALC lifetime. API/schema прежние.

Свежие D/R: Aether3554/Kton157/Kul103passed каждый,0failed/0skipped;22новых cases;
solution builds0errors. Первый full Debug имел diagnostic50msStop timeout; новые
GC/allocation tests изолированы, полные D/R повторены, production deadlines прежние.
Причина timeout не доказана. Independent ECS52×4,104macro/attribution fixtures,
62400Steps и14старых/14новых packages прошли, final owners/pools0.

Native/SDK/TFM/dependencies не менялись; native rebuild не нужен. Full16suites/GPU/
other OS не повторялись; PHYS8 в целом открыт. Expanded physics profiles/visual/
platform/release и import/ED/AUD планы самостоятельны. Requests/Claims и код игры/
Studio не менялись. [Предыдущая сводка](HISTORY.md#common-status-before-phys83-2026-11-16).
