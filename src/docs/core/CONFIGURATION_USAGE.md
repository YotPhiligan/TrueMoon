# Единый профиль конфигурации: standalone API

CORE7.1, 2026-11-10. Реализованы схема ключей, явные sources, приоритеты,
immutable raw snapshots и общий CLI/env/JSON parser. Старые IConfiguration,
ConfigurationBuilder и providers продолжают работать как раньше. Новый профиль
выбирается явно; [CORE7.2 App stage](CONFIGURATION_APP.md) подключает его через Setup, без автоматической миграции модулей.
[Карта использования](CONFIGURATION.md), [API/архитектура](CONFIGURATION_API.md),
[этапы и критерии](PLAN.md#core7-unified-configuration).

## Пример

```csharp
using TrueMoon.Configuration;

var schema = ConfigurationSchemas.Create("Worker", () => new Settings(),
    value => value with { Tags = value.Tags.ToArray() },
    value => { if (value.BatchSize is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(value.BatchSize)); },
    fields =>
    {
        fields.Field("BatchSize", value => value.BatchSize,
            (value, size) => value.BatchSize = size, new(Aliases: ["--batch", "-b"]));
        fields.Field("Tags", value => value.Tags,
            (value, tags) => value.Tags = tags, new(Aliases: ["--tag"], Argument: ConfigurationArgumentKind.Repeated));
    });

var file = Path.Combine(AppContext.BaseDirectory, "settings.json"); // Explicit application path.
var profile = ConfigurationProfiles.Create(builder =>
{
    builder.AddSchema(schema);
    builder.AddJsonFile("settings", file, optional: true);
    builder.AddEnvironment("environment", "MYAPP_");
    builder.AddArguments("arguments", args); // Without executable; copied here.
});

var snapshot = await profile.LoadAsync();
var options = schema.Bind(snapshot); // Pure callback + semantic validation, independent copy.
var source = snapshot.Read<int>("worker:batchsize").Origin;

internal sealed record Settings
{
    public int BatchSize { get; set; } = 32;
    public string[] Tags { get; set; } = [];
}
```

JSON: `{"Worker":{"BatchSize":16,"Tags":["json"]}}`.
Environment: `MYAPP_WORKER__BATCHSIZE=24`.
CLI: `--batch 48 --tag=a,b --tag=c` → BatchSize=48, Tags=`["a,b","c"]`.
Register order между уровнями не влияет на результат: defaults < files < env <
args < explicit overrides. В одном уровне последний source побеждает.
`builder.Override("Worker:BatchSize", 64)` имеет старший приоритет.

## Правила ввода и ошибок

| Область | Поведение |
| --- | --- |
| Ключи | Полные Section:Key, OrdinalIgnoreCase; нет глобального поиска leaf в других sections. Дубликаты/aliases и пересечение leaf/section — ошибки схемы |
| CLI | `--key=value`, `--key value`, объявленные short aliases, last scalar wins, ordered Repeated; `--` завершает parsing. Flag без `=` означает true, false задаётся `--flag=false`. Следующий token flag не поглощается |
| Позиционные аргументы | Settings-only AddArguments отклоняет их. Для команд вызвать ConfigurationCommandLine.Parse(args, schema.Keys) и обработать Positionals явно |
| Отрицательные значения | Для числового поля допускается `--count -12`. Строку с неоднозначным ведущим `-` передавать через `=` |
| Env | Только непустой prefix и optional allowlist логических keys. `__` обозначает раздел, single `_` сохраняется. Unknown выбранного prefix — ошибка, посторонние переменные игнорируются |
| JSON | Явный абсолютный файл; nested objects → paths, arrays — одно значение. Объект для объявленного complex key передаётся его codec целиком. Optional пропускает только отсутствие; повреждённый/недоступный файл не скрывается defaults. Буквальный `:` и case-дубликаты property запрещены |
| Значения | Missing, explicit null, empty и invalid различаются. Invalid победившего source не вызывает fallback. Standard scalar/nullable/enum/Guid/TimeSpan и arrays/List/read-only collection interfaces преобразуются invariant; enum должен иметь объявленное значение. Остальные типы/особые enum policies требуют явный codec |
| Схема | Options не владеют disposable ресурсами; поля не содержат delegates/services. Create/copy/get/set/validate/codecs — чистые. Copy отделяет все изменяемые данные и возвращает новый экземпляр; identity copy отклоняется. Отсутствующий codec для неподдержанного типа — ArgumentException до загрузки |
| Проверка | Load конвертирует raw winning values, Bind затем применяет configure callback и semantic validation. Required проверяется после callback. Load не активирует services и не выполняет semantic validator за Bind |
| Ошибки | Metadata/caller errors — ArgumentException/InvalidOperationException; данные/IO/conversion — ConfigurationException с canonical Path и безопасным SourceId. Raw values, исходные custom exception messages и полное окружение не печатаются. Requested source cancellation — OperationCanceledException |

## Снимки и bootstrap

Каждый LoadAsync создаёт независимый immutable snapshot generation=1. Чтение
не выполняет IO; возвращённые collections/JSON отделены от storage. Неудачная
повторная загрузка не меняет ранее полученные снимки. Это **не** App reload:
нет общей Current generation, уведомлений или автоматического watcher.
Для standalone options используется schema.Bind(snapshot); GetOptions на standalone
snapshot бросает InvalidOperationException. [App stage](CONFIGURATION_APP.md) уже
подготавливает typed options и предоставляет GetOptions через IConfigurationReader.

ConfigurationEnvironment.Capture читает только явно перечисленные exact names
и сохраняет missing как null. Это helper для bootstrap adapters без App/DI;
он не смешивает private launch identity с user CLI/env. Существующий Mithril/Aether
bootstrap пока не мигрирован, его wire/проверки/lifecycle не менялись.

SourceId — заданный приложением безопасный ID, сравнение Ordinal; `$` зарезервирован
встроенными defaults/overrides. Custom source реализует IConfigurationSource,
возвращает полный набор owned ConfigurationEntry и поддерживает cancellation.
Custom generic codec передаётся в Field; schema публикует его через необязательный
ConfigurationKey.Codec (type-erased IConfigurationValueCodec). Ошибочный codec type
отклоняется; codecs не получают DI/IO или права изменять snapshot.

## Проверка и поставка

[Новые xUnit cases](../../TrueMoon.Tests/Configuration/UnifiedConfigurationProfileTests.cs) и существующие Core tests
проверяют приоритеты, грамматику, ошибки, копирование и cancellation.
[Независимый consumer](../../ManualTests/ConfigurationProfileConsumer/Program.cs) и
[runner](../../ManualTests/ConfigurationProfileConsumer/Verify.ps1) повторяют source
Debug, published Release и fresh-package proof в отдельном каталоге/кэше с посторонним cwd.
Датированные результаты — [HISTORY](HISTORY.md#core71-2026-11-10).
Общая версия не менялась. App binding/reader реализованы в CORE7.2; reload/write — CORE7.3;
[Core/Mithril migration](CONFIGURATION_CORE_MITHRIL.md) реализована в CORE7.4;
[остальные consumers](CONFIGURATION_MODULES.md) реализованы в CORE7.5.

Модульные библиотеки используют `ConfigurationSchemaDefinitions.Create<T>` и
`ConfigurationModuleBinding.Configure` из Contracts. Public facade
`ConfigurationSchemas.Create<T>` в TrueMoon сохранён. `ParseSingleUse` — общий
строгий вариант для command surfaces: повтор scalar/flag отвергается; обычные
Parse/ParseKnown остаются last-wins. [Применение и совместимость](CONFIGURATION_MODULES.md).
