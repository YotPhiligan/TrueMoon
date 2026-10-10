# Cobalt / DI: состояние и точка продолжения

Дата: **2026-10-10 — исправление runtime/generator/adapter и Core integration**. [План и контракты](PLAN.md), [история](HISTORY.md), [общий статус](../STATUS.md).

## Реализация и проверки

| Область | Реализация | Текущий запуск |
| --- | --- | --- |
| Runtime registration/resolution / COB1 | Отбор service+implementation+lifetime, runtime factories, missing required/optional, typed enumerable и generics | Cobalt.Tests:37passed/0failed/0skipped, discovery37 |
| Lifetime/disposal / COB2 | Singleton concurrency, complete TypeContainer init, borrowed instances, owned factory/generated/transient values, reverse unique cleanup и error preservation | Direct regression cases в тех же37, включая concurrent и recursive disposal |
| Generator / COB3 | Collision-free sources, Composite aliases2–8 одного instance, closed/open generic dependencies, runtime factory exclusion | Cobalt.Generator.Tests:6passed/0failed/0skipped; compilation/emit и executable generated graphs |
| Microsoft DI adapter | Один wrapper внутри provider, provider ownership, sync/async/concurrent/reentrant disposal | AdapterRegressionTests входят в37 runtime cases; lifecycle graphs обоих providers — Core suite81/81 |
| Core integration | AppCreate выбирает только зарегистрированную implementation; lifetime aliases shared | AppCreate Passed в текущем solution run; прежний review failure устранён |

Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Полный solution build exit0/errors0; итоговый solution test/discovery подтверждает отдельные37/6. Команды/TRX и quality review — в HISTORY.

## Ограничения

- Cobalt гарантирует reference-unique reverse cleanup с продолжением после ошибки. Microsoft DI сохраняет стандартную семантику: disposable Composite aliases могут освобождаться повторно; после provider cleanup failure дальнейший disposal зависит от Microsoft DI. Это проверенное различие, сервис должен поддерживать повторный Dispose.
- Generator остаётся net10.0; RS1041 и прочие shared build warnings не устранены. Новые scopes/любые unsupported declarations этой работой не добавлены.
- ProjectReference/compilation tests не подтверждают analyzer delivery в NuGet consumer. Pack/Release/independent consumer и performance baseline не запускались; ManualTests/CobaltTest собран в solution, но отдельно не запущен.

## Следующий конкретный шаг

COB4 вместе с CORE5: package consumer с generated registration, Composite и generic dependency, затем небольшой headless lifecycle scenario на Cobalt/adapter. COB5 startup/resolution/allocations измерять после consumer proof.
