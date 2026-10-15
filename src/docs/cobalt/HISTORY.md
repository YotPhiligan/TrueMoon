# Cobalt / DI: история проверок

[План](PLAN.md), [текущий статус](STATUS.md), [общий статус](../STATUS.md).

## Core headless quality integration — 2026-10-15

Собственный Core проход подтвердил Cobalt source28 +package headless28/direct4
processes, Debug/Release build/publish; normal/cancel/start/stop/build/config и
startup+Dispose failure. Unique cleanup/continue-on-error проверены на реальном
эксклюзивном FileStream; borrowed/transient counters и original errors сохранены.
Общий run52source +80package +3README, expected opt-out CS0246, Core107/107 в каждой
Debug/Release, full Debug build0errors/1183warnings. [Единый протокол и артефакты](../core/HISTORY.md#сквозной-headless-proof--2026-10-15).
Cobalt/generator/adapter production и собственные tests не менялись/не запускались;
COB7 graph v1/benchmarks/migration decision прежние, performance не измерялся.
Новые manifests относятся к Core proof, не заменяют исторические COB7 fingerprints.

## COB7: provider comparison — 2026-10-15

**Результат: comparison закрыт, массовую migration explicit Microsoft DI consumers
отложить.** Cobalt остаётся existing default TrueMoon; adapter и текущие consumers
сохранены. [Полная comparison matrix/решение](COMPARISON.md),
[критерии](PLAN.md#cob7-сравнение-providers-на-app-graph-v1--2026-10-15).
Следующее общее направление — Core quality. Allocation/CPU investigation Cobalt
отдельным backlog перед пересмотром migration, без performance patches этим этапом.

### Изменения и проверенный функциональный scope

Benchmark project получил reference на существующий DI adapter, ProviderSelection/
ComparisonScenario,20paired methods/10categories с Microsoft baseline и executable
ComparisonVerification. Program поддерживает `--verify-provider cobalt|microsoft`
и cold sample provider; RunBaseline — `-CompareProviders`, сохраняя отдельный
filter10cases для прежнего COB5 mode. Cold samples чередуют порядок providers.
Добавлен RunComparisonDelivery для независимых package consumers. Runtime,
Contracts, generator, adapter C# и Microsoft DI contracts/bridges не менялись.

Scenario.Register12registrations/10keys сохранён byte-for-byte из COB5, SHA256
`B8E4EAEF7F1C1EB57A311941D303240DBE171CE9548AA04E28577BE48934098E`.
Comparison App использует App.Builder/Setup/Build симметрично; старый COB5 App.Build
helper не считается before/after run. Оба providers используют один module и одну
owned factory; дополнительной обвязки/ручных factories для одного из них нет.

| Functional scenario / executable method | Конкретные assertions на обоих providers |
| --- | --- |
| `VerifyNormalGraph` | Concrete open/closed types, shared dependencies, singleton/transient/alias identity, plugin order/lifetimes, resolver isolation, owned1/borrowed0; Worker Dispose1 Cobalt/3MS |
| `VerifyNormalAppAsync` | Repeat Start/Stop no-op, counters1/1, completed cleanup и provider disposal counts |
| `VerifyModules` | Null/enabled branch, repeated Try даёт1value, повторная ordinary open registration даёт2, reversed named Type mapping, actual closed types |
| `VerifyMissingDependency` | Optional missing null; Cobalt exact MissingService/path, MS InvalidOperationException/message; valid graph recovery |
| `VerifyFactoryRecovery` | Original exception reference, failed singleton retry2calls, successful cache, completed dependency retained then disposed1, borrowed0 |
| `VerifyConstructorRecovery` | Original constructor exception object/throw site и singleton recovery/cache |
| `VerifyCycleRecovery` | Cobalt exact CircularDependency/pathA→B→A, MS InvalidOperationException с обоими types, subsequent valid construction |
| `VerifyStartupRollbackAsync` | Original startup failure, Worker Start/Stop1/1, failing Gate Stop0, resources released; Worker Dispose1 Cobalt/2MS (один alias был resolved) |
| `VerifyRunAsync` | Normal lifetime cancellation завершает настоящий App.RunAsync, counters/ownership/cleanup завершены |
| `VerifyExternalCancellationAsync` (external branch `VerifyRunAsync`) | Original external token в OperationCanceledException, затем Stop/disposal и borrowed0 |

10functional scenarios/provider прошли из source, затем повторены без framework
ProjectReferences из тех же sources в fresh NuGet consumer. Это executable probes,
не новая xUnit suite. Build warnings CS9113 в первичных dependency fixtures устранены
сохранением dependencies в properties до измерений; production defect не найден.

### Измерения и границы

Использован `dotnet-diag:microbenchmarking`: existing BDN0.15.8, paired methods,
categories/baseline, returned results, fully materialized enumerations, no manual
loops/IterationSetup. GlobalSetup создаёт и прогревает оба resolvers вне timing,
GlobalCleanup освобождает их. Build/lifecycle methods имеют одинаковые helpers и
включают named cleanup, включая разные фактические Dispose counts providers.

**2026-10-15 10:30:03–10:38:05 UTC**, Windows11 10.0.26300.9711 x64,
Ryzen9 9900X12physical/24logical, SDK10.0.402/.NET10.0.13/RyuJIT x86-64-v4,
Release/no debugger/Concurrent Workstation GC.20Dry перед20default calibrated cases;
retained samples13–50. Power plan Ultimate Performance/e9a42b02-d5df-448d-aa00-
03f14749eb61 совпал до/после; UserPowerPlan mutator не меняет global settings.
Selected DOTNET/COMPlus TieredCompilation/TieredPGO/gcServer env overrides отсутствуют.

Все means/99.9% CI half-width/allocated bytes и ratio — в [таблице](COMPARISON.md#производительность).
Основные результаты Cobalt/MS: singleton52.101/14.908ns (32/0B), transient
433.133/34.171ns (440/184B), graph build3299.889/1191.229ns (11562/9848B),
App lifecycle16906.445/19320.142ns (72200/75125B). Cobalt быстрее empty build и
полного warmed App lifecycle; Microsoft быстрее других measured timings.
Не вычитать independent means для isolated ctor cost и не переносить microbenchmark
ratio на общую скорость UI/engine. Причины costs/background/provider work не
профилировались; hardware contention/desktop workloads не контролировались.

Cold:12fresh processes/provider +1discarded executable/file-cache prime/provider,
alternating provider order. Cobalt median29.17505ms, min28.7301/max-P95nearest32.122ms,
process-wide111832–118024B. MS median26.71115ms, min26.3468/max-P95nearest27.6969ms,
106864–113400B. Excludes process/CLR launch, module initializers, input construction,
Stop/dispose; cleanup проверен после sample.24unique PIDs/Completed=true. С12samples
P95=max — exploratory, не SLA; process-wide bytes не равны MemoryDiagnoser bytes/op.

```powershell
pwsh -NoProfile -File Benchmarks/TrueMoon.Cobalt.Benchmarks/RunBaseline.ps1 -CompareProviders -RunDirectory E:/source/my/TrueMoon/src/TestResults/CobaltComparison/cob7-20261015
```

Exit0/Completed=true:20Dry/20final/24cold,36commands exit0,5full JSON reports,
127source files. Artifacts `TestResults/CobaltComparison/cob7-20261015` содержат
raw JSON/CSV/Markdown/logs и source/binary/report SHA256. Design Dry20/20 ранее
прошёл отдельно (`TestResults/CobaltComparison/design`); timing из него не используется.
Measured benchmark DLL SHA `29DE62F7A1F097587316F636304AA7112254A7D9886DA8F2ECAB5352237E3CFC`,
Cobalt DLL `9F289F9025404F12AD50B152D306D1F32D4E23A0ECE26B20F31F453F43F448C4`,
adapter `8A2450C7BADE502AA30E0D3F6CF5F6A634EC535586C87CA801BB0436E7E98B40`.
Measured Cobalt/TrueMoon binaries совпали с COB5; сравнение не содержит optimization.

### Независимая поставка

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/cob7-20261015 -PackageVersion 0.2.2.7-alpha.cob7.20261015
pwsh -NoProfile -File Benchmarks/TrueMoon.Cobalt.Benchmarks/RunComparisonDelivery.ps1 -PackageRunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/cob7-20261015
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/cob7-20261015
```

Все exit0/Completed=true. Main64/64 +3exact README, opt-out expected CS0246;
127producer/consumer source fingerprints. Local/unpublished package version
`0.2.2.7-alpha.cob7.20261015`. Its analyzer SHA
`3DB9C243AE06BE251FC90B9EB73971363797DD0ACC87A62B3D1FA7BD0A136CCB` отличается от
ordinary project build metadata version; сверяется с packaged/cached analyzer,
а не с DLL, перезаписанной последующим обычным benchmark build.

`comparison-20261015-102859872`: exact Scenario/ProviderSelection/WorkloadVerification/
ComparisonVerification copies,2PackageReferences TrueMoon/adapter, fresh package
cache и0framework ProjectReferences. Debug/Release build/publish:8generated-mode
processes (both providers) +4Microsoft opt-out processes =**12processes/120functional
scenarios**.4compiler analyzer checks: bundled matching DLL ровно1 и собственный
generated initializer в enabled mode,0в opt-out; no analyzer/Roslyn/BDN runtime DLLs.
22commands exit0,8source copies/SHA verified. Script SHA
`0CACB4A20892793719BCF49DE54EA28644B4841F4596F43AEDD775CFFD5B7FA7`.

`delivery-20261015-102942551`:4library/app processes +2expected COB001–COB006
controls,12commands, Completed=true. Legacy standalone analyzer reference в этом
run не повторялся; его previous COB6d evidence остаётся историческим.

### Current regression и решение о consumers

После measurement, последовательно (не конкурентно с ним), `run-tests`:

```powershell
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Tests.trx' --results-directory TestResults/CobaltComparison/validation -v quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Generator.Tests.trx' --results-directory TestResults/CobaltComparison/validation -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Tests.trx' --results-directory TestResults/CobaltComparison/validation -v quiet
dotnet build TrueMoon.slnx --configuration Release --no-incremental -v quiet
```

Cobalt179/179, generator91/91, Core81/81 Release:3TRX/**351passed/0failed/0skipped**,
exit0. Full Release solution build exit0/errors0/**warnings1185**. Existing
xUnit2.9.3/VSTest, overlay absent, no test dependency/framework changes. Logs/TRX в
`TestResults/CobaltComparison/validation`. Debug unit suites/IPC/units/UI/native/
other OS/RID/self-contained/trimming/AOT/contended throughput не проверены.

Source inventory `TestResults/CobaltComparison/consumer-inventory.json`:22UseDI call
sites —13UI calls,3manual dual-provider selectors,1legacy MithrilTest,4unit-test
selectors,1benchmark selector. Private/external consumers не исследовались.
Functional parity этого graph не доказывает parity nullable/constructor selection/
всех generic/registration shapes. UI flows не мигрированы по headless proof.
Final audit `TestResults/CobaltComparison/VerifyFinal.ps1`, `final-audit.log`:
127measurement +127package source fingerprints/mismatches0,7current measured binary
SHA/5raw report SHA совпали; original graph SHA неизменён.20measurements/24cold,
64+3main/12comparison/4library processes/2diagnostic controls и351/0/0tests сверены.
9documents/177local links/anchors, оба PowerShell AST, new source whitespace и
git working/staged diff checks passed. Отдельный `--list flat` подтвердил прежние
10COB5cases/exit0 (`original-list.log`), без повторного performance run. Source
benchmark/COMPARISON остаются рабочими файлами; intermediate artifacts ignored.

COB7 закрыт решением отложить массовую migration: сохранить default Cobalt,
развивать его confirmed hot-path backlog отдельно, дальше Core quality по общему
порядку. Общая готовность engine/release и native blockers не меняются этим этапом.

## COB5: App baseline — 2026-10-15

Добавлен постоянный `Benchmarks/TrueMoon.Cobalt.Benchmarks` (не packable), reference
из solution `/Benchmarks/`. `dotnet add ... package BenchmarkDotNet` без версии
разрешил **0.15.8**, закреплённую CLI в Directory.Packages.props; это tool dependency
только benchmark project. Runtime/Contracts/generator/adapter не менялись, optimized
веток и миграции нет. [Протокол](../../Benchmarks/TrueMoon.Cobalt.Benchmarks/README.md),
[критерии](PLAN.md#cob5-измеренный-baseline-app-graph-v1--2026-10-15).

### Workload и границы

Graph v1:12caller registrations/10keys — borrowed settings, owned factory,
open Repository<T>/exact closed StringRepository, Composite Worker/IStartable/
IStoppable, transient Ticket/Request,3ordered plugins с2singleton/1transient.
App добавляет стандартные bootstrap/configuration/events/lifetime registrations.
Это отдельный фиксированный graph по мотивам headless package consumer, а не запуск
его error/process matrix. Внешнего I/O/delay/logging нет; transients non-disposable,
поэтому миллионы steady-state calls не растят owned-disposable root collection.

`WorkloadVerification.VerifyAsync` исполняет actual construction: shape12/10,
shared factory dependency, concrete types open/closed repository, closed-argument
singleton isolation, Composite alias identity, ordered plugins и их lifetimes,
transient request/ticket identity, независимость двух resolver, reference-unique
owned cleanup/borrowed0. Реальный App Start/Stop повторён дважды для no-op semantics,
Worker counts1/1, disposal worker/resource1, borrowed0. Это executable workload
guard, не новая unit suite и не замена существующим regressions.

10BDN methods возвращают результаты, fully materialize collections и не используют
ручные amplification loops. Warm resolution имеет один resolver/GlobalSetup с
активацией Request, cleanup/GlobalCleanup. Build/app cases включают named cleanup
на каждом invocation; input delegates/settings созданы вне measurement. Нет
IterationSetup с шумным одиночным hot invocation. Первая версия harness исправлена
до measurements: BenchmarkSwitcher.Run возвращает IEnumerable, материализован
ToArray для проверки всех reports и nonzero exit при ошибке. Это harness fix,
не найденный runtime defect.

BenchmarkDotNet0.15.8 default adaptive Throughput/калибровка/warmup/iterations и
MemoryDiagnoser; сначала Dry (только validation). Representative Singleton измерен
первым, затем остальные4resolution/3resolver lifecycle/2App lifecycle cases.
UserPowerPlan задан mutator job (без добавления второго job к CLI Dry); фактическая
Windows схема **Ultimate Performance/e9a42b02-d5df-448d-aa00-03f14749eb61** совпала
до/после. Никаких GC/JIT/affinity overrides. [Версионный enum](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Environments/PowerPlan.cs)
сверен с installed XML/API; глобальная схема BenchmarkDotNet не переключалась.

### Фактические результаты

**2026-10-15 08:06:18–08:10:39 UTC**, Windows11 10.0.26300.9711 x64,
AMD Ryzen9 9900X4.40GHz,12physical/24logical cores, SDK10.0.402,
.NET10.0.13/RyuJIT x86-64-v4, Concurrent Workstation GC, Release/no debugger.
Selected DOTNET/COMPlus TieredCompilation/TieredPGO/gcServer environment variables
отсутствуют; Directory.Build.props закреплён source fingerprints, raw reports
сохраняют GC/job/runtime evidence. Graph/protocol одинаковы для всех cases.

| Benchmark class.method | Mean | Error (99.9% CI half-width) | Allocated/op | Retained measurements |
| --- | --- | --- | --- | --- |
| Resolution.Singleton |52.453ns |1.008ns |32B |39 |
| Resolution.TransientRequest |425.023ns |8.070ns |440B |19 |
| Resolution.OpenGenericSingleton |138.885ns |0.860ns |96B |12 |
| Resolution.ClosedGenericSingleton |41.637ns |0.412ns |32B |13 |
| Resolution.MaterializeMultiplePlugins |181.069ns |1.824ns |248B |13 |
| ResolverLifecycle.BuildAndDisposeEmpty |133.457ns |2.079ns |1368B |14 |
| ResolverLifecycle.BuildAndDisposeGraph |3184.461ns |62.814ns |11482B |31 |
| ResolverLifecycle.BuildResolveAndDisposeGraph |4468.458ns |86.051ns |14211B |23 |
| AppLifecycle.BuildAndDispose |14217.589ns |277.775ns |68472B |16 |
| AppLifecycle.BuildStartStopDispose |16333.020ns |205.015ns |72669B |14 |

Bytes/op округлены в таблице, raw full JSON сохраняет исходную точность. Build и
first Request boundaries cumulative и включают dispose; независимые means нельзя
вычитать для isolated constructor cost. Open/closed measurements отражают разные
пути одного согласованного API, это не сравнение providers. Наличие32B на warm
singleton и96B на open path наблюдается; причины/оптимизации здесь не исследовались.

Отдельно **12fresh processes +1discarded OS/file-cache prime**: первый App.Build+
Start внутри Main, median**29.7169ms**, min**29.2696ms**, max/nearest-rank P95
**30.8571ms**. Все12Completed=true/разныеPID; process-wide allocations
**110704–116896B**. Boundary исключает process/CLR launch, module initializers,
Scenario/input construction, Stop/dispose. Cleanup проверен после каждого sample.
GC.GetTotalAllocatedBytes(precise:true) process-wide может включать runtime/
background allocations; его bytes не приравниваются BDN steady-state figures.
При12samples P95 равен максимуму и не является SLA/стабильной tail estimate.

### Команды, artifacts и validation

```powershell
pwsh -NoProfile -File Benchmarks/TrueMoon.Cobalt.Benchmarks/RunBaseline.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/CobaltBaseline/cob5-20261015
```

Exit0, summary.Completed=true: **10Dry/10final/12cold**,21commands exit0,
7full JSON reports (3Dry/4measurement groups),117source files. Run имеет raw
Markdown/CSV/full JSON/logs, inputs/binary/report SHA256, git HEAD, SDK/runtime/
CPU/job/environment/power plan. Сохранять весь `TestResults/CobaltBaseline/cob5-20261015`,
а не только aggregate means. Перед ним design Dry10/10 также прошёл отдельно,
`TestResults/CobaltBaseline/design-dry`; его timing не используется в baseline.

Ключевые SHA256 измеренных binaries:

- Benchmark assembly: `056B6449F4D7D2BCE6B94A79451B88808D601FBC3F9CAD1BA2685F170A669B1F`.
- Cobalt: `9F289F9025404F12AD50B152D306D1F32D4E23A0ECE26B20F31F453F43F448C4`.
- TrueMoon: `9A0C46FACF8F382DEC2710A6DA718ECD22EC42CCEBF07E98C2C1F2C8C2E2F746`.
- BenchmarkDotNet: `D460626B2E1A9FA2F786E09E6C85CDF4D7B73DECA636450A0E23892C8CE6192B`.

После полного завершения measurements (не конкурентно с ними), `run-tests`:

```powershell
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Tests.trx' --results-directory TestResults/CobaltBaseline/validation -v quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Generator.Tests.trx' --results-directory TestResults/CobaltBaseline/validation -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Tests.trx' --results-directory TestResults/CobaltBaseline/validation -v quiet
dotnet build TrueMoon.slnx --configuration Release --no-incremental -v quiet
```

Current Release: Cobalt179/179, generator91/91, Core81/81 — **3TRX/351passed/
0failed/0skipped**, exit0. Full Release solution build exit0/errors0/**warnings1185**.
Artifacts в `TestResults/CobaltBaseline/validation`. Existing xUnit2.9.3/VSTest,
overlay отсутствует; test packages/production code не менялись. Debug suites,
package consumers, IPC/units/UI/native/other OS/RID/self-contained/trimming/AOT и
contended throughput этим шагом не проверены. Предыдущие COB6d702/64+3+5/2controls
не переносятся в current run. General release readiness не меняется автоматически.

Final audit `TestResults/CobaltBaseline/VerifyFinal.ps1`, `final-audit.log`:
117source fingerprints/mismatches0,5current binary SHA/7raw report SHA совпали;
10final/10Dry/12cold/21commands и3TRX/351passed/0failed/0skipped подтверждены.
7documents/135local links/anchors, PowerShell AST, benchmark non-packable flag и
git working/staged whitespace checks прошли. Intermediate artifacts в ignored
TestResults, source benchmark project — постоянная часть solution.

COB5 закрыт как первый historical baseline в указанном scope. Desktop background
workloads не контролировались; абсолютные цифры относятся к этой машине/версии/
graph. **Далее COB7:** тот же graph на Cobalt/Microsoft DI плюс failure/cancellation/
ownership/workflow comparison; решение о consumer migration отдельно, без ecosystem
bridge и без предварительных оптимизаций на основании одной цифры.

## COB6d: registration workflow — 2026-10-15

Изменён только production generator CobaltGenerator: syntax candidates включают
member access, conditional member binding и simple names; semantic invocation
проверяется по фактическому contract member/implementation либо собственному Try
extension. Type operands читаются через IArgumentOperation.Parameter/Value, включая
named order и conversions. Unknown generic wrapper construction сообщает COB001;
factory overload исключается до этой проверки. Runtime/Contracts/Microsoft adapter
не менялись. [Принятый scope](PLAN.md#cob6d-registration-workflow-и-завершение-cob6--2026-10-15).

### Failure proofs и границы

Первый test-only запуск:22failed/65passed/0skipped; среди failures обнаружены две
ошибки самих fixtures: C# не импортирует extension methods как обычные functions
через using static, а builder не имеет overload Build(handles). Fixtures исправлены
до production changes: static calls через type alias; inherited context строит
low-level containers из своего accessor/публичного registry. Исправленный baseline:
18failed/69passed/0skipped, exit1; `TestResults/CobaltWorkflow/before-corrected.trx`.
Он подтверждает conditional calls, unrelated-method false positives и пропуск
unqualified inherited method. Positional-only разбор named Type operands найден
в исходнике; финальные named-order tests используют обычный call, чтобы проверять
binding отдельно от conditional discovery. Исходные logs сохранены отдельно;
первый malformed fixture не выдаётся за product failure proof.

После первичного fix87/87 passed. Дополнительные cases обнаружили silent skip
generic module Singleton<T>; добавлена COB001 с factory guidance. Финальная suite
91/91. Type variables/reflection/wrapper specialization не анализируются, dynamic
construction использует factory/instance или заранее generated/handwritten resolver.
Conditional branch не подавляет compile-time validation; runtime registration
выполняется только при исполнении call. Generated declarations сами graph не меняют.

### Requirement / evidence: 11 методов, 29 новых cases

| Поведение | Exact test |
| --- | --- |
| Singleton/Transient/Try conditional chain, null/non-null branch, no implicit self alias, enumeration и lifetime | `ConditionalRegistrations_ActivateOnlyExecutedBranches_WithDeclaredLifetime` (4) |
| Static class alias, named receiver, one/two type Try и duplicate suppression | `AliasQualifiedStaticTryRegistrations_ExecuteAndSuppressDuplicates` (4) |
| Positional/mixed/reordered named Type operands и parentheses/Type casts, оба lifetime | `TypeRegistrations_BindSemanticParameters_RegardlessOfArgumentOrder` (8) |
| Посторонние Singleton/Transient/Composite/Try не дают code/diagnostics даже для abstract types | `UnrelatedSameNamedMethods_DoNotPublishResolversOrDiagnostics` (5) |
| Concrete/derived context, unqualified/base calls, actual constructed types и identity | `ConcreteAndInheritedContextCalls_ImplementContract_AndGenerateResolvers` |
| Module branch + Composite alias identity + repeated Try | `ConditionalCompositeModule_PreservesAliasIdentity_AndRepeatedModuleTrySemantics` |
| Conditional invalid construction: exact COB001 severity/site и отсутствие sources | `ConditionalInvalidConstruction_ReportsDiagnosticAtInvocation_EvenWhenBranchIsDisabled` |
| Factories/instances/runtime Type variables не выводят construction | `FactoriesInstancesAndRuntimeTypeVariables_DoNotInferConstruction` |
| Conditional reordered open mapping, две closures, declared singleton/transient | `ConditionalOpenTypeRegistration_UsesNamedMappingAndClosedLifetime` (2) |
| Unknown T: exact COB001/site/factory guidance; explicit generic factory компилируется | `GenericRegistrationModule_RequiresConcreteTypesOrExplicitFactory` |
| C# using static unqualified extension: exact compiler CS0103, без ложной generation | `UsingStatic_DoesNotImportExtensionsAsUnqualifiedCalls` |

Focused class workflow (`code-testing-agent`, `run-tests`): существующий xUnit2.9.3/
VSTest/net10.0, SDK10.0.402, overlay отсутствует; новый test project не нужен.
Inline assertion/gap review: positive sources исполняются из emitted assemblies,
проверяются actual types, identity, enumeration cardinality и пустой runtime graph.
Negatives проверяют exact diagnostic ID/site либо отсутствие code/errors; без
генератора/semantic binding/branch filtering соответствующие assertions нарушаются.
Это статическое mutation reasoning, реальные mutants/coverage не запускались.

### Текущие команды и результаты

Windows x64/net10.0/SDK10.0.402, Debug/Release отдельно:
Cobalt179/179, generator91/91, Core81/81;6TRX/**702passed/0failed/0skipped**, exit0.
Artifacts: `TestResults/CobaltWorkflow/final-tests`, соседние suite logs.

```powershell
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=generator-debug.trx' --results-directory TestResults/CobaltWorkflow/final-tests -v quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=runtime-debug.trx' --results-directory TestResults/CobaltWorkflow/final-tests -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CobaltWorkflow/final-tests -v quiet
```

Те же три команды с configuration Release и `*-release.trx` прошли.
`dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental -v quiet`
exit0/errors0/**warnings1185**, log `final-solution-debug.log`; shared warnings остаются.

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/cobalt-workflow-20261015 -PackageVersion 0.2.2.7-alpha.cobworkflow.20261015
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/cobalt-workflow-20261015 -LegacyGeneratorPackage E:/source/my/TrueMoon/src/TestResults/UnitsPackages/run-20261014-094823275/feed/TrueMoon.Cobalt.Generator.0.2.2.7-alpha.units.20261014094823288.nupkg
```

Обе команды exit0/Completed=true. Main:64/64matrix +3/3exact packaged README
processes, opt-out expected CS0246, fresh isolated feed/cache. Source manifest127files.
Final delivery `delivery-20261015-062324628`: library с одной Cobalt PackageReference,
app только с собственной Library ProjectReference;4Debug/Release build/publish
processes +1legacy-reference Release,2expected COB001–006 controls,5compiler analyzer
checks/15commands. Library исполняет constructor/error/registration regressions и
новый WorkflowChecks: null/enabled conditional module, repeated Try, named/parenthesized
open Type mapping с duplicates/transient closures и Composite aliases. Generated
module initializer доставляется из library; analyzer/Roslyn в runtime output отсутствуют.

Generator SHA256 `67A7FABF78FD17243D167345DB6F886684B582372568080701AA91B301BF5FB7`.
Delivery script SHA256 `CAE52D1F52A8B70741349774BAB49D66EB28E7E821D85928CE9185A681F9427F`.
Final audit `TestResults/CobaltWorkflow/VerifyFinal.ps1`, `final-audit.log`:8documents,
141local links/anchors,127source fingerprints/mismatches0,11new test methods в
evidence matrix,6TRX/702passed/0failed/0skipped; package/delivery counts и analyzer/
script SHA совпадают, PowerShell AST и git working/staged diff checks passed.
Packages local/unpublished. COB6a–d закрыты в согласованном собственном scope;
COB5 measurements и COB7 provider comparison/migration остаются следующими.
Other module suites/IPC/units/UI/native/other OS/RID/self-contained/trimming/AOT и
performance этим запуском не проверены; full solution build не заменяет их tests.

## COB6c: registration contracts — 2026-10-15

Собственный TrueMoon scope, два projects/один package сохранены. Изменены runtime
ServiceRegistration/ServicesRegistrationContainer, context queries, TypeContainer
ordinal metadata, builder grouping и merged generic enumeration в resolver base;
IServicesRegistrationAccessor получил XML contract. Contracts, Microsoft DI adapter
и generator C# logic не менялись. [Принятые semantics](PLAN.md#cob6c-registration-contracts--2026-10-15).

### Failure proof и реализация

До production changes16новых cases failed/84existing passed/0skipped, total100,
exit1: `TestResults/CobaltRegistration/before/before.trx`, `before.log`.
Воспроизведены unchecked invalid Type mappings, live/mutable registration accessor,
отсутствующий exact implementation для self/instance registrations, duplicate pair
Try после self и потеря open registrations при mixed generic enumeration.

Validation централизована в ServiceRegistration и выполняется до Add. Context/
container ошибки имеют public parameter names service/implementation; helper —
serviceType/implementationType и прежний optional-null self fallback. Factories
не исполняются ради query, их implementation неизвестен. Instance actual type и
self mapping нормализованы. Handle snapshot — cloned metadata/read-only collection
под lock, ссылки на instances/delegates сохраняются. Exact queries под lock не
создают snapshot. Duplicates/first-pair Remove semantics сохранены и проверены.

Builder присваивает ordinal до grouping по service type; TypeContainer хранит его
внутренне, публичный ITypeContainer не расширен. Synthesized IEnumerable merges
closed/open entries по global ordinal; single exact closed key по-прежнему wins.
Handcrafted containers без ordinal имеют stable open-before-closed fallback.
Explicit open IEnumerable теперь имеет приоритет над synthesis; GetService(open
definition) даёт ArgumentException. Constraints при closing не фильтруются молча.

### Requirement/evidence matrix

Новые cases — `TrueMoon.Cobalt.Tests/RegistrationContractTests.cs`:26methods,
95executed cases/configuration. Каждая invalid mapping проверяет singleton/transient;
24partitions ×3API route methods дают72cases, остальные23 — behavioral cases.

| Требование COB6c | Точный тест / evidence |
| --- | --- |
| Invalid Type mappings до mutation, exact exception/parameter | TypeOverloads_RejectInvalidMappingsBeforeMutatingContext, ContainerTypeOverloads_RejectInvalidMappingsWithExactParameter, HandleTypeFactories_ValidateMappingsAndRetainOptionalSelfFallback: null/value/array/delegate/void/byref/pointer/abstract/interface/unrelated/partial/mixed/arity/reordered/nested-generic/static/user-conversion shapes |
| Self normalization, convenience helper, valid reference mappings | SelfContainerAndHandleFactories_NormalizeTypesAndRejectInvalidSelfTypes, ValidMappings_AcceptBaseVarianceOpenInheritanceAndClosedNestedTypes: exact metadata/lifetimes, base/covariance/inherited open/closed nested |
| Null instance/factory/container guards | NullInstanceFactoryAndContainerInputs_AreRejectedWithoutAddingRegistration: exact ArgumentNullException parameter и empty context |
| Read-only detached metadata и сохранение references | RegistrationAccessor_ReturnsReadOnlyDetachedMetadataSnapshots (all fields), Snapshot_PreservesFactoriesAndBorrowedObjectIdentity_WithoutExposingLiveHandles: no live handle mutation, read-only IList, reference identity, old snapshots after RemoveAll |
| Frozen build и synchronized snapshot reads | BuiltGraph_IsolatedFromLaterContextAndSnapshotMutation, ConcurrentSnapshotReads_AreStableAndOrderedDuringRegistration: original graph identity;200two-phase captures during writer registration, exact stable prefixes after all writes |
| Known self/instance exact pairs, no duplicate pair Try | SelfAndInstanceRegistrations_HaveExactImplementationQueriesAndRemoval, TwoTypeTryAfterSelfRegistration_DoesNotCreateDuplicate: Exist/Remove и one singleton value |
| Duplicate/order/lifetime/last single contract | DuplicateRegistrations_PreserveOrderLifetimeAndLastSingleSelection:4entries, exact type sequence, distinct duplicate singleton identities, transient changes, last selected |
| First exact Remove across lifetimes, RemoveAll exact service | RemovePair_RemovesFirstExactMatchAcrossLifetimes_AndRemoveAllIsExact: remaining transient first, other implementation/service retained, missing removal no-op |
| Try single/pair/opaque factory behavior | TryHelpers_UseExactKnownPairs_AndTreatFactoriesAsOpaque, OneTypeTry_PreservesExistingFactory_AndReaddsAfterRemoveAll: known instance pair suppresses, different implementation retained, opaque factory +type coexist, removed service re-added transient |
| Queries describe explicit keys, never execute factory | ExactQueries_IncludeOpenKeysButDoNotPretendClosedRegistrationsExist, OpaqueFactoryQueriesAndPairRemoval_DoNotInvokeOrGuessImplementation: open key true/closed key false, null guards, factory calls0 |
| Values remain supported via instance/explicit low-level factory | ValueInstancesAndExplicitValueFactories_RemainSupported: zero/false resolution and typed enumerable, real singleton factory resolves zero twice/calls1 |
| Merged closed/open order/lifetime and exact single precedence | MixedGenericEnumeration_IncludesOpenAndClosedInGlobalRegistrationOrder, MixedGenericEnumeration_InterleavesKeysAndDuplicates_AndRetainsClosedPrecedence: sparse global order, duplicate closed registrations, exact singleton/transient identities и closed single wins over later open |
| Closed removal leaves open fallback | RemoveClosedGeneric_LeavesOpenRegistrationAvailable: one matching open singleton, distinct generic closure и empty unrelated enumeration |
| Explicit IEnumerable priority and closure failures | ExplicitOpenEnumerableRegistration_OverridesSynthesis_AndClosedOverridesOpen, OpenConstraintFailureDuringMixedEnumeration_IsNotSilentlyFiltered: exact explicit object/type, original ArgumentException path, valid other closure/single recovery |
| Closed resolution argument and low-level fallback order | OpenResolutionType_IsRejectedClearlyInsteadOfEnteringSynthesis, HandcraftedMixedContainers_HaveDeterministicOpenThenClosedFallbackOrder: open/null argument names, healthy optional query, manual open-then-closed sequence/closed single |

Broad research/plan/status находятся в session temp вне worktree. Roslyn static
pairing выполнен один раз на bounded snapshot runtime/generator/test projects:
31source/28test,19paired/12unpaired; container paired с CobaltServiceResolverTests,
TypeContainer также с ResolverRegressionTests/ResolutionErrorTests. ServiceRegistration
не paired по token heuristic; новый file даёт direct helper tests. Это **не line/
branch coverage**. Test-gap и assertion-quality review по xUnit extension выполнены:
26methods/183syntactic Assert calls,0skip markers, exact mapping/identity/state/
ordered sequence/error checks. Factory test усилен с presence до actual zero/calls1;
concurrent snapshot test — двухфазным capture с независимыми expected prefixes.
Pseudo-mutation candidates (guards, live snapshot/missing field copy, guessed factory
type, unnormalized self/instance, last/all removal, open-first merge без ordinal,
lost closed/lifetime priority, silent constraint filtering/synthesis precedence)
имеют конкретные counterexample assertions. Mutations не запускались; coverage %
и performance не измерялись.

### Финальные component checks

Windows x64/net10.0, SDK10.0.402, xUnit2.9.3/VSTest, overlay отсутствует; packages/
runner не менялись. Для Configuration = Debug, Release выполнены:

```powershell
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=cobalt-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltRegistration/final-tests -v quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=generator-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltRegistration/final-tests -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=core-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltRegistration/final-tests -v quiet
dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental -v quiet
```

Все commands exit0: Cobalt179/179 (95new +84existing), generator62/62, Core81/81
в каждой configuration;6TRX/644passed/0failed/0skipped. Full nonincremental Debug
build exit0/errors0/warnings1185; `TestResults/CobaltRegistration/final-solution-debug.log`.
Shared warnings/RS1041 сохраняются; successful build не является full solution test run.

### Независимые packages

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-registration-20261015 -PackageVersion 0.2.2.7-alpha.cobreg.20261015
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-registration-20261015 -LegacyGeneratorPackage E:\source\my\TrueMoon\src\TestResults\UnitsPackages\run-20261014-094823275\feed\TrueMoon.Cobalt.Generator.0.2.2.7-alpha.units.20261014094823288.nupkg
```

- Main exit0/Completed=true:64/64 direct/headless/provider processes +3/3 delivered
  README examples, Debug/Release build/publish; opt-out expected CS0246. Local4nupkg,
  не опубликованы. Generator SHA256:
  `D212F5A480CE4EBDB5CD25C19C4FE5626BE946D13BD0D64C1A9A1894789A67A3`.
- Delivery `delivery-20261015-060338753`: exit0/Completed=true,5runtime processes,
  5analyzer path/SHA checks,2expected constructor negative builds,15commands.
  Script SHA256 `3AB829E3CB428E3119603C9C9C03C58E27BC37A3BE4159876743AD4C68BE579E`.
  Library имеет только Cobalt package, App только own-library ProjectReference.
  В4Debug/Release build/publish processes добавлен RegistrationChecks: detached
  snapshot/mutation, runtime invalid mapping/no mutation, instance query/Remove/Try,
  self suppression, mixed generic order/lifetime/closed precedence и frozen graph.
  Предыдущие constructor/default/error/recovery/cleanup controls сохраняются.
  Legacy package reference выбирает matching bundled analyzer и выполняет direct
  ownership graph в1Release process; COB001–COB006 negative builds отдельны.
  Generator/Roslyn не попадают в runtime output.

Финальный audit:8Markdown documents/138local links и anchors, balanced fences/
conflict/NUL checks, delivery PowerShell AST; все26new test method names есть в
матрице evidence.6TRX644/0/0,127source fingerprints/mismatches0, package/delivery
Completed, script SHA и current Release analyzer SHA совпали. Working/staged
git diff --check прошли. Артефакт `TestResults/CobaltRegistration/final-audit.log`;
audit scratch ignored, research/plan/status вне worktree.

COB6c проверен в описанном scope. Далее COB6d method forms/modules/final generator
consumer workflow, затем COB5 measurements и COB7 provider comparison/conditional
migration. Compound Try/Composite не обещают atomic setup across threads; runtime
validation не создаёт dynamic fallback; constructors и constraints имеют прежние
generator/runtime границы. Enerit/Mithril suites, IPC/units108matrix, UI/native/
performance, other SDK/OS/RID/trimming/AOT этим шагом не повторены.

## COB6b: runtime errors и recovery — 2026-10-14

Продолжение собственного TrueMoon API, два projects/один package. Изменены
CobaltServiceResolverBase, ServiceResolvingException, FactoryResolver и одна строка
reflection invocation в generator. Contracts, provider adapter и Core production
без изменений. Новый ServiceResolutionFailure различает MissingService,
CircularDependency, DepthExceeded и InsufficientExecutionStack; ServiceType/
DependencyPath дают structured context. Generic missing exception subtype сохранён.

Цикл определяется по resolver reference identity + closed service type, а не
только по service type/типу resolver. Per-resolver activation/path state защищён
прежним synchronous lock и снимается в finally. Enumerable elements добавлены
в path. Exception-search filters сохраняют deepest immutable snapshot до unwind
и возвращают false: исходный exception продолжает поиск handler без замены и
повторных catch/rethrow на каждом уровне. Original user exceptions, включая
TargetInvocationException, сохраняют object/type/throw site. Reflection вызывает
метод/constructor с DoNotWrapExceptions по
[официальному контракту .NET](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/7.0/reflection-invoke-exceptions).

### Failure proof и уточнение guard

- До production changes:10новых bounded runtime cases failed,37existing passed,
  total47/exit1/Skipped0. `TestResults/CobaltRuntimeErrors/before/before.trx` и
  `before.log`. Factory recursion ограничена test fallback после8calls, чтобы
  baseline не приводил к unbounded recursion. Нет cycle error/context; open generic
  constructor возвращал infrastructure TargetInvocationException вместо original.
- Expanded depth cases выявили native Stack overflow/testhost abort: лимит128сам
  по себе не гарантирует доступного execution stack. Первоначальные catch/rethrow
  в каждой runtime frame также исчерпывали stack при propagation. Aborted runs
  не считаются passing suite; сохранён `TestResults/CobaltRuntimeErrors/stack-abort.log`.
- Добавлен TryEnsureSufficientExecutionStack и exception-search recording без
  nested catch/rethrow. Numeric boundary127/128/129 проверен на owned thread8MiB;
  явно уменьшенный execution stack на owned thread256KiB даёт observable
  InsufficientExecutionStack до construction и допускает recovery после unwind.
  Время/завершение этих threads ограничены10s. Available stack может ограничить
  resolution раньше128; это часть принятого scope, а не обещание любого thread stack.
- Review выявил stale reused missing path, null singleton caching и self-referential
  max-depth test expectation. Path теперь обновляется для текущей попытки с
  сохранением предыдущего snapshot; null не инициализирует singleton cache.
  Boundary expectations заданы явно127=false/128=false/129=true и maximum pinned128.

### Requirement/evidence matrix

Все новые names ниже находятся в `TrueMoon.Cobalt.Tests/ResolutionErrorTests.cs`.

| Требование COB6b | Точный тест и observable evidence |
| --- | --- |
| Direct/indirect cycles, all entry points, singleton/transient | DirectFactoryCycle_FailsBeforeRecursion_ThroughEveryEntryPoint (6cases), IndirectFactoryCycle_ReportsFullDependencyPath_AndDoesNotPoisonHealthyService:1factory call, exact ordered Type path, Failure/ServiceType, healthy resolution |
| Generated closed/open и Composite/Enumerable cycles | GeneratedConstructorCycle_IsDetectedForClosedAndOpenTypes, CompositeCycle_ReportsAliasesAndEnumerableElements: repeated closed type/alias, full synthesized element path, CircularDependency |
| Finite generic graph legal, expanding graph bounded, stack shortage observable | OpenGenericResolver_CanConstructAnotherClosedType, ResolutionDepth_BoundaryAndExpandingGenericGraph_AreBoundedAndRecoverable (127/128/129), SmallExecutionStack_FailsObservablyBeforeOverflow_AndRemainsRecoverable: exact counts/boundary reason, parent type arguments, recovery |
| No false cycles from separate registrations/resolvers/equality | DifferentRegistrationsOfSameService_CanResolveOneAnother, IndependentResolvers_DoNotShareActivationState, ResolverIdentity_IsReferenceBasedEvenWhenCustomResolversCompareEqual: exact identity/counts, custom value-equal resolver objects |
| Required missing/nullable absence и explicit null value | MissingGeneratedDependency_ReportsParentAndRequiredType, MissingOpenDependency_AndNullFactory_HaveStructuredErrors: exact generic subtype/path/service/failure, GetService/TryResolve null |
| Preserve original exception/stack, including user TargetInvocationException | OpenGenericConstructorFailure_PreservesOriginalExceptionAndStack, UserTargetInvocationException_IsPreserved_ForTypedClosedAndOpenPaths (3routes): Same original/inner, stack marker, correct root type |
| Immutable paths, safe metadata, exception reuse | ExceptionPath_IsImmutable_AndUpdatedWhenOriginalExceptionIsReused, ReusedMissingException_UpdatesTheCurrentPathWithoutMutatingPreviousSnapshot, MetadataFailure_DoesNotReplaceCustomException: previous snapshots unchanged, latest full/short paths, readonly/unavailable Data preserves original |
| Cycle/null/factory/concurrent retry and singleton identity | CyclicFactory_CanRetryAfterFailure_WithoutLosingOwnership, NullSingletonFactoryResult_IsOptionalOrRequired_AndCanRetryToAValue (3entry points), FailedSingletonFactory_RetriesAndKeepsCompletedDependenciesOwned, ConcurrentSingletonFailure_LeavesOneRetryAndNoFalseCycles: exact attempts/value identity,1failure+31successes |
| Completed dependencies owned, failed object not cached, sync/async cleanup | GeneratedConstructorFailure_RetriesAndCleansCompletedDependenciesInReverseOrder (8closed/open ×singleton/transient ×sync/async): original failure, no premature disposal, attempts/identity, exact reverse parent/dependency names, borrowed disposal0 |
| Partial enumeration failure/retry/shutdown | PartialEnumerableFailure_RetainsCompletedValuesForRetryOrShutdown: exact error path, no premature disposal, recovered sequence, shutdown order/counts with/without retry |
| Generic constraint failure preserves requested type and permits another closure | GenericConstraintFailure_IdentifiesClosedService_AndAllowsAnotherTypeArgument: ArgumentException, implementation message, failed closed service path, subsequent valid singleton |

Broad test-generation research/plan/status сохранены вне worktree в session temp.
Roslyn static pairing выполнен один раз на bounded snapshot4Cobalt projects:
31source/27test,18paired/13unpaired. ServiceResolvingException paired с existing
ResolverRegressionTests; generator — с TestHelper. Abstract resolver base не
распознан token heuristic, хотя вызывается через concrete resolver в existing tests.
Это static pairing, **не line/branch coverage**. Assertion-quality и pseudo-mutation
review по xUnit extension выполнены: exact types/ordered paths/identity/stack,
state counts/negative results и cleanup order; helpers traced. Meaningful candidates
(remove guard/finally, Type-only/value equality key, lose original wrapper/stack,
cache failed/null singleton, discard owned dependencies, change cleanup/path mutability)
имеют observable counterexamples. Mutations не запускались, coverage % не измерялся.

### Финальные component checks

Windows x64/net10.0, SDK10.0.402, xUnit2.9.3/VSTest; packages/runner не менялись.
Для Configuration = Debug, Release:

```powershell
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=cobalt-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltRuntimeErrors/final-tests -v quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=generator-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltRuntimeErrors/final-tests -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=core-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltRuntimeErrors/final-tests -v quiet
dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental -v quiet
```

6TRX: Cobalt84/84 (47new cases +37existing), generator62/62, Core81/81 отдельно
в каждой configuration; total454/Failed0/Skipped0, все commands exit0.
Generator/Core checks запущены после финальных production changes; последующий
test-only boundary/extra runtime cases повторили только runtime suites.
Full solution Debug build exit0/errors0/warnings1187,
`TestResults/CobaltRuntimeErrors/final-solution-debug.log`. Existing shared/RS1041
warnings остаются; full solution tests/other module suites не запускались.

### Независимые packages

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-runtime-errors-20261014 -PackageVersion 0.2.2.7-alpha.coberrors.20261014
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-runtime-errors-20261014 -LegacyGeneratorPackage E:\source\my\TrueMoon\src\TestResults\UnitsPackages\run-20261014-094823275\feed\TrueMoon.Cobalt.Generator.0.2.2.7-alpha.units.20261014094823288.nupkg
```

- Main exit0/Completed=true:64/64 direct/headless/provider processes +3/3 actual
  packaged README examples, Debug/Release build/publish; opt-out expected CS0246.
  Analyzer SHA256 `F65D4CA7D5C0841082FBAD726C42F44DA717EC023F9382B23EC64208970FCF80`.
  Source manifest127files/mismatches0; producers4local packages, unpublished.
- Delivery `delivery-20261014-161854749`, exit0/Completed=true:5runtime processes,
  5analyzer path/SHA checks,2expected negative constructor builds,15commands.
  Script SHA256 `E02AB88F05578DCC857AC51D27B6D3F777C1C830D9AFFD6880D69ACB76A93116`.
  Library-only Cobalt reference и App-only own-library ProjectReference сохранились.
  В4Debug/Release build/publish processes, помимо constructor/default controls,
  выполняются cycle path/recovery, required missing type, null singleton retry,
  original closed/open constructor exception/stack и owned/borrowed cleanup.
  Matching bundled analyzer с legacy reference и direct graph проверены в1Release
  process; COB001–COB006 negative controls остаются отдельными2failed builds.
  Generator/Roslyn DLL отсутствуют в runtime output.

Финальный audit:8Markdown documents/130local links и anchors, fences/conflict/NUL
checks, delivery PowerShell AST,24новых test methods присутствуют в evidence matrix;
6TRX454/0/0,127source fingerprints/mismatches0, package/delivery summaries,
script SHA и current Release analyzer SHA совпали. Working/staged git diff --check
прошли. Артефакт `TestResults/CobaltRuntimeErrors/final-audit.log`; scratch scripts
остаются ignored, research/plan/status находятся вне worktree.

COB6b проверен в описанном scope; COB6c registration contracts — следующий шаг.
COB6d, COB5/COB7 и conditional migration открыты. Lock/concurrency architecture,
async activation, resources inside throwing construction и unavailable custom
exception Data имеют явно описанные границы в PLAN/package README. Enerit/Mithril
suites и IPC/units108matrix, UI/native/performance, other SDK/OS/RID/trimming/AOT
не повторены; их исторические passes не являются результатами этого шага.

## COB6a: конструкторы и diagnostics — 2026-10-14

Продолжение выбранного TrueMoon scope, два projects/один package. Закреплён
unique longest public constructor, без graph-based fallback/чужих DI attributes.
Closed generated arguments теперь сохраняют explicit defaults; nullable/default
open path использует Roslyn symbol contract. Public generated resolvers заменены
internal для доступных internal service types. Runtime/Contracts public API и
runtime implementation не менялись; текущий шаг не добавляет scopes/cycle guard.

### Поведение и evidence

| Требование COB6a | Точный тест в ConstructorTests |
| --- | --- |
| Longest public constructor, short/private не выбираются; missing nullable и defaults; registered zero/enum/decimal/string/dependency имеют приоритет | ConstructorSelection_UsesUniqueLongestPublic_WithConsistentDefaultsAndNullability: closed/open × missing/registered, итоговые значения и singleton identity |
| Abstract/interface/struct/file-local/private nested, no public ctor, равная длина независимо от declaration order, ref/in/out/ref-like/allows-ref-struct и required/inherited members | InvalidConstruction_ReportsDiagnosticAtRegistration_WithoutGeneratedCompilerErrors и PrivateNestedImplementation_ReportsInaccessibleTypeAtRegistration: ID/severity/source span/type/factory guidance, no generated source/compiler errors |
| Pointer/function pointer и незаданные type arguments | UnsafePointerParameters_ReportUnsupportedParameter и RegistrationWithUnspecifiedTypeParameter_ReportsUnsupportedType |
| Incompatible/mixed/arity/reordered generic mappings и user-defined conversion | UnsupportedMapping_ReportsDiagnosticAtRegistration: COB005 на call без вторичных compiler errors |
| Invalid Composite не создаёт aliases; valid соседние registrations остаются, ошибки каждого call сохраняются | InvalidComposite_DoesNotPublishAliasesForUnconstructibleConcrete и Diagnostics_PreserveValidRegistrations_AndReportEveryInvalidSite |
| Factory обходит constructor validation | RuntimeFactory_BypassesConstructorDiagnostics: нет diagnostics/generated code и compilation errors |
| Required members поддержаны при SetsRequiredMembers | SetsRequiredMembersConstructor_IsSupportedForClosedAndOpenTypes: initialized property, transient identity |
| Missing required service не выбирает короткий ctor | MissingRequiredParameter_DoesNotFallBackToShorterConstructor: ServiceResolvingException с типом dependency, closed/open |
| Escaped string/char, bool, uint/ulong, float, NaN/infinity/decimal и nullable value | ConstantDefaults_AndNullableValueParameters_ArePreserved: значения после реального Resolve |
| Nullable generic T? согласован для int/string | NullableGenericParameter_UsesSymbolContract_ForValueAndReferenceArguments: closed/open, zero/null |
| Nested types и array dependencies | ClosedNestedGenericImplementation_AndArrayDependency_AreSupported и OpenGenericInsideNonGenericContainer_IsSupported: values, array identity, singleton/closed generic identity |

Исходный запуск новых cases, до production changes: generator total48,
Passed23/Failed25/Skipped0, exit1; `TestResults/CobaltConstructors/before/before.trx`.
Failures воспроизводили отсутствие diagnostics и closed optional/internal code errors.
Первый full build выявил слишком широкое ограничение nested open types — исправлено:
только generic containing types unsupported, ordinary containing types проверены
новым executable case и existing runtime generic regressions. В ходе реализации
исправлена неоднозначная Roslyn SequenceEqual overload; временный RS2007 header
release manifest исправлен. Исторические failed/intermediate artifacts сохранены.

### Финальная регрессия

Windows x64/net10.0, SDK10.0.402, xUnit2.9.3/VSTest, без обновления test packages.
Для каждой Configuration = Debug, Release выполнены следующие commands:

```powershell
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=generator-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltConstructors/final-tests -v quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=cobalt-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltConstructors/final-tests -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration $Configuration --no-restore --logger "trx;LogFileName=core-$($Configuration.ToLowerInvariant()).trx" --results-directory TestResults/CobaltConstructors/final-tests -v quiet
dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental -v quiet
```

Все6 test commands exit0: generator62/62, Cobalt37/37, Core81/81 в каждой
configuration; total360, Failed0/Skipped0, exact counters из6TRX. Debug full
build exit0/errors0/warnings1191, `TestResults/CobaltConstructors/final-solution-debug.log`.
Нет новых RS2007/RS2008; descriptors заведены как Unshipped AdditionalFiles по
[Roslyn release tracking](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md).
RS1041/net10 compiler scope и shared XML/nullability warnings остаются.

### Финальная независимая поставка

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-constructors-final-20261014 -PackageVersion 0.2.2.7-alpha.cobaltctorsfinal.20261014
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-constructors-final-20261014 -LegacyGeneratorPackage E:\source\my\TrueMoon\src\TestResults\UnitsPackages\run-20261014-094823275\feed\TrueMoon.Cobalt.Generator.0.2.2.7-alpha.units.20261014094823288.nupkg
```

- Main exit0/Completed=true:64/64 direct/headless/provider cases +3/3 delivered
  README examples, Debug/Release build/publish. Opt-out build exit1/expected CS0246.
  Новый package compiler analyzer SHA256:
  `AC6FE797A36BD93C90262FD84AC6E67B6509BF1751516F78046084D72C3A84F7`.
  Source manifest127files/mismatches0 при финальной сверке; пакеты local/unpublished.
- Delivery `delivery-20261014-152646779`: exit0/Completed=true,5runtime processes,
  5analyzer path/SHA checks,2negative constructor controls,15commands. Script SHA256:
  `C0D04B72BB8FF5C33143073C32782FB0F9D31D27E2E7F80B5E62FAC3CE557A52`.
  Собственная Library имеет только Cobalt PackageReference; App только ProjectReference
  на неё. Internal closed/open implementations, longest constructor, nullable/default
  и registered zero/dependency precedence, singleton/per-closed-type identity проверены
  в4Debug/Release build/publish processes. Legacy package control выбирает matching
  bundled DLL и выполняет direct ownership graph в1Release process.
- Два negative builds Debug/Release из actual bundled analyzer дают все COB001–COB006
  на Invalid.cs registration sites; неожиданных CS/MSBuild errors нет. Failed builds
  являются expected controls, отдельно от successful builds/runs. Generator/Roslyn
  отсутствуют в runtime output; scripts ограничивают время и владеют своими processes.
- Предыдущий intermediate package `cobalt-constructors-20261014` имел64+3 и delivery5+2;
  он сохранён отдельно и не является evidence финальной изменённой DLL.

Финальная audit проверка:10Markdown documents,123local links/anchors, balanced
fences/conflict/NUL checks, generator project XML и delivery PowerShell AST;
working/staged git diff --check прошли.6TRX counters360/0/0,127source fingerprints
без mismatch, final package/delivery summaries и script/analyzer SHA сверены.
Артефакт `TestResults/CobaltConstructors/final-audit.log`; scratch audit остаётся
в ignored TestResults, не входит в tracked changes.

COB6a завершён в описанном scope. COB6b — cycles/dependency path, missing/factory/
constructor failures и recovery/cleanup — следующий шаг; constructor invocation
exception wrapping не изменялся. COB6c/d, COB5 measurements, COB7 comparison
и conditional migration остаются. Enerit/Mithril suites, IPC/units108 matrix,
UI/native/performance, other SDK/OS/RID/trimming/AOT не повторены; общий выпуск
не считается готовым по этой локальной работе.

## Единый пакет и TrueMoon scope — 2026-10-14

Пользователь принял два проекта/один пакет и Cobalt для TrueMoon и собственных
приложений. Новая совместимость с IServiceCollection/Microsoft DI/Hosting соглашениями
не нужна. Принятое решение записано в PLAN; existing Microsoft DI adapter не удалён.
Runtime public API/implementation и generator algorithms в этом шаге не менялись.
Предыдущие Try helpers/research этого дня остаются отдельными работами.

### Реализация поставки

- Cobalt pack использует TargetsForTfmSpecificContentInPackage и GetTargetPath
  отдельного generator project; nupkg содержит runtime lib и analyzer DLL.
  Query не зависит от hardcoded bin path и не копирует Roslyn dependencies.
- Generator IsPackable=false. Analyzer ProjectReference остаётся в source graph,
  PrivateAssets=all исключает его из runtime dependency graph.
- buildTransitive target после ResolveReferences выбирает ровно один bundled
  analyzer с normalized package path; EnableCobaltGenerator=false удаляет Cobalt
  analyzers из compilation. Старый explicit generator не имеет приоритета над
  bundled matching version; migration instructions предлагают удалить ссылку.
- Headless/units independent fixtures и runners обновлены: Cobalt analyzer path
  находится в truemoon.cobalt cache, separate Cobalt generator dependency запрещена,
  только Enerit analyzer остаётся explicit. Producer graph9projects даёт8units
  nupkgs; headless graph даёт4nupkgs. README snippets используют одну runtime reference.
- Package README и корневой README обновлены; source ProjectReference workflow
  и net10/compiler boundaries сохранены. Package publishing не выполнялся.

### Фактические проверки

Windows x64/net10.0/SDK10.0.402. Fresh feeds/caches, framework-dependent build/publish
без RID. Main runners проверяют source/version provenance, analyzer paths/SHA,
generated graph и отсутствие analyzer/Roslyn DLL в runtime payload.

| Run | Результат и артефакты |
| --- | --- |
| CorePackageConsumer |64/64matrix processes +3/3точных packaged README examples, Completed=true; EnableCobaltGenerator=false negative build exit1/CS0246. TestResults/CorePackages/cobalt-unified-20261014/summary.json |
| UnitsPackageConsumer |108/108owned case processes Cobalt/DI и DI с отключённым Cobalt analyzer, Completed=true;128commands,8nupkgs. Omitted Enerit analyzer expected exit1/CS0234. TestResults/UnitsPackages/cobalt-unified-20261014/summary.json |
| Library→App | Собственная Library с одной Cobalt PackageReference; App имеет только ProjectReference на эту Library. TrySingleton registration в compiled Library, payload42/singleton identity, matching analyzer в обеих compilations и runtime payload проверены Debug/Release build/publish:4/4процесса |
| Legacy migration | Новый Cobalt + настоящий старый standalone Generator package0.2.2.7-alpha.units.20261014094823288; compiler получает одну bundled matching DLL с текущим SHA; executable direct graph/disposal проходит1/1Release процесс |
| Source regressions | Generator16/16,Cobalt37/37,Core81/81 отдельно Debug/Release:6TRX/268passed/0failed/0skipped, commands exit0. TestResults/CobaltUnified/tests |
| Solution | Full nonincremental Debug build exit0/errors0/warnings1227. TestResults/CobaltUnified/solution-debug.log |

Library/legacy final artifact:
TestResults/CorePackages/cobalt-unified-20261014/delivery-20261014-120849002/summary.json,
Completed=true,5case processes/5analyzer checks/12commands. Initial auxiliary failures
были в verifier fixture: fallback на completed local package cache нарушал ожидание
isolated path; Legacy AssemblyName не совпадал с exact copied generator marker.
Оба исправлены, final run имеет fresh каталог; ранние failed summaries сохранены.

### Команды воспроизведения

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-unified-20261014 -PackageVersion 0.2.2.7-alpha.cobaltunified.20261014
pwsh -NoProfile -File ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:\source\my\TrueMoon\src\TestResults\UnitsPackages\cobalt-unified-20261014 -PackageVersion 0.2.2.7-alpha.cobaltunits.20261014
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunDeliveryChecks.ps1 -PackageRunDirectory E:\source\my\TrueMoon\src\TestResults\CorePackages\cobalt-unified-20261014 -LegacyGeneratorPackage E:\source\my\TrueMoon\src\TestResults\UnitsPackages\run-20261014-094823275\feed\TrueMoon.Cobalt.Generator.0.2.2.7-alpha.units.20261014094823288.nupkg
```

Main runners требуют **новый** RunDirectory/unique prerelease version; literal
paths/version выше идентифицируют actual run, повторить их поверх evidence нельзя.
Legacy package argument необязателен без сохранённого старого nupkg.

Source validation commands:

```powershell
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Generator.Tests-Debug.trx' --results-directory TestResults/CobaltUnified/tests -v quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Generator.Tests-Release.trx' --results-directory TestResults/CobaltUnified/tests -v quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Tests-Debug.trx' --results-directory TestResults/CobaltUnified/tests -v quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Tests-Release.trx' --results-directory TestResults/CobaltUnified/tests -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=TrueMoon.Tests-Debug.trx' --results-directory TestResults/CobaltUnified/tests -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Tests-Release.trx' --results-directory TestResults/CobaltUnified/tests -v quiet
dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental -v quiet
```

Новые unit tests не добавлялись: это packaging change с existing regressions и
реальными independent consumers. Scoped/keyed/bridge APIs не добавлены.

Source SHA/nupkg/compiler SHA сохранены в run summaries. После runs изменились
только инструкция дополнительных checks в CorePackageConsumer/README.md и
bundled-delivery prose в корневом README: по1document fingerprint mismatch каждого
main run, не production source mismatch. Дополнительный script записывает собственный
актуальный SHA. Исторические hashes не перезаписаны и не объявляются current/full
fingerprint match. UI/native/real device/DPI/stress/performance и Enerit/Mithril unit
suites не повторены. Следующий функциональный шаг — COB6a diagnostics/constructors.

## Оценка объединения и замены Microsoft DI — 2026-10-14

Запрос пользователя: оценить совмещение runtime/generator в одном проекте и
требования полноценной замены Microsoft DI. Это source/documentation research,
не реализация новой архитектуры и не решение о миграции. Предыдущие code changes
и268passed относятся к предыдущему Try helper run этого дня, не к этой оценке.

Сверены Cobalt runtime/generator csproj, builders/resolution/cache/disposal,
registration API и Microsoft DI adapter. Generator сейчас IIncrementalGenerator,
target net10.0, Roslyn5.9.0; в restored project.assets compile asset имеет путь
lib/net10.0/Microsoft.CodeAnalysis.CSharp.dll. Runtime не имеет Roslyn runtime
PackageReference; generator не имеет ProjectReference на Cobalt/Contracts и
создаёт consumer code по Roslyn symbols/именам. Cobalt.csproj использует generator
как Analyzer, package generator отдельно размещает DLL в analyzers/dotnet/cs.

### Варианты объединения — предложения

| Вариант | Возможность и последствия |
| --- | --- |
| Один csproj/одна DLL | Технически допустим mixed runtime/generator assembly, но появляются compiler/runtime dependencies и loader compatibility concerns; assembly не может использовать свой generator при собственной компиляции. Это ограничение self-generation, не запрет объединения как такового |
| Один csproj/две DLL | Conditional source sets и отдельные compilations/targets возможны; output paths/assembly names/pack/analyzer/reference selection и tests требуют отдельной build схемы. Разделение compilation сохраняется, выигрыш в количестве csproj не равен упрощению |
| Два csproj/один NuGet | Рекомендуемый вариант оценки: runtime DLL в lib/net10.0, generator DLL в analyzers/dotnet/cs; потребитель получает один PackageReference. Сохраняются независимые dependencies/TFM и tests |

Для третьего варианта изменить pack graph, analyzer opt-out/propagation policy,
обработку old explicit Generator reference (исключить double loading), package
README и negative control. Проверить fresh source/package consumer, actual
compiler analyzer path/bytes, runtime deps без Roslyn/generator, library→app и
прямую ссылку на Cobalt; transitive analyzer behavior не считать автоматической
гарантией. Standard SDK не получает PackAsAnalyzer инфраструктуру dotnet/runtime
просто от копирования этого metadata: нужен собственный корректный pack setup.
Текущий net10 generator scope сохраняется; совместимость других compiler hosts
и иной analyzer TFM требует проверки зависимостей и отдельного решения.

### Два уровня замены

1. Надёжный собственный DI для TrueMoon: COB6 качество, понятный registration API,
   lifecycle/disposal, packages и COB5/COB7 measurements/comparison.
2. Альтернативный контейнер экосистемы .NET: собственный API плюс import
   IServiceCollection/ServiceDescriptor, все требуемые lifetimes и framework
   interfaces, Generic Host integration и third-party registrations.

Текущий TrueMoon.Extensions.DependencyInjection направлен TrueMoon→Microsoft
provider и вызывает BuildServiceProvider. Для второго уровня нужен обратный
bridge Microsoft registrations→Cobalt, не переименование existing adapter.
Рекомендуется использовать Microsoft.Extensions.DependencyInjection.Abstractions
для совместимых contracts, не делегируя resolution другому root provider.

| Область | Текущая source сверка | Что требуется для второго уровня |
| --- | --- | --- |
| Registration | Own API: type/factory/instance, singleton/transient, Composite/Try | ServiceDescriptor import включая registration identity/order/duplicates, factories/instances, open generics и keys; helpers TryAdd/TryAddEnumerable/Replace должны сохранять descriptor semantics |
| Scopes | Scoped есть только enum value; scope API/cache/ownership отсутствуют | Root/scoped caches, IServiceScopeFactory/IServiceScope, async scope cleanup; singleton строится в root context, scoped/transient ownership принадлежит правильному scope |
| Activation | Type registration без exact generated factory бросает; runtime factory поддержана | Cached runtime activation для неизвестных генератору/third-party types; generated fast path и fallback имеют общий constructor/dependency contract |
| Constructors | Выбор max parameter count; nullable closed dependencies optional; defaults только open reflection path | Выбор applicable constructor, ambiguity, default values, ясные errors; Cobalt optional-nullable semantics отдельно от Microsoft compatibility expectations |
| Resolution | Один last registration; enumerable из exact либо open container | Ordered exact+open generic enumeration, constraints и descriptor-specific identity; required/optional errors, service presence queries и keyed resolution |
| Validation | Нет cycle guard/ValidateScopes/ValidateOnBuild | Dependency path, missing dependencies, lifetime mismatch checks; opaque user factories не объявлять полностью статически проверенными |
| Lifetime/disposal | Resolver-local ownership, unique reverse cleanup/continue-on-error | Scope-local ownership, concurrent resolution vs shutdown, root/scope identity; отдельно документировать intentional отличия от Microsoft alias/error disposal |
| Ecosystem | TrueMoon IServiceResolver extends IServiceProvider | IServiceProviderFactory builder/UseServiceProviderFactory, IServiceProviderIsService и keyed counterparts; реальные Logging/Options/HttpClient/HostedService consumers |
| Generator | Syntax scan текущего compilation, global metadata registration | Модульные registrations, cross-assembly/private types и dynamic descriptors; generation улучшает activation, не подменяет runtime registration/selection |
| Performance/deployment | Один resolver-wide lock, reflection open generics; baseline нет | Fixed graph/concurrent/scoped benchmark, cold/warm/allocations; trimming/AOT отдельный режим с явными limits, не следствие наличия generator |

### Предлагаемая архитектура и порядок

Нормализовать registrations в immutable descriptors: service/key, registration
identity, implementation/factory/instance/alias, lifetime/ownership. Generated
factories и runtime activators создают значения; root/scope runtime отвечает
за caching, dependency path и disposal. Сейчас generated Source/GenericSource
содержат singleton instances/cache и locks; scopes и единые guarantees требуют
пересмотра этой границы. Общая semantic specification и parity scenarios полезнее
слияния Roslyn symbols и System.Type в одну runtime DLL. Можно разделять pure
модель/правила исходниками без новой обязательной публичной assembly.

Предложенный порядок: COB6 constructor/errors → нормализованный activation
contract и fallback → scopes/validation → ecosystem bridge/keyed services →
one-package delivery → conformance/real consumers/COB5 measurements и COB7 decision.
Package часть может идти независимо после выбора delivery contract. Decorators,
property injection/assembly scanning и async factories не обязательны для замены
built-in DI; API conveniences вводятся по потребности. Никакой новой поддержки
scopes/keys/AOT или faster-than-Microsoft claim эта оценка не создаёт.

### Источники и выполненные проверки

Проверены2026-10-14 primary sources:

- [Roslyn incremental generator cookbook: analyzer packaging](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md).
- [Roslyn source generator assembly/self-generation boundary](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md).
- [dotnet/runtime companion analyzer packaging](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/libraries-packaging.md).
- [Microsoft constructor selection/scope validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview).
- [Service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes).
- [IServiceProviderFactory source](https://source.dot.net/Microsoft.Extensions.DependencyInjection.Abstractions/IServiceProviderFactory.cs.html).
- [Generic Host factory integration source](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs).
- [Resolution/descriptor/presence semantics source](https://source.dot.net/Microsoft.Extensions.DependencyInjection/ServiceLookup/CallSiteFactory.cs.html).
- [Keyed service interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ikeyedserviceprovider).

Upstream main/current docs описывают capabilities; точная compatibility matrix
при реализации должна быть закреплена за выбранной Microsoft DI version (сейчас
repo10.0.12). Чтение source/project.assets и git diff выполнено; новые code/build/
unit/package/native/performance проверки в этой исследовательской работе не
запускались. Изменены только research record и локальные PLAN/STATUS.

## Cobalt quality и Try helpers — 2026-10-14

Пользователь выбрал качество текущих модулей до engine/editor, начиная с Cobalt,
и условный переход consumers с Microsoft DI после сравнения удобства.
Прочитаны общие/модульные планы, Cobalt runtime/generator/registration/ownership,
общий registration API, Microsoft DI adapter и existing generator harness.
Worktree был clean. AppBuilderConfigurationContext уже использует Cobalt builder
по умолчанию; фактическая будущая миграция относится к explicit DI consumers.
COB6/COB7 добавлены, порядок COB6 → COB5 → COB7 закреплён в PLAN.

### Первый дефект и ограниченная правка

Syntax predicate CobaltGenerator распознавал только Singleton/Transient/Composite.
TrySingleton/TryTransient из Contracts не создавали construction resolver для
конкретных consumer types. Добавлены четыре существующих generic overloads,
extension и static syntax; semantic check ограничивает Try methods общим
ServicesRegistrationContextExtensions. Runtime helpers/API/DI adapter не менялись.
Два type arguments проверяют exact pair, один — наличие service; это сохранённое
различие, не новый TryAdd contract. PACKAGE.md дополнен этим описанием.

Тесты компилируют generated assembly и реально строят/используют resolver:

| Проверка | Точное имя / результат |
| --- | --- |
| Два lifetime × две arity × extension/static, repeated Try, concrete type identity, singleton/transient identity, enumerable count и отсутствие self-alias | TryRegistrations_GenerateExecutableResolvers_WithExpectedLifetimeAndNoDuplicates:8cases |
| Existing runtime factory остаётся выбранной | TryRegistration_PreservesExistingRuntimeFactory |
| Чужие методы с совпадающими Try names не создают sources | UnrelatedTryRegistrationMethods_DoNotGenerateResolvers |

До изменения filtered8cases failed (generated trees0 вместо2); после полная
generator suite16/16 Debug и16/16 Release. Assertions повторно просмотрены:
удаление generation, смена lifetime, duplicate registration или extra concrete
alias нарушают отдельные oracles. Existing factory control проверяет actual
reference identity; negative control проверяет отсутствие unintended sources.

### Команды и фактическая область проверки

Windows x64/net10.0/SDK10.0.402, xUnit2.9.3/VSTest, existing restored dependencies.
Артефакты ignored TestResults/CobaltQuality/20261014:before log/TRX,
generator-debug.log/TRX и project-configuration log/TRX.

```powershell
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~TryRegistrations_GenerateExecutableResolvers --logger 'trx;LogFileName=try-before.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=generator-debug.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
```

Затем выполнены unfiltered команды:

```powershell
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Generator.Tests-Release.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Tests-Debug.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Cobalt.Tests-Release.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=TrueMoon.Tests-Debug.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=TrueMoon.Tests-Release.trx' --results-directory TestResults/CobaltQuality/20261014 -v quiet
dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental -v quiet
```

Все final commands exit0;
generator16, Cobalt37, Core81 в каждой configuration:6TRX/268passed/0failed/0skipped.
Warnings сохраняются; clean test summary не означает zero-warning build.
Full nonincremental Debug solution build exit0/errors0/warnings1227;
полный solution test run не выполнялся. Лог solution-debug.log сохранён отдельно.
Independent packages/own-process IPC/Mithril/UI/native/performance не повторены.
Предыдущая package108 матрица и её analyzer SHA принадлежат прежнему generator.

### Остальные findings и следующий шаг

Source review, без нового failure reproduction: runtime не содержит cycle guard;
generated closed constructor выбирается по max parameter count без ambiguity/
shape diagnostics; default parameters реализованы в reflection open-generic path,
но closed path всегда требует Resolve для nonnullable parameter; GetHandles
возвращает backing List; conditional access calls не входят в predicate.
Эти темы записаны в COB6a/b/c/d; их исправление/проверка ещё не выполнены.
Следующий конкретный шаг — COB6a diagnostics и constructor selection contract.
Scopes/new lifetimes/полная совместимость Microsoft DI не добавлены; COB6 целиком
не закрыт, оптимизации/сравнение/миграция впереди.

## Исправление registration, generator и ownership — 2026-10-10

Запрос «давай починим все проблемы» продолжает [review Core](../core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Исправлены exact service/implementation/lifetime matching, generated source names, runtime-owned factories, Composite2–8 aliases, closed/open generic dependency resolution и typed enumeration. TypeContainer полностью инициализирует все factories, singleton resolution синхронизирован и изолирован между resolvers.

Cobalt владеет generated/factory/transient values, сохраняет borrowed instance registrations, учитывает unique references и reverse creation order. Sync/async disposal сохраняет ошибки и продолжает cleanup. Shared completion + AsyncLocal reentrancy guard позволяют concurrent calls ждать одно завершение, а owned service не блокироваться при recursive resolver disposal.

Microsoft DI wrapper зарегистрирован один раз как borrowed instance до provider construction и затем attached к provider. Factories получают тот же IServiceResolver; wrapper владеет provider и поддерживает matching sync/async mode, failures/concurrent/reentrant cleanup. Стандартный provider учёт disposable Composite aliases сохранён: shared identity и три Dispose для concrete+двух aliases закреплены тестом; требования unique cleanup/continue after failure не перенесены на Microsoft DI.

### Проверки

Agent direct runs exit0: `dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --no-restore --verbosity quiet` —37/37; generator project аналогичной командой —6/6. Root повторил после заморозки всех правок полный non-incremental solution build и стандартный solution test/discovery. Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest: **runtime37passed/0failed/0skipped, generator6passed/0failed/0skipped**. Core81passed отдельно подтверждает AppCreate и lifecycle обоих providers.

[Точные итоговые команды, counters и артефакты](../core/HISTORY.md#исправление-проблем-ядра--2026-10-10). TRX runtime `TestResults/CoreFixes-Final-20261010/MEGA6_PH-DEV_2026-10-10_16_58_57_net10.0.trx`; generator — timestamp16:58:58 `[1].trx`. Generator tests проверяют diagnostics/compiler/emit и выполняют emitted alias/open-generic graph; references Core/TrueMoon удалены из generator test project как не нужные этому consumer.

| Requirement | Evidence |
| --- | --- |
| Exact registration/lifetime/isolation | `RuntimeGraph_SelectsImplementationAndLifetime_WithoutAssemblyPollution`, `GeneratedSingletons_AreIndependentAcrossResolvers` |
| Concurrent singleton | `SingletonFactory_ConcurrentResolution_CreatesOneOwnedInstance`, `FactoryResolver_DirectConcurrentSingletonCalls_RunFactoryOnce` |
| Composite2–8 identity | `Composite_AllAliasesShareOneOwnedInstance`; executable emitted graphs в GeneratorTests |
| Unique ownership/order/errors | `OwnedFactories_DisposeUniqueInstancesInReverseOrder_AndPreserveBorrowedInstances`, `CleanupFailure_StillDisposesRemainingServices_AndIsNotRetried`, `TransientFactories_OwnEachCreatedDisposable` |
| Modes/concurrent/reentrant disposal | `SyncDisposal_HandlesAsyncOnlyFactoryValues`, `DisposablesContainer_PrefersMatchingMode_ForDualDisposableValues`, `DisposablesContainer_ConcurrentAsyncCleanup_SharesCompletion`, `OwnedServiceDisposal_CanReenterResolverDisposal_WithoutDeadlocking` |
| Generics/enumerable/missing | `OpenGenerics_ResolveClosedDependenciesAndTypedEnumerables_WithConcurrentSingletonIdentity`, `MissingServices_AreOptionalOrRequired_AndOnlyTypedEnumerableIsSynthesized` |
| Containers/queries/legacy typed path | `TypeContainer_InitializesAllFactoriesOnce_WhenSingleAndMultipleResolutionMix`, `RegistrationQueries_ReflectRemovalAndExactImplementation`, `TypeOverload_ForClosedServicesUsesGeneratedConstruction`, `TypedOnlyResolvers_WorkThroughProviderAndEnumerable_AndPreserveOriginalErrors` |
| Adapter wrapper/provider ownership | `Builder_ReturnsSameResolverToFactories_AndDisposesOwnedProviderOnce`, `Wrapper_PrefersMatchingDisposalMode_AndDisposesProviderOnce`, `ConcurrentAsyncDisposal_WaitsForSameProviderCleanup`, `SyncDisposal_CleansAsyncOnlyProvider`, `ProviderCleanupFailure_PreservesErrorAndDoesNotRetry`, `OwnedServiceDisposal_CanReenterWrapperDisposal_WithoutDeadlocking` |
| Provider difference | `CompositeAliases_ResolveSameConcreteSingleton` — same objects и explicit3 Dispose |

Assertion-quality/static test-gap review:32 named regression/generator methods,150 static Assert call sites включая shared compiler helper;160 effective assertions с helper expansion, average5/method. Assertion-free/trivial/self-field0.10/12 категорий; approximation/comparison не нужны этому contract. Removing metadata/lifetime filter, owned tracking/reverse order/uniqueness, optional resolution or recursive bypass изменит identity/count/error/completion assertions. Это static likely-killed reasoning, реальные mutants не запускались.

Package/analyzer delivery, Release/scopes и performance не проверялись. RS1041/net10 generator и прочие shared warnings остаются; ManualTests/CobaltTest собран, отдельный запуск не выполнялся.

## COB4 / CORE5 package consumers — 2026-10-11

Добавлен explicit analyzer/runtime PackageReference consumer и самостоятельные
Cobalt/generator/adapter PACKAGE.md с конкретной metadata. Runtime/generator C#,
net10.0 и provider semantics не менялись. Подробный совместный scenario, команды,
source/package hashes, промежуточный verifier failure и ограничения — в
[основной истории Core](../core/HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11);
[воспроизведение](../../ManualTests/CorePackageConsumer/README.md).

На Windows x64/net10.0/SDK10.0.401 проверены actual packaged analyzer path/SHA256,
headless8/direct6 generated sources/configuration, module initializer и working
Composite/open/closed generic graph. Runtime output без analyzer/Roslyn. Local version
0.2.2.7-alpha.headless.20261011142810069: 64matrix +3delivered README processes exit0,
включая Microsoft DI без consumer generator; отрицательная сборка без обязательного
analyzer package expected exit1/CS0246. Main runner exit0/Completed=true.

Runtime suite37/37, generator6/6, Core81/81 — fresh Debug component runs,
Failed0/Skipped0; full solution не повторялся. Cobalt shared Worker disposed1;
Microsoft DI calls3/resource release1, borrowed0 — ранее принятые различия сохранены.
Pack warnings/other compiler hosts/RID/AOT/performance сохраняют ограничения Core
record. COB4 проверен в этой матрице; COB5 baseline остаётся следующим локальным шагом.

## Regression при MIT4/ENE4 — 2026-10-14

Cobalt runtime37/37 и generator6/6 прошли отдельно Debug/Release,
Failed0/Skipped0, Windows x64/net10.0/SDK10.0.402.
[Команды/TRX и общий scope](../mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14).
Production Cobalt/DI adapter без изменений. Explicit Enerit factories решают
visibility sibling generated classes; local consumer implementation по-прежнему
имеет явную Composite registration. Source composition36cases проверена на
Cobalt/Microsoft DI; независимые packages/headless64+3/COB5 baseline не повторены.

## Package regression MIT5/ENE5 — 2026-10-14

[Общий package журнал/команды](../mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14).
Windows x64/net10.0/SDK10.0.402: independent App/IPC/units consumer108/108,
Cobalt/Microsoft DI включая DI без Cobalt analyzer. Actual packaged dependencies/
compiler path/SHA и no-runtime-analyzer delivery подтверждены. Related source
regressions Core81/81, Cobalt37/37/generator6/6 в Debug/Release, Failed0/Skipped0.
Core получил только package Description; Cobalt/DI adapter C# runtime без изменений.
Headless64+3/COB5 performance baseline не повторены. Full solution Debug nonincremental
exit0/errors0/warnings1227; другие UI/native checks не запускались.
