# Общая конфигурация Core и Mithril

CORE7.4, 2026-11-10. [App stage](CONFIGURATION_APP.md),
[источники](CONFIGURATION_USAGE.md), [reload/write](CONFIGURATION_RELOAD.md),
[единственный план](PLAN.md#core7-unified-configuration).

## Core: явное типизированное подключение

```csharp
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril;

var file = Path.GetFullPath("user-settings.json");
await using var app = App.Build(context =>
{
    // Объявление после Setup/legacy callbacks позволяет явно импортировать их снимок.
    context.Configuration(legacy => context.UseConfiguration(ConfigurationProfiles.Create(profile =>
    {
        profile.AddSchemas(); // точные CoreConfigurationSchemas.App и .Paths
        profile.AddSchema(DiagnosticsConfigurationSchema.Default);
        profile.AddLegacySources(legacy);
        profile.AddJsonFile("user", file, optional: true);
        var keys = CoreConfigurationSchemas.App.Keys.Concat(CoreConfigurationSchemas.Paths.Keys)
            .Concat(DiagnosticsConfigurationSchema.Default.Keys).Select(key => key.Path).ToArray();
        profile.AddEnvironment("public-env", "TRUEMOON_", keys);
        profile.Override("Diagnostics:MemoryCapacity", 1024);
    })));
    context.UseCoreConfiguration();
    context.UseConfiguredDiagnostics(configure: settings => settings.ShutdownTimeout = TimeSpan.FromSeconds(3));
    context.AddUnit(_ => { }, unit => unit.ExecuteInMainProcess());
});
await app.StartAsync();
await app.StopAsync();
```

App builder с Microsoft DI adapter использует тот же context/profile без смены
источников, aliases или lifetime. AddSchemas не читает ОС и не добавляет источники
неявно. UseCoreConfiguration требует обе точные standard schemas; произвольная
схема с тем же section/type не подменяет контракт модуля.

| Канонический ключ | Потребитель | Совместимость |
| --- | --- | --- |
| App:Name | AppSettings и private App name snapshot | AddLegacySources импортирует appName из default/env/args; CLI appName=value нормализуется явно |
| Paths:Root | PathSettings → IPathResolver | CLI -root=value, -root value, --Paths:Root=value, --Paths:Root value; последний alias одного поля побеждает |
| Diagnostics:* | DiagnosticsSettings → DiagnosticPipelineOptions | UseConfiguredDiagnostics; прежние UseDiagnostics overloads остаются |

Порядок источников стандартный: defaults → files → environment → arguments →
overrides, затем typed configure и validation. AddLegacySources импортирует только
описанные aliases из named sections; произвольные объекты/custom sections не
копируются. Внутри уровня последний source побеждает. Legacy env -root не стал
новым alias: прежний PathResolver читал только CLI. Новый prefixed Paths:Root
включается обычным выбранным environment provider.

CommandLineArgsProvider сохраняет original token capture, одновременно оставляя
старую dictionary projection args. Common ParseKnown поддерживает separate values,
negative numeric values, aliases и delimiter --. Legacy Set после capture отмечается
явно, включая null/same-value, и адаптер применяет это переопределение. Capture
выполняется один раз на provider refresh; bootstrap не перечитывает аргументы ОС.
Dictionary-only compatibility sections остаются допустимыми bounded adapters.

App.Name фиксируется при UseCoreConfiguration. Изменение legacy appName или
внедрённого AppSettings после Build его не меняет. PathResolver получает отдельную
копию и не зависит от мутации внедрённого PathSettings до первого resolution.
Без UseCoreConfiguration прежнее dynamic App.Name сохраняется; наличие любого
пользовательского App:Name в raw profile само по себе не включает стандартный Core.

Root=null выбирает LocalApplicationData/company/product. Secrets остаётся в
CommonApplicationData/company/product. Метаданные entry assembly, OS folders и PID
являются фактами ОС. PathResolver(arguments) использует общий selected parser и
schema/profile до App/DI, PathResolver(PathSettings) принимает готовые options.
Неверный -root сохраняет ArgumentException старого constructor; profile validation
сообщает безопасный ConfigurationException без исходного пути/значения.

## Diagnostics: данные отдельно от ресурсов

DiagnosticsSettings включает все data defaults DiagnosticPipelineOptions:
minimum level, queue/memory budgets, truncation limits, shutdown timeout, JSON
directory/rotation/run retention, source/sink thresholds, disabled events,
emergency limits и operation telemetry. Вложенные paths имеют вид
Diagnostics:JsonRetention:MaxRuns, Diagnostics:Emergency:Enabled,
Diagnostics:Telemetry:MetricsEnabled. Enum maps используют явный codec с именами
уровней; duplicate entries и invalid enum/limit не скрываются fallback.
Maps/arrays копируются независимо для defaults, reader и injected settings.

Sink factories, listeners и borrowed emergency TextWriter передаются отдельно
через UseConfiguredDiagnostics(action, configure, sinkFactories, emergencyWriter).
Они не сериализуются в configuration. Factory products принадлежат pipeline,
factory/writer заимствуются. Data Snapshot validation выполняется до register/DI;
factory metadata проверяется при composition до открытия sink/file/worker.
Current.GetOptions и реальные
pipeline/telemetry получают соответствующие подготовленные значения.

Активация DiagnosticSubscription выбирается регистрацией DI. IDiagnosticsConfiguration
тоже зарегистрирован через DI. Старый object-bag ключ DiagnosticsConfiguration
сохраняется как compatibility mirror только прежним UseDiagnostics; новый путь
не использует его для выбора ресурсов. Legacy listeners/repeated UseDiagnostics
сохраняют прежнее поведение.

Все standard Core/diagnostics fields StartupOnly. Reload проверяет кандидата, но
при изменении любого такого поля возвращает RestartRequired и сохраняет прежние
Current/options/resources. Применение нового имени, путей и pipeline требует нового
App. Их наличие не является обещанием live resource reconfiguration.

## Mithril: отдельный защищённый launch snapshot

AddUnit готовит UnitLaunchSettings один раз до unit actions/DI/resources. Handler
получает typed snapshot; old IConfiguration constructor остаётся bounded adapter.
UnitLaunchSettings immutable, содержит UnitId, ParentProcessId, ControlPipe и
UnitsDisabled. Локальные unit definitions/options, hosting/restart/lifetime callbacks
не превращаются в providers: их прежнее Snapshot validation сохраняется.

| Вход | Правило |
| --- | --- |
| -u, -p, TrueMoon.Mithril.ControlPipe в environment | Защищённая identity group. Любое присутствующее non-null поле выбирает всю группу; CLI/default identity не дополняет её |
| -u/-p или --Mithril:UnitId/ParentProcessId/ControlPipe | Bootstrap aliases, если environment group отсутствует; common selected parser |
| -ud=1/0, -ud=true/false, --Mithril:Disabled | Управление parent unit declarations; bare flag допускает common parser |
| TRUEMOON_MITHRIL__DISABLED | Пользовательский control; CLI имеет больший приоритет |
| Явные default.Set(-u/-p/control pipe) | Адаптер старого composition root, когда нет authoritative environment group |

UnitId должен быть положительным; parent положительный и отличается от текущего
PID; endpoint не содержит control/path characters, максимум256 и требует parent.
UnitId без parent допустим как прежний unmanaged child selection; partial env
не подбирает parent из CLI. Неверное выигравшее значение не заменяется младшим.
Parse/Read возвращают безопасный ConfigurationException. AddUnit сохраняет
ArgumentException boundary для invalid/unknown unit selection.

```csharp
var launch = UnitBootstrap.Parse(ConfigurationCommandLine.Capture(),
    ConfigurationEnvironment.Capture("-u", "-p", UnitParentProcessEventsHandler.ControlPipeKey,
        "TRUEMOON_MITHRIL__DISABLED"));
// Нет App/DI, factory activation, listeners или IO внутри Parse.
```

Обычный App profile не участвует в identity: даже reader с пользовательским
Mithril:UnitId не меняет выбранный unit/parent/pipe. CommonConfiguration предоставляет
optional IConfigurationBootstrapInputs — исходные sections стандартных providers
до JSON/custom overlays. Файл env.json/args.json/default.json не становится trusted
launch envelope. Удаление стандартного provider не вызывает нового OS capture.
Custom IConfiguration без optional capability явно предоставляет trusted bounded
sections как compatibility adapter; framework не может проверить его provenance.

ProcessUnitHandle по-прежнему формирует environment из typed unit/parent data и
создаёт новый control endpoint на каждый restart. Parent monitor остаётся borrowed,
pipe/tasks owned listener. Start/stop/restart/failed startup/kill/reap ownership не
переработаны. Frozen launch не меняется при Set/Refresh/reload. Legacy
IsProcessingUnit/GetProcessingUnit* helpers остаются compatibility API; runtime
selection и parent handler больше их не используют.

Mithril теперь ссылается на TrueMoon для уже общего concrete parser/schema/profile,
сохраняя Contracts/Core. Цикла зависимостей нет, mandatory члены existing interfaces
не добавлены. В fresh consumer остаются6пакетов: Contracts/Core/Cobalt/TrueMoon/
MS DI adapter/Mithril. Общая версия не меняется автоматически.

## Проверки и границы

[Core tests](../../TrueMoon.Tests/Configuration/CoreConfigurationMigrationTests.cs),
[bootstrap tests](../../TrueMoon.Tests/Configuration/BootstrapConfigurationTests.cs),
[Mithril tests](../../TrueMoon.Mithril.Tests/UnitBootstrapConfigurationTests.cs).
[Независимый consumer](../../ManualTests/ConfigurationAppConsumer/Program.cs) и
[runner](../../ManualTests/ConfigurationAppConsumer/Verify.ps1) проверяют source,
published и fresh packages, оба DI и настоящий owned child: env identity против
CLI/user-profile spoof, parent/control pipe, graceful exit/stop marker и reap.
Фактические версии/числа/команды — [CORE7.4 history](HISTORY.md#core74-2026-11-10).

CORE7.4 не переводит Aether RuntimeBootstrap, Editor/прочие module options и не
закрывает MIT6 containment/resource trends, native/GPU/hardware release gates.
Это следующий scope CORE7.5 у соответствующих владельцев.
