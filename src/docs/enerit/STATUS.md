# Enerit: состояние и точка продолжения

Дата: **2026-10-14 — ENE5/MIT5 package portion**.
[Основной contract](../mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14),
[локальный plan](PLAN.md#ene5-package-portion--2026-10-14),
[команды/evidence](../mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14),
[runner](../../ManualTests/UnitsPackageConsumer/README.md), [общий статус](../STATUS.md).

## Реализация и проверки

| Область | Изменения / scope | Текущий run |
| --- | --- | --- |
| Package metadata | Enerit/generator descriptions и самостоятельные PACKAGE.md, generator analyzer-only |9nupkgs, license/icon/readme и dependency versions проверены; packages остаются local |
| Independent consumer | Own build/CPM/source mapping boundary, isolated cache, copied app source без ProjectReference/Compile links |108/108cases Debug/Release ×build/publish, Cobalt/DI +DI без Cobalt analyzer, Completed=true |
| Analyzer delivery | Explicit references, compiler paths/SHA и generated client/handler/DTO/initializer |4compiler configurations; omitted Enerit analyzer — ожидаемый CS0234 |
| Runtime deployment | Apphost/assembly/deps/runtimeconfig/dependencies из packages; own selected child | Typed/concurrent/cancel/error/restart/budget/Stop/rollback/parent-exit; exact PID/exit/count/cleanup; generators/Roslyn/UI DLLs отсутствуют |
| Regression | Existing suites на source graph после pack | Enerit126/126, generator114/114, Mithril89/89, Core81/81, Cobalt37/37/generator6/6 отдельно Debug/Release; Failed0/Skipped0 |
| Solution | Final non-incremental Debug | Exit0/errors0/warnings1227; UI/native checks не запускались |

Windows x64/build26300, net10.0, SDK10.0.402, xUnit2.9.3/VSTest.
12final TRX/906passed/0failed/0skipped. Package matrix129commands:9packs,
2restores/4builds/4publishes,108case processes,negative restore/build. TrueMoon package
source/version и nupkg/compiler hashes проверены; source fingerprints current.
Run artifacts — ignored TestResults/UnitsPackages; детали/version у Mithril HISTORY.

## Ограничения и сохранённые contracts

Package portion ENE5 выполнена только в этом scope; latency/throughput/allocations
baseline ещё не измерен, поэтому полный ENE5 не закрыт. Operational diagnostics
MIT5, MMF, other OS/compiler, RID/self-contained/single-file/trimming/NativeAOT,
long stress/resource trend/backpressure/uncooperative shutdown не подтверждены.

Same-build/generator sources и native layouts/Roslyn method IDs без schema
negotiation сохраняются. Correct wire format не менялся. Новая generation требует
matching runtime. Local cancellation/timeout, no replay и independent process
restart semantics прежние. Killed parent client cleanup не наблюдаем.

[Strict decode ownership](PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14)
сохраняется: failed nested decode возвращает whole rentals, successful result
принадлежит caller, validated inputs принадлежат service от entry даже при throw.
Public DTO prefix reader/manual callbacks имеют прежний offset/ownership contract;
user side effects не откатываются. Queues не ограничены, cooperative shutdown.

## Следующий конкретный шаг

**ENE5 IPC baseline** на фиксированном same-build pipes consumer: latency,
throughput и allocations с явно выбранными payload/concurrency, warmup/повторами
и сохранёнными raw measurements. После измерения выбирать оптимизации. MIT5
operational diagnostics остаются отдельным process-level продолжением.
