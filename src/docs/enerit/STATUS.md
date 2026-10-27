# Enerit: состояние и точка продолжения

Связанное планирование — **2026-10-27, ARCH2 package/host transition**.
Explicit runtime launch/bootstrap и согласование fingerprint при разных executable
hosts ведутся у [Aether](../engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27).
Generic IPC negotiation/framing этим решением не расширяются. Code/API не менялись,
build/tests не запускались; ARCH2 предшествует следующим Editor diagnostics features.

Предыдущая связанная реализация и проверка — **2026-10-27, ENE6d.4 отдельный child diagnostic lane**.
Aether использует existing typed invocation с отдельным endpoint,72KiB wire cap,
one request/connection, copied batch/rate/deadline limits и выключенной telemetry
самой delivery lane. Control не делит admission с logs; generic framing/generator
не расширялись. [Domain contract](../engine/DIAGNOSTICS_DELIVERY.md),
[связанные проверки](HISTORY.md#ene6d4-отдельный-child-log-канал--2026-10-27).

Текущие Enerit343/343 и generator114/114 Debug/Release, failed/skipped0.
Full solution4285/4285 в каждой конфигурации; Core373/Aether1338/Editor741.
Builds0errors: Debug nonincremental1546warnings, Release incremental1316warnings,
SDK10.0.403. [Evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
3actual Preview children подтвердили independent control/log flood/reap; это
Aether consumer proof, не весь ENE6c/long-load/platform/package acceptance.
Пользовательский Microsoft DI10.0.14 сохранён; historical FrameReceiver/Mithril races открыты.

Предыдущий собственный срез — ENE6d.3 outcome/wait metrics:13instruments/
37bounded series, pull-only callbacks, fixed failure classes, reconnect/late и
queue wait count/seconds. Его52newcases и ENE6d.2 resource profile прошли текущие
full suites. [Schema](DIAGNOSTICS.md#исходы-reconnect-и-время-ожидания),
[история](HISTORY.md#ene6d3-исходы-ipc-и-ожидание-admission--2026-10-27).
ENE6d частичен: далее Editor console/export, domain/frame aggregates и queue
distribution; ENE6c negotiation, remote cancel, MMF и long trends отдельны.
MEL не добавлен; прежние trace/resource/telemetry flags сохраняются.

Предыдущая работа — **2026-10-23, первый ENE6a/MIT6a implementation slice**.
Finite client admission/wait queue и shared server count/request-byte/connection
limits реализованы через copied PipesTransportOptions, factories и App registration.
Sent local cancellation удерживает capacity до late reply/disconnect и local
writer/decode cleanup; rejected work не вызывает handler. Async Stop/Completion
сохраняет ownership при cancelled wait и ошибках cancellation callbacks; App
передаёт shutdown token до final disposal. Wire/generated mapping прежние.
[Контракт](PLAN.md#ene6a-и-mit6a-первый-контракт--2026-10-23).

Предыдущая проверка2026-10-23: Enerit **166/166 Debug и166/166 Release**, generator114/114 в каждой
конфигурации; failed0/skipped0. Full solution2361/configuration, по10TRX, build
Debug0errors/1522warnings. Детерминированный queued-writer CTS test подтвердил
RED→GREEN; все scheduled paths теперь используют заранее captured token value.
Source/published **44/44** и fresh independent package **132/132** прошли:
Cobalt/DI и DI без Cobalt analyzer,8nupkgs/152commands, expected omitted Enerit
analyzer CS0234. Final summaries Completed=true в
TestResults/EneritAdmission-20261023/final3. [Команды и границы](HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23).
ENE6a и async-stop часть ENE6b проверены в первом bounded профиле; на дату этого
предыдущего среза ENE6c/d, remote cancel, long trends и MMF не были реализованы.
Срез не закрывает весь MIT6a.

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
- В pipes finite admission реализован; запросы в Core scheduler ограничены
  transport gate, общий scheduler не переписан. Logical request-byte budget
  не равен process heap/rental-cache budget. In-process shutdown кооперативный:
  ignoring cancellation handler удерживает Completion/Dispose и dependencies;
  отмена Stop wait не бросает drain. Long stress/resource trends не подтверждены.
- Closed-loop latency не open-loop SLA; allocations включают service/harness/control
  и небольшие batch-boundary расходы, не native/retained memory. Startup/connect,
  App/DI/restart/failure-path performance, exact wire bandwidth и engine frames не измерены.
- Generated CS8669 nullable-context warnings и shared warnings остаются quality backlog;
  добавление benchmark не является исправлением generator template или общим выпуском.

## Следующий конкретный шаг

Первый ENE6a/MIT6a срез, ARCH1b.1 и ED1a завершены в указанном scope. Далее
прикладные задачи [engine STATUS](../engine/STATUS.md#следующий-конкретный-шаг) с явными preview limits и lifecycle ownership.
После ARCH2 ближайший diagnostics consumer — Editor console/export поверх готового отдельного
child-log канала; затем domain/frame aggregates. Queue distribution остаётся отдельной задачей.
ENE6c negotiation развивать перед расширением contracts.
Полный fault matrix MIT6a и trends MIT6c не выводятся из этого среза;
remote cancel/replay не обещать существующим API.
Enerit performance backlog: scalar c1/c8 allocation/CPU profile до выбора изменений,
затем тот же frozen workload для сравнения. Baseline не выявил нового correctness
blocker; он не требует автоматической реализации MMF/новых IPC APIs. Перед общим
release package/platform matrix и ограничения подтверждаются отдельно.
