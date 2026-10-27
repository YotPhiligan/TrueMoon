# Core: состояние и точка продолжения

Последняя работа — **2026-10-27, bounded history reader/importer для child logs**.
UseDiagnostics(pipeline) предоставляет borrowed nondisposable reader/importer views;
импорт назначает host identity/sequence, сохраняет child Origin/PID/time/trace,
применяет прежние filters/budgets и не re-emits global listeners.
Legacy overload остаётся без delivery views.
[API](DIAGNOSTICS.md#чтение-и-импорт-bounded-history),
[подробные проверки](HISTORY.md#чтение-и-импорт-child-diagnostics--2026-10-27).

Новые59cases; Core373/373 Debug/Release, failed/skipped0. Full solution4285/4285
в каждой конфигурации; builds0errors, Debug nonincremental1546warnings,
Release incremental1316warnings, SDK10.0.403.
[Evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Ошибка изоляции нового App-теста в общем запуске исправлена в тесте; global routing
не менялся. Исторические CPU FrameReceiver/Mithril races остаются открытыми.
Aether отдельно проверил3actual Preview children и control/log isolation;
published/GPU/package/performance acceptance не повторены.

CORE6 остаётся частичным: далее Editor console/export и domain/frame metrics.
Bootstrap-before-pipeline, App-private routing, generator и общий quota report/
artifact roots отдельны. [Child transport](../engine/DIAGNOSTICS_DELIVERY.md)
принадлежит Aether; предыдущие [operations/telemetry](HISTORY.md#операции-и-трассы-диагностики--2026-10-26),
[explicit parent/kind](HISTORY.md#explicit-parent-и-kind-для-ipc--2026-10-26)
и JSONL/emergency budgets сохранены.

Связанная проверка — **2026-10-23, ENE6a/MIT6a integration**: Core117/117
Debug/Release в текущем полном solution2361/configuration; new App/IPC lifecycle
scenarios и fresh package132 прошли на Cobalt/DI. Core runtime/API не менялись;
собственная headless delivery/performance матрица повторно не запускалась.
[Команды и scope](../enerit/HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23).

Последняя работа — **2026-10-23, сверка предпосылок Aether по планам и исходникам**.
Новых Core blockers для локального ARCH1b не выявлено; текущие App/factory/lifecycle
contracts достаточны в прежнем bounded scope. Неограниченный scheduler сам по себе
не требует переписывания Core: admission для IPC предложен на границе Enerit.
[Общая сверка и зависимости](../PLAN.md#сверка-основы-перед-aether--2026-10-23).
Код не менялся, build/tests/consumers не запускались; прежние ограничения сохраняются.

Текущая реализация — **2026-10-22, общие фабрики/lifecycle adapters**:
IFactory<T,TArguments>, Factory adapters и non-owning AppServiceLifecycle добавлены
в Contracts без provider/module dependencies. Legacy IFactory<T> сохранён.
Core117/117 Debug/Release (10новых cases), включая обе DI реализации и exactly-once
product ownership. Подробности в [Aether DI evidence](../engine/HISTORY.md#единые-фабрики-и-di--2026-10-22).
Прежняя Core delivery matrix не повторялась целиком; свежие bounded package checks
этого изменения перечислены там же.

Документационное решение — **2026-10-22**: правила ИИ-агентов закрепляют сборку
систем/сервисов через Cobalt и внедрённые IFactory, constructor injection и явное
владение результатами фабрик. App остаётся точкой composition/lifecycle, общие
contracts независимы от provider. [Границы решения](../cobalt/PLAN.md#организация-систем-и-сервисов-через-di-и-ifactory--2026-10-22).
Код/API не менялись; аудит consumers и build/tests не запускались. Следующие
адресные изменения следуют правилу, прежние readiness и порядок работ сохраняются.

Исправление структуры — **2026-10-21**: AppCreationException находится в корне
Contracts/namespace TrueMoon; UnitsConfigurationExtensions перенесён в Mithril.
Роль общей сборки contracts/base models/interfaces сохранена, новых dependencies
нет. Shared consumer RootNamespace заданы явно. Core107/107 Debug/Release;
свежие package consumers80cases и3README examples прошли. Public namespace/assembly
изменения требуют обновления using и пересборки внешних consumers.
[Миграция и точные команды](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

App entry-point audit — **2026-10-19**: bootstrap/configuration и UseDiagnostics
подключены к App; Contracts/Core utilities не требуют отдельного UseX.
Focused Debug AppTests/AppLifecycleTests/DiagnosticsTests:46passed/failed0/skipped0.
Runtime code не менялся; прочие Core suites/Release/consumers не повторялись.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Integration regression — **2026-10-19, UseAether**: Core production API не менялся.
Полный Debug повторил Core107/107; новые Aether integration tests и независимые
package consumers проверили App startup/stop/rollback и sync/async disposal на
Cobalt и Microsoft DI. Это engine integration scope; прежние Core Release/source/
package матрицы ниже не повторялись.
[Подробные команды/evidence](../engine/HISTORY.md#app-integration-реализация--2026-10-19).

Package metadata check — **2026-10-19**: TrueMoon/Contracts/Core наследуют общие
project/repository/license/icon metadata; Core получил собственный PACKAGE.md.
Release pack и nuspec/payload checks прошли в общей matrix; runtime code и own
unit/native consumers не менялись/не запускались в этом metadata шаге.
[Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Документационная сверка2026-10-15: [общая quality matrix](../QUALITY.md)
определяет следующий scope — UI package/Hosting после текущих Core/Cobalt changes.
Новых code/build/tests/package запусков в этой сверке нет.

Последняя работа — **2026-10-15, сквозной headless quality proof**.
Одни consumer sources проверены через ProjectReference и независимые свежие
PackageReference: configuration/JSON, diagnostics, App lifecycle/Cobalt и реальный
эксклюзивный FileStream, включая cancel/start/stop/build/config/cleanup failures.
**Согласованный bounded Core quality проход завершён** на указанной конфигурации;
новых public APIs/runtime fixes этим этапом не потребовалось.
[Контракт](PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[runner](../../ManualTests/CoreHeadlessProbe/README.md),
[команды, matrix и границы](HISTORY.md#сквозной-headless-proof--2026-10-15).

**Текущие проверки:** Windows x64/net10.0, SDK10.0.402, xUnit2.9.3/VSTest.
TrueMoon.Tests: **107 passed / 0 failed / 0 skipped в каждой Debug/Release**;
два final TRX,214passed суммарно. Source52processes, package80 +3README, оба
summary Completed=true;130/127source fingerprints совпали, opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1183.
Microsoft DI:6compatibility scenarios; Cobalt:7, включая startup+Dispose failure
с сохранением обеих причин. Матрица Debug/Release × build/publish, без RID.

Предыдущие этапы2026-10-15: [configuration/bootstrap](HISTORY.md#configuration-и-bootstrap-quality--2026-10-15)
и [diagnostics](HISTORY.md#diagnostics-quality--2026-10-15). Их исправления сохраняют
текущую regression107cases; новые unit tests в headless этапе не добавлялись.

## Реализация и проверки

| Область | Реализация | Проверки2026-10-15 |
| --- | --- | --- |
| Bootstrap/lifecycle / CORE1 | Runner, reverse rollback/stop, cooperative shutdown token, повторные операции, owned disposal | AppLifecycleTests23 + AppTests5; regression Debug/Release |
| Lifetime | Snapshot callbacks вне lock, asynchronous wait continuations, owned/borrowed CTS | AppLifetimeTests12; regression Debug/Release |
| Configuration / CORE2 | Приоритеты, conversion, null/invalid, args/env/JSON refresh; исправлен GetOrCreate в предыдущем этапе | ConfigurationTests27; regression Debug/Release |
| Bootstrap/configuration | Actions до registrations/factories, единый IConfiguration у callbacks/App/DI; validation failure сохраняет cause и не начинает registrations | Две theory по Cobalt/Microsoft DI,4cases; входят в27 configuration cases |
| Diagnostics / CORE2 | Completed source retirement, late handle cleanup, повторный setup без старых registrations, независимое освобождение Apps | DiagnosticsTests18; regression Debug/Release; текущий consumer подтверждает повторный setup/events/cleanup |
| Utilities / CORE4 | Scheduler drain/affinity, writer guards, validated rental ownership | CoreUtilityTests22; regression Debug/Release |
| Headless/App proof | JSON persistence/failed refresh recovery, lifecycle/errors/token identity, реальный file ownership и owned/transient/borrowed cleanup | Source52cases: Cobalt28/Microsoft24, Debug/Release build/publish |
| Package / CORE5 + COB4 | Fresh feed/cache/version, bundled direct/transitive analyzer, opt-out и exact README examples | Package80cases +3README;4nupkgs, analyzer path/SHA/generated initializer и expected CS0246. Library→app/legacy controls не повторены |

JSON failure checks различают serialization error до записи и sharing violation
при открытом read-only handle; после ошибки старое значение/файл доступны,
после освобождения handle запись можно повторить. Failed sync/async refresh не
публикует частично обновлённые providers; recovery и старые section handles проверены.
Обычный Set без имени секции сохраняет default target; invalid GetOrCreate
не изменяет providers. Это текущая regression предыдущего configuration этапа.

Несколько Apps наблюдают global DiagnosticListener по своим prefix filters;
закрытие одной не отключает другую. Same-name источники независимы по identity.
Новые filters действуют при discovery; rebinding прежних sources не добавлен.

Предыдущие результаты Core81, Cobalt comparison/package proof и ранние исправления:
[HISTORY](HISTORY.md), [Cobalt comparison](../cobalt/COMPARISON.md).
Собственные Cobalt/Enerit/Mithril/generator suites, IPC/units, UI/native и benchmarks
текущим проходом не запускались. Independent headless package proof повторён;
старый benchmark graph v1 и его consumer не изменялись/не измерялись. Solution build
не считается passing tests соседних модулей.

## Ограничения

- Shutdown timeout30s кооперативный; user Task, игнорирующий cancellation, может удерживать завершение. Scheduler consumers должны дождаться своих async continuations до закрытия scheduler.
- Microsoft DI сохраняет собственные disposal semantics для aliases и cleanup errors; текущий default — Cobalt, массовая migration отложена.
- GetOrCreate не обещает atomic create при concurrent calls. Null replacement в JSON является синхронной записью и может выбросить файловую ошибку.
- Sharing violation проверена; disk-full/permission injection, crash-safe/atomic file replacement и межпроцессные JSON writes не проверены. Refresh доступен на CommonConfiguration; нового reload API нет.
- Memory из pool может быть manager-backed; partial/foreign/double/stale Return запрещён, старые spans/pins после Return использовать нельзя.
- Linux/macOS, другие SDK/compiler hosts, RID/self-contained/trimming/single-file/NativeAOT и engine workloads не подтверждены.
- Dispose подписки не обещает дождаться callback, уже выполняющегося на другом потоке. Bounded concurrency128sources не является exhaustive race/stress проверкой. Factory удерживает собственные sources до своего Dispose; нового release API нет.
- Собственный headless solution build дал1183warnings; более поздний общий build указан в [общем статусе](../STATUS.md). XML/nullability/analyzer warnings и общий выпуск не закрыты.

## Следующий конкретный шаг

Для текущего diagnostics направления — Editor console/export через Core reader,
затем domain/frame metrics поверх CORE6d и готовой child delivery lane.
App-private routing и
bootstrap capture требуют самостоятельного контракта; общий quota report roots
не выводится из JSONL retention.

Актуальный прикладной маршрут — [engine STATUS](../engine/STATUS.md#следующий-конкретный-шаг).
ARCH1b.1 и ED1a выполнены в своих границах. Прежний UI package/Hosting quality проход завершён в своём
scope, не остаётся текущим prerequisite. Новые общие Core APIs/utilities вводить
только по потребителю; IPC admission/limits и process cleanup развиваются у
Enerit/Mithril. Общий release/platform scope и перечисленные ограничения открыты.
