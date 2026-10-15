# Cobalt / DI: план модуля

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Новые этапы запланированы, конкретные API/semantics определяются перед работой.

## Границы

TrueMoon.Cobalt, Cobalt.Generator, Cobalt.Tests, Cobalt.Generator.Tests, TrueMoon.Extensions.DependencyInjection и ManualTests/CobaltTest. Ответственность: регистрация и разрешение сервисов, lifetime/disposal, generated resolvers и provider adapter к общим service contracts.

App startup/stop/rollback принадлежит [Core](../core/PLAN.md). Cobalt отвечает за resolver ownership; совместные сценарии относятся к G2/G5. UI registration/Hosting остаётся в [UI плане](../alloy/PLAN.md).

## Цель

Пользователь TrueMoon и приложений на нём регистрирует сервисы через собственный
API и получает предсказуемые resolution/lifetime/error semantics. Generated и
runtime пути согласованы; runtime и generator поставляются одним пакетом Cobalt.

## Архитектура и решения

Сохранять IServiceResolver/IServicesRegistrationContext/IServiceResolverBuilder в Contracts. Cobalt runtime использует Contracts и свой generator; Microsoft DI adapter использует Contracts. Общие service contracts не получают зависимости на конкретный контейнер.

Зафиксировать реально поддержанные lifetimes, factory/instance ownership, разрешение нескольких регистраций и generics. Возможности разных providers не объявлять идентичными заранее; различия описать и проверить. Новые scopes или автоматическую container migration этот roadmap не вводит.

### Принятые контракты исправления ревью — 2026-10-10

Основание: исправление Core integration и выявленных resolver/generator дефектов. [Проверки и ограничения](HISTORY.md).

- Generated factory выбирается по service type, implementation type и lifetime runtime-регистрации. Переданные factories остаются runtime-owned; лишние generated declarations не добавляют сервисов в runtime graph.
- Singleton instance принадлежит конкретному resolver, создаётся один раз при конкурентном resolution. Composite aliases2–8 разрешаются в один concrete instance; closed/open generics и typed IEnumerable используют тот же graph. Optional missing-service resolution возвращает null/default, required — наблюдаемую ошибку.
- Cobalt владеет созданными generated/factory values, включая transients, но не переданными instance registrations. Disposal учитывает reference identity, идёт в обратном порядке, продолжает cleanup после errors и сохраняет их. Sync/async mode использует соответствующий интерфейс, sync поддерживает async-only values.
- Concurrent Dispose ожидает одно завершение; рекурсивное disposal в той же async call chain не ожидает самого себя. Microsoft DI wrapper — один borrowed instance внутри provider, владеющий provider снаружи.
- Microsoft DI сохраняет стандартный учёт disposable aliases: Composite aliases имеют shared identity, но provider может вызвать Dispose одного concrete instance несколько раз. Такой сервис должен поддерживать повторный Dispose. Provider cleanup после ошибки также следует стандартному Microsoft DI поведению; гарантия reference-unique/continue-on-error относится к Cobalt.

Scopes, delivery analyzer через package и произвольная поддержка всех declarations не добавлены этим исправлением. COB4/COB5 остаются самостоятельными этапами.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| COB0 | Runtime/generator/adapter baseline | Отдельные discovery/results существующих test projects и CobaltTest; выбранный provider явно указан |
| COB1 | Registration/resolution contract | Concrete/interface/factory/instance, multiple registrations, generics и missing-service errors проверены в пределах поддержанного API |
| COB2 | Lifetime и disposal ownership | Повторное resolution и resolver shutdown соответствуют lifetime; owned/borrowed и sync/async disposal зафиксированы, failure cleanup проверен |
| COB3 | Согласованные runtime/generated/provider paths | Поведение сравнивается на одних service graphs; generator diagnostics и unsupported cases имеют ясный результат |
| COB4 | Analyzer/runtime package consumer | Pack и отдельная сборка потребителя подтверждают доставку analyzer/generated sources и работоспособный resolution |
| COB5 | Контрольный app и измерения | Core integration работает; startup/resolution/allocations baseline на фиксированном graph, оптимизации проверяются повтором |
| COB6 | Качество и удобство текущего API | Runtime/generator дают наблюдаемые ошибки, registration/lifetime contracts описаны и проверены; поддержанный consumer не требует обхода defects |
| COB7 | Сравнение providers и решение о переходе | Один реальный App scenario на Cobalt/Microsoft DI; удобство, ограничения, диагностика, lifetime, пакеты и измерения сопоставлены, миграция определяется отдельным результатом |

COB1/COB2 согласовать с CORE1/CORE3. COB3 не означает смену DI provider по ходу app lifecycle. COB4 проверяет именно package consumption, а не только ProjectReference.

**COB4, уточнение и проверка 2026-10-11:** основной совместный сценарий и критерии
живут в [Core CORE5/COB4](../core/PLAN.md#core5--cob4-независимый-headless-consumer--2026-10-11).
`TrueMoon.Cobalt.Generator` подключается consumer явно через PackageReference
с PrivateAssets=all; автоматического transitive analyzer API не вводится.
Проверены actual analyzer/generated sources, direct Cobalt и App на обоих providers,
Microsoft DI без consumer generator и отрицательная сборка без обязательного analyzer.
Ближайший локальный шаг уточнён2026-10-14: COB6 качество текущего API, затем COB5
измерения фиксированного graph и COB7 сравнение providers.

### Качество Cobalt до engine/editor — решение2026-10-14

Основание: пользователь выбрал доведение существующих модулей до определённого
уровня качества перед движком, начиная с Cobalt; переход с Microsoft DI зависит
от фактического удобства Cobalt. [Общий порядок](../PLAN.md#качество-модулей-до-engineeditor--2026-10-14).
Это изменение приоритета, а не объявление полной готовности или миграция consumers.

Первый рабочий уровень: предсказуемая регистрация и resolution для текущих Apps,
ясные runtime/generator errors, проверенные ownership/lifetime/cleanup и поставка,
понятная документация поддержанных способов регистрации. Общие Contracts и
Microsoft DI adapter сохраняют границы; новые scopes/decorators/keyed services
вводятся только по выявленной потребности, а не ради полной копии другого DI.

| Подэтап | Результат и критерий |
| --- | --- |
| COB6a. Generator diagnostics | Явные diagnostics на registration site для abstract/inaccessible/unsupported shapes; согласованное правило выбора конструктора, ambiguity, nullable/default parameters и open/closed generic behavior. Ошибка не сводится к неясному compiler error в generated file |
| COB6b. Runtime errors | Cycles завершаются исключением с dependency path; missing service и factory/constructor failures сохраняют типы/исходную причину; failed resolution не нарушает последующий resolution/cleanup |
| COB6c. Registration contracts | Валидация Type overloads, snapshot accessor, duplicate/order/remove/Try semantics и open/closed generic enumeration имеют поведенческие checks; новые гарантии согласуются явно |
| COB6d. Developer workflow | Обычные и static Try helpers, условные вызовы и registration modules исследованы; поддержанные формы документированы и tested. Analyzer delivery и точные README snippets проверены в fresh PackageReference consumer после финальных generator изменений |

Первое исправление2026-10-14: generator распознаёт четыре существующих Try helpers
из ServicesRegistrationContextExtensions, включая static calls. Singleton/transient,
повторная регистрация, отсутствие concrete self-registration и сохранение existing
runtime factory проверяются executable generated graphs. Semantics helpers не меняются:
один type argument проверяет наличие service; два — конкретную service/implementation
пару. Это часть COB6d, а не завершение COB6.

Исходная сверка выявила дальнейшие темы, пока без собственного failure proof:
отсутствие cycle guard; constructor selection по максимальному числу параметров без
diagnostics; разные default-parameter paths closed/open generic; live mutable
GetHandles list; пропуск conditional access registrations. Детали/evidence —
[HISTORY](HISTORY.md#cobalt-quality-и-try-helpers--2026-10-14).

После COB6 выполнить COB5 на фиксированном graph без преждевременной оптимизации.
COB7 использует один App сценарий на обоих providers: регистрация subsystem modules,
factories/instances/Composite/generics, startup failure, cancellation и disposal.
Оценить число ручных factories/ограничений, ясность ошибок и generated sources,
startup/resolution/allocations и independent package workflow. Учесть реальные
provider differences, включая borrowed instances и повторный disposal aliases.
App.Builder уже выбирает Cobalt по умолчанию; будущий переход касается consumers,
явно выбравших Microsoft DI. Их миграция следует после сравнения и регрессий,
а Microsoft DI adapter сохраняется как поддержанный альтернативный путь.

### COB6a: конструкторы и diagnostics — 2026-10-14

Правила для собственного TrueMoon API, без переноса Microsoft DI соглашений:

- Выбирается единственный публичный instance constructor с наибольшим числом
  параметров. При равной максимальной длине — COB003 на registration call.
  Наличие сервисов в graph не меняет выбор; отсутствующий required dependency
  вызывает runtime error, без fallback к короткому constructor.
- Missing nullable parameter получает null/default; explicit parameter default
  используется при отсутствии service value. Зарегистрированное значение имеет
  приоритет, включая zero/false. Closed/open пути используют symbol nullability
  одного выбранного constructor; open reflection не повторяет выбор по порядку.
- Implementation — accessible non-abstract class. Internal и nested внутри
  non-generic container поддержаны; private nested/file-local и незаданные generic
  arguments получают COB001. Нет public constructor — COB002. Ref/in/out,
  pointers/function pointers/ref-like и allows-ref-struct parameters — COB004.
- Closed mapping требует implicit reference/identity conversion, без user-defined
  operators. Open mapping: equal arity и прямой positional parameter mapping;
  открытые типы внутри generic container unsupported. Нарушение — COB005.
  Closed nested types поддержаны; generic constraints проверяются при closing.
- Required members, включая inherited, требуют выбранного constructor с
  SetsRequiredMembers; иначе COB006. Factory registration остаётся явным способом
  собственного создания и не получает constructor diagnostics.
- Error указывает registration site и способ исправления. Invalid construction
  не публикует resolver/Composite aliases; valid соседние registrations продолжают
  генерироваться. Generated resolver classes internal, чтобы не раскрывать
  internal service types через публичную generated сигнатуру.

[Реализация, tests и package proof](HISTORY.md#cob6a-конструкторы-и-diagnostics--2026-10-14).
Эта проверка закрывает COB6a в описанном scope; COB6b/c/d, COB5/COB7 остаются открыты.

### COB6b: runtime errors и recovery — 2026-10-14

- Cycle guard сопоставляет resolver по reference identity и точному closed service
  type. Другие registrations одного service и другие generic arguments могут
  зависеть друг от друга. Path включает requested services, Composite aliases
  и synthesized IEnumerable elements. State принадлежит конкретному Cobalt resolver
  и удаляется в finally после каждой попытки, включая failures.
- Missing required value сохраняет ServiceResolvingException<T>; циклы дают
  ServiceResolvingException/Failure=CircularDependency. ServiceType/DependencyPath
  доступны как structured data. GetService/TryResolve возвращают null/default
  для missing values, но не скрывают failures зарегистрированной construction.
- Factory/constructor errors сохраняют исходный exception object/type/throw site.
  GetDependencyPath(error) и Data[TrueMoon.Cobalt.DependencyPath] дают immutable
  snapshot. Metadata best effort при custom unavailable/read-only Data. Snapshot
  текущей попытки обновляется при reuse exception; ранее сохранённый snapshot неизменен.
  Reflection invoke использует DoNotWrapExceptions; user-thrown TargetInvocationException
  остаётся исходным, его InnerException не подменяет сам exception.
- MaximumResolutionDepth=128 entries, включая synthesized enumerable elements.
  DepthExceeded ограничивает expanding generic recursion без повторения closed types.
  TryEnsureSufficientExecutionStack даёт InsufficientExecutionStack, если текущему
  потоку не хватает стека; доступный стек может ограничить depth раньше128.
  Exception search filters фиксируют deepest path до finally, без nested catch/rethrow.
- Failed/null singleton construction не кэшируется; null value остаётся optional
  absence/required error и допускает retry. Completed dependencies при отказе
  остаются owned, доступны последующим resolutions и очищаются при shutdown в reverse
  acquisition order. Borrowed instances не освобождаются. Half-constructed resources
  внутри throwing constructor/factory остаются ответственностью этого кода.
- Construction остаётся synchronous под существующим resolver lock. Blocking factory
  на другой поток, который должен resolve через тот же container, unsupported;
  async activation и смена concurrency architecture этим шагом не вводятся.

[Tests, failure proofs и independent package](HISTORY.md#cob6b-runtime-errors-и-recovery--2026-10-14).
Далее COB6c registration contracts, COB6d workflow/final consumers, затем COB5/COB7.

### COB6c: registration contracts — 2026-10-15

- Runtime Type overloads context/container и ServiceRegistration helpers проверяют
  mapping до добавления handle: non-null class/interface service, non-abstract class
  implementation, closed reference assignability либо equal-arity positional open
  definitions. Arrays/delegates/value/pointer/byref/partial generic constructor
  mappings и open types внутри generic container unsupported. Generator продолжает
  проверять constructor shapes/доступность; runtime activation fallback не добавлен.
  Context/container pair overloads отвергают null implementation; optional null
  implementation аргумент ServiceRegistration helper сохраняет self fallback.
- Type self-registration имеет ImplementationType=ServiceType. Instance registration
  хранит actual runtime implementation type; factories остаются opaque, implementation
  не угадывается и factory не выполняется ради query. Explicit value instances и
  low-level value factories сохраняются; constructor Type mappings — reference only.
- GetHandles возвращает read-only collection detached mutable metadata copies под lock.
  Instance/factory references не клонируются. Mutation returned handle, последующие
  регистрации/удаления и изменения context после Build не меняют предыдущий snapshot
  и построенный graph. Exist читает metadata под lock без создания snapshot/factory.
- Duplicates сохраняются; last registration одного service key выбирается single
  resolution. Remove<TService,TImplementation> удаляет **первую** exact known pair
  независимо от lifetime. RemoveAll<T> удаляет все entries exact service key, без
  disposal со стороны registration context; built graph владеет своим lifetime.
- Exist описывает explicit keys, включая open definitions, а не effective resolution.
  One-type Try проверяет any entry exact service; two-type Try — exact known pair
  across lifetimes. Matching instance/type suppresses pair registration; opaque
  factory может сосуществовать с последующей type registration. Composite/Try check-add
  предназначены для setup; их составные вызовы не обещают межпотоковую atomicity.
- Single closed generic resolution предпочитает exact closed key; без него — last
  open registration. Synthesized IEnumerable<T> объединяет exact closed/open entries
  в исходном **global builder order**, с сохранением duplicates/lifetimes/identities.
  Builder сохраняет ordinal metadata внутри TypeContainer; ITypeContainer API не менялся.
  Handcrafted containers без ordinal metadata имеют stable open-before-closed merge.
- Explicit closed/open IEnumerable<T> overrides synthesis, exact closed wins. Generic
  constraint failure не скрывается фильтрацией: original error/path сохраняется,
  valid другие closures и single exact registration остаются доступны. GetService(Type)
  требует closed type; запрос open definition даёт ArgumentException, не synthesis error.

[Failure proofs/tests/package scope](HISTORY.md#cob6c-registration-contracts--2026-10-15).
Следующий шаг COB6d: conditional/static registration forms, modules и final consumer
workflow. COB5 baseline и COB7 comparison/migration остаются последующими этапами.

### COB6d: registration workflow и завершение COB6 — 2026-10-15

- Generator распознаёт ordinary/fluent/conditional (`?.`) calls и unqualified
  inherited contract methods. Semantic identity требует IServicesRegistrationContext
  member/его implementation либо собственный Try extension; одноимённые посторонние
  методы не создают resolvers и не получают Cobalt diagnostics.
- Type overloads сопоставляют service/implementation через semantic argument binding,
  включая переставленные named arguments, parentheses/Type casts и open definitions.
  Для generation нужны прямые typeof operands; Type variables/reflection не выводятся.
- Static Try calls поддержаны через имя extension class или type alias. C# using static
  не импортирует extension methods как unqualified functions; этот способ не обещается.
- Registration module — обычный method/extension с concrete calls в своей compilation.
  Library имеет одну Cobalt PackageReference и собственный generated module initializer;
  app вызывает её registration method при setup. Module discovery/авторегистрации нет.
  Runtime graph включает только выполненные calls, повторный module повторяет обычные
  registrations, Try сохраняет прежние exact-key/pair semantics.
- Все найденные registration sites проверяются независимо от runtime branch.
  Generic wrapper с неизвестным T получает COB001; specialization по вызовам wrapper
  не выводится. Explicit factory/instance — поддержанный dynamic construction путь;
  Type registration по переменным требует заранее generated/handwritten resolver.

COB6a/b/c/d закрыты для согласованного собственного API на Windows x64/net10.0/
SDK10.0.402: source regressions и fresh independent package подтверждены.
[Evidence и ограничения](HISTORY.md#cob6d-registration-workflow--2026-10-15).
Это первый проверенный quality level, не общий release/полная ecosystem compatibility.
Следующий шаг — COB5: зафиксировать App graph и измерить startup/resolution/allocations
без предварительных оптимизаций, затем COB7 comparison и отдельное решение о migration.

### COB5: измеренный baseline App graph v1 — 2026-10-15

Согласованный предыдущим порядком baseline до оптимизации реализуется постоянным
[benchmark project](../../Benchmarks/TrueMoon.Cobalt.Benchmarks/README.md), включённым
в solution, но не в поставляемые пакеты. BenchmarkDotNet — отдельная tool dependency,
runtime/Contracts/generator не получают новой зависимости или performance changes.

- Один fixed headless graph v1 по мотивам уже проверенного package consumer:
  12caller registrations/10keys, borrowed settings, owned factory, Composite Worker
  lifecycle, open repository и exact closed repository, transient request/ticket,
  3ordered plugins. App добавляет стандартные bootstrap registrations. Workload
  не выполняет внешний I/O и не содержит timed logging/delay. Горячие transients
  не disposable: повторный measurement не накапливает owned roots.
- 10cases: empty/graph build+dispose, build+first Request+dispose; warm singleton,
  transient Request, open/closed singleton и materialized multiple registrations;
  App build+dispose и build+Start+Stop+async dispose. Это cumulative boundaries с
  названным cleanup, а не изолированная constructor/start latency через subtraction.
- BenchmarkDotNet MemoryDiagnoser и default calibrated jobs после Dry. Отдельно
  12fresh processes измеряют первый App.Build+Start внутри Main; process/CLR launch,
  module initializers, input creation и cleanup вне cold timing. OS/file caches
  прогреты отдельным discarded process; это не machine-cold measurement. Cold
  allocations process-wide, steady-state BDN allocations публикуются отдельно.
- Historical comparison axis: это исходный baseline для будущего одинакового graph,
  без преждевременного provider comparison/оптимизации. Сохраняются статистика,
  raw JSON/CSV/Markdown, source/binary hashes, SDK/runtime/hardware/job/environment
  и текущая схема питания. Global power plan/GC/JIT settings не изменяются.
- Критерий: workload shape/identity/order/owned-borrowed cleanup проверен, все
  10Dry и10final cases успешно завершены,12fresh samples имеют Completed=true;
  frozen source fingerprints и power plan совпадают до/после. Build/regression
  evidence отделяется от measurement и от прежних package results.

Проверено2026-10-15:10Dry/10default calibrated cases/12fresh-process samples,
Completed=true; Release351regression cases и full solution build exit0.
[Результаты и пределы достоверности](HISTORY.md#cob5-app-baseline--2026-10-15).
COB5 закрыт как первый baseline указанного graph/окружения. Далее COB7 использует
тот же graph для сравнения Cobalt/Microsoft DI
через собственный TrueMoon API, включая errors/cancellation/ownership и удобство;
миграция определяется этим сравнением отдельно.

### COB7: сравнение providers на App graph v1 — 2026-10-15

- Один неизменённый Scenario.Register из COB5 исполняется Cobalt и существующим
  TrueMoon→Microsoft DI adapter. Сравнивается собственный TrueMoon API; обратные
  Microsoft DI bridges/соглашения и новый API не вводятся.
- Shared ComparisonScenario использует симметричный App.Builder/Setup/Build для
  обоих providers. Старый COB5 использовал App.Build; historical numbers не служат
  before/after speedup. Registration graph сохраняется byte-for-byte и по SHA256.
- 10operation pairs с Microsoft baseline: warm singleton/transient/open/closed/
  multiple, empty/graph build, first graph activation, App build и App lifecycle.
  Одинаковые boundaries и named cleanup, no optimization.20Dry перед20default BDN
  cases;12fresh-process App.Build+Start на provider с чередованием порядка.
- Functional verification на каждом provider: graph identity/order/lifetimes,
  repeat Start/Stop, conditional/repeated modules и named open Type calls, required
  missing/cycle errors и recovery, original factory/constructor exceptions/retry,
  startup rollback, normal RunAsync и external cancellation с исходным token.
  Разные exception contracts и Composite disposal counts фиксируются явно.
- Independent PackageReference consumer компилирует те же graph/verification
  sources без framework ProjectReferences. Debug/Release build/publish на обоих
  providers, дополнительно Microsoft с analyzer opt-out; actual delivery/path/SHA,
  generated initializer и отсутствие analyzer/BDN в runtime output проверяются.
- Критерий завершения: current behavior/package/results без failures, сохранённые
  confidence intervals/allocations/source fingerprints и честная comparison matrix
  удобства/ограничений. Итог — явное решение о применимости Cobalt и migration,
  без автоматической правки UI/IPC/process consumers только по headless proof.

Проверено2026-10-15:20Dry/20default measurements/24fresh startup samples,
20source functional scenarios и12independent processes/120behavior scenarios,
Release351regressions/full solution build exit0. [Сравнение и решение](COMPARISON.md),
[команды/evidence](HISTORY.md#cob7-provider-comparison--2026-10-15).
COB7 закрыт; **массовую migration отложить**, Cobalt остаётся existing default,
Microsoft DI adapter/explicit consumers сохраняются. Удобство регистрации graph
одинаково, hot-path costs не дают общего performance основания для перехода.
22UseDI call sites инвентаризированы;13в UI flows не проверялись этим headless run.
Tests/provider selectors остаются для регрессий. Следующее общее направление —
Core quality; Cobalt performance backlog ниже не блокирует его автоматически.

### Оценка вариантов поставки и ecosystem replacement — 2026-10-14

Оценка2026-10-14: [объединение runtime/generator и полная замена Microsoft DI](HISTORY.md#оценка-объединения-и-замены-microsoft-di--2026-10-14).
Рекомендованы два compilation projects в одном consumer NuGet и гибрид generated
activation/runtime fallback, единые runtime lifetime/scopes и обратный
IServiceCollection→Cobalt bridge для уровня совместимости экосистемы .NET.
Это **предложения для выбора scope**, а не утверждённая архитектурная смена.
Принятые runtime/analyzer contracts одного consumer package, отсутствие scopes и COB6 acceptance
сохраняются до явного решения. Полная ecosystem replacement существенно шире
первого рабочего уровня COB6: потребуется отдельный roadmap для activation,
scopes/validation, descriptors/keys, Host integration и conformance consumers.

### Принятый scope и один пакет — 2026-10-14

Пользователь выбрал **два проекта, один пакет** и Cobalt прежде всего для TrueMoon
и собственных приложений. Эта запись заменяет неопределённость предыдущей оценки:
IServiceCollection/ServiceDescriptor import, Microsoft DI contract/Host bridge,
keyed counterparts и полная ecosystem conformance **не входят в текущую цель**.
Существующий TrueMoon→Microsoft DI adapter сохраняется как альтернативный provider;
новая обратная обвязка не разрабатывается.

- TrueMoon.Cobalt.csproj строит runtime, TrueMoon.Cobalt.Generator.csproj — analyzer.
  Только первый packable; его nupkg содержит lib/net10.0/TrueMoon.Cobalt.dll и
  analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll. Generator/Roslyn не являются
  runtime dependencies; нет standalone generator nupkg новых версий.
- Package consumer использует одну ссылку на Cobalt либо TrueMoon. buildTransitive
  target выбирает matching bundled analyzer один раз, включая transitive App
  consumption. EnableCobaltGenerator=false отключает его в конкретном compilation.
  Старую explicit generator PackageReference удалить при migration; если старый
  analyzer ещё подключён, выбор отдаётся bundled matching DLL.
- Source ProjectReference consumers сохраняют analyzer reference: NuGet bundling
  не меняет исходный project graph/TFM/net10 compiler scope.
- COB4 acceptance повторяется direct/App, Debug/Release build/publish, actual
  analyzer path/SHA/generated sources, clean runtime payload и opt-out negative
  compile. IPC/units package composition проверяется отдельно в своём scope.

Дальнейший маршрут: COB6 diagnostics/constructor/runtime errors/registration
удобство → COB5 baseline → COB7 сравнение выбранного TrueMoon application scenario
и условная миграция explicit Microsoft DI consumers. Scopes, runtime fallback и
новые capabilities добавляются по потребности собственных приложений, без
обязательного соответствия чужому контейнеру. Синхронизация runtime/generator
contracts и корректные errors/ownership остаются обязательными.

## Критерии готовности

- Поддержанные регистрация/resolution/generics/lifetime cases имеют поведенческие результаты; ошибки сохраняют полезную причину.
- Resolver освобождает свои ресурсы по контракту и не уничтожает чужие instances.
- Сгенерированные resolvers компилируются и работают; unsupported declarations получают проверяемые diagnostics.
- Provider differences документированы, bootstrap/start/stop согласован с Core.
- Независимый package consumer подтверждает generator и runtime delivery; версии/commands/configuration зафиксированы.

## После первой рабочей проверки

Расширять registration API и startup optimizations только по потребности consumers и baseline. Общая совместимость/release policy остаётся в общем плане, новые lifetime guarantees требуют отдельного решения.

Измеренный backlog2026-10-15 после COB7: исследовать32B warm resolve,96B open-generic
и transient overhead с allocation/CPU evidence, затем менять только подтверждённые
paths и повторять paired graph/functional/package regressions. Это будущая работа;
оптимизация этим comparison не выполнена. Перед новой migration recommendation
сверить performance budget и constructor/ownership forms конкретного consumer.
