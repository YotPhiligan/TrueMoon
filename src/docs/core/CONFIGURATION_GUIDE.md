# Конфигурация TrueMoon: рабочее руководство

CORE7.0–7.6: общий механизм реализован. Приложение явно выбирает schemas, sources
и configured module helpers в composition root. Cobalt и Microsoft DI используют
одинаковые правила конфигурации. [Точные результаты и ограничения](HISTORY.md#core76-2026-11-10)
относятся к указанным профилям проверки, а не ко всему native/platform release.

## Подключение

```csharp
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;

var profile = ConfigurationProfiles.Create(builder =>
{
    builder.AddSchemas(); // Exact standard App and Paths schemas.
    builder.AddSchema(DiagnosticsConfigurationSchema.Default);
    builder.AddJsonFile("user-json", Path.GetFullPath("user.json"), optional: true);
    builder.AddEnvironment("environment", "MYAPP_", ["App:Name", "Paths:Root"]);
    builder.AddArguments("arguments", args); // Settings only, excluding executable/commands.
});

await using var app = App.Build(context => context.UseConfiguration(profile)
    .UseCoreConfiguration()
    .UseConfiguredDiagnostics());
await app.StartAsync();
Console.WriteLine(app.Name);
await app.StopAsync();
```

Это полный короткий console пример: он запускает App и сразу завершает работу.
В рабочем приложении между Start/Stop находится его lifetime/workload; `RunAsync`
может управлять этим циклом. Microsoft DI выбирается при создании builder через
`App.Builder(context => context.UseDI())`; schemas и источники от этого не меняются.
Простое создание profile не читает файлы и не активирует сервисы.

В модуле используйте точную schema instance, которую ожидает `UseConfigured*`.
Выбранные schemas целиком готовятся и проверяются после Setup, до module registration
и DI activation. Typed configure callback должен оставаться чистым. Сервисам
передавайте проверенные options/private copy или узкий reader; файл/args/env не
разбираются заново в кадре, запросе или элементе UI.

## Ключи, источники и приоритеты

[Каталог139стандартных ключей](CONFIGURATION_KEYS.md) получен из public metadata
свежих пакетов; [соответствие schemas и module helpers](CONFIGURATION_MODULES.md)
включает engine/ECS/Editor/Enerit/Nom/Hosting. Runtime identity, factories, scene/asset
documents, Build/Play captures и изменяемое UI/игровое состояние не являются fields.

| Вход | Пример | Правило |
| --- | --- | --- |
| Canonical key | `App:Name` | Пути и поиск не зависят от регистра; schema задаёт имена и типы |
| JSON | `{"App":{"Name":"Editor"}}` | Явно выбранный абсолютный путь; optional пропускает только отсутствие файла |
| Environment | `MYAPP_APP__NAME=Editor` | Обязательный prefix; `__` → `:`; allowlist ограничивает выбранные ключи |
| Arguments | `--App:Name=Editor` или `--App:Name Editor` | Без executable; команды/позиционные аргументы обработать отдельно |
| Explicit override | `builder.Override("App:Name", "Editor")` | Явное переопределение в profile |
| Typed configure | `value => value.QueueCapacity = 40` | Последний чистый шаг подготовки options; raw value/provenance не переписывает |

Уровни: schema defaults → files100 → environment200 → arguments300 → overrides400.
В одном уровне побеждает последний source в порядке добавления. Несовместимое
победившее значение, включая null для nonnullable field, вызывает ошибку; fallback
на более слабый source не маскирует ошибку. Unknown keys, коллизии и malformed JSON
отклоняются. Unknown prefixed env key тоже ошибка, если он не исключён allowlist.
Числа/Guid/TimeSpan/enum/booleans конвертируются invariant codecs; `1/0` для validation
является специальной совместимостью bootstrap adapter, не общим boolean форматом.

`Parse`/`ParseKnown` сохраняют last-wins scalar semantics. `ParseSingleUse` для
строгого command surface запрещает повтор scalar/flag, включая разные aliases.
Repeated fields сохраняют collection semantics. Bare flag разрешён только при
соответствующей field metadata; явный boolean надёжно задаётся `=true`/`=false`.
`AddArguments` принимает только настройки; отдельный command parser может использовать
`Positionals` либо `ParseKnown` для явно выбранной bootstrap поверхности.

## Legacy и единый reader

`App.Configuration`/старый `IConfiguration` сохраняют прежние provider sections и
object bag. Opt-in `UseConfiguration(profile)` не превращает их автоматически в
итоговый unified snapshot. Новые потребители используют `IConfigurationReader`.
Standard `UseCoreConfiguration` фиксирует App name/root; без этого остаётся legacy
поведение. Core `AddLegacySources(legacy)` явно импортирует ограниченные старые
`appName`/`-root` inputs с прежними уровнями приоритета.

| Нужно | API / поведение |
| --- | --- |
| Значение и источник | `reader.Current.Read<T>(path)` возвращает state/value/origin |
| Готовые options | `reader.Current.GetOptions(schema)` возвращает отдельную копию, включая mutable вложения |
| Несколько согласованных чтений | Один раз захватить `var snapshot = reader.Current`, читать из него |
| Legacy настройка | Явный ограниченный adapter; произвольный короткий legacy key не становится alias |
| Собственная module schema без bootstrap dependency | `ConfigurationSchemaDefinitions.Create<T>` и `ConfigurationModuleBinding.Configure` из Contracts |
| Совместимость прежнего schema factory | `ConfigurationSchemas.Create<T>` остаётся фасадом TrueMoon |

Reader/reloader/changes/writer заимствованы и не требуют Dispose. Runtime и его
cleanup принадлежат App. Detached snapshots остаются данными после Dispose App;
обращение к reader после Dispose отклоняется. Options не владеют DI services,
GPU/native handles или результатами factories.

## Reload, уведомления и запись

Стандартные139полей — `StartupOnly`: для изменившегося кандидата нужен новый App.
Собственная schema может явно объявить `Reloadable` field. Reload сериализован с
write, повторно готовит все data schemas и атомарно заменяет raw+prepared snapshot.
Module registrations, DI factories и создание ресурсов не повторяются.

| Результат | Что происходит |
| --- | --- |
| Ошибка/отмена до commit | Прежние Current и generation остаются |
| No-op | Published=false, generation не меняется |
| StartupOnly change | RestartRequired=true, весь кандидат отклонён, включая dynamic changes |
| Успешный dynamic update | Published=true, generation+1, уведомления после публикации |

Сравниваются canonical typed raw values, state и origin; одинаковое значение от
другого source также может считаться изменением. Configure callbacks выполняются
для кандидата, но не должны запускать побочные действия. Для реакций существуют
bounded subscriptions:64на App, один pending update на подписку, coalescing;
callback обязан быть коротким, Dispose снимает подписку, shutdown дожидается
callbacks. Автоматический file watcher не включён.

Запись — отдельный `IConfigurationWriter.WriteAsync(sourceId, edits)` в явный
writable JSON source. Она не публикует reader generation и не вызывает reload.
`ConfigurationEdit(path, null)` удаляет значение, JSON null задаётся отдельным
JsonElement. Изменение reader/options не пишет JSON/env/CLI. Запись сохраняет
незатронутые данные и использует atomic file replacement в описанном local scope;
interprocess conflict resolution/power-loss durability не обещаны.
[Подробная семантика и примеры](CONFIGURATION_RELOAD.md).

## Process bootstrap и сохранённые aliases

| Поверхность | Совместимость и граница |
| --- | --- |
| Core | `appName`, `-root` через явный legacy bridge; canonical `App:Name`/`Paths:Root` |
| Mithril | `-u`, `-p`, `-ud`, `TrueMoon.Mithril.ControlPipe`: typed protected launch; original bootstrap inputs до overlays |
| Aether | Bounded `TM_ENGINE_BOOTSTRAP`, parent/control и session/nonce/version/build identity отдельно от пользовательских sources |
| Runtime validation | Canonical `TRUEMOON_AETHER_RUNTIME__GPUVALIDATION` сильнее legacy `TRUEMOON_GPU_VALIDATION`, поддержаны1/0 |
| Editor CLI | Прежние aliases, canonical/inline forms и strict duplicate rejection; absent validation наследует selected env, explicit true/false его перекрывает |

Capture/parser работает до App без DI; immutable результат передаётся в composition
root. Runtime не перечитывает environment. Parent пишет launch/flags только в
новый `ProcessStartInfo.Environment`; глобальный env не меняется. Обычные sources
не подменяют identity/parent/control и защищённые report/delivery captures.
Системные PID/paths/capabilities и compiler isolation остаются OS/bootstrap facts.
[Core/Mithril](CONFIGURATION_CORE_MITHRIL.md), [Aether/Editor и модули](CONFIGURATION_MODULES.md).

## Поставка и границы результата

[Единый runner](../../ManualTests/ConfigurationDelivery/README.md) использует одну
новую версию16локальных пакетов, external dependencies из точного существующего
cache и отдельные пустые fresh caches. Standalone profile, App/reload с owned
Mithril children, module consumers и настоящий Aether child bootstrap проверяются
в source Debug, published Release и fresh package Release на обоих DI.
Fresh graphs содержат0ProjectReference, runtime DLL hashes совпадают с installed
packages. Aether child готовит runtime graph без его запуска и отдельно проверяет
Mithril stop/reap; renderer/game/native runtime не стартуют.

Это configuration/package acceptance, не f.4 game-surface readiness и не новый
hardware/native/GPU release. [Текущий статус](STATUS.md) и [HISTORY](HISTORY.md#core76-2026-11-10)
разделяют текущие tests/build/package/CLI результаты и старые либо открытые gates.
