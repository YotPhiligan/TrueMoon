# Reload, уведомления и явная запись

CORE7.3, 2026-11-10. [Подключение App](CONFIGURATION_APP.md),
[источники и schemas](CONFIGURATION_USAGE.md), [контракт](CONFIGURATION_API.md).

App с `UseConfiguration(profile)` предоставляет четыре borrowed services:
`IConfigurationReader`, `IConfigurationReloader`, `IConfigurationChanges`,
`IConfigurationWriter`. Они не реализуют Dispose. Один runtime принадлежит App;
источники и schemas являются заимствованными data adapters, не owned resources.

```csharp
// В composition root передайте нужные зависимости через конструктор сервиса.
// Здесь reader, reloader, changes и writer уже внедрены через DI.
using var subscription = changes.Subscribe(update =>
{
    // Короткая реакция: например, поставить команду в очередь модуля.
    // Для согласованного чтения нескольких полей захватите Current один раз.
    var current = reader.Current;
    var options = current.GetOptions(schema);
});

await writer.WriteAsync("user-json", [
    new ConfigurationEdit("Demo:Count", JsonSerializer.SerializeToElement(12)),
    new ConfigurationEdit("Demo:Optional", null) // удалить, чтобы разрешить fallback
]);
var result = await reloader.ReloadAsync(cancellationToken);
// result.RestartRequired: весь кандидат отклонён, нужен новый App.
// result.Published: готовый raw+options снимок опубликован с generation+1.
```

## Атомарная публикация

Начальная generation равна 1. Reload сериализован с другими reload/write одного
runtime и заново загружает все источники, конвертирует значения, выполняет все
schema binding/configure/validation и создаёт detached options. Module register,
UseModule, DI factories и создание сервисов не повторяются. `Current` заменяется
одной атомарной операцией после успешной подготовки всех schemas.

Ошибка либо отмена до commit сохраняет прежний объект Current и generation.
После commit результат сообщает публикацию даже при последующей отмене caller.
No-op возвращает Published=false/RestartRequired=false без увеличения generation.
Изменение любого StartupOnly ключа отклоняет весь кандидат, включая Reloadable
ключи; возвращает RestartRequired=true и прежнюю generation.

Сравнение выполняется по canonical typed raw values до configure, ReadState и
Origin (source/priority/order). Codec.Write задаёт JSON-представление сравнения;
структурное равенство объектов не зависит от порядка свойств. Разная запись одного
типизированного числа в одном источнике не создаёт изменения. Смена источника
учитывается, даже при равном значении. ChangedKeys содержит canonical имена в
Ordinal порядке. В закрытии typed readers используется reflection один раз при
composition; Current/Read/GetOptions не выполняют reflection, IO или resolution.

Configure/copy/validate/codec callbacks обязаны быть чистыми и детерминированными.
Configure не участвует в определении ChangedKeys. При зависимости startup-поля от
reloadable-поля модуль должен согласовать собственный контракт применения;
такую зависимость нельзя считать безопасным hot reload автоматически.
Пока module schemas не мигрированы, их поля остаются StartupOnly по умолчанию.

Уже внедрённый T остаётся startup copy. Динамический потребитель явно захватывает
новый Current и GetOptions; применение к ресурсам выполняет сам модуль. Старые
snapshots и их options не изменяются и остаются доступными после Dispose App.
Standalone profile.LoadAsync по-прежнему возвращает отдельный raw snapshot с
generation=1; автоматического watcher нет.

## Ограниченные уведомления и остановка

Подписчики получают только успешные публикации. Отказ, ошибка и no-op не вызывают
callback. Не более 64 capacity slots; у каждого один исполняемый callback и один
pending update. При coalescing остаётся последняя generation и объединение
ChangedKeys, поэтому уведомление не является журналом всех промежуточных версий.
Current к моменту callback может быть новее его update.

Callbacks выполняются на worker вне publication lock и сериализованного gate.
Они могут читать Current, повторно reload/write и снимать свою подписку. Ошибки
изолируются; диагностика содержит только безопасный код NotificationCallback.
Dispose handle снимает подписку и pending delivery; уже исполняемый callback
может закончиться позднее. Он сохраняет capacity slot до выхода, чтобы повторное
subscribe/unsubscribe не создавало неограниченное количество workers.

App Stop закрывает reload/write/subscribe admission, отменяет незавершённые data
operations и pending deliveries, ожидает исполняемые callbacks. Borrowed lifecycle
adapter запускается перед сервисами и участвует также в failed-start rollback.
Reader доступен до Dispose, stopped операции бросают InvalidOperationException;
после Dispose aliases бросают ObjectDisposedException. Повторный Stop/Dispose
безопасен. Caller shutdown token ограничивает ожидание, а не принудительно
завершает блокирующий callback/source. Callback должен быстро возвращаться и
не ждать App Stop/Dispose: shutdown сам ожидает его завершения.

## Явная запись JSON

Стандартный JSON source поддерживает optional `IConfigurationMutableSource`.
Built-in profile реализует optional `IConfigurationProfileWriter`; существующие
source/profile interfaces не получили обязательных членов. Custom profile без
writer capability остаётся пригоден для чтения/reload и отклоняет запись.

Source ID сравнивается Ordinal; неизвестный ID не раскрывается в ошибке.
Defaults, env, args и explicit overrides доступны только для чтения. Patch
копируется при вызове, целиком проверяет known keys, case collisions, типы,
Undefined и сериализуемость до source IO. Весь профиль и semantic validators
повторно проверяются при явном ReloadAsync, а не при source patch.

`ConfigurationEdit.Value = null` удаляет ключ. Для явного null используйте
`JsonSerializer.SerializeToElement<string?>(null)`. После удаления разрешение
приоритетов может выбрать defaults или другой источник. Запись lower-priority
source не гарантирует изменение effective значения при наличии override.
Пустой patch не выполняет JSON IO; отсутствующий optional файл создаётся только
при явной записи, родительская папка автоматически не создаётся.

JSON writer читает актуальный выбранный файл и применяет patch, сохраняя остальные
известные поля. Нормализует структуру и форматирование; комментарии/исходные
пробелы не сохраняются. Полный текст сначала записывается во временный файл рядом
с целевым, затем заменяет его rename. Ошибка записи не меняет Current; корректную
операцию можно повторить. Запись не увеличивает generation, не уведомляет и не
вызывает reload автоматически. Отмена после rename не сообщается как отменённый
write. Custom mutable adapter обязан соблюдать такую же границу commit.

Общий runtime gate не является межпроцессной транзакцией. Согласование нескольких
App/процессов, внешних писателей, потеря питания, filesystem durability и recovery
после сбоев ОС требуют отдельного контракта. Запись собственного файла и успешный
reload не доказывают runtime reconfiguration native/GPU/child ресурсов.
