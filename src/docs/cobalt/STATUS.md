# Cobalt / DI: состояние и точка продолжения

Последняя package/regression работа **2026-10-14, MIT5/ENE5 package portion**:
actual Cobalt compiler analyzer/package graph в independent App/IPC/units consumer,
в том числе DI без Cobalt analyzer. Cobalt37/37/generator6/6 Debug/Release,
Failed0/Skipped0; [scope/команды](HISTORY.md#package-regression-mit5ene5--2026-10-14).
Cobalt/adapter C# logic не менялась; headless64+3/COB5 ниже не повторены.

Предыдущая regression **2026-10-14, MIT4/ENE4**: Cobalt37/37 и generator6/6
отдельно Debug/Release, Failed0/Skipped0, SDK10.0.402; production без изменений.
[Scope и команды](HISTORY.md#regression-при-mit4ene4--2026-10-14).
Source App/IPC composition36cases на обоих providers проверена отдельно;
package/headless64+3 и COB5 baseline ниже не повторены.

Предыдущая package работа: **2026-10-11 — COB4/CORE5 package/analyzer consumers**.
Actual compiler analyzer path/SHA256 и generated ModuleInitializer проверены из
изолированного NuGet cache. Headless/direct Cobalt и оба App providers:
**64/64 matrix processes + 3/3 packaged README examples**, Debug/Release build/publish;
Microsoft DI также проверен без consumer generator. Отрицательная сборка без
обязательного generator package завершилась ожидаемым CS0246.
[План](PLAN.md), [история](HISTORY.md#cob4--core5-package-consumers--2026-10-11), [общий статус](../STATUS.md).

## Реализация и проверки

| Область | Реализация | Текущий запуск |
| --- | --- | --- |
| Runtime registration/resolution / COB1 | Отбор service+implementation+lifetime, runtime factories, missing required/optional, typed enumerable и generics | Cobalt.Tests:37passed/0failed/0skipped, discovery37 |
| Lifetime/disposal / COB2 | Singleton concurrency, complete TypeContainer init, borrowed instances, owned factory/generated/transient values, reverse unique cleanup и error preservation | Direct regression cases в тех же37, включая concurrent и recursive disposal |
| Generator / COB3 | Collision-free sources, Composite aliases2–8 одного instance, closed/open generic dependencies, runtime factory exclusion | Cobalt.Generator.Tests:6passed/0failed/0skipped; compilation/emit и executable generated graphs |
| Microsoft DI adapter | Один wrapper внутри provider, provider ownership, sync/async/concurrent/reentrant disposal | AdapterRegressionTests входят в37 runtime cases; lifecycle graphs обоих providers — Core suite81/81 |
| Core integration | AppCreate выбирает только зарегистрированную implementation; lifetime aliases shared | AppCreate Passed в текущем Core81/81 run; прежний review failure устранён |
| Package/analyzer / COB4 | Explicit generator PackageReference, runtime/analyzer package README и isolated consumers | Analyzer совпал с packaged DLL SHA256; headless8/direct6 generated sources/configuration; 4 generation checks, 64 matrix + 3 README processes Passed. Без generator Microsoft DI работает, обязательная Cobalt generation даёт expected CS0246 |

Текущая регрессия2026-10-11: runtime **37 passed / 0 failed / 0 skipped**, generator
**6 passed / 0 failed / 0 skipped**, TRX total37/6. Core81/81 запущен отдельно.
Windows x64, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Consumer matrix:
framework-dependent Debug/Release без RID; 6 builds/6 publishes + 3 README builds
exit0/0 warnings/0 errors. Предыдущий full solution run2026-10-10 не повторялся.

## Ограничения

- Cobalt гарантирует reference-unique reverse cleanup с продолжением после ошибки. Microsoft DI сохраняет стандартную семантику: disposable Composite aliases могут освобождаться повторно; после provider cleanup failure дальнейший disposal зависит от Microsoft DI. Это проверенное различие, сервис должен поддерживать повторный Dispose.
- Generator остаётся net10.0; RS1041 и прочие shared build warnings не устранены. Новые scopes/любые unsupported declarations этой работой не добавлены.
- Generator подключается consumer явно: runtime package dependencies не доставляют
  его автоматически. PrivateAssets=all сохраняет analyzer только на build boundary.
- Package scope проверен на SDK10.0.401; другие compiler hosts/SDK/OS,
  self-contained/RID/trimming/NativeAOT и performance baseline не проверены.
  ManualTests/CobaltTest не запускался; новый bounded consumer имеет собственные результаты.

## Следующий конкретный шаг

COB4 вместе с CORE5 проверен в указанной матрице. Следующий локальный шаг — COB5:
зафиксировать контрольный graph/параметры и измерить startup/resolution/allocations
обоих providers; не менять performance semantics без baseline. Общий own-child
IPC/lifecycle сценарий остаётся задачей Mithril/Enerit.
