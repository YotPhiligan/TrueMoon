# CORE7.0: добавочный API конфигурации

Проектное решение 2026-11-10. [Карта текущего использования](CONFIGURATION.md),
[единственный план работ](PLAN.md#core7-unified-configuration).
Это спецификация API: standalone часть реализована в CORE7.1, App stage — CORE7.2,
reload/write — CORE7.3. Методы в образце деклараций ниже намеренно имеют
заглушки; это образец сигнатур, не код для запуска. Runner использует реализованные
типы из assembly, удаляя соответствующие declarations. Компиляция образцов не
подтверждает App lifecycle или child launch. [Рабочий standalone пример](CONFIGURATION_USAGE.md).

CORE7.4 добавляет [Core schemas и Mithril bootstrap adapters](CONFIGURATION_CORE_MITHRIL.md).
Их runtime/package proof ведётся отдельно от компиляции этих образцов.
CORE7.5 добавляет [модульные schemas и helpers](CONFIGURATION_MODULES.md).
`ConfigurationSchemaDefinitions.Create<T>` и `ConfigurationModuleBinding.Configure`
живут в Contracts; прежний `ConfigurationSchemas.Create<T>` остаётся публичным
фасадом TrueMoon. Изменение добавочное: существующие обязательные interfaces
не расширены; чистые codecs/metadata имеют одну общую реализацию.

<a id="compatibility"></a>
## Совместимость и размещение

- Существующие IConfiguration, IConfigurable, IConfigurationBuilder,
  IConfigurationProvider, IAppBuilder, IAppConfigurationContext и IServiceResolverBuilder
  не получают новых обязательных членов. Их Get/Set/GetOrCreate/AddProvider,
  constructors, sections и legacy tests/consumers сохраняют семантику.
- Явный unified-v1 профиль даёт **отдельный** IConfigurationReader. В DI остаётся
  legacy IConfiguration; он не подменяется фасадом с другими GetSection/Set правилами.
  Нет автоматического импорта legacy object bag или всего process environment.
- Interfaces/records/enums блока ниже — TrueMoon.Contracts/Configuration.
  ConfigurationProfiles, ConfigurationSchemas, ConfigurationSources,
  ConfigurationCommandLine, ConfigurationEnvironment и ConfigurationAppExtensions —
  TrueMoon/Configuration. Файл каждого типа соответствует namespace/папке.
  Module-specific schemas/codecs/validators остаются в своём модуле.
- Схема/профиль состоят из данных и чистых delegates: ими нельзя владеть окном,
  stream, store, resolver или процессом. Создание этих объектов остаётся в DI factories.
  Модули подключаются через публичные Use/AddModule, выбранные при composition.
- Новых NuGet-зависимостей, scopes, async service activation и DI-provider-specific
  контрактов нет. JsonElement уже используется в Contracts; входы клонируются,
  чужой JsonDocument и изменяемые коллекции не удерживаются.

<a id="signatures"></a>
## Сигнатуры unified-v1

Конструкторы records ниже показывают значения по умолчанию. В реализации все
коллекции копируются, null/дубликаты проверяются; JsonElement Undefined запрещён.
Типы immutable по контракту, включая содержимое коллекций. Сигнатуры фабрик/
extensions содержат только заглушки для проверки компиляции.

```csharp
// api: declarations
using System.Text.Json;
using TrueMoon;

namespace TrueMoon.Configuration
{
    public enum ConfigurationSourcePriority
    {
        Defaults = 0, Files = 100, Environment = 200, Arguments = 300, Overrides = 400
    }
    public enum ConfigurationChangeMode { StartupOnly, Reloadable }
    public enum ConfigurationReadState { Missing, Null, Value }
    public enum ConfigurationArgumentKind { Value, Flag, Repeated }

    public sealed record ConfigurationField(
        IReadOnlyList<string>? Aliases = null,
        bool Required = false,
        bool Sensitive = false,
        ConfigurationChangeMode Change = ConfigurationChangeMode.StartupOnly,
        ConfigurationArgumentKind Argument = ConfigurationArgumentKind.Value);
    public sealed record ConfigurationKey(
        string Path, Type ValueType, ConfigurationField Field)
    {
        public IConfigurationValueCodec? Codec { get; init; }
    }
    public sealed record ConfigurationOrigin(
        string SourceId, ConfigurationSourcePriority Priority, int Order);
    public sealed record ConfigurationRead<T>(
        ConfigurationReadState State, T? Value, ConfigurationOrigin? Origin);
    public sealed record ConfigurationEntry(string Path, JsonElement Value);
    public sealed record ConfigurationEdit(string Path, JsonElement? Value);
    public sealed record ConfigurationIssue(
        string Code, string? Path, string? SourceId, string Message);
    public sealed record ConfigurationUpdate(
        long Generation, bool Published, bool RestartRequired,
        IReadOnlyList<string> ChangedKeys);
    public sealed record ConfigurationCommandLineResult(
        IReadOnlyList<ConfigurationEntry> Entries, IReadOnlyList<string> Positionals);

    public sealed class ConfigurationException : Exception
    {
        public ConfigurationException(IReadOnlyList<ConfigurationIssue> issues)
            : base("Configuration is invalid.") => Issues = issues;
        public IReadOnlyList<ConfigurationIssue> Issues { get; }
    }

    public interface IConfigurationValueCodec
    {
        Type ValueType { get; }
        object? Read(JsonElement value);
        JsonElement Write(object? value);
    }
    public interface IConfigurationValueCodec<T>
    {
        T Read(JsonElement value);
        JsonElement Write(T value);
    }
    public interface IConfigurationSchemaBuilder<T> where T : class
    {
        void Field<TValue>(string relativePath, Func<T, TValue> get,
            Action<T, TValue> set, ConfigurationField? field = null,
            IConfigurationValueCodec<TValue>? codec = null);
    }
    public interface IConfigurationSchema<T> where T : class
    {
        string Section { get; }
        IReadOnlyList<ConfigurationKey> Keys { get; }
        IReadOnlyList<ConfigurationEntry> GetDefaults();
        T Bind(IConfigurationSnapshot snapshot, Action<T>? configure = null);
        T Copy(T value);
    }
    public interface IConfigurationSnapshot
    {
        long Generation { get; }
        ConfigurationRead<T> Read<T>(string path);
        T GetOptions<T>(IConfigurationSchema<T> schema) where T : class;
    }
    public interface IConfigurationSource
    {
        ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(
            IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default);
    }
    public interface IConfigurationProfileBuilder
    {
        void AddSchema<T>(IConfigurationSchema<T> schema) where T : class;
        void AddSource(string id, ConfigurationSourcePriority priority, IConfigurationSource source);
        void Override<T>(string path, T value, IConfigurationValueCodec<T>? codec = null);
    }
    public interface IConfigurationProfile
    {
        ValueTask<IConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    }
    public interface IConfigurationProfileSchemas
    {
        void VisitSchemas(IConfigurationSchemaVisitor visitor);
    }
    public interface IConfigurationSchemaVisitor
    {
        void Visit<T>(IConfigurationSchema<T> schema) where T : class;
    }
    public interface IConfigurationReader
    {
        IConfigurationSnapshot Current { get; }
    }
    public interface IConfigurationBootstrapInputs
    {
        IConfigurationSection? GetBootstrapSection(string name);
    }
    public interface IConfigurationReloader
    {
        ValueTask<ConfigurationUpdate> ReloadAsync(CancellationToken cancellationToken = default);
    }
    public interface IConfigurationWriter
    {
        ValueTask WriteAsync(string sourceId, IReadOnlyList<ConfigurationEdit> edits,
            CancellationToken cancellationToken = default);
    }
    public interface IConfigurationMutableSource : IConfigurationSource
    {
        ValueTask WriteAsync(IReadOnlyList<ConfigurationKey> keys,
            IReadOnlyList<ConfigurationEdit> edits, CancellationToken cancellationToken = default);
    }
    public interface IConfigurationProfileWriter : IConfigurationWriter { }
    public interface IConfigurationChanges
    {
        IDisposable Subscribe(Action<ConfigurationUpdate> observer);
    }
    public interface IAppConfigurationStage
    {
        void UseConfiguration(IConfigurationProfile profile);
        void ConfigureModule<T>(IConfigurationSchema<T> schema,
            Action<IAppConfigurationContext, T> register, Action<T>? configure = null)
            where T : class;
    }

    public static class ConfigurationSchemas
    {
        public static IConfigurationSchema<T> Create<T>(string section, Func<T> create,
            Func<T, T> copy, Action<T> validate, Action<IConfigurationSchemaBuilder<T>> fields)
            where T : class => throw new NotImplementedException();
    }
    public static class ConfigurationProfiles
    {
        public static IConfigurationProfile Create(Action<IConfigurationProfileBuilder> configure)
            => throw new NotImplementedException();
    }
    public static class ConfigurationSources
    {
        public static void AddValues(this IConfigurationProfileBuilder builder, string id,
            ConfigurationSourcePriority priority, IReadOnlyList<ConfigurationEntry> entries)
            => throw new NotImplementedException();
        public static void AddJsonFile(this IConfigurationProfileBuilder builder,
            string id, string absolutePath, bool optional = false)
            => throw new NotImplementedException();
        public static void AddEnvironment(this IConfigurationProfileBuilder builder,
            string id, string prefix, IReadOnlyList<string>? allowlist = null)
            => throw new NotImplementedException();
        public static void AddArguments(this IConfigurationProfileBuilder builder,
            string id, IReadOnlyList<string> arguments)
            => throw new NotImplementedException();
    }
    public static class ConfigurationCommandLine
    {
        public static IReadOnlyList<string> Capture() => throw new NotImplementedException();
        public static ConfigurationCommandLineResult ParseKnown(IReadOnlyList<string> arguments,
            IReadOnlyList<ConfigurationKey> keys) => throw new NotImplementedException();
        public static ConfigurationCommandLineResult Parse(IReadOnlyList<string> arguments,
            IReadOnlyList<ConfigurationKey> keys) => throw new NotImplementedException();
    }
    public static class ConfigurationEnvironment
    {
        public static IReadOnlyDictionary<string, string?> Capture(params string[] names)
            => throw new NotImplementedException();
    }
    public static class ConfigurationAppExtensions
    {
        public static IAppConfigurationContext UseConfiguration(this IAppConfigurationContext context,
            IConfigurationProfile profile) => throw new NotImplementedException();
        public static IAppConfigurationContext ConfigureModule<T>(this IAppConfigurationContext context,
            IConfigurationSchema<T> schema, Action<IAppConfigurationContext, T> register,
            Action<T>? configure = null) where T : class => throw new NotImplementedException();
    }
}
```

### Значения, схемы и источники

Уточнение CORE7.1, 2026-11-10: добавлено необязательное Key.Codec с nongeneric
codec metadata. Конструктор ConfigurationKey и generic codec<T> сохранены.
Так custom codec передаётся через публичные schema/profile interfaces без downcast
к встроенной схеме; Codec.ValueType должен совпадать с ValueType. Built-in enum
принимает только объявленные значения; другая политика требует явный codec.
Source IDs сравниваются Ordinal, `$` зарезервирован для defaults/overrides.
Branch/leaf paths (Editor:Theme и Editor:Theme:Color) несовместимы.
Неподдержанный тип без codec — ошибка декларации ArgumentException, ошибочные входы —
ConfigurationException. Pure codec/callback не получает cancellation token: его
исключения, включая OperationCanceledException с raw message, маскируются. Cancellation
source API по запрошенному token передаётся безопасным OperationCanceledException(token).

| API | Точная семантика |
| --- | --- |
| ConfigurationSchemas.Create | Section — непустой полный путь. Field.relativePath добавляется к нему через `:`. Get читает default/результат проверки, set выполняет binding. Create/copy/validate и codec — чистые, без DI/IO/owned resources. Defaults снимаются с одного private create() и сериализуются codec; initial null остаётся explicit null. GetDefaults возвращает immutable entries для внутреннего Defaults source, без запуска semantic validation до callback |
| Field | Aliases — полные CLI tokens (`--project`, `-p`), не env aliases. Логические ключи и aliases OrdinalIgnoreCase, пустые сегменты запрещены. Конфликт нормализованных ключей/aliases отклоняется. Required проверяется **после callback**: null/whitespace string запрещены, числовые диапазоны проверяет validate. Nullable задаётся TValue, числовой default 0 сам по себе не ошибка |
| Bind/Copy | Bind: Copy private defaults → значения снимка → callback → Required/validate → глубокая копия результата. Не поддерживается binding сервисов, delegates, handles и IDisposable options. Callback не обходит ошибку преобразования победившего raw value. Copy обязана отделять все изменяемые данные; модули могут затем использовать прежний Snapshot |
| Codecs | Штатные scalar string/bool/numbers/enum/Guid/TimeSpan и коллекции — invariant; строки env/args конвертируются так же, как JSON string. Arrays не делятся по запятой. Произвольный сложный тип требует явный codec. Write возвращает cloned JsonElement. Required не делает неправильно типизированный null допустимым |
| Snapshot.Read<T> | Полный логический путь; неизвестный корректный путь даёт Missing/null Origin. Известный ключ требует совпадения T со схемой; неверный тип/конверсия — ConfigurationException. Missing/Null/Value различаются State, не значением default(T). Возвращаемые коллекции отделены от storage; Read не выполняет IO. Прямое частое чтение заменяется заранее связанными options |
| Snapshot.GetOptions<T> | Копия уже подготовленных stage options для данного экземпляра schema и **этой** generation. Не повторяет callback/validation. Standalone Load не создаёт stage options: метод бросает InvalidOperationException, standalone caller использует schema.Bind. App stage готовит все AddSchema options, с callback при наличии ConfigureModule. На reload runtime сначала готовит все bound options, затем публикует единый снимок raw+options |
| Origin | SourceId — заданный приложением безопасный ID, не raw value или автоматически полный путь. Order — порядок объявления внутри уровня, больший побеждает. Source provenance относится к raw snapshot; callback меняет options после binding, поэтому не приписывается файлу/env и не меняет snapshot задним числом |
| AddSchema/AddSource/Override | Уникальные sections/ключи/source IDs; повтор — ошибка. Priority берётся из enum; внутри уровня последний source выигрывает. Override копируется как data source уровня Overrides; повтор пути в программных overrides — последний побеждает. После Create профиль и inputs frozen; делегат builder наружу не сохраняется |
| ReadAsync/LoadAsync | Source возвращает полный снимок своих entries, с cancellation. Повторы нормализованного пути внутри source — ошибка; Undefined запрещён, JsonElement Null — явный null. Load собирает **все** источники и конвертирует победившие значения до выдачи immutable snapshot generation=1; ошибка не возвращает частичный результат. Новый вызов Load — независимая загрузка generation=1, не reload существующего reader |
| JSON | Только заданный абсолютный файл. Корневой объект; вложенные properties образуют Section:Key, arrays остаются одним значением. Буквальный `:` в property запрещён, чтобы не скрывать конфликт с вложенным путём. Optional пропускает только отсутствие; malformed/permission/type errors не подавляются. Unknown schema keys — ошибка |
| Env | Prefix обязателен/непустой. `__` → `:`, single `_` сохраняется. Allowlist содержит логические пути; ограничения применяются до публикации entries. Вне prefix/allowlist переменные игнорируются; неизвестные внутри выбранной схемы диагностируются. Два исходных имени одного normalized path — ошибка. Capture читает только явно перечисленные имена и возвращает отдельный immutable dictionary, включая null для отсутствующих |
| CLI | Массив без executable копируется при AddArguments; reload его не меняет. Parse возвращает canonical entries и positionals. Settings-only AddArguments отклоняет непустые positionals; command host явно вызывает standalone Parse и обрабатывает их сам. Value: `--key=value`/`--key value`; повтор scalar last wins. Flag: bare flag=true, false задаётся `--flag=false`; следующий token flag не поглощает. Repeated накапливает JSON array с сохранением порядка. `--` завершает parsing; unknown option — ошибка. Отрицательное число допускается для числовой Value; строковое значение с неоднозначным `-` задаётся через `=`. Алиасы задаёт отдельная схема host, не глобальный список всех executables |

### Фаза App и ownership

CORE7.2 реализован 2026-11-10; [рабочее подключение](CONFIGURATION_APP.md).
Уточнение capability API: optional IConfigurationProfileSchemas/SchemaVisitor
передаёт весь catalog, включая schemas без ConfigureModule, без reflection или
новых mandatory членов IConfigurationProfile. Встроенный profile реализует catalog;
сторонний без него пригоден standalone, но App extension выдаёт NotSupportedException.
Один reader/runtime на App, GetOptions возвращает prepared copies. Bind — в порядке
AddSchema, register — ConfigureModule. Ошибка копирования любого register input
проверяется до первого register. Legacy callbacks после register исполняются очередью
для всех AddUnit declarations. Чужие catalog/profile/schema implementations соблюдают
data-only/immutable/copy contracts; malformed initial generation !=1 отклоняется.

AppConfigurationContext реализует **добавочный** IAppConfigurationStage. Extensions
проверяют capability и бросают NotSupportedException для чужого context без неё;
тихого fallback/service locator/глобальной таблицы настроек нет. Схемы Declare
в профиле и ConfigureModule должны быть теми же экземплярами; один binding на section.
Повтор UseConfiguration, неизвестная схема и изменение frozen stage — ошибки.

Порядок unified сборки:

1. Прежние configuration builder callbacks и построение legacy IConfiguration.
2. Все Setup callbacks: запись плана профиля/bindings и обычных service registrations.
3. Прежние context.Configuration callbacks, включая AddUnit/UnitContext semantics.
4. Загрузка unified sources; binding **всех** AddSchema, callbacks ConfigureModule,
   Required/semantic validation и копирование. До успешного завершения всех bindings
   ни один register callback не вызывается.
5. Register callbacks в порядке объявления с отдельными копиями validated options;
   private options снимка им не передаются. Они подключают
   существующие Use/AddModule и добавляют Services/legacy Configuration callbacks.
   Новые legacy Configuration callbacks выполняются очередью **после всех register**:
   это сохраняет сбор всех AddUnit definitions до Prepare. Источники/schema/bindings
   unified на этой фазе уже менять нельзя; legacy Set не модифицирует unified snapshot.
6. Регистрация reader и сборка прежнего resolver, diagnostics/CreateApp/startup.
   ConfigureModule не является factory activation; ресурсы создаются только DI/lifecycle.

App.Build остаётся синхронным. Data-only LoadAsync выполняется без захвата вызывающего
SynchronizationContext; при синхронном ожидании App adapter запускает только загрузку
данных в изолированном worker context. Setup/register/DI/View factories туда не
переносятся. Внешний source не имеет доступа к UI/resolver и обязан поддерживать
cancellation своего async API. Синхронный Build не получает новую гарантию прерывания
непослушного source. Standalone caller может await Profile.LoadAsync(ct).

IConfigurationReader — nondisposable borrowed alias; App владеет одним runtime.
После Dispose runtime доступ через aliases бросает ObjectDisposedException;
уже полученные immutable snapshots остаются обычными значениями без owned ресурсов.
Legacy IConfiguration остаётся доступен. После CORE7.4 явный UseCoreConfiguration
связывает standard App:Name/Paths:Root, UseConfiguredDiagnostics — diagnostic data;
без такого подключения legacy поведение сохраняется. Новые consumers не используют
object bag для выбора diagnostic resources. Старые UseModule callbacks остальных
модулей сохраняют прежний способ вызова; CORE7.5 добавляет UseConfigured helpers
для источникового binding через optional App configuration stage. Повтор Setup не пересоздаёт ресурсы; ошибки
регистрации/сборки очищаются прежним App rollback и не публикуют новый runtime.

### Reload, уведомления и запись (CORE7.3)

Reader.Current — атомарное чтение уже готового immutable snapshot, generation=1
после Build. Reloader строит полный candidate и повторяет conversion/binding/validation
без повторного register/UseModule/создания сервисов. Схема и configure callbacks
frozen; callbacks обязаны оставаться чистыми. Ошибка бросает ConfigurationException,
текущий снимок/generation не меняются. Отмена до публикации имеет тот же эффект;
после commit нельзя вернуть видимость «ничего не изменилось» через cancellation.

Если изменился StartupOnly field, весь candidate отклоняется: Published=false,
RestartRequired=true, прежняя generation. Для полностью Reloadable изменения
публикуются вместе с подготовленными options, generation увеличивается на 1.
Без изменений Published=false/RestartRequired=false, generation прежняя.
Existing injected T остаётся startup copy: dynamic module явно читает
Current.GetOptions(schema); семантическое применение к уже работающему ресурсу — его контракт,
а не повторное подключение UseModule. Нельзя объявить поле Reloadable только потому,
что его тип допускает изменение. До отдельного согласования **все module fields StartupOnly**.

Changes.Subscribe доставляет только успешно опубликованные изменения; отказ/restart
возвращается caller ReloadAsync, не считается изменением generation. ChangedKeys
использует canonical schema names с детерминированной Ordinal сортировкой.
Это отдельный bounded канал (максимум 64 подписки, один pending
latest-update на подписчика с объединением ChangedKeys). Результат IDisposable
снимает подписку, не освобождает runtime. Callback выполняется вне publication lock;
исключение регистрируется с маскированием данных, не откатывает commit. Stop закрывает
admission, отменяет доставку и ожидает cooperative callbacks; ignoring-token/blocking
callback не получает обещание принудительного завершения. Disposable aliases runtime
в DI не регистрируются. Automatic watcher не входит в первый профиль.

Writer отдельно адресует mutable source по ID; args/env/default/override read-only.
ConfigurationEdit.Value=null **удаляет** ключ, JsonElement с ValueKind.Null записывает
явный JSON null. Patch проверяет известные keys/types и сериализуемость до IO;
не публикует reader generation и не вызывает reload автоматически. После успешной
записи caller явно вызывает ReloadAsync. Ошибка IO не меняет текущий memory snapshot;
сохранность файла при partial write/power loss/external writer не обещается.
Writer/reload сериализуются одним runtime; это не межпроцессная транзакция.

Уточнение реализации2026-11-10: diff сравнивает canonical typed raw values,
ReadState и Origin; смена источника учитывается даже при равном значении.
Configure не определяет ChangedKeys; зависимость startup options от dynamic поля
требует отдельного module contract. Callback не должен ждать App Stop/Dispose,
который сам ожидает его завершения. Retiring callback сохраняет capacity slot
до выхода. Стандартный JSON mutable; optional IConfigurationMutableSource и
IConfigurationProfileWriter добавляют запись без новых обязательных членов.
JSON patch целиком валидируется до IO, staging/rename предшествует success;
форматирование нормализуется. [Рабочее руководство](CONFIGURATION_RELOAD.md).

<a id="examples"></a>
## Три сценария подключения

Образцы ниже компилируются вместе с declarations, **не исполняются**. Existing
App/DI/Mithril/Aether/Editor и configuration типы берутся из локальных assembly.
Это type-check схемы подключения; рабочие quickstarts находятся в App/reload guides.

### 1. Headless App на двух DI

```csharp
// api: headless
using TrueMoon;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Services;
using MicrosoftBuilder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace ConfigurationDesignExamples
{
    internal sealed record WorkerOptions
    {
        public int BatchSize { get; set; } = 32;
    }
    internal static class HeadlessExample
    {
        internal static readonly IConfigurationSchema<WorkerOptions> Schema = ConfigurationSchemas.Create(
            "Worker", () => new WorkerOptions(), value => value with { },
            value => { if (value.BatchSize is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(value.BatchSize)); },
            fields => fields.Field("BatchSize", value => value.BatchSize,
                (value, size) => value.BatchSize = size, new(Aliases: ["--batch", "-b"])));

        internal static IApp Build(string[] args, IServiceResolverBuilder resolver)
        {
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(Schema);
                builder.AddEnvironment("env", "TRUEMOON_");
                builder.AddArguments("cli", args);
            });
            return new AppBuilder(resolver).Setup(context =>
            {
                context.UseConfiguration(profile);
                context.ConfigureModule(Schema, (app, options) =>
                    app.Services(services => services.Instance(options)));
            }).Build();
        }

        internal static IApp Cobalt(string[] args) => Build(args, new CobaltServiceResolverBuilder());
        internal static IApp Microsoft(string[] args) => Build(args, new MicrosoftBuilder());
    }
}
```

### 2. Parent и Mithril child

Схема конфигурации unit не включает назначенные -u/-p/control pipe. Public env prefix
одинаков в parent/child; служебный wire и legacy Prepare продолжают работать отдельно
до CORE7.4. Этот пример не объявляет устранённой прежнюю возможность CLI/env override
legacy identity. В CORE7.4 producer и parent handler переводятся вместе на protected
typed snapshot, с проверками index/parent/endpoint до активации handler.

```csharp
// api: mithril
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Mithril;
using TrueMoon.Services;

namespace ConfigurationDesignExamples
{
    internal sealed record UnitOptions
    {
        public int RestartLimit { get; set; } = 1;
    }
    internal static class MithrilExample
    {
        internal static IApp Build(IServiceResolverBuilder resolver)
        {
            var schema = ConfigurationSchemas.Create("Mithril:Worker", () => new UnitOptions(),
                value => value with { },
                value => { if (value.RestartLimit < 0) throw new ArgumentOutOfRangeException(nameof(value.RestartLimit)); },
                fields => fields.Field("RestartLimit", value => value.RestartLimit,
                    (value, limit) => value.RestartLimit = limit));
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(schema);
                builder.AddEnvironment("unit-settings", "TRUEMOON_");
            });
            return new AppBuilder(resolver).Setup(context =>
            {
                context.UseConfiguration(profile);
                context.ConfigureModule(schema, (app, options) => app.AddUnit(
                    child => child.Services(services => services.Instance(options)),
                    unit => { unit.Name = "worker"; unit.RestartLimit = options.RestartLimit; }));
            }).Build();
        }
    }
}
```

### 3. Editor и pre-App Aether Runtime

RuntimeLaunch поступает typed аргументом composition root: executable/assembly/args
не угадываются по текущему процессу. Host/build identity также не берётся из user
config. Child читает только два назначенных env keys через общий helper и сохраняет
существующий RuntimeBootstrap.Parse с limits/parent/nonce/build checks. Native window,
fonts и запуск/остановка сервисов здесь не выполняются.

```csharp
// api: editor
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Hosting;
using TrueMoon.Configuration;
using TrueMoon.Services;

namespace ConfigurationDesignExamples
{
    internal sealed record EditorInputs
    {
        public string? ProjectPath { get; set; }
    }
    internal static class EditorExample
    {
        internal static IApp Build(string[] args, string reports, string hostContractId,
            NativeRuntimeLaunch launch, IServiceResolverBuilder resolver)
        {
            if (!Path.IsPathFullyQualified(reports)) throw new ArgumentException("Reports must be absolute.");
            var schema = ConfigurationSchemas.Create("Editor", () => new EditorInputs(),
                value => value with { }, value =>
                {
                    if (value.ProjectPath != null && (!Path.IsPathFullyQualified(value.ProjectPath) ||
                        !value.ProjectPath.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase)))
                        throw new ArgumentException("Project must be an absolute .aetherproj path.");
                }, fields =>
                {
                    fields.Field("ProjectPath", value => value.ProjectPath,
                        (value, path) => value.ProjectPath = path, new(Aliases: ["--project"]));
                });
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(schema);
                builder.AddEnvironment("editor-env", "TRUEMOON_");
                builder.AddArguments("editor-cli", args);
            });
            return new AppBuilder(resolver).Setup(context =>
            {
                context.UseConfiguration(profile);
                context.ConfigureModule(schema, (app, options) => app.UseAetherEditor(engine =>
                {
                    engine.HostContractId = hostContractId;
                    engine.GameBuildId = new string('0', 64);
                    engine.ReportDirectory = reports;
                    engine.RuntimeLaunch = launch;
                }, editor => editor.ProjectPath = options.ProjectPath));
            }).Build();
        }

        internal static RuntimeBootstrap ReadChildBootstrap()
        {
            var environment = ConfigurationEnvironment.Capture("TM_ENGINE_BOOTSTRAP", "-p");
            return RuntimeBootstrap.Parse(environment["TM_ENGINE_BOOTSTRAP"] ?? "", environment["-p"] ?? "");
        }

        internal static IApp BuildChild(IServiceResolverBuilder resolver)
        {
            var bootstrap = ReadChildBootstrap();
            var launch = bootstrap.Launch;
            return new AppBuilder(resolver).Setup(context =>
            {
                context.UseAether(engine =>
                {
                    engine.HostContractId = launch.Identity.HostContractId;
                    engine.GameBuildId = launch.Identity.GameBuildId;
                    engine.ReportDirectory = launch.ReportDirectory;
                    engine.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
                });
                if (launch.Role == TrueMoon.Aether.Sessions.RuntimeRole.EditPreview)
                    context.UseAetherPreviewRuntime(launch, bootstrap.ParentProcessId);
                else context.UseAetherRuntime(launch, bootstrap.ParentProcessId);
            }).Build();
        }
    }
}
```

<a id="delivery"></a>
## Границы поставки и проверок

CORE7.0 фиксирует signatures/semantics и type-check образцов. CORE7.1 поставляет
sources/schema metadata/normalization/standalone parser и effective values;
CORE7.2 — binding, App stage и reader; CORE7.3 — reload/changes/writer. Не публиковать
пустые feature interfaces как работающие services и не копировать заглушки в пакеты.
CORE7.4/5 переводят consumers и добавляют модульные schema APIs; старые callbacks
при этом не меняют порядок исполнения без явного выбора нового stage.

Все изменения API добавочные; прежние interfaces и assembly identities остаются.
Решение не назначает breaking release и не меняет общую версию. Перед поставкой
реализации проверить public API diff/пакеты; только тогда определить номер выпуска.
Independent package/DI/child/native проверки выполняются на соответствующих этапах,
их не заменяет компиляция текста с декларациями. Детальные критерии — в PLAN.

Повторяемая проверка формы API:
[ManualTests/ConfigurationApiDesign/Verify.ps1](../../ManualTests/ConfigurationApiDesign/Verify.ps1).
Она извлекает четыре C# блока, создаёт изолированный net10.0 library project без
NuGet sources/родительских build/package imports и компилирует Release с warnings
as errors против имеющихся Debug/net10.0 assembly TrueMoon/Cobalt/MS DI/Aether/Editor.
Runtime не запускается, production проекты не пересобираются. Для свежего API proof
эти assemblies сначала должны быть актуальны; текущая проверка фиксирует их SHA,
а не выдаёт их за свежую сборку. Артефакты/manifest/binlog — TestResults/ConfigurationPlan/CORE70Api.
