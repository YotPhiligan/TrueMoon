# Конфигурация App: отложенная проверка и reader

CORE7.2, 2026-11-10. [Standalone sources/parser](CONFIGURATION_USAGE.md),
[API-контракт](CONFIGURATION_API.md), [план](PLAN.md#core7-unified-configuration).

## Подключение

Schema/profile создаются прежними standalone API. В Setup выбрать один профиль
и объявить ConfigureModule; callbacks регистрации выполнятся после проверки всех
schemas. Пример продолжает `schema/profile` из standalone guide:

```csharp
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Services;

using var app = App.Build(context =>
{
    context.UseConfiguration(profile);
    context.ConfigureModule(schema,
        (module, options) => module.Services(services => services.Instance(options)),
        options => options.BatchSize = Math.Min(options.BatchSize, 512));
});

// Composition/infrastructure access; services normally receive options via constructor.
var reader = app.Services.Resolve<IConfigurationReader>();
var snapshot = reader.Current;
var optionsCopy = snapshot.GetOptions(schema);
```

Для настоящего модуля register callback вызывает его существующий Use/AddModule,
передавая подготовленные значения через typed options/configure callback. Не создавать
services/resources внутри configure/validate: они чистые и выполняются до DI.
Регистрация T options явно определяется модулем: ConfigureModule не регистрирует
каждый DTO автоматически. Источник programmatic overrides применяется раньше
configure; semantic validation выполняется после него.

Schema должна быть **тем же экземпляром**, который передан в AddSchema. Один
profile и один ConfigureModule на section; несколько Setup callbacks допустимы.
Порядок ConfigureModule относительно UseConfiguration в Setup не важен. Bind идёт
в порядке AddSchema; register callbacks — в порядке ConfigureModule. Даже schemas
без module callback проходят проверку и доступны через GetOptions.

## Порядок выполнения

1. Построение прежнего IConfiguration и все Setup callbacks.
2. Legacy context.Configuration callbacks. Они могут объявить профиль/bindings.
3. Freeze stage, проверка catalog/schema identity, загрузка raw values.
4. Bind всех schemas, configure callbacks, semantic validation и глубокие копии.
   Затем готовятся копии **для всех register callbacks**, прежде чем вызвать первый.
5. Все register callbacks; добавленные ими legacy callbacks исполняются после всей
   группы. Поэтому AddUnit Prepare видит все unit definitions, включая объявленные
   позднейшим register callback. UnitContext сохраняет immediate configuration semantics.
6. Сборка resolver и активация одного App-owned runtime; reader — недиспозируемый
   borrowed alias. Далее обычные CreateApp/start/stop/dispose.

Только data-only LoadAsync выполняется на worker без caller SynchronizationContext.
Setup/bind/register/DI остаются на caller thread. App.Build синхронный: новый API
не добавляет async service activation и не обещает прервать непослушный source.

## Чтение и владение

- IConfigurationReader.Current возвращает уже готовый immutable snapshot generation=1,
  без IO, resolution или повторного binding. GetOptions(schema) возвращает отдельную
  копию prepared options; configure/validate повторно не вызываются.
- Raw Read и Origin описывают источники до configure callback. Например raw Count=12
  и prepared Count=13 одновременно корректны; callback не переписывает provenance.
- Injected options — startup copy. Для горячего цикла сохранить внедрённую зависимость,
  не выполнять Resolve или GetOptions на каждый кадр. Динамическое чтение и явный
  reload описаны в [CORE7.3 guide](CONFIGURATION_RELOAD.md).
- App владеет runtime через singleton registration factory. Reader не реализует
  IDisposable/IAsyncDisposable и не создаёт второго владельца. Dispose App закрывает
  reader.Current; уже выданные snapshots/options остаются независимыми данными.
- Stop не освобождает provider: reader доступен до Dispose. Failed start/creation
  сохраняют прежний App cleanup contract; reader закрывается при освобождении графа.

Прежний IConfiguration зарегистрирован отдельно и сохраняет Get/Set/GetSection.
Без явного UseCoreConfiguration App.Name читает legacy appName. Standard Core schema
и UseCoreConfiguration фиксируют App:Name/Paths:Root; произвольная пользовательская
App schema сама по себе не подменяет имя. [Core/Mithril migration](CONFIGURATION_CORE_MITHRIL.md)
реализована в CORE7.4; [остальные модули/private Aether bootstrap](CONFIGURATION_MODULES.md) — в CORE7.5.

## Дополнительный каталог и ошибки

Для App требуется optional IConfigurationProfileSchemas: VisitSchemas передаёт
точные typed instances через IConfigurationSchemaVisitor.Visit<T>. Встроенный
ConfigurationProfiles.Create реализует capability. Сторонний IConfigurationProfile
без catalog остаётся допустимым standalone профилем, но UseConfiguration явно
бросает NotSupportedException. Старые interfaces не получили mandatory members.

Ошибки stage state (повтор profile/binding, отсутствие profile, frozen mutation,
подмена зарезервированного reader) — InvalidOperationException. Неизвестный или
другой instance schema — ArgumentException до Load/DI. Значения и semantic/copy
failures — ConfigurationException без raw messages. Неподдерживаемый context —
NotSupportedException. Custom profile должен выдать immutable raw snapshot generation=1.

В недействительном graph/load/bind ни один module register callback/DI factory
не выполняется. Register callbacks предназначены только для composition: после
их ошибки внешние side effects не откатываются автоматически. Если resolver уже
создан, CreateApp failure освобождает его обычным App rollback; незавершённый
provider внутри custom IServiceResolverBuilder остаётся ответственностью builder.

## Проверки

[AppConfigurationStageTests](../../TrueMoon.Tests/Configuration/AppConfigurationStageTests.cs)
проверяют порядок фаз, обе DI реализации, schema catalog, private copies, повторные
Build, два App, caller thread, failures/disposal и denial paths.
[Consumer](../../ManualTests/ConfigurationAppConsumer/Program.cs) и
[runner](../../ManualTests/ConfigurationAppConsumer/Verify.ps1) проверяют настоящие
две Mithril MainProcess units, оба DI, source/published/fresh-package profiles,
unrelated cwd и отдельный package cache. OS child/GPU/window не запускаются.
Результаты App stage — [CORE7.2 history](HISTORY.md#core72-2026-11-10); расширенный consumer и актуальные counts/version — [CORE7.3 history](HISTORY.md#core73-2026-11-10).
CORE7.3 добавляет [explicit reload, notifications и write](CONFIGURATION_RELOAD.md).
CORE7.4 добавляет [Core settings и защищённый Mithril bootstrap](CONFIGURATION_CORE_MITHRIL.md).
CORE7.5 добавляет [модульные consumers](CONFIGURATION_MODULES.md); следующий этап — CORE7.6.
