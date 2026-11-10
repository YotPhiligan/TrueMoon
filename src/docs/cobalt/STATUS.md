# Cobalt / DI: состояние и точка продолжения

**2026-11-09 — COB5a.1 production no-capture cycle check выполнен.**
Один захватывающий predicate в ResolveItem заменён индексным обходом прежнего
resolver/service tuple. Lock, depth/path, failure recording, activation push/pop,
retry, aliases и ownership/lifetime сохранены. Public API/dependencies не менялись.
[План](PLAN.md#cob5a1-production-cycle-check), [измерения/команды](HISTORY.md#cob5a1-production-2026-11-09).

Предкоммитная сверка11ноября: runtime179/179 и generator96/96 каждый D/R,
failed/skipped0; source SHA unchanged. [Команды](HISTORY.md#precommit-2026-11-11).
Измерения allocations ниже относятся к09ноября и не повторялись.

## Готовность и ограничения

Warm singleton/exact-closed32→0B, open-generic96→64B, transient440→216B,
materialized plugins248→152B на frozen COB7 graph v1. Единственное различие
между45файлами двух measured output snapshots — TrueMoon.Cobalt.dll.
Workload/adapter/generated graph совпадают побайтно; Scenario source SHA неизменён.
Короткий in-process BDN job даёт exploratory timing: control means дрейфуют,
часть CI перекрывается. Снижение allocations подтверждено; общий speedup не заявлен.

COB6e сохранён: hint names ≤128символов со стабильным SHA-256 полного ключа,
generated class/registration identity и XML summary служебного registration/Init.
Cobalt остаётся default, Microsoft DI adapter и прежние registration/ownership
контракты сохранены. Новые scopes/factory API и массовая миграция не назначены.

## Текущие проверки

Windows x64 / SDK10.0.403 / runtime10.0.14 / net10.0,09ноября2026:

| Scope | Фактический результат COB5a.1 |
| --- | --- |
| Runtime/adapter tests |179/179 каждый Debug/Release, failed0/skipped0 |
| Generator tests |96/96 каждый Debug/Release, failed0/skipped0 |
| Frozen graph/provider behavior |40scenarios:10каждого provider на original и optimized runtime; workload verification оба snapshot прошли |
| BDN |10Dry+10Short original и10Dry+10Short optimized; singleton representative Default прошёл отдельно |

BDN0.15.8, InProcessEmitToolchain, warmup3/iterations10/100ms, MemoryDiagnoser.
Parent agent не запускал builds/tests во время измерений; background desktop
workloads не контролировались. Power plan до/после совпадает. Restore/build/test
изолированы в TestResults/CobaltCob5a, обычные bin/obj не использовались.
Новые тесты не дублировали существующие: cycle/path/alias/closed-generic/retry/
depth127/128/129 и cleanup контракты прямо проверяются текущей suite.

Предыдущий COB6e local package proof0.2.5.2-alpha (9sources/11checks D/R и
CS1591 negative control) выполнен до этой runtime оптимизации и не повторён ею.
[COB6e evidence](HISTORY.md#cob6e-generator-2026-11-09),
[runner](../../ManualTests/CobaltGeneratorConsumer/README.md).
Fresh packages, actual Studio, full solution/native/cold/App performance не запускались.
External publish и смена package version не выполнялись.

## Следующий конкретный шаг

Выбрать отдельный bounded generic-metadata срез: GenericTypeArguments /
MakeGenericType на warm open resolution и synthesized IEnumerable; сначала
зафиксировать workload/contracts и измерить. Оставшиеся64B open и32B enumerable
overhead не устранялись этим изменением. Другой независимый шаг — consumer
приёмка готового COB6e в Studio после обновления пакетов.
[Общая очередь](../PLAN.md#current-roadmap),
[предыдущий STATUS целиком](HISTORY.md#cobalt-status-before-cob5a1-2026-11-09).

<!-- Совместимость прежних ссылок STATUS; записи перенесены в HISTORY. -->
<a id="предыдущий-cob7-run--2026-10-15"></a>
Прежние датированные разделы — в [архиве](HISTORY.md#status-before-planning-revision-2026-11-05).