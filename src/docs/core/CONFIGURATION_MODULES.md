# Общая конфигурация Aether, Editor и модулей — CORE7.5

[Главное руководство](CONFIGURATION_GUIDE.md), [все стандартные ключи](CONFIGURATION_KEYS.md),
[единый App/child delivery runner](../../ManualTests/ConfigurationDelivery/README.md).

Владелец задачи — [Core / CORE7.5](PLAN.md#core7-unified-configuration).
Источники, приоритеты, атомарный снимок и reload использует существующий общий слой:
[profile](CONFIGURATION_USAGE.md), [App stage](CONFIGURATION_APP.md),
[reload/write](CONFIGURATION_RELOAD.md). Старые типизированные `Use*` callbacks
сохраняются для совместимости. Новые `UseConfigured*` явно подключают общий профиль;
скрытое чтение всех args/env каждым модулем не добавлено.

## Схемы и точки подключения

| Схема / section | Точка подключения | Настраиваемые данные |
| --- | --- | --- |
| `AetherConfigurationSchemas.Engine` / `Aether` | `UseConfiguredAether(composition, configure)` | Report/build roots, build timeout/retention, child diagnostics, session/replacement deadlines, GPU validation |
| `.World` / `Aether:Ecs:World` | `UseConfiguredAetherEcs` | Initial capacity, entity/component limits |
| `.Scheduler` / `Aether:Ecs:Scheduler` | `UseConfiguredAetherEcsSystems` | System/phase-command limits; system graph передаётся отдельно |
| `.SceneLoading` / `Aether:Ecs:SceneLoading` | `UseConfiguredAetherSceneLoading` | Entity/component/property/resource admission; registry factory передаётся отдельно |
| `.Primitives` / `Aether:Ecs:Primitives` | `UseConfiguredAetherPrimitiveEcs` | Transform/primitive limits, fixed step, FOV/near/far; Camera только через typed callback/runtime arguments |
| `.RenderResources` / `Aether:RenderResources` | `UseConfiguredAetherRenderResources` | CPU/GPU/staging/lease/preparation budgets |
| `.Authoring` / `Aether:Authoring` | `UseConfiguredAetherAuthoring` | History, batch and operation limits |
| `EditorConfigurationSchemas.Options` / `Editor` | `UseConfiguredAetherEditor` | Initial project path и SourceTracking policy |
| `.Diagnostics` / `Editor:Diagnostics` | `UseConfiguredAetherEditorDiagnostics` | Bounded console/presentation budgets |
| `.SourceEditor` / `Editor:SourceEditor` | `UseConfiguredAetherEditorSourceNavigation` | Explicit executable/argument template; подключать только при нужной функции |
| `.Window` / `Editor:Window` | `UseConfiguredAetherEditorWindow(fonts)` | Standard Editor scalar window settings, default 1280x800, minimum 1100x700 |
| `PipesConfigurationSchema.Default` / `Enerit:Pipes` | `UseConfiguredPipesServiceTransport` | Existing admission, connection and deadline limits |
| `NomConfigurationSchema.Default` / `Nom` | `UseConfiguredNom` | Open-store/queue/batch/document/index limits, drain timeout |
| `WindowConfigurationSchema.Default` / `Alloy:Window` | `UseConfiguredPresentation` | Scalar graphics API/window/frame/chrome settings |

Все перечисленные поля — `StartupOnly`. Новое значение после reload даёт
`RestartRequired`, оставляя прежние reader generation, сервисы и factory limits.
Автоматическое пересоздание окна/renderer/transport/store не выполняется.
Каждое `GetOptions(schema)` возвращает отдельную копию; вложенные mutable данные
также копируются. Применяется прежняя семантическая проверка соответствующего модуля.

`UseConfiguredAetherEditor` требует ровно четыре стандартные schema instances:
Engine, Authoring, Editor Options и Diagnostics. Window/SourceEditor/ECS/Render
подключаются по необходимости. DI выбирает реализацию сервисов, а не приоритет
источников. Binding/configure/validation завершаются до первой регистрации/factory
activation; callbacks конфигурации должны оставаться чистыми.

## Пример composition root

```csharp
var profile = ConfigurationProfiles.Create(builder =>
{
    builder.AddSchema(AetherConfigurationSchemas.Engine);
    builder.AddSchema(AetherConfigurationSchemas.Authoring);
    builder.AddSchema(EditorConfigurationSchemas.Options);
    builder.AddSchema(EditorConfigurationSchemas.Diagnostics);
    builder.AddJsonFile("settings", absoluteSettingsFile, optional: true);
    builder.AddEnvironment("environment", "MYEDITOR_");
    builder.AddArguments("arguments", settingsArguments);
});
await using var app = App.Build(context => context.UseConfiguration(profile)
    .UseConfiguredAetherEditor(new AetherComposition
    {
        HostContractId = hostContractId,
        GameBuildId = gameBuildId,
        RuntimeFactory = runtimeFactory
    }, configureDiagnostics: value => value.MaximumRecords = 256));
```

Например `--Editor:SourceTracking:Enabled=false` и
`MYEDITOR_EDITOR__SOURCETRACKING__ENABLED=false` относятся к одной schema field;
JSON содержит вложенный объект `Editor.SourceTracking.Enabled`.
Приоритет: schema defaults → files → environment → arguments → profile overrides;
typed configure callback завершает подготовку data options.
Исходное значение и provenance доступны через `Read`, подготовленное — через
`GetOptions`. Пример не запускает native window: для этого отдельно подключаются
Window schema, fonts и соответствующий window helper.

## Граница библиотек и ownership

`AetherSettings` содержит данные; `AetherComposition` содержит вычисленные identity,
runtime/viewport factories и callbacks. Прежние `AetherOptions` собираются из этих
двух частей внутри существующего App registration. Ресурсные factories не
вызываются при schema bind. Окно, system graph, asset-store roots/IDs и scene
documents остаются явной композицией или типизированными runtime arguments.
Selection, viewport/camera state, Undo/Redo и Build/Play captures не стали providers.
Существующие правила stop/drain/dispose и владения factory products сохранены.

Для определения схем модули используют нейтральные
`ConfigurationSchemaDefinitions.Create<T>` и `ConfigurationModuleBinding.Configure`
из Contracts. Это чистые schema/conversion utilities и обращение к optional
`IAppConfigurationStage`; чтения ОС, providers и реализации App/DI там нет.
Прежний публичный `ConfigurationSchemas.Create<T>` остаётся фасадом в TrueMoon,
с прежней сигнатурой и assembly identity. Editor, Enerit, Nom и Alloy.Hosting не
получили production dependency на bootstrap TrueMoon. Hosting.Tests ссылается на
bootstrap отдельно, поскольку проверяет настоящий App.

## Pre-App runtime и совместимость CLI

`RuntimeBootstrap.ReadEnvironment()` через общий selected capture читает только
`TM_ENGINE_BOOTSTRAP`, `-p`, `TrueMoon.Mithril.ControlPipe` и два public flag names.
`ParseEnvironment(explicitCapture)` не читает ОС и не строит App/DI. Прежние
ограничения envelope: 16384 символа, JSON depth32, строгие parent/role/version/
runtime/game/build identity проверки. Envelope — протокол owned child, отдельный
от обычной JSON-конфигурации пользователя.

Новый путь `UseConfiguredAetherRuntime(bootstrap)` использует frozen parent/control,
launch identity, report directory и child diagnostic delivery. Обычные profile
sources и configure callback не подменяют эти launch inputs. Старые overloads
`UseAetherRuntime(launch, parentId)`/Preview сохранены: выбранные flags/control
снимком читаются при регистрации, затем runtime их не перечитывает.

`RuntimeStartupSettings` принимает legacy `TRUEMOON_GPU_VALIDATION` (1/0 или
true/false) и canonical `TRUEMOON_AETHER_RUNTIME__GPUVALIDATION`; canonical побеждает.
Результат неизменяемый, передаётся host factory/host и
`NativeRuntimeLaunch.WithStartupSettings`. Parent не меняет глобальное окружение;
значения записываются только в `ProcessStartInfo.Environment` нового owned child.
Это явный bootstrap adapter, а не второе правило приоритетов каждого сервиса.

`AetherEditorHost.EditorLaunchOptions` использует общий schema parser с
`ParseSingleUse`. Сохранены прежние `--project`, `--check-project`, `--check-scene`,
`--smoke`, `--assembly`, `--entry`, `--light`, `--validation` и smoke aliases,
проверки extensions и совместимости режимов. Поддержаны inline `=`, canonical
`--EditorLaunch:ProjectPath` и явные boolean false. Повтор scalar/flag, включая
разные aliases одного ключа, отклоняется; обычные `Parse`/`ParseKnown` сохраняют
last-wins. Ошибки не включают чувствительные исходные значения.
Editor фиксирует presence validation отдельно: без CLI наследует selected legacy/
canonical env, явные --validation=true/false побеждают даже invalid lower env.
ResolveRuntimeSettings объединяет уже captured inputs до App; глобальный env
не меняется. Programmatic Validation=true также остаётся явным выбором.

## Остальные чтения и проверки

Source audit не нашёл прямого `GetEnvironmentVariable(s)`/`GetCommandLineArgs`
в Aether/Editor/Enerit/Nom/Alloy/Argentis/Hosting/platform/rendering, Core utilities,
Cobalt и generators. OS reads остаются в общих providers/capture. PID, process path,
system folders и platform capabilities — системные сведения. Child environment
serialization и изолированное compiler-job environment остаются явными adapters.
JSON проектов/сцен/ассетов, build manifests и документы Editor являются форматами
данных, а не providers настроек. CLI остальных probes/benchmarks выбирает сценарии,
нагрузку и артефакты измерений; это ограниченные executable harness adapters.

Новый [независимый consumer](../../ManualTests/ConfigurationModulesConsumer/README.md)
проверяет source Debug, published Release и свежие packages на обоих DI, без
native window/GPU/child runtime. Точные test names, команды, результаты и ограничения
текущего запуска находятся в [HISTORY](HISTORY.md#core75-2026-11-10) и
[матрице110новых test cases](CONFIGURATION_MODULE_TESTS.md).
Hardware/native/release gates не выводятся из headless configuration acceptance.
