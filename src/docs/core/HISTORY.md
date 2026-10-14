# Core: история проверок и ревью

[План](PLAN.md), [текущий статус](STATUS.md), [общий статус](../STATUS.md).

## Ревью ядра и CORE0 baseline — 2026-10-10

Запрос: посмотреть ядро TrueMoon и предложить улучшения. Прочитаны общие и локальные планы, bootstrap/lifecycle, configuration, diagnostics, Contracts и Core utilities. Cobalt runtime/generator и Microsoft DI adapter прочитаны в части app integration. Production код и тесты не изменялись; предложения ниже не являются принятыми изменениями PLAN.

### Фактически выполненные проверки

Windows, SDK **10.0.401**, Debug/net10.0, xUnit **2.9.3**, VSTest. `global.json`: 10.0.100 с `rollForward: latestMinor`. Overlay `src/.agents/skill-overlays/dotnet-test/run-tests.md` отсутствует.

```powershell
dotnet --version
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --framework net10.0 --logger 'trx;LogFileName=core-baseline.trx' --results-directory TestResults/CoreReview-20261010 --verbosity quiet
dotnet build TrueMoon.Core/TrueMoon.Core.csproj --configuration Debug --framework net10.0 --verbosity quiet
```

- TrueMoon.Tests: exit **1**, discovered **4**, **Passed: 3, Failed: 1, Skipped: 0**. Restore/build позволили выполнить тесты. Исторический AppCreate failure воспроизведён на текущем checkout.
- TrueMoon.Core: build exit **0**, errors **0**. Сборка incremental; runtime проверки scheduler/buffers не выполнялись. Этот проект не входит в зависимости TrueMoon.Tests.
- В логах есть NU1506 (повторные Roslyn PackageVersion), NU1507 (несколько package sources без mapping), в тестовом build — CS8602 в AppTests. Package настройки не исправлялись.
- Артефакты ignored: `TestResults/CoreReview-20261010/core-baseline.trx`, `TestResults/CoreReview-20261010/core-build.log`, `TestResults-CoreReview-20261010.log`.
- Cobalt.Tests/Cobalt.Generator.Tests, отдельный adapter scenario, Enerit/Mithril/UI suites, общий solution, Release/package consumer не запускались. Их исторические passing результаты сюда не переносятся.

### Воспроизведённый bootstrap/DI отказ

`TrueMoon.Tests.AppTests.AppCreate` падает с `ServiceResolvingException<IAppLifetime>: Could not resolve service for type: TrueMoon.IAppLifetime`.

Стек: `DefaultApp.StartAsync` → `ResolveAll<IStartable>` → `CobaltServiceResolverBase.ResolveEnumerable` → generated `LifeTimeExecutorResolver.Resolve` → `Resolve<IAppLifetime>`. В AppCreate зарегистрированы CommonStartableService и CommonService1; LifeTimeExecutor зарегистрирован в других сценариях того же test assembly.

По исходникам [CobaltServiceResolverBuilder](../../TrueMoon.Cobalt/CobaltServiceResolverBuilder.cs) выбирает все generated factories по одному ServiceType, не фильтруя выбранную ImplementationType/lifetime runtime handle. [ServiceResolvers](../../TrueMoon.Cobalt/ServiceResolvers.cs) хранит factories глобально, generator регистрирует их через ModuleInitializer. Поэтому runtime graph получает чужую implementation из той же сборки. Это не доказательство зависимости от порядка тестов: generated registrations общие уже при загрузке assembly.

Дополнительно по коду [generator](../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs): Composite aliases получают FactoryCode, но GenerateTypeResolver не использует его при создании singleton. Alias resolver может возвращать незаполненное `_instance`. Требуется проверка единого экземпляра IAppLifetime/IAppLifetimeHandler. Владение resolver/generator fixes — COB1/COB3; интеграционный сценарий — CORE3.

### Приоритеты улучшения

**P0 — наблюдаемый lifecycle и cleanup (CORE1).** [App.RunAsync](../../TrueMoon/App.cs) ловит Exception и только отправляет diagnostic event; caller может получить successful Task после startup failure. [AppExtensions.RunAsync](../../TrueMoon/AppExtensions.cs) выполняет тот же сценарий без такого catch. В обоих путях отмена/ошибка StartAsync или WaitAsync обходит StopAsync/Stopping/Stopped; await using запускает disposal, но это не эквивалент graceful stop. StopAsync получает тот же run token, который при внешней отмене уже отменён. DefaultApp.StopAsync также подавляет ошибки отдельных сервисов.

Предложение: один runner для обоих entry points; явно определить cancellation outcome, startup rollback и shutdown errors; использовать независимый ограниченный shutdown token; сохранять первичную причину при cleanup failure. Учитывать реально запущенные сервисы, останавливать их в обратном порядке; определить repeat/concurrent Start/Stop/Dispose. Это предложения к contract review, текущим PLAN эти semantics ещё не утверждены.

**P0 — изоляция graph и lifetime aliases (CORE3 ↔ COB1/COB3).** Factory registry должен давать способ создания выбранной runtime registration, включая implementation и lifetime. Generated resolver не должен автоматически включать сервис во все app graphs. Composite должен разрешать один instance через несколько interfaces. Cobalt и Microsoft DI нужно проверить на одинаковом небольшом app scenario.

**P1 — scheduler shutdown (CORE4).** [TmTaskScheduler](../../TrueMoon.Core/Threading/TmTaskScheduler.cs) в Dispose завершает writer, но ThreadLoop выходит только по внешнему cancellation token. Enerit consumers создают scheduler без такого token: idle worker продолжает цикл после dispose. QueueTask после завершения writer бесконечно повторяет TryWrite. TryExecuteTaskInline допускает исполнение на вызывающем потоке, поэтому отдельный STA worker не гарантирует affinity. Предложение: ожидание очереди с учётом completion, явный drain/cancel contract, отказ от новых задач после stop и управляемое завершение workers. Inline/affinity определить по требованиям consumers; не превращать общий scheduler в UI dispatcher без отдельного решения.

**P1 — владение буферами (CORE4).** [ArrayPoolBufferWriter.Dispose](../../TrueMoon.Core/ArrayPoolBufferWriter.cs) каждый раз возвращает тот же массив; методы продолжают обращаться к нему после возврата. Предложение: идемпотентный dispose, ObjectDisposedException при последующем использовании, очистка references при возврате и копирование только written region при росте. [MemoryPoolUtils](../../TrueMoon.Core/MemoryPoolUtils.cs) возвращает backing array любого array-backed Memory, не доказывая происхождение/владение, включая slices. Для нового API предпочтителен owner/lease с однократным Dispose; существующие Enerit consumers мигрировать отдельно. [.NET ArrayPool.Return](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.return?view=net-10.0) требует возврата rented buffer ровно один раз и прекращения использования после возврата.

**P1 — process и diagnostic subscriptions (CORE1/CORE2).** [DefaultApp](../../TrueMoon/DefaultApp.cs) подписывает анонимный Console.CancelKeyPress handler и не отписывается: static event удерживает app. Handler не устанавливает args.Cancel; для управляемого Ctrl+C shutdown это необходимо по [Console.CancelKeyPress contract](https://learn.microsoft.com/en-us/dotnet/api/system.console.cancelkeypress?view=net-10.0). Предложение: выбираемый console lifetime, хранение/освобождение delegate и явное поведение нескольких app instances.

[DiagnosticObserver](../../TrueMoon/Diagnostics/DiagnosticObserver.cs) теряет IDisposable от каждой value.Subscribe. DiagnosticSubscription освобождает только AllListeners subscription; EventsSource не освобождает собственный DiagnosticListener. Предложение: app-owned набор всех handles с cleanup при shutdown/build failure. LoggingObserver вызывает listeners без изоляции исключений: diagnostic listener способен прервать app operation. Определить error policy для callbacks.

**P1 — configuration APIs (CORE2).** [ConfigurationBuilder](../../TrueMoon/Configuration/ConfigurationBuilder.cs) игнорирует AddProvider/RemoveProvider и создаёт фиксированный набор. [JsonConfigurationSection](../../TrueMoon/Configuration/JsonConfigurationSection.cs) бросает NotImplementedException для всех операций чтения/записи. Предложение: реализовать минимальный заявленный набор providers либо явно ограничить доступный API; определить precedence и invalid-value errors до расширения.

Текущие способы доступа расходятся: ConfigurationExtensions.Get без section просматривает все секции; indexer/Exist/GetName обращаются к default; CommonConfiguration.GetSection берёт первую секцию с данным именем. Args/environment хранят строки, ConfigurableBase.Get<int> не преобразует их. Refresh обновляет список секций, но args/environment snapshot получен в constructor. Дополнительные дефекты по коду: Split('=') теряет хвост `token=a=b`; PathResolver передаёт в GetFullPath весь `-root=...`, а GetInfo возвращает processName вместо вычисленного productName; GetOrCreate с default key читает typeof(T).Name, но при создании передаёт null в Set. Отдельные поведенческие проверки этих случаев не запускались.

**P2 — API и зависимости после исправления поведения.** Сохранить разделение Contracts/bootstrap/utilities и отсутствие UI зависимости в Core. Выбор resolver уже есть через App.Builder/UseDI; перед перемещением Cobalt default в отдельный composition layer проверить package consumers. Для обычного hosting можно отдельно оценить adapter к [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host), который объединяет lifecycle, DI, configuration и logging. Это возможная интеграция, не решение заменить TrueMoon или добавить новую зависимость сейчас. Typed configurator вместо reflection Configure и XML docs полезны после фиксации contracts. Startup/allocations оптимизировать после измерений.

### Обзор четырёх существующих тестов

Использованы run-tests и test-anti-patterns с .NET extension; [AppTests](../../TrueMoon.Tests/AppTests.cs) не переписывались. Class-level fixtures/resources отсутствуют.

| Тест | Oracle и текущий результат | Оценка |
| --- | --- | --- |
| AppCreate | Тип CommonStartableService и IsStarted; Failed до assertions на resolution | Полезный smoke с проверяемым состоянием; отсутствие Stop assertion — отдельный пробел |
| Create | App not null, Name not empty; Passed | Корректный ограниченный smoke; не проверяет precedence/конкретное имя |
| RunAsync | await вызова и Assert.True(true); Passed | T1: Always-true assertion, не проверяет lifecycle outcome |
| RunAsyncWithConfigurator | await вызова и Assert.True(true); Passed | Тот же T1, отдельный reflection entry point |

Findings: **1 Critical (T1, два теста)**, **1 High (T2, общий helper двух тестов)**; Medium/Low findings не выделялись.

- **T1:** заменить константный assert наблюдаемым результатом runner. Предлагаемый oracle после принятия CORE1: trace строго `Start → Stopping → Stop → Stopped → Dispose`, каждое событие один раз; при failure сохранить исходный exception instance, остановить только реально запущенные сервисы, подтвердить disposal. Это будущие критерии, а не результаты текущего запуска.
- **T2:** LifeTimeExecutor.StartAsync запускает неотслеживаемый Task.Run с Task.Delay(500) и lifetime.Cancel. Нельзя дождаться фоновой операции и проверить её ошибку. Заменить fixed-delay coordination управляемым сигналом старта/отмены, хранить и await завершение helper task; проверять ровно один cancellation request. Длительность suite около100ms не доказывает, что scheduled task успела вызвать Cancel.
- Положительные наблюдения: app operations awaited; using/await using присутствуют; AppCreate проверяет тип и observable IsStarted; Create действительно проверяет создание/имя.
- Смежные непроверенные сценарии: startup rollback, external cancellation, stop failure, repeat/concurrent lifecycle, disposal/subscription ownership, оба DI providers. Coverage не собирался; отсутствие dedicated тестов не выдаётся за измеренный процент покрытия.

### Следующая конкретная работа

Сначала устранить загрязнение runtime graph и Composite alias resolution в COB1/COB3, сохранив AppCreate как regression proof. В CORE1 определить наблюдаемый runner/error/cancellation/rollback contract и заменить T1/T2 проверками принятого поведения. Затем закрыть scheduler/pool ownership по CORE4 и providers/diagnostic subscriptions по CORE2. Архитектурное расширение до этих шагов увеличит поверхность неподтверждённого поведения.

## Исправление проблем ядра — 2026-10-10

Запрос: «давай починим все проблемы» из предыдущего ревью. Область: lifecycle/bootstrap, Cobalt/generator/Microsoft DI adapter, configuration/diagnostics, scheduler/pools и необходимые Enerit consumers. Исходники поверх HEAD `9d7090a`, изменения не закоммичены. Существующие пользовательские UI изменения сохранены. Optional Generic Host adapter, новый typed-configurator API и переезд архитектуры не вводились.

### Реализация

- App и IApp.RunAsync используют один AppRunner. Ошибки больше не маскируются; startup/cleanup exceptions сохраняются, rollback/normal shutdown выполняются в reverse order. Shutdown token отдельный, timeout30s кооперативный. DefaultApp сериализует операции, повторные calls идемпотентны, restart запрещён; owned provider освобождается один раз, concurrent и recursive disposal не ждут сами себя.
- Console handler устанавливается только на работающем lifecycle, ставит Cancel=true и снимается после stop/failure/dispose. DefaultAppLifetime выполняет snapshots callbacks вне lock, завершает wait асинхронно, освобождает owned CTS и сохраняет borrowed. Последний review обнаружил Cancel/Dispose race: CTS теперь освобождается после активных callbacks; external borrowed source cancellation освобождает waiters.
- Configuration providers действительно add/remove; priority, reads и conversion согласованы. JSON sections читаются/сохраняются, failed refresh сохраняет snapshot. Исправлены args equals-tail/executable exclusion, root path/product metadata и default GetOrCreate key. Diagnostic subscriptions/factory созданы лениво и принадлежат resolver; individual handles и sources освобождаются, observers не ломают app.
- Scheduler больше не spin-wait, закрывает приём/drain и завершает workers. Writer защищает Dispose/use-after и copy только written region. Memory rentals имеют отдельного manager-owner, проверяют whole-region identity, отвергают foreign/sliced/double/stale return; добавлен IMemoryOwner API. Array extraction compatibility и обязательное прекращение работы с ранее полученными spans/pins описаны в PLAN.
- Cobalt exact registration metadata, aliases/generics, singleton/thread-safety и owned/borrowed disposal исправлены; подробности принадлежат [его HISTORY](../cobalt/HISTORY.md). Microsoft DI wrapper владеет provider, но стандартный многократный disposal disposable aliases сохраняется.
- Enerit переведён на tracked rental, собственный scheduler/connection cleanup и async handler drain. Partial header/EOF и pending-response failures исправлены без изменения framing; [его20 новых regression cases](../enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10).
- В Directory.Packages.props удалены lower-version дубликаты Roslyn CSharp/Analyzers: effective5.9.0 сохранена. Repository NuGet.Config задаёт один публичный source и mapping, не меняя global user config. После исправления NU1506/NU1507 в полном build отсутствуют. Проверка выполнена с .NET best practices, run-tests, code-testing-agent, find-untested-sources, assertion-quality/test-gap-analysis и msbuild-antipatterns skills.

### Итоговая валидация

Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Сначала direct Core run78/78 и Cobalt37/37+generator6/6; затем окончательные команды после всех правок:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --logger trx --results-directory TestResults/CoreFixes-Final-20261010 --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --list-tests --verbosity quiet
```

Все три команды exit0. Build: **0errors/1271warnings**, не warning-free. Tests: **643passed/0failed/0skipped**. TRX counters сверены по проектам:

| Проект | Discovered/Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| TrueMoon.Tests | 81 | 81 | 0 | 0 |
| TrueMoon.Cobalt.Tests | 37 | 37 | 0 | 0 |
| TrueMoon.Cobalt.Generator.Tests | 6 | 6 | 0 | 0 |
| TrueMoon.Enerit.Tests | 30 | 30 | 0 | 0 |
| TrueMoon.Enerit.Generator.Tests | 9 | 9 | 0 | 0 |
| TrueMoon.Alloy.Tests | 480 | 480 | 0 | 0 |
| TrueMoon.Mithril.Tests | 0 | 0 | 0 | 0 |

Mithril zero discovery — отсутствие executable тестов, а не successful lifecycle suite. Alloy480 проверяет unit scenarios на уже существующих UI изменениях; physical/native/soak результаты этим run не меняются. Core81 распределены: AppTests5, AppLifecycle23, AppLifetime12, Configuration11, Diagnostics8, CoreUtility22.

Ignored logs: `TestResults-CoreFixes-solution-build-final-20261010.log`, `TestResults-CoreFixes-solution-tests-final-20261010.log`, `TestResults-CoreFixes-discovery-final-20261010.log`. TRX: `TestResults/CoreFixes-Final-20261010/`, семь файлов с timestamps16:58:56–16:58:58 local. Intermediate failing compile/assertion attempts сохранены отдельно и не считаются final passing evidence: исправлены unconstrained nullable override, synchronous Throws overloads и exact JsonException assertion для derived JsonReaderException.

### Requirement → Evidence

| Requirement | Evidence |
| --- | --- |
| «давай починим все проблемы» — L1 runner/rollback/error/cancellation | `AppLifecycleTests.StartupFailure_RollsBackReverseOrderAndPreservesOriginalError`, `StartupAndCleanupFailures_PreserveEveryErrorAndContinueCleanup`, `ExternalCancellation_StopsWithIndependentTokenAndDisposes`, `InstanceRun_UsesSameShutdownOrderAsBuilderRun` — exact trace, original exception identity и dispose count на обоих providers |
| «давай починим все проблемы» — L2 repeat/subscription/lifetime ownership | `ConcurrentStartStopDispose_AreIdempotentAndRestartIsRejected`, `StoppedAndDisposedApplication_IsCollectibleAfterConsoleUnsubscription`, `AppLifetimeTests.Cancel_DoesNotRunWaitContinuationsInline`, `ConcurrentDispose_DefersSourceDisposalUntilCancellationCallbacksFinish`, `BorrowedSourceCancellation_ReleasesLifetimeWaiters` |
| «давай починим все проблемы» — D1 graph/aliases/containers | `AppTests.AppCreate`, `ResolverRegressionTests.RuntimeGraph_SelectsImplementationAndLifetime_WithoutAssemblyPollution`, `Composite_AllAliasesShareOneOwnedInstance`, `TypeContainer_InitializesAllFactoriesOnce_WhenSingleAndMultipleResolutionMix`; полный Cobalt matrix в его HISTORY |
| «давай починим все проблемы» — C1 providers/JSON/paths/conversion | `ConfigurationTests.BuilderAddsProvidersByPriorityAndRemovesByIdentityOrType`, `NamedAndUnqualifiedReadsUseTheSameHighestExistingKey`, `ValuesConvertUsingInvariantCultureAndPreserveMissingNullAndInvalidDistinctions`, `JsonInvalidFilesSurfaceErrorsAndFailedRefreshKeepsLastValidSnapshot`, `PathResolverParsesExactRootArgumentAndUsesProductMetadata` |
| «давай починим все проблемы» — G1 diagnostics | `DiagnosticsTests.DisposingSubscriptionDetachesExistingAndFutureSources`, `ListenerExceptionsDoNotStopDeliveryAndSubscriptionsRespectFilters`, `AppCreationFailureDisposesResolverOwnedSubscriptions`, `AppDiagnosticsAreObservedAndDisposedEvenWhenAListenerThrows` |
| «давай починим все проблемы» — U1 scheduler | `CoreUtilityTests.SchedulerDisposeDrainsAcceptedWorkAndTerminatesWorkers`, `SchedulerCancellationDrainsQueueAndRejectsNewTasks`, `SchedulerExposesPendingTasksAndRefusesForeignInlineExecution`, `SchedulerAllowsWorkerInlineExecutionAndSelfDisposalWithoutDeadlock` |
| «давай починим все проблемы» — U2 writer/rental/consumer ownership | `BufferWriterGrowsPreservingOnlyWrittenRegionAndReturnsOldBuffer`, `BufferWriterDisposeReturnsOnceClearsReferencesAndGuardsEveryMember`, `MemoryRentalRejectsForeignAndSlicedMemoryWithoutReturningWholeRental`, `MemoryOwnerDisposalAndReturnShareOwnershipAndStaleRentalCannotReturnNewLease`, `MemoryConcurrentReturnsHaveExactlyOneWinner`; Enerit `UtilityLifetimeTests.ConnectionDisposeWaitsAsyncHandlersAndReturnsTheirRequestMemory` |

### Assertion и static gap review

Новые/переписанные тесты перечитаны против исходников: exact lifecycle order и single disposal, отсутствие rollback незапущенных сервисов, original error identity/aggregate contents, live independent shutdown token, отсутствие retained console roots; configuration exact winners/shadowing/invalid/missing, реальная persisted JSON запись и snapshot retention; subscription delivery после individual cleanup и observer failure; scheduler accepted-work drain/foreign inline/worker identity; writer sentinel отличает copy-written от copy-capacity; rental stale lease не может вернуть новую rental. Старые Assert.True(true) удалены, delayed fire-and-forget helper заменён observable synchronous lifetime cancellation.

В bounded новых regression files нет assertion-free/always-true/self-field tests; identity assertions сравнивают независимо разрешённые объекты/известные исключения. Assertions охватывают equality, state/side effects, collections/deep sequence, negative, type/identity и errors. TCS/manual barriers вместо arbitrary sleep; timeout guards ограничивают зависший тест. Static pseudo-mutations remove-cleanup/error propagation/priority/ownership/queue close/EOF checks имеют наблюдаемый oracle. Mutations не выполнялись, empirical mutation score и line/branch coverage не заявляются.

Ограничения: cooperative user Task shutdown, standard Microsoft DI alias/error cleanup, disk/permission fault injection, multi-process JSON writes, pointer-level pinning/active-pin return behavior, independent package consumers и Release. Полный IPC/units/native UI matrix принадлежит другим этапам; исправление известных Core дефектов не закрывает G3/G4/G6.

Финальная документальная сверка:13 изменённых planning/status/history документов,113 local links, missing files/anchors0. `git -c core.safecrlf=false diff --check` exit0; удалена лишняя пустая строка JSON section EOF без semantic changes. Scratch research/plan/status/pairing files отсутствуют в working-tree change list. Root assertion inventory:65 named methods/320 static Assert call sites/0 assertion-free в6 Core test files; theory/loop executions не входят в эту метрику.

## CORE5 / COB4 headless package consumer — 2026-10-11

Реализован [воспроизводимый consumer](../../ManualTests/CorePackageConsumer/README.md)
с собственными исходниками и только PackageReference, вне solution. Runtime C# и
lifecycle/provider contracts не менялись. Пять package projects получили конкретные
Description и самостоятельные PACKAGE.md: TrueMoon, Contracts, Cobalt, generator,
Microsoft DI adapter. Общая версия и net10.0 сохранены, публикации пакетов не было.

Generator доставляется **явным** TrueMoon.Cobalt.Generator reference с PrivateAssets=all;
runtime nuspec не содержит его автоматической transitive зависимости. Это принятое
правило поставки, не исправление analyzer runtime. Actual compiler analyzer path/SHA256,
consumer-generated ModuleInitializer и resolution проверены на NuGet, отдельно от
ProjectReference unit tests. Microsoft DI consumer дополнительно собирается без generator.

### Итоговый запуск

Windows x64 build26300, SDK10.0.401/runtime.NET10.0.12, net10.0, framework-dependent
Debug/Release build/publish без RID. Свежая local version:
`0.2.2.7-alpha.headless.20261011142810069`. Generator DLL SHA256:
`AB7D23337C3866C6877264F92D825A961DA789FB450D1F3077F42FF8867C5288`.
Все пять TrueMoon packages получены из нового local feed в isolated cache; source
mapping и .nupkg.metadata сверены. Consumer assets только package, без UI/Skia/Silk,
IPC или units. Runtime output не содержит generator/Roslyn DLL.

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/final-consumer

dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Cobalt.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Cobalt.Generator.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
```

Повторять runner в **новом** каталоге; final-consumer уже содержит evidence и не
перезаписывается. Три component test commands завершились exit0, xUnit2.9.3/VSTest:
Core81/81, Cobalt37/37, generator6/6, Failed0/Skipped0; TRX totals совпадают.

| Проверка | Фактический результат |
| --- | --- |
| App + explicit generator, оба providers | 40/40 processes: 5scenarios ×2providers ×Debug/Release ×build/publish |
| Direct Cobalt без App/adapter | 4/4 processes, Composite/open generic/transient/borrowed cleanup |
| Microsoft DI без consumer generator | 20/20 processes, те же5scenarios ×Debug/Release ×build/publish |
| Generation | По8headless/6direct generated sources на configuration; 4 actual compiler path/hash checks; marker в consumer assembly и автоматически опубликованный graph |
| Consumer compilation/delivery | 6builds/6publishes exit0/0warnings/0errors |
| Delivered README | 3точных C# snippets извлечены из packaged PACKAGE.md, 3Release builds +3processes exit0/0warnings/0errors; примеры вручную не адаптировались |
| Missing generator control | Fresh isolated consumer без generator, compile-time marker сохранён; expected build exit1/CS0246 RHeadlessConsumer. Это отрицательный контроль, не passing build |
| Runner | exit0, summary.Completed=true, Cases64/ReadmeExamples3, Failure=null |

Сценарии: normal lifetime.Cancel; external cancellation с исходным token;
startup failure с rollback только worker; stop failure с продолжением worker cleanup;
build failure с resolver-owned source/subscription/factory cleanup. В normal/cancel/
stop-failure lifecycle order worker.Start→gate.Start→Stopping→gate.Stop→worker.Stop→Stopped;
startup failure worker.Start→gate.Start→worker.Stop→Stopping→Stopped, build failure
не запускает сервисы. Исходные exception identities сохранены (build может иметь wrapper).
Отдельный shutdown token остаётся cancelable и не canceled после отмены run token.

Composite Worker/aliases и lifetime aliases shared; open Repository<int>/closed
Repository<string> singleton возвращают Int32:17/String:17 из приоритетного provider,
app name write/read совпадают. Runtime factory singleton shared; две Ticket transient
instances различны и освобождены. Factory1/Gate1/Ticket2/WorkerResource1 releases,
borrowed instance0. Cobalt Worker.Dispose calls1, Microsoft DI calls3 в normal graph;
Worker идемпотентен и собственный ресурс освобождает один раз. Это сохранённое
provider difference, а не обещание reference-unique Microsoft DI cleanup.

Observer failure изолирован; framework и consumer diagnostics наблюдались.
После shutdown/build failure fresh DiagnosticListener не enabled, events не
доставляются, retained owned source отклоняет операции как disposed. Нет UI,
clipboard, system settings или чужих процессов; runner ограничивает только
свои children timeout и завершает только свои зависшие процессы.

Pack exit0 во всех5проектах, **535 warning entries** в pack logs (31/147/177/75/105),
не zero-warning source build и не535уникальных проблем. Shared XML/RS1041 и прочие
warnings не исправлялись. Полная solution сборка/другие suites не запускались.

### Промежуточные попытки, артефакты и ограничения

Первый runner `first-consumer`: все64runtime processes прошли, omitted-generator
build верно отказал CS0246, но verifier ожидал только CS0234 и завершился exit1.
Classifier исправлен на обе формы отсутствующего namespace; весь итоговый runner
повторён с новой версией/feed/cache. First summary.Completed=false сохранён и не
считается итоговым passing run. Позднее добавлена проверка примеров из actual package
README; она входит в final-consumer. Одна локальная reporting command имела PowerShell
foreach/pipeline parse error; исправленная сводка сверила все actual reports/TRX.

Ignored TestResults/CorePackages содержит оба runs, feed/package SHA256, isolated
cache, source copies/SHA256, compiler analyzer list/generated source hashes,
build/publish output, process reports/logs, negative control и summary; tests/TRX
отдельно. После executable proof менялась только навигационная документация/статусы.

Не проверялись другие OS/SDK/compiler hosts, физический Ctrl+C/kill/parent exit,
RID/self-contained/trimming/single-file/NativeAOT, owned disposal failure matrix
в отдельном package consumer, disk/JSON fault injection и performance. Component
coverage этих контрактов не выдаётся за более широкий package run. UI K7/physical
DPI/real loss/VRAM, Enerit/Mithril own-process и общий выпуск остаются открыты.
CORE5/COB4 выполнен в указанном scope; локально далее COB5 baseline фиксированного
graph, общий функциональный приоритет — MIT0 bounded own-child, затем ENE1/ENE2.

Final static verification: 31 changed/new text files без NUL/conflict markers,
source final newline, XML/PowerShell syntax valid. 34documents/374local links/85heading
links без ошибок; git diff --check exit0. Все123 recorded source fingerprints и
5package SHA256 совпадают; все64 child reports Completed=true. TestResults ignored.

## Diagnostics test isolation при MIT4 — 2026-10-14

Во время [MIT4/ENE4 regression](../mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14)
TrueMoon.Tests Debug сначала дал80passed/1failed/0skipped.
AppCreationFailureDisposesResolverOwnedSubscriptions получил IAppLifetime.Cancel
от параллельного теста через default TrueMoon filters. Исправлена только проверка:
ConcurrentBag собирает unique eventName собственного DiagnosticListener probe,
сохраняются original exception, IsEnabled=false и Assert.Empty для post-failure
события. Production Core без изменений. Final Debug81/81 и Release81/81,
Failed0/Skipped0, Windows x64/net10.0/SDK10.0.402. Команды/TRX в общем integration
журнале. Packages/headless64+3/UI этой regression не повторены.

## Package regression MIT5/ENE5 — 2026-10-14

[Общий package журнал/команды](../mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14).
Windows x64/net10.0/SDK10.0.402: independent App/IPC/units consumer108/108,
Cobalt/Microsoft DI включая DI без Cobalt analyzer. Actual packaged dependencies/
compiler path/SHA и no-runtime-analyzer delivery подтверждены. Related source
regressions Core81/81, Cobalt37/37/generator6/6 в Debug/Release, Failed0/Skipped0.
Core получил только package Description; Cobalt/DI adapter C# runtime без изменений.
Headless64+3/COB5 performance baseline не повторены. Full solution Debug nonincremental
exit0/errors0/warnings1227; другие UI/native checks не запускались.
