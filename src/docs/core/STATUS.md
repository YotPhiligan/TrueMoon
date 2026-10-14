# Core: состояние и точка продолжения

Последняя package/regression работа **2026-10-14, MIT5/ENE5 package portion**:
Core Description уточнён, Core package доставлен как transitive utility dependency
в independent App/IPC/units consumer108cases. Core81/81 Debug/Release прошли отдельно,
Failed0/Skipped0; [команды/scope](HISTORY.md#package-regression-mit5ene5--2026-10-14).
Headless64+3 ниже не повторён; Core C# logic не менялась.

Предыдущая regression **2026-10-14, MIT4/ENE4**: Core81/81 отдельно Debug/Release,
Failed0/Skipped0, SDK10.0.402. Исправлена изоляция Diagnostics build-failure test
от событий параллельных Apps; production Core без изменений.
[История](HISTORY.md#diagnostics-test-isolation-при-mit4--2026-10-14).
Package64+3 ниже — предыдущий run2026-10-11, не повторён.

Предыдущая package работа: **2026-10-11 — CORE5/COB4 headless package consumer**.
Проверены независимые PackageReference приложения на Cobalt и Microsoft DI,
включая Microsoft DI без consumer generator. **64/64 процессов матрицы**
Debug/Release × build/publish и **3/3 delivered README examples** прошли;
отрицательная сборка без generator завершилась ожидаемым CS0246.
Обновлены package descriptions и самостоятельные packaged README.
[План](PLAN.md), [общий статус](../STATUS.md), [команды и область вывода](HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11).

## Реализация и проверки

| Область | Реализация | Текущий запуск |
| --- | --- | --- |
| Bootstrap/lifecycle / CORE1 | Общий runner, наблюдаемые ошибки, reverse rollback/stop, отдельный shutdown token, повторные операции, console unsubscription и owned disposal | AppLifecycleTests:23 cases; AppTests:5. AppCreate, падавший при review, Passed |
| Lifetime | Snapshot callbacks вне lock, asynchronous wait continuations, owned/borrowed CTS, deferred disposal во время callbacks | AppLifetimeTests:12/12, включая concurrent Cancel/Dispose и внешнюю отмену borrowed source |
| Configuration / CORE2 | Provider add/remove/priority, согласованные reads/conversion, args/env refresh, JSON read/write/snapshot, paths и GetOrCreate | ConfigurationTests:11/11 |
| Diagnostics / CORE2 | Resolver-owned subscription/factory, individual handle cleanup, listener error isolation и snapshots | DiagnosticsTests:8/8, включая startup/build failure и observable app events |
| Utilities / CORE4 | Scheduler drain/completion/affinity, writer guards/reference cleanup, validated memory rental ownership и IMemoryOwner | CoreUtilityTests:22/22; зависимые Enerit regression tests отдельно в его STATUS |
| DI integration / CORE3 | Одни lifecycle graphs на Cobalt и Microsoft DI, shared lifetime aliases | Provider theory cases Passed; собственные Cobalt runtime37/37 и generator6/6 относятся к Cobalt |
| Package / CORE5 + COB4 | Изолированный feed/cache, explicit generator, headless/direct Cobalt/Microsoft-only consumers | 64 matrix + 3 exact README processes Passed; 6 builds/6 publishes + 3 README builds exit0/0 warnings/0 errors. Configuration/diagnostics/cancellation/start/stop/build errors и owned/borrowed cleanup проверены |

Регрессия предыдущего package run2026-10-11: TrueMoon.Tests **81 passed / 0 failed / 0 skipped**,
TRX total81. Windows x64, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest.
Cobalt37/37 и generator6/6 запущены отдельно; это не новый solution run.
Package matrix использует framework-dependent output без RID, local version
`0.2.2.7-alpha.headless.20261011142810069`; пакеты не опубликованы.

Предыдущая non-incremental TrueMoon.slnx проверка **2026-10-10**:
exit0/errors0/warnings1271, solution tests643passed, в том числе Core81;
Mithril discovery0 не является passing suite. NU1506/NU1507 были устранены
без изменения effective5.9.0 Roslyn. [Предыдущая история](HISTORY.md#исправление-проблем-ядра--2026-10-10).

## Ограничения

- Shutdown timeout30s кооперативный; сервис/handler, игнорирующий cancellation, способен удерживать завершение. Scheduler consumers обязаны дождаться async continuations перед его закрытием.
- Microsoft DI имеет собственные disposal semantics для alias registrations и cleanup failures; различие описано в Cobalt PLAN. DefaultApp/Lifetime повторный Dispose поддерживают.
- Manager-backed Memory может не иметь доступного массива через TryGetArray; partial slices/foreign/double/stale Return теперь отвергаются. Старые spans/pins после возврата использовать нельзя.
- Disk-full/permission injection и межпроцессные JSON writes не проверялись.
  Process/IPC integration, physical UI, Linux/macOS, другие SDK/compiler hosts,
  RID/self-contained/trimming/single-file/NativeAOT и performance этим шагом не проверены.
  Pack/component builds сохраняют shared warnings; чистые consumer builds не означают
  zero-warning solution. Общий выпуск не закрыт.

## Следующий конкретный шаг

CORE5/COB4 проверен в указанном package scope. Локально — COB5 startup/resolution/
allocations baseline на фиксированном consumer graph, без оптимизации до измерения.
Общий функциональный приоритет — bounded own-child MIT0/ENE2 lifecycle/IPC proof
по планам владельцев. Выявленные ранее lifecycle/configuration/diagnostics/scheduler/
pool defects сохраняют свои регрессии.
