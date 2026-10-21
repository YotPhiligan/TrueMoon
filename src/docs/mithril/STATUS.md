# Mithril: состояние и точка продолжения

Последняя работа — **2026-10-21, graceful wait при ошибке доставки exit**.
В StopAsync исправлена гонка stdin/control pipe: ошибка доставки больше не
пропускает оставшийся graceful budget; timeout/cancellation/owned kill/reap
сохраняются. Новая delayed EOF regression подтверждена red/green; native20cases
Debug/published Release прошли. Mithril95/95 и full solution1907/1907 отдельно
Debug/Release, failed0/skipped0. Fresh units package matrix108/108cases прошла:
8nupkgs/128commands/206source fingerprints, Cobalt/DI, Debug/Release build/publish,
expected missing Enerit analyzerCS0234. Own standalone restart matrix не повторена.
[Причина, точные команды и ограничения](HISTORY.md#graceful-wait-после-ошибки-доставки-exit--2026-10-21).

Исправление структуры — **2026-10-21**: RootNamespace тестов исправлен на
TrueMoon.Mithril.Tests; TestPoco.cs размещён в корне MithrilTest, namespaces
manual/integration consumers согласованы. Module-specific UnitsConfigurationExtensions
находится в Mithril вместо Contracts. Mithril94/94 Debug/Release и свежая package
матрица108cases прошли; native children из затронутых editor scenarios освобождены.
[Миграция и точные команды](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

App entry-point audit — **2026-10-19**: AddUnit связывает App-local unit definitions,
configuration selection и UnitsController/parent-monitor lifecycle. Focused Debug
UnitCompositionTests:9passed/failed0/skipped0 на Cobalt/DI. Runtime code не менялся;
own full/Release/process/restart/package/performance matrix не повторялись.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Package metadata check — **2026-10-19**: тематические tags и общие project/
repository metadata; Release pack/nuspec/README/license/icon payloads проверены.
Runtime code, own unit/process/restart/performance checks в этом metadata шаге
не менялись/не повторялись. [Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Integration check2026-10-18 в UI INF3: первый full Debug run дал readiness-file
sharing IOException в Restart_OwnedSequence (always-crash); targeted20/20 и
повторный full Debug Mithril94/94 прошли. Код Mithril/probe не менялся, race не
объявлена исправленной. [Failure и точные runs](HISTORY.md#readiness-file-race-при-ui-inf3--2026-10-18).

Последняя работа — **2026-10-15, MIT5 operational diagnostics и свежая package regression**.
Добавлены UnitProcessDiagnostic/UnitProcessStage через существующий IEventsSource:
run/generation/PID, effective policy/budget/delays, launch/exit/restart/terminal,
kill/release/stop и исходные failures. Ownership/restart limits не расширены.
[Подключение и semantics](DIAGNOSTICS.md), [контракт](PLAN.md#mit5-operational-diagnostics--2026-10-15),
[команды/артефакты](HISTORY.md#mit5-operational-diagnostics--2026-10-15).

## Реализация и фактические проверки

Windows x64/build26300/net10.0/SDK10.0.402, результаты **2026-10-15**:

| Область | Scope | Результат |
| --- | --- | --- |
| Mithril regression | Existing lifecycle/controller/composition +4 focused diagnostic cases +throwing-sink case; exact trace assertions в existing scenarios | Debug94/94 и Release94/94, Failed0/Skipped0 |
| Related Enerit regression | Typed/generated/App/IPC process suite после изменения shared fixture | Debug126/126 и Release126/126, Failed0/Skipped0; generator suite отдельно не повторена |
| Own child traces |20restart +9shutdown scenarios, Debug apphost/published Release |40/40 +18/18, оба Completed=true;129restart/7lifecycle source fingerprints совпали |
| Package composition |8fresh nupkgs из9projects, isolated feed/cache/consumers; Cobalt/DI/DI без Cobalt analyzer, Debug/Release build/publish |108/108 Completed=true;96cases с живым parent проверяют public diagnostics через App.UseDiagnostics;12parent-exit cases не наблюдают killed parent sink |
| Delivery controls | Compiler analyzer paths/SHA, generated sources, metadata, no analyzer DLL в runtime; omitted Enerit analyzer | Expected CS0234;205source fingerprints совпали; version0.2.2.7-alpha.units.20261015125732156 |
| Solution | Nonincremental Debug build |0errors/1193warnings; warnings не подавлены, count как у предыдущего ENE5 run |

Итого4current TRX:440passed/0failed/0skipped. Core/Cobalt/generator/UI/native suites,
performance и прочие package consumers не повторялись. Package/source traces —
разные реальные runs, не перенос результатов между модулями.

## Ограничения

MIT5 выполнен в bounded Windows x64/net10.0 same-build/cooperative own-child scope.
Main использует общий App graph и не получает process trace. External остаётся
launched/owned без borrowed attach; failed external launch проверен, независимый
external restart не подтверждён. Default Always bounded3/delay250 и terminal-only
OnExit/stop fence сохраняются. Started подтверждает OS start, не readiness.

Diagnostic observer синхронный; sink errors изолированы, blocking/reentrancy не
обещаются. Concurrent Stop/monitor notifications могут перемежаться; Terminal —
решение, не callback acknowledgement. Repeated Stop может дать NoProcess; reentrant
terminal Dispose даёт TerminalCallback и пропускает self-wait. Error сохраняет
исходный Exception и может содержать OS details. StopFailed реализован, реальные
OS permission/reap failures не инъецировались. Это не durable audit/recovery API.

Other OS/compiler/RID/self-contained/single-file/trimming/NativeAOT, arbitrary
hooks/transports, uncooperative services и long resource trend не подтверждены.
Parent abrupt-exit proof относится к child/server cleanup, не workers/sink убитого parent.

## Следующий конкретный шаг

Следующий общий шаг ведётся в [engine STATUS](../engine/STATUS.md#следующий-конкретный-шаг).
Graceful delivery fix выполнен как необходимая регрессия ENG4e; новые process
capabilities и расширение restart/ownership scope этим не начинаются.
UI physical DPI/real loss/VRAM и внешний K7 сохраняются в [локальном статусе](../alloy/STATUS.md);
bounded engine consumers имеют собственные проверки в engine STATUS.
Cobalt allocations и Enerit scalar CPU/allocation profile остаются отдельным
измерительным backlog, не новыми process capabilities.

Предыдущие MIT0–MIT4 и package runs2026-10-14 сохранены в [HISTORY](HISTORY.md).
