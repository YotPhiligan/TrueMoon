# Cobalt / DI: состояние и точка продолжения

**2026-11-05 — ревизия очереди, без изменений runtime/generator/adapter и новых запусков.**
[План](PLAN.md#current-cobalt-queue), [полный прежний STATUS](HISTORY.md#status-before-planning-revision-2026-11-05).

## Готовность и ограничения

Registration/generation/lifetime/errors/owned disposal и App integration имеют
собственные bounded proofs. Cobalt остаётся штатным provider; Microsoft DI adapter
и explicit UseDI consumers сохраняются. Новые scopes/factory APIs и массовая
миграция не следуют из ревизии планов.

Последняя собственная работа — **2026-10-29, COB5a warm allocation attribution**:
10Dry +10bounded paired BDN cases и20behavior scenarios; production не оптимизирован.
На фиксированном graph v1 singleton/closed32B, open96B, transient440B, multiple248B.
Экспериментальная no-capture cycle check:0/0/64/216/152B; это variant, не текущий API.
[Результаты и границы](HISTORY.md#cob5a-warm-allocations--2026-10-29).

Предыдущие runtime179/generator91 D/R и package/provider proofs сохраняют свои
даты и graph. UI native Cobalt/MS DI consumer не равен новому полному Cobalt run.
Latency exploratory; performance budget/migration конкретного consumer требуют
отдельного решения. Other compiler/OS/RID/AOT и новые lifetime guarantees не обещаются.

## Следующий конкретный шаг

**COB5a production follow-up:** перенести no-capture cycle check с сохранением
dependency path, reentrancy и failure cleanup; повторить runtime/generator/lifecycle
и paired graph measurements. Затем отдельно решать open-generic reflection и
synthesized enumerable metadata costs. Это полезная независимая performance задача,
а не prerequisite Editor/Observer. Общая очередь — [roadmap](../PLAN.md#current-roadmap).

<!-- Совместимость прежних ссылок STATUS; записи перенесены в HISTORY. -->
<a id="предыдущий-cob7-run--2026-10-15"></a>
Прежние датированные разделы — в [полном архиве](HISTORY.md#status-before-planning-revision-2026-11-05).
