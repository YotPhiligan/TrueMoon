# Enerit: состояние и точка продолжения

Текущая реализация — **2026-10-22, factory integration Aether**:
типизированные pipes client/server factories реализуют IFactory; App invocation
products резолвятся через общий factory adapter. Wire/generator semantics прежние.
Runtime126/126 и generator114/114 Debug/Release в полном solution; реальные
App+units+pipes scenarios на Cobalt/DI повторены после migration probes на explicit
owner registration. [Команды/ограничения](../engine/HISTORY.md#единые-фабрики-и-di--2026-10-22).
ENE5 performance/MMF и полная собственная package matrix не повторялись.

Исправление структуры — **2026-10-21**:6файлов из Extensions/Utils размещены
в корне generator с namespace TrueMoon.Enerit.Generator. Benchmark явно задаёт
RootNamespace=EneritBaseline; global types EneritPipesProbe получили namespace.
Runtime126/generator114 Debug/Release, units package108cases и native engine
control/checkpoint/replace consumers прошли. Protocol layout не менялся;
performance baseline не повторялся. [Миграция и команды](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

App entry-point audit — **2026-10-19**: UseInvocationService/ListenInvocationService
и PipesServiceTransport регистрируют proxy/handler/factories и transport lifecycle.
Focused Debug AppInvocationRegistrationTests:13passed/failed0/skipped0, Cobalt/DI,
включая реальное named-pipe connection в одном процессе. Runtime code не менялся;
own full/Release/generator/cross-process/package/performance suites не повторялись.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Package metadata check — **2026-10-19**: Enerit и отдельный Enerit.Generator
унаследовали repository/project metadata и получили тематические tags. Release
pack/nuspec/README/license/icon и actual analyzer DLL payload проверены; package
ownership не менялся. Own unit/IPC/performance consumers в этом metadata шаге
не повторялись. [Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Последняя связанная проверка — **2026-10-15, Mithril MIT5 diagnostics/package**.
Shared App/units/IPC fixture проверяет UnitProcessDiagnostic через App.UseDiagnostics;
Enerit126/126 Debug и126/126 Release, Failed0/Skipped0; fresh package108/108,
из них96cases с наблюдаемым parent sink. Runtime/generator Enerit не менялись;
own generator suite и performance повторно не запускались.
[Команды и scope](../mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15).

Предыдущая локальная работа — **2026-10-15, ENE5 pipes performance baseline**.
Runtime/generator/transport и unit tests не менялись. Добавлен постоянный same-build
cross-process workload v1 без App/DI/Mithril, с actual generated codecs.
[Измерения/ограничения](BASELINE.md), [контракт](PLAN.md#ene5-измерительный-контракт--2026-10-15),
[runner](../../Benchmarks/TrueMoon.Enerit.Benchmarks/README.md),
[команды/артефакты](HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).

## Предыдущие проверки ENE5 baseline — 2026-10-15

Windows11 x64/build26300, Ryzen9 9900X12/24, SDK10.0.402/.NET10.0.13.
Release/Workstation GC, power plan до/после совпал; background/frequency не контролировались.

| Область | Scope | Результат2026-10-15 |
| --- | --- | --- |
| Typed IPC workload | Scalar, bytes256/16384/65536, DTO +1024byte buffer; concurrency1/8 на одном pipes connection |10verification pairs и30fresh measurement pairs прошли,90samples/571944RPC |
| Lifecycle/ownership | Полная проверка каждого результата, rental return, exact server call count, no measurement errors/reconnects; bounded own processes | Client pending0/workers complete, child exit0/stderr empty |
| Measurements | RTT raw ticks, batch calls/s, all-thread allocations/GC counters у обоих processes | Sample windows0.456–1.294s; median9metrics/3pairs per case; без SLA/before-after claim |
| Provenance | Source/generated/published/raw report fingerprints и metadata |104source/10binary/4generated sources,42commands; Completed=true |
| Runtime regression | TrueMoon.Enerit.Tests |126passed/0failed/0skipped в каждой Debug/Release |
| Generator regression | TrueMoon.Enerit.Generator.Tests |114passed/0failed/0skipped в каждой Debug/Release |
| Solution compile | Full nonincremental Debug | Exit0/errors0/warnings1193;10новых CS8669 от generated benchmark, не подавлены |

В baseline run4TRX/480passed/0failed/0skipped. Scalar/c1: RTT P50183.9µs,
4896calls/s, client2512B/call; c8:779.3µs,9062calls/s, client2214B/call.
У каждого case заметный межзапусковый spread, полный min/max в BASELINE.
Server bytes echo включает намеренную копию результата; эти allocations не
являются transport-only или retained memory.

Измерительная часть ENE5 завершена в указанном scope. Package portion — отдельный
**предыдущий2026-10-14** independent App/IPC/units108case run (Cobalt/DI +DI без
Cobalt analyzer), actual Enerit analyzer path/SHA/generated sources и expected
omitted-analyzer CS0234. Свежая package matrix текущим baseline не запускалась.
[История package regression](HISTORY.md#package-regression-с-bundled-cobalt--2026-10-14),
[основной package contract](../mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14).
Собственные Core/Cobalt/Mithril suites, UI/native и другие benchmark workloads
не повторены. Runtime suite исполнил свои существующие source process tests;
это не отдельная новая matrix независимых script/package consumers.

## Ограничения и сохранённые contracts

- Same-build peers/generated method IDs и native scalar layouts без schema/version
  negotiation. MMF ENE3, other OS/RID/self-contained/trimming/AOT не подтверждены.
- Local cancellation/timeout не отменяет уже отправленный handler; replay нет.
  Reconnect для последующих вызовов; killed parent client cleanup не наблюдаем.
- Strict response/decode ownership сохраняется: failed nested decode возвращает
  whole rentals; successful response принадлежит caller, decoded input — service
  от entry даже при throw. Public DTO prefix/manual callback contracts прежние.
- Queues не ограничены, shutdown кооперативный; ignoring cancellation handler может
  удерживать Dispose. Long stress/resource trend/backpressure не подтверждены.
- Closed-loop latency не open-loop SLA; allocations включают service/harness/control
  и небольшие batch-boundary расходы, не native/retained memory. Startup/connect,
  App/DI/restart/failure-path performance, exact wire bandwidth и engine frames не измерены.
- Generated CS8669 nullable-context warnings и shared warnings остаются quality backlog;
  добавление benchmark не является исправлением generator template или общим выпуском.

## Следующий конкретный шаг

Сверка2026-10-15 завершила [общую quality matrix](../QUALITY.md); новых runtime/
generator/tests/performance запусков она не создаёт. Следующий общий этап —
[UI package/Hosting quality](../alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15),
[MIT5 diagnostics](../mithril/STATUS.md) выполнен в bounded scope.
Enerit performance backlog: scalar c1/c8 allocation/CPU profile до выбора изменений,
затем тот же frozen workload для сравнения. Baseline не выявил нового correctness
blocker; он не требует автоматической реализации MMF/новых IPC APIs. Перед общим
release package/platform matrix и ограничения подтверждаются отдельно.
