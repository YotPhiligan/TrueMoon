# Core: состояние и точка продолжения

Дата: **2026-10-10 — исправление проблем ревью и полный solution check**. [План и принятые контракты](PLAN.md), [общий статус](../STATUS.md), [команды и регрессии](HISTORY.md#исправление-проблем-ядра--2026-10-10).

## Реализация и проверки

| Область | Реализация | Текущий запуск |
| --- | --- | --- |
| Bootstrap/lifecycle / CORE1 | Общий runner, наблюдаемые ошибки, reverse rollback/stop, отдельный shutdown token, повторные операции, console unsubscription и owned disposal | AppLifecycleTests:23 cases; AppTests:5. AppCreate, падавший при review, Passed |
| Lifetime | Snapshot callbacks вне lock, asynchronous wait continuations, owned/borrowed CTS, deferred disposal во время callbacks | AppLifetimeTests:12/12, включая concurrent Cancel/Dispose и внешнюю отмену borrowed source |
| Configuration / CORE2 | Provider add/remove/priority, согласованные reads/conversion, args/env refresh, JSON read/write/snapshot, paths и GetOrCreate | ConfigurationTests:11/11 |
| Diagnostics / CORE2 | Resolver-owned subscription/factory, individual handle cleanup, listener error isolation и snapshots | DiagnosticsTests:8/8, включая startup/build failure и observable app events |
| Utilities / CORE4 | Scheduler drain/completion/affinity, writer guards/reference cleanup, validated memory rental ownership и IMemoryOwner | CoreUtilityTests:22/22; зависимые Enerit regression tests отдельно в его STATUS |
| DI integration / CORE3 | Одни lifecycle graphs на Cobalt и Microsoft DI, shared lifetime aliases | Provider theory cases Passed; собственные Cobalt runtime37/37 и generator6/6 относятся к Cobalt |

Итого TrueMoon.Tests **81passed/0failed/0skipped**, discovery81. Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Полная non-incremental TrueMoon.slnx сборка: **exit0, errors0, warnings1271**. Solution tests:643passed, в том числе Core81; Mithril discovery0 не является passing suite. Сводка TRX сверена отдельно. NU1506/NU1507 устранены: удалены duplicate central versions без изменения effective5.9.0 Roslyn, добавлены локальные NuGet source/mapping settings.

## Ограничения

- Shutdown timeout30s кооперативный; сервис/handler, игнорирующий cancellation, способен удерживать завершение. Scheduler consumers обязаны дождаться async continuations перед его закрытием.
- Microsoft DI имеет собственные disposal semantics для alias registrations и cleanup failures; различие описано в Cobalt PLAN. DefaultApp/Lifetime повторный Dispose поддерживают.
- Manager-backed Memory может не иметь доступного массива через TryGetArray; partial slices/foreign/double/stale Return теперь отвергаются. Старые spans/pins после возврата использовать нельзя.
- Disk-full/permission injection и межпроцессные JSON writes не проверялись. Release/pack/independent consumer, process integration и physical UI matrix не запускались. Build warnings остаются; passing component tests не закрывают общий выпуск.

## Следующий конкретный шаг

CORE5/COB4: собрать и запустить независимый headless package consumer с обоими выбранными providers, cancellation/error/diagnostics и доставкой generator. Затем измерять startup/allocations на фиксированном graph. Выявленные в review lifecycle/configuration/diagnostics/scheduler/pool defects исправлены и имеют поведенческие регрессии.
