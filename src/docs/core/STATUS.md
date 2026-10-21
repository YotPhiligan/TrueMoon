# Core: состояние и точка продолжения

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

По [общей сводке](../QUALITY.md) следующий этап — UI package/Hosting proof на
текущих Core/Cobalt с explicit fonts. Детальные задачи — в [UI плане](../alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15).
Enerit ENE5 и Mithril MIT5 завершены в собственных bounded scopes; Core не требует
новых APIs или переписывания utilities для начала UI quality этапа.
