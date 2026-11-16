# TrueMoon.Kton: состояние и следующий шаг

**2026-11-16 — PHYS8.3: allocations deferred-команд контактов уменьшены.**
Версия `1.2611.16.4-alpha`; [задача](PLAN.md#phys83),
[реализация/числа](DEFERRED_COMMAND_ALLOCATIONS.md), [команды](HISTORY.md#phys83-2026-11-16),
[manifest/SHA](PHYS83_RESULTS.json). [Предыдущий статус](HISTORY.md#physics-status-before-phys83-2026-11-16).

Shared Runtime buffer хранит value records/copied payload blocks и переиспользует
validation/targets. Полный preflight, independent mappings, retry/fault semantics,
tokens/generations и private-type/ALC lifetime сохранены; API/schema прежние.

Один probe наimmutable `.3-alpha`/`.4-alpha` cohorts:256controllers full ECS
209719–209732→6720Б/tick(~96.8%) на обоих DI; bridge48Б, playback6224Б.
Последние6224Б сохраняют detached result/256reservation handles. Timing SLA нет.

Свежая регрессия D/R: Aether3554, Kton157, Kul103passed каждый,0failed/0skipped;
22новых cases, solution D/R0errors. Independent ECS52×4 и104macro/attribution
fixtures/62400measured Steps прошли;14старых/14новых package archives, fresh graphs,
output/license SHA и final owners/pools0 проверены. Первый full Debug имел diagnostic
50msStop timeout; после изоляции forced-GC/allocation tests полные D/R повторены.
Причина конкретного timeout не доказана, production deadlines не менялись.

Native/SDK/TFM/dependencies прежние, native rebuild не нужен. Full16suites/GPU/other OS
не повторялись. Далее PHYS8: expanded capacity/worker presets, raw callbacks/stalls,
visual pose/interpolation/Stop и platform/release matrix; PHYS7 ragdoll optional.
