# Конфигурация: карта использования и контракт CORE7.0

Для текущего использования начните с [рабочего руководства](CONFIGURATION_GUIDE.md)
и [каталога139ключей](CONFIGURATION_KEYS.md). Эта страница сохраняет исходную
карту и датированные переходы; старые строки не являются текущей очередью.

Сверено 2026-11-10 по текущим исходникам. Владелец — Core; детальная задача —
[CORE7](PLAN.md#core7-unified-configuration). CORE7.0 завершён как проектный этап:
карта, [точные сигнатуры/семантика API и три примера](CONFIGURATION_API.md) зафиксированы.
Standalone часть реализована в [CORE7.1](CONFIGURATION_USAGE.md), [App stage/reader](CONFIGURATION_APP.md)
в CORE7.2, [reload/changes/write](CONFIGURATION_RELOAD.md) в CORE7.3; [Core/Mithril migration](CONFIGURATION_CORE_MITHRIL.md) — CORE7.4, [Aether/Editor и модули](CONFIGURATION_MODULES.md) — CORE7.5. Исходная карта ниже фиксирует legacy пути
до CORE7.1, новые APIs перечислены отдельно. Общая версия не менялась.

<a id="scope"></a>
## Область сверки

Базовый HEAD: `2e3b2b1a00d67ef10d44e8c8deab1d811584d42d`; рабочая копия содержит
незакоммиченные изменения. Карта описывает именно прочитанные файлы, включая
предыдущие изменения ED5b/observer/render snapshots, а не только этот commit.
Studio/Tales и другие checkout вне `src` не исследованы.

Для исходной карты до CORE7.1 перечислены 1515 C# файлов без `bin`, `obj`, `TestResults` и `*.Designer.cs`;
833 относятся к production-проектам `TrueMoon*` без `.Tests`. Поиск охватил
`IConfiguration`, `IConfigurable`, providers, `GetCommandLineArgs`,
`GetEnvironmentVariable(s)`, `SetEnvironmentVariable`, передачу child environment,
App setup, options и собственные CLI parsers. Прочитаны реализации найденных
providers/consumers и точки копирования/проверки options. Примеры, ManualTests
и Benchmarks перечислены отдельно; их чтения не включены в production-счётчик.

Дополнительный поиск `getenv`/`GetEnvironmentVariable`/`GetCommandLine` в доступных
локальных C/C++ исходниках без vendor/third_party не дал совпадений. Сценарии
PowerShell проверены как launch infrastructure по известным ключам, не как полный
аудит build/native tooling. Содержимое реального окружения и секреты не читались.
Это статическая сверка, без сборок, запуска тестов или доказательства отсутствия
любого динамического/reflection consumer.

После CORE7.1 добавлены29production файлов; текущее перечисление —1548C# всего,
862production. Прежние1515C# hashes не изменились. Два добавочных OS-reader sites:
[Capture](../../TrueMoon/Configuration/ConfigurationEnvironment.cs), строка17,
GetEnvironmentVariable по явным именам; [env source](../../TrueMoon/Configuration/ConfigurationEnvironmentSource.cs),
строка43, GetEnvironmentVariables внутри общего provider. Теперь выбранных API
8calls/7files; CORE7.2/3 не добавили OS-reader sites, прежний ledger6/5 ниже относится к исходной карте. Никаких новых
readers в прикладных services не добавлено. [Рабочее API](CONFIGURATION_USAGE.md).
После CORE7.2/3 перечисление было:1574C# всего,883production; исходные counts
выше относятся к исходной карте/CORE7.1. CORE7.3 сохраняет1552из1560baseline hashes.

<a id="current-sources"></a>
## Текущие источники и порядок сборки

Общие интерфейсы находятся в [TrueMoon.Contracts/Configuration](../../TrueMoon.Contracts/Configuration/IConfiguration.cs),
реализации — в [TrueMoon/Configuration](../../TrueMoon/Configuration/ConfigurationBuilder.cs).
Это собственная конфигурация TrueMoon. Microsoft DI adapter не добавляет
`Microsoft.Extensions.Configuration` или `IOptions<T>`.

| Источник/операция | Как подключается сейчас | Ключи, приоритет и побочные эффекты |
| --- | --- | --- |
| [CommandLineArgsProvider](../../TrueMoon/Configuration/CommandLineArgsProvider.cs) | Автоматически в каждом ConfigurationBuilder; также принимает функцию аргументов без executable | Раздел `args`, выше env/default. Деление по первому `=`; `-x=a=b` даёт ключ `-x` и значение `a=b`. Префиксы сохраняются, отдельный токен без `=` даёт null; повтор ключа заменяет предыдущее значение. Нет схемы, `--key value`, отдельной семантики `--` или boolean flags |
| [EnvironmentVariablesProvider](../../TrueMoon/Configuration/EnvironmentVariablesProvider.cs) | Автоматически; снимок всех process variables при GetSections | Раздел `env`, ниже args. Исходные имена, без префикса/allowlist и преобразования `__`; внутренний Dictionary сравнивает ключи с учётом регистра |
| [DefaultConfigurationProvider](../../TrueMoon/Configuration/DefaultConfigurationProvider.cs) | Автоматически | Раздел `default`; `appName` из entry assembly, иначе `TrueMoon`. Provider удерживает одну изменяемую секцию |
| [JsonConfigurationProvider](../../TrueMoon/Configuration/JsonConfigurationProvider.cs) | Только явный AddProvider с IPathResolver; App сам JSON и PathResolver не регистрирует | Все `*.json` верхнего уровня Paths.Configuration, сортировка Ordinal; имя файла — раздел. Отсутствующий каталог даёт пустой набор; ошибки чтения/JSON распространяются. Новый provider попадает выше args/env/default |
| Собственный provider | IAppBuilder.Configuration → AddProvider | Последний добавленный provider первый; повтор того же экземпляра перемещает его, RemoveProvider удаляет по типу либо экземпляру. Build фиксирует массив providers, последующая правка builder не меняет уже построенный список |
| IAppConfigurationContext.Configuration | Отложенный callback после всех Setup, перед регистрациями resolver | Работает с уже построенным IConfiguration. Set без раздела пишет в default; это **не** гарантированное старшее переопределение относительно args/env/custom provider |
| Типизированный callback UseModule | Обычно исполняется непосредственно во время Setup | Модуль создаёт options, копирует/проверяет их и замыкает результат в DI factories. Общего binding из IConfiguration в эти options сейчас нет |

[AppBuilder.Build](../../TrueMoon/AppBuilder.cs) выполняет:
ConfigurationBuilder callbacks → Build конфигурации → базовые регистрации →
все Setup callbacks → все context.Configuration callbacks → service registrations
с тем же IConfiguration → сборка выбранного resolver → диагностика/создание App.
[Cobalt](../../TrueMoon.Cobalt/CobaltServiceResolverBuilder.cs) и
[Microsoft adapter](../../TrueMoon.Extensions.DependencyInjection/ServiceResolverBuilder.cs)
получают один и тот же контракт; выбор DI не выбирает Args или EnvironmentVars.
В Mithril UnitContext.Configuration выполняется сразу внутри Prepare, а не добавляет
ещё один callback в основной список: порядок этих вложенных действий тоже учитывать.

### Семантика чтения, изменения и перечитывания

- [Get/Exist/indexer](../../TrueMoon.Contracts/Configuration/ConfigurationExtensions.cs)
  без раздела ищут ключ во всех секциях в порядке providers. Ключ — буквальная
  строка, включая `:` и префиксы. Секция/ключ сравниваются с учётом регистра.
- [GetSection(null/whitespace)](../../TrueMoon/Configuration/CommonConfiguration.cs)
  выбирает только `default`; одноимённые секции объединяются OverlaySection.
  Это отличается от глобального поиска Get. В Overlay Set пишет в первую секцию,
  даже если прочитанное значение было найдено ниже.
- [ConfigurableBase](../../TrueMoon.Contracts/Configuration/ConfigurableBase.cs)
  уже умеет invariant conversion, nullable, enum и JSON→объект. Недопустимое старшее
  значение не вызывает fallback: Get бросает FormatException, TryGetValue даёт false.
  Нет общей схемы ключей и semantic validation типизированных options.
- Get без явного ключа использует `typeof(T).Name`. GetOrCreate сохраняет identity
  существующего объекта; при null создаёт объект в выигравшей секции, при отсутствии
  — в default. Через JSON это может выполнить запись при чтении.
- [JsonConfigurationSection.Set](../../TrueMoon/Configuration/JsonConfigurationSection.cs)
  синхронно сериализует и перезаписывает файл, затем меняет память. При ошибке записи
  прежнее значение в памяти остаётся; это не обещание атомарной замены файла на диске.
- CommonConfiguration уже собирает новый массив секций перед публикацией и при
  ошибке provider сохраняет предыдущий. Refresh/RefreshAsync есть на реализации,
  не на IConfiguration. Это не глубокий immutable snapshot: секции/объекты изменяемы,
  default переиспользуется, уже выданные ссылки продолжают жить. Нет общего
  bind/validate/reload поколения options, подписок и filewatch.

<a id="consumer-map"></a>
## Карта production-потребителей

| Потребитель и точка входа | Что читает/передаёт сейчас | Категория и переход |
| --- | --- | --- |
| [DefaultApp](../../TrueMoon/DefaultApp.cs) | IConfiguration доступен через IApp.Configuration; Name каждый раз читает `appName` | Настройка App; CORE7.4. Сохранить публичный доступ и legacy alias, определить startup snapshot имени |
| [UseDiagnostics](../../TrueMoon/Diagnostics/AppCreationContextExtensions.cs), [diagnostic extension](../../TrueMoon.Contracts/Diagnostics/ConfigurationExtensions.cs), AppBuilder | Кладёт объект IDiagnosticsConfiguration в default под `DiagnosticsConfiguration`; AppBuilder по наличию ключа активирует subscription. Pipeline options копируются отдельно, factories используют замыкания | Смешаны служебный object bag и настройки; CORE7.4. Экземпляры, listeners и factories остаются DI/callbacks. IDiagnosticConfigurationReader описывает диагностический pipeline, не является общим reader настроек App |
| [PathResolver](../../TrueMoon/Configuration/PathResolver.cs) | Независимо разбирает последнее точное `-root=`. Иначе LocalApplicationData/company/product; Secrets — CommonApplicationData. Entry assembly и ProcessPath нужны для метаданных | Root — пользовательский bootstrap параметр; OS folders/company/product — системные сведения. CORE7.4, общий pre-App parser без зависимости от resolver |
| [AddUnit/Prepare](../../TrueMoon.Mithril/AppCreationContextExtensions.cs), [UnitsConfigurationExtensions](../../TrueMoon.Mithril/UnitsConfigurationExtensions.cs) | Глобальные Get для `-u`/`-p`; `-ud=1` только в разделе args. Выбор child/main графа, unit-local callbacks. [UnitConfiguration](../../TrueMoon.Mithril/UnitConfiguration.cs) использует собственный ConfigurableBase и [валидацию копии](../../TrueMoon.Mithril/UnitConfigurationSnapshot.cs) | `-ud` — управление запуском; `-u/-p` — служебная identity. CORE7.4, не превращать все IConfigurable в публичные settings |
| [UnitParentProcessEventsHandler](../../TrueMoon.Mithril/UnitParentProcessEventsHandler.cs) | IConfiguration в конструкторе; захватывает parent ID и `TrueMoon.Mithril.ControlPipe` | Служебный bootstrap, без чтений на каждый tick. CORE7.4: typed launch snapshot, сохранить borrowed parent и stop/drain/pipe lifecycle |
| [UseAether](../../TrueMoon.Aether/AppConfigurationExtensions.cs), [AetherOptions](../../TrueMoon.Aether/Hosting/AetherOptions.cs) | Callback → Snapshot; identity, report/build paths, deadlines, retention, child diagnostics, runtime/viewport factories | Публичные limits/paths отделить от вычисленных identity и DI delegates; CORE7.5. Автоматического env/CLI binding нет |
| [RuntimeBootstrap](../../TrueMoon.Aether/Hosting/RuntimeBootstrap.cs), [UseAetherRuntime](../../TrueMoon.Aether/RuntimeConfigurationExtensions.cs) | До App читает `TM_ENGINE_BOOTSTRAP` и env `-p`; Parse отдельно доступен. JSON launch: identity, endpoint, role, build/report, restore, diagnostics; runtime registration передаёт launch в DI и подключает parent handler через IConfiguration | Защищённый process boundary; CORE7.5. Сохранить предел 16384 символов/глубину 32, parent/version/build identity и selected-game/preview проверки |
| [AetherRuntimeHost](../../TrueMoon.Aether/Runtime/AetherRuntimeHost.cs) | Прямой env `TRUEMOON_GPU_VALIDATION == "1"` при создании GPU producer | Публичная настройка запуска, а не системный факт. CORE7.5: typed rendering/launch option; не читать изменяемый env после начала работы |
| [Aether ECS](../../TrueMoon.Aether/EcsConfigurationExtensions.cs), [scene loading](../../TrueMoon.Aether/EcsSceneLoadingConfigurationExtensions.cs), [primitive ECS](../../TrueMoon.Aether/PrimitiveEcsConfigurationExtensions.cs), [render resources](../../TrueMoon.Aether/RenderResourceConfigurationExtensions.cs), [authoring](../../TrueMoon.Aether/AuthoringConfigurationExtensions.cs) | Callback/options → Snapshot: WorldOptions, scheduler, load limits, simulation, resource/snapshot budgets, authoring limits. Системы/схемы и ресурсы регистрируются отдельно | Уже пригодный typed setup; CORE7.5 добавляет источник данных перед существующей проверкой. RenderViewConfiguration/camera/entity/revisions — runtime аргументы, не ambient settings |
| [UseAetherEditor](../../TrueMoon.Aether.Editor/AetherEditorConfigurationExtensions.cs), [AetherEditorOptions](../../TrueMoon.Aether.Editor/AetherEditorOptions.cs) | ProjectPath и SourceTracking копируются; configureEngine получает AetherOptions | Начальные settings Editor; CORE7.5. После запуска выбранный проект/сцена, Build/Play captures и UI state остаются состоянием приложения |
| [Editor diagnostics](../../TrueMoon.Aether.Editor/Diagnostics/DiagnosticsConfigurationExtensions.cs), [window](../../TrueMoon.Aether.Editor/EditorWindowConfigurationExtensions.cs), остальные *ConfigurationExtensions Editor | Copied diagnostics/source-editor options; fonts/window setup и typed view factories. Projects/documents/editing/hierarchy/header/statusbar подключают сервисы, а не providers | Scalar limits/executable path могут получать общие defaults; fonts, factories, commands, views и selection не становятся ключами конфигурации |
| [PipesServiceTransport](../../TrueMoon.Enerit/IO/Pipes/EneriteConfigurationContextExtensions.cs), [UseInvocationService](../../TrueMoon.Enerit/AppCreationContextExtensions.cs) | PipesTransportOptions.Snapshot, endpoint Func<Type,string>, factory registrations; generic IConfiguration не читается | CORE7.5: limits/timeouts — typed settings, endpoint resolver/handlers — DI. Старый MemoryMappedFiles configuration extension полностью закомментирован, активным provider не считается |
| [Alloy.Hosting](../../TrueMoon.Alloy.Hosting/AppConfigurationExtensions.cs), [AlloyOptions](../../TrueMoon.Alloy.Hosting/AlloyOptions.cs), [Compatibility](../../TrueMoon.Alloy.Hosting/Compatibility/AppCreationContextExtensions.cs) | Renderer/window callbacks → Snapshot/Validate; legacy PresentationConfiguration создаётся и передаётся Instance без общего binding | CORE7.5: явная схема scalar window settings; renderer/backend/font factories остаются composition. Theme/styles/control values относятся к UI. У Alloy/Argentis/platform/rendering нет собственных args/env settings readers в найденных production-кодах |
| [UseNom](../../TrueMoon.Nom/AppConfigurationExtensions.cs), [NomOptions](../../TrueMoon.Nom/Hosting/NomOptions.cs) | Callback → immutable NomSettings, bounded store/queue/batch/document/index/drain limits. Открытие/restore задаются typed factory arguments | CORE7.5: limits — settings; project/store IDs, каталоги отдельной операции, asset bytes и backup manifests — данные/аргументы. Собственных IConfiguration/args/env settings readers не найдено |
| Cobalt, generator, Microsoft DI adapter | IConfiguration только в composition contract; выбирают реализации/lifetime, не источники настроек | Общий контракт независим от DI. Generator output/hints и MSBuild props не мигрируют в runtime configuration |

### Прямые чтения args/env и передача дочернему процессу

Production ledger: **6 мест вызова в 5 файлах**. Из одного перечня получаются
2 GetCommandLineArgs, 1 GetEnvironmentVariables, 3 GetEnvironmentVariable,
0 SetEnvironmentVariable. Это счётчик выбранных API, не всех Environment/IO вызовов
и не число дефектов: первые providers уже являются штатной границей чтения ОС.

| Место (строка на дату сверки) | API/ключ | Роль и требуемый механизм |
| --- | --- | --- |
| [CommandLineArgsProvider.cs](../../TrueMoon/Configuration/CommandLineArgsProvider.cs), 8 | GetCommandLineArgs | Сохранить явный ввод args, общий parser в CORE7.1 |
| [EnvironmentVariablesProvider.cs](../../TrueMoon/Configuration/EnvironmentVariablesProvider.cs), 11 | GetEnvironmentVariables | Общий env provider с выбранным профилем/префиксом |
| [PathResolver.cs](../../TrueMoon/Configuration/PathResolver.cs), 12 | GetCommandLineArgs | Общий bootstrap parser, CORE7.4 |
| [RuntimeBootstrap.cs](../../TrueMoon.Aether/Hosting/RuntimeBootstrap.cs), 20 | GetEnvironmentVariable(TM_ENGINE_BOOTSTRAP) | Явный защищённый reader, CORE7.5 |
| RuntimeBootstrap.cs, 21 | GetEnvironmentVariable(-p) | Тот же typed bootstrap, сохранить identity checks |
| [AetherRuntimeHost.cs](../../TrueMoon.Aether/Runtime/AetherRuntimeHost.cs), 62 | GetEnvironmentVariable(TRUEMOON_GPU_VALIDATION) | Typed startup option, CORE7.5 |

Отдельно четыре записи bootstrap keys в ProcessStartInfo.Environment:
[ProcessUnitHandle](../../TrueMoon.Mithril/Units/ProcessUnitHandle.cs), строки 119/120/122,
пишет `-u`, `-p`, `TrueMoon.Mithril.ControlPipe`; [NativeRuntime](../../TrueMoon.Aether/Hosting/NativeRuntime.cs),
строка 102, пишет `TM_ENGINE_BOOTSTRAP`. Источник — typed unit/launch data; на каждый
restart создаётся свежая identity/control pipe. Это сериализация границы процесса,
не запись пользовательской конфигурации. Fault probe `ignore-stop/hang-stop` явно
удаляет control pipe и должен остаться только своим режимом проверки.

[GameBuildPreparation](../../TrueMoon.Aether/Builds/GameBuildPreparation.cs) очищает
child environment и задаёт изолированные SystemRoot/PATH/temp/home/DOTNET/NuGet/
MSBuild значения. Это воспроизводимость compiler job, не configuration provider;
generic env merge при миграции запрещён. ProcessId, ProcessPath, special folders,
CurrentManagedThreadId/Is64BitProcess в lifecycle, UI и GPU — системные сведения,
не кандидаты на механическую замену IConfiguration.

<a id="hosts"></a>
## Примеры, ручные consumers и инструменты

Таблица группирует вспомогательные CLI по владельцу/назначению; она не объявляет
публичными все flags каждого probe. Синтаксический scanner можно переиспользовать,
схемы команд и ограничения fixture сохраняются у инструмента.

| Точки входа | Текущее поведение и статус перехода |
| --- | --- |
| [Examples/AetherEditor](../../Examples/AetherEditor/Program.cs) | Собственный parser `--runtime/--project/--font/--report`, пути, запрет дублей/unknown/missing; затем UseAetherEditor и window setup. `--runtime` здесь **путь executable**, а не boolean child flag. CORE7.5: единая грамматика, прежняя схема и ошибки совместимости |
| [Examples/AetherRuntimeHost](../../Examples/AetherRuntimeHost/Program.cs), [Manual Editor Program](../../ManualTests/AetherEditorHost/Program.cs), [launch options](../../ManualTests/AetherEditorHost/EditorLaunchOptions.cs) | RuntimeHost принимает `--runtime/--console-diagnostics`, затем RuntimeBootstrap; Manual Editor сначала help/runtime ветки, далее свой parser project/check-project/check-scene/assembly/entry/light/validation/smoke и профили документов/transform/hierarchy/inspector/camera/navigation/viewport/native-input/translation. Дубли запрещены; несовместимые режимы проверяются. Общий parser не объединяет схемы разных executables |
| [EditorApplication](../../ManualTests/AetherEditorHost/EditorApplication.cs) | `--validation` пишет process env TRUEMOON_GPU_VALIDATION=1; CORE7.5 заменить передачей typed launch настройки, проверить наследование дочернему процессу |
| [EngineSessionProbe](../../ManualTests/EngineSessionProbe/Program.cs), [AetherPackageConsumer runtime](../../ManualTests/AetherPackageConsumer/Consumer/Runtime/Program.cs) | Pattern-matching CLI сценариев/путей и protected runtime branch. Probe readers дополнительно видят bootstrap, GPU validation; Measurements/BaselineCapture использует TRUEMOON_FRAME_BASELINE. Последнее — instrumentation, не публичная настройка engine |
| [AlloyTest](../../ManualTests/AlloyTest/Program.cs), [AlloyVulkanTest](../../ManualTests/AlloyVulkanTest/Program.cs) и их probes | Собственные backend/validation/native DLL/window/style/settings/performance/trace CLI. Параметры golden/soak/acceptance — команды проверки; слово Settings в имени не означает отдельный production settings store |
| [AlloyFactoryConsumer](../../ManualTests/AlloyFactoryConsumer/Program.cs), [AlloyPackageConsumer](../../ManualTests/AlloyPackageConsumer/Program.cs), [RichTextEditorTest](../../ManualTests/RichTextEditorTest/Program.cs) | Позиционные provider/backend/profile/report. Сохранять CLI опубликованных consumers; optional shared parser не меняет смысл полей |
| [CorePackageConsumer](../../ManualTests/CorePackageConsumer/ConsumerProbe.cs), [Graph](../../ManualTests/CorePackageConsumer/Graph.cs) | Явные JSON/custom providers, Answer/Checkpoint/ConsumerOptions, object identity, GetOrCreate с записью JSON, failed refresh/recovery; generic IConfiguration внедрён как часть legacy proof. Это обязательная совместимость CORE7, не новый образец прикладных services |
| [MithrilCompositionProbe](../../ManualTests/MithrilCompositionProbe/Program.cs), [MithrilLifecycleProbe](../../ManualTests/MithrilLifecycleProbe/Program.cs), [MithrilRestartProbe](../../ManualTests/MithrilRestartProbe/Program.cs), [AppUnitsPipesProbe](../../ManualTests/AppUnitsPipesProbe/Program.cs) и Contract.cs | Прямые -u/-p/control pipe, process env TM_MITHRIL_DIR/PROVIDER, TM_APP_PIPES_DIR/PROVIDER/ENDPOINT/SCENARIO; local-marker через context.Configuration. Fixture координация и provider selection; мигрировать отдельно от public env prefix |
| [NomAssetTool](../../ManualTests/NomAssetTool/ToolArguments.cs), [TrueMoon.Nom.Probe](../../ManualTests/TrueMoon.Nom.Probe/Program.cs) | Positional verbs/project/store IDs/root/backup/receipt/report и дочерние crash/VM команды; аргументы операций, не автоматически process defaults Nom |
| [ModelImportResearch](../../ManualTests/ModelImportResearch/Program.cs), [CapturedIo](../../ManualTests/ModelImportResearch/CapturedIo.cs) | Worker/captured/fixtures/memory/block commands; TRUEMOON_CAPTURE_TRACE — исследовательский tracing. Не вводить производственный importer config API из research CLI |
| [RenderingCpu](../../ManualTests/RenderingCpu/Program.cs), [RenderResources](../../ManualTests/RenderResources/Program.cs), [RenderSnapshots](../../ManualTests/RenderSnapshots/Program.cs), [AetherCameraContractProbe](../../ManualTests/AetherCameraContractProbe/Program.cs) | Output directory/native profile/report; это входы проверок, а не отдельные настройки renderer |
| [AetherSourceWatchPerformance](../../ManualTests/AetherSourceWatchPerformance/Program.cs), [Cobalt benchmarks](../../Benchmarks/TrueMoon.Cobalt.Benchmarks/Program.cs), [Enerit benchmarks](../../Benchmarks/TrueMoon.Enerit.Benchmarks/Program.cs) | Сценарии/нагрузка/пути и provider selection; Enerit Measurement читает DOTNET_TieredPGO/DOTNET_gcServer как метаданные измерения, не переопределяет их |
| PowerShell runners в ManualTests | Устанавливают/передают GPU validation/bootstrap/baseline и запускают перечисленные CLI. Например [VerifyExampleHosts](../../ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1), [VerifyViewport](../../ManualTests/AetherPackageConsumer/VerifyViewport.ps1), [RunViewportBaseline](../../ManualTests/EngineSessionProbe/RunViewportBaseline.ps1). При cutover повторять актуальные команды этих consumers, не менять только C# parser |

<a id="migration"></a>
## Карта совместимости ключей и схем options

Имена в правой колонке — **целевые**, ещё не поддерживаемые текущим API.
Служебные поля не получают public aliases. Произвольный global key не мигрирует
автоматически по совпадению короткого имени: нужны явная схема и профиль приложения.

| Текущий вход | Целевой логический вход/категория | Правило совместимости |
| --- | --- | --- |
| appName | App:Name, string, startup | Legacy alias только в явно выбранном профиле; default из entry assembly сохранён |
| -root=path | App:RootPath, path, bootstrap/startup | Сохранить точный старый синтаксис в legacy; новый --root связан схемой с App:RootPath. Итоговый путь абсолютный до открытия config files; root не зависит от файла, который сам выбирает |
| args -ud=1 | Mithril:DisableUnits, bool, startup control | Alias в Mithril host profile; старое правило «только args» не расширять неявно на env/files |
| -u/-p/TrueMoon.Mithril.ControlPipe | Защищённый Unit launch: index/parent/endpoint | Пока читаются старым путём; CORE7.4 переводит producer и consumer вместе. Конфликт legacy CLI и назначенной parent identity должен диагностироваться, а не давать пользовательскому override подменить процесс |
| TM_ENGINE_BOOTSTRAP, env -p | Защищённый RuntimeBootstrap/SessionLaunch | Сохранить wire format/limits/version/nonce/build checks; смена wire protocol требует отдельной совместимости. Generic env prefix не поглощает эти ключи |
| TRUEMOON_GPU_VALIDATION == 1; --validation | Rendering:GpuValidation, bool, startup | В legacy сохранить точное значение 1. Новый bool использует общий conversion; при переходе проверить false, отсутствие, наследование и два App без process-global утечки |
| --project | Editor:ProjectPath, nullable path, startup | .aetherproj/явный legacy .csproj и preflight restrictions остаются у host. Смена открытого проекта после запуска — команда Editor |
| --runtime (Editor example) | Aether:RuntimeExecutable, path, startup | Не смешивать с boolean --runtime в дочернем executable; arguments остаются типизированным NativeRuntimeLaunch, executable не угадывается из user cwd |
| --report/--font/--light | Aether:ReportDirectory / Ui:FontPath / Ui:Theme, startup data | Явные host aliases; font object/backend factories не сериализуются; динамическая смена Theme остаётся UI контрактом |
| JSON filename + key; ключи вида typeof(T).Name | Legacy раздел/буквальный ключ; в новом профиле полный Section:Key | Старый JSON/объектный GetOrCreate сохраняется в legacy. Новый JSON задаёт вложенные sections; структурные и буквальные пути не смешиваются молча |
| DiagnosticsConfiguration и delegate/object values | Typed DI registration, legacy object bag adapter | Не предоставлять binding произвольного экземпляра сервиса/делегата из строки |

Схема регистрируется модулем явно; весь public тип options автоматически
пользовательским API не становится. Для будущего binding выделяются группы:

| Группа | Данные для схемы | Что остаётся типизированным кодом/данными операции |
| --- | --- | --- |
| App/paths/diagnostics | Name, RootPath, выбранные budgets/filters/sink paths | Listeners, sinks/factories, IAppLifetime и сами services |
| Mithril | Явно разрешённые hosting/restart/timeouts по декларации unit | Assigned Index/parent/control endpoint; unit action и lifetime owner |
| Aether | Report/BuildDirectory, BuildTimeout, retention/deadlines, limits ChildDiagnostics/ECS/scene loading/render resources/snapshots | Вычисленные build/host IDs, selected build и captures; runtime/viewport factories, ECS systems/schema, RenderViewConfiguration |
| Editor | Initial ProjectPath, SourceTracking, diagnostics budgets, SourceEditor ExecutablePath/Arguments | Documents/history/selection, команды preflight/smoke, fonts и view factories |
| Enerit | Все scalar PipesTransportOptions limits/timeouts/trace/metrics flags | Func<Type,string> endpoint, transport/server factories/handlers и private session endpoints |
| Alloy.Hosting | Явно выбранные scalar window/graphics settings | BackendFactory, platform resources, Theme/styles и текущее состояние controls |
| Nom | MaximumOpenStores, QueueCount/Bytes, BatchRows/Bytes, DocumentBytes, DomainIndexBytes, DrainTimeout | OpenAssetStoreArguments/RestoreAssetStoreArguments, asset contents, catalogs, backup manifests |

<a id="target-contract"></a>
## Функциональный контракт следующего профиля

Это проектное решение для CORE7; текущий запуск приложения остаётся legacy.
Точные добавочные C# типы/методы и порядок App stage теперь записаны в
[API-контракте](CONFIGURATION_API.md#signatures). При расхождении общей формулировки
и сигнатур приоритет имеет этот датированный API-контракт. Реализация следует
этапам CORE7.1–3, consumer migration — CORE7.4/5.

1. **Совместимость.** Существующие IConfiguration/IConfigurable, Get/Set/GetOrCreate,
   constructors и AddProvider сохраняют поведение. Новый профиль выбирается явно
   в composition root и использует отдельное представление итоговых значений;
   прежний raw GetSections не переименовывается в merged root. Старый профиль
   продолжает работать с обоими DI providers и опубликованными consumers.
2. **Приоритеты.** Defaults < files < env < args < explicit overrides, затем callback
   options и финальная semantic validation. У нескольких источников одного уровня
   объявленный порядок детерминирован: последний выигрывает. Никакой зависимости
   от порядка регистрации сервисов; callback должен применяться к связанным options
   до копирования, а не к уже активированному экземпляру. Это требует нового
   отложенного bind/validation этапа App, поскольку сейчас UseModule копирует options
   во время Setup раньше context.Configuration.
3. **Ключи и состояния.** OrdinalIgnoreCase для логического Section:Key; пустые
   сегменты запрещены. Изменения регистра/aliases, приводящие к двум именам одного
   ключа в одном источнике, — ошибка; повтор CLI scalar имеет отдельное правило
   last wins. Различать missing, explicit null, empty и invalid. Только missing
   допускает default; invalid/null недопустимого типа не скрываются fallback.
4. **Ввод.** Parser принимает явный массив без executable, выдаёт значения и
   позиционные аргументы без DI/IO. Aliases/флаги/коллекции и неизвестные options
   определяются схемой конкретного host; unknown CLI — ошибка. Отрицательное число
   может быть значением объявленной числовой option; `--` завершает разбор.
   Env читает только заданный prefix/allowlist, `__` — раздел; посторонний env
   игнорируется, неизвестный ключ внутри выбранной строгой схемы диагностируется.
   Required files падают при отсутствии, optional отсутствующие пропускаются;
   повреждённый существующий файл всегда даёт ошибку. JSON flattening и aliases
   обязаны выявлять конфликты, а не выбирать значение по порядку обхода объекта.
5. **Binding и владение.** Схема задаёт тип/default/nullable/коллекцию/aliases,
   converter, semantic validator, чувствительность, startup-only/dynamic. Применять
   invariant scalar conversion и явные codecs сложных типов; не разделять строки
   на коллекции по запятой. Сервисы получают копию options через DI, не весь resolver
   или ambient settings. Factories/listeners/identity не допускают string binding.
   Ошибка настроек возникает до создания owned runtime/window/store ресурсов.
6. **Перечитывание.** Снимок включает итоговые значения, options и generation.
   Explicit reload собирает и проверяет полный candidate; публикация едина,
   неудачный candidate не меняет текущую generation. Startup-only изменения
   требуют restart; dynamic только по явной схеме и с ограниченными подписками,
   владельцем/disposal и без callbacks под lock. Автоматический watcher не нужен
   в первой версии. Legacy Refresh секций не выдавать за этот новый контракт.
7. **Запись и диагностика.** Чтение/binding/default не выполняют persistence.
   Explicit write адресует конкретный изменяемый источник и отдельно сообщает
   ошибку; общая конфигурация не пишет в args/env. Диагностика хранит ключ/тип/
   источник/приоритет без секретного raw value или полного окружения. В legacy
   GetOrCreate/JSON остаются прежние гарантии; новые durability обещания не вводятся.
8. **Граница процесса.** User settings и protected launch — разные схемы общего
   parser/reader слоя. Только назначенные parent/launch значения создают identity.
   Источник user env/CLI не заменяет protected fields. Публичные rendering flags
   передаются child явно, compiler job сохраняет captured environment без merge.

<a id="remaining-core70"></a>
## Завершение CORE7.0–2

Карта/контракт CORE7.0 сохраняются. [CORE7.1 usage](CONFIGURATION_USAGE.md) описывает
реализованные profiles/schema metadata/defaults/codecs/sources/parser/raw snapshots;
schema.Bind — standalone data utility, не App stage. [API-контракт](CONFIGURATION_API.md)
уточнён optional Key.Codec, не меняя прежний constructor/generic codec.
Core506/506 D/R и fresh3package consumer по15checks проверены [в указанном scope](HISTORY.md#core71-2026-11-10).
[CORE7.2](CONFIGURATION_APP.md) добавляет deferred App stage, prepared GetOptions,
optional schemas catalog и App-owned borrowed reader; Core554/554 D/R и source/
published/fresh6package consumer по36checks проверены [в указанном scope](HISTORY.md#core72-2026-11-10).
[CORE7.3](CONFIGURATION_RELOAD.md) добавляет atomic reload/bounded changes/explicit JSON write; Core619 D/R,72consumer checks/profile. [CORE7.4](CONFIGURATION_CORE_MITHRIL.md) завершает Core/Mithril scope; следующий шаг — CORE7.5. Прежние runtime
bootstrap и module consumers не объявлены переведёнными.

<a id="core74-current"></a>
## Текущее использование после CORE7.4 — 2026-11-10

Core App name/paths и diagnostics data имеют standard schemas/public setup;
legacy-only consumers сохраняют explicit compatibility behavior. Mithril AddUnit
и parent handler используют frozen typed launch и common minimal parser/profile.
Optional original-provider bridge защищает identity от ordinary overlays.
Подробная alias/source/boundary карта — [migration guide](CONFIGURATION_CORE_MITHRIL.md),
runtime/package evidence — [HISTORY](HISTORY.md#core74-2026-11-10).

Текущее перечисление1588C#/894production. Из baseline1574C# изменены13целевых,
1561hashes сохранён; ещё1из61csproj изменён (Mithril→TrueMoon). Добавлены11production
и3test C#. Ledger выбранных OS settings calls теперь7calls/6files: common
ConfigurationCommandLine.Capture заменяет прежние2GetCommandLineArgs; env provider,
explicit ConfigurationEnvironment.Capture, unified env source и прежние Aether
RuntimeBootstrap/GPU reader сохраняют свои роли. PID/company/product/system folders
не являются settings reads. Прежние tables/counts выше относятся к исходной карте.

Этот абзац фиксирует состояние CORE7.4. Переход module consumers реализован в
CORE7.5 ниже; native/GPU release и Mithril MIT6 остаются отдельными gates.

<a id="core75-current-map"></a>
## Актуализация CORE7.5 — 2026-11-10

[Схемы, ключи, callbacks и bootstrap aliases](CONFIGURATION_MODULES.md),
[матрица110новых cases](CONFIGURATION_MODULE_TESTS.md),
[команды/результаты](HISTORY.md#core75-2026-11-10).
Aether/Editor/ECS/render/authoring, Enerit pipes, Nom и Hosting получили общий
schema → profile → validated options → existing App/DI registration путь.
RuntimeBootstrap фиксирует protected launch/parent/control и public flags до App;
runtime loop не перечитывает environment. Editor launch CLI использует общий parser.

В production ledger выбранных OS settings API осталось4calls/4files:
ConfigurationCommandLine.Capture, ConfigurationEnvironment.Capture,
ConfigurationEnvironmentSource и legacy EnvironmentVariablesProvider. В Aether,
Mithril и остальных перечисленных модулях прямых чтений выбранных API нет.
Передача child environment и isolated compiler environment — явные adapters;
PID/process path/system folders/measurement metadata не являются настройками App.
Probe/benchmark CLI — ограниченные executable harness adapters для сценариев и
артефактов. Форматы проектов/сцен/ассетов остаются данными.

Baseline1649C#/csproj:1633hashes сохранены,14изменены (12C#+2csproj),
2internal files перенесены из TrueMoon в Contracts,26C# добавлены (включая эти
2переноса,6test files и independent consumer). Текущее перечисление1612C#+61csproj.
Старые таблицы выше описывают исходную карту, а не новые remaining tasks.
