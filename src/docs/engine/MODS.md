# Mod SDK и managed collectible loader (ARCH3.6)

SDK находится в `TrueMoon.Aether.Abstractions.Mods` и использует общие `IFactory`
из `TrueMoon.Contracts`. Мод с собственными компонентами/системами не требует
Runtime, Tooling, Editor, Cobalt или Microsoft DI. Реализация загрузчика и
App-интеграция находятся в Runtime; явная публикация артефактов — в Tooling.
`GameBuildLoader` остаётся отдельным загрузчиком game sessions.

ARCH3.6 предоставляет загрузку и полное освобождение группы, включая её частные
World. [Замена систем на существующем stable World — ARCH3.7](MOD_RELOAD.md), перенос данных модовых
CLR-компонентов в новый World — [отдельная owned-World сессия ARCH3.8](MOD_MIGRATION.md).
`WholeGroup` определяет границу замены;
автоматического watcher нет. Явный capture/commit предоставляется отдельным
`ModSystemSession`; обычный load/unload путь ARCH3.6 не выполняет replacement сам.

## Entry point и локальный граф

Публичный concrete тип реализует `IModEntryPoint` со статическим
`Create(IModHostServices)`. Вызов выполняет внедрённый
`IFactory<IModEntry, ModEntryArguments>` (`ModEntryFactory` по умолчанию).
Фабрика возвращает новый group-owned `IModEntry`. При исключении до возврата
именно фабрика освобождает свои неопубликованные ресурсы.

Entry получает зависимости при создании и передаёт их конструкторам своих
сервисов/систем через локальные типизированные фабрики. `Register` объявляет:

- `AddComponent<T>(schema, IFactory<T, ComponentData>)`: точное соответствие
  нейтральной schema и reference-free struct, scalar values/defaults;
- `AddSystem(metadata, IFactory<ISystem>)`: immutable access/order declarations,
  новый scheduler-owned экземпляр при каждом создании World;
- `AddService(id, IFactory<IModService>)`: один свежий group-owned экземпляр.

Ресурсы самих фабрик принадлежат entry; регистрация заимствует фабрики до
завершения группы. Aliased service/entry results запрещены. Scheduler сохраняет
свои существующие проверки alias/graph/access. Для importer с ресурсами и
entity/asset reference resolution scalar SDK API пока не предназначен.

`IModHostServices.GetRequired<T>()` доступен только в composition. Разрешены
типы явно shared API и framework; все generic/element arguments проверяются
рекурсивно. Запрос `IFactory<PrivateModSystem>` отклоняется до обращения к root
resolver, чтобы его cache не удержал collectible Type. После registration
wrapper закрывается и освобождает resolver. В `Execute` используются уже
внедрённые зависимости. Мод не получает root `IServiceResolver`, `IServiceProvider`,
`IApp` или bootstrap/registration owners: они блокируются и как requested types,
и как returned values. Стабильная shared host API сама отвечает за то, чтобы не
отдавать контейнер через свои методы или свойства.

**Не регистрировать модовые Types, фабрики, делегаты или instances в App DI,
static caches или долгоживущих host subscriptions.** Своё сохранённое состояние
мод освобождает в Stop/Dispose. Дополнительная shared game API сборка тоже должна
иметь стабильный lifetime; host API не должен хранить мод после отписки.

## Manifest и артефакт

`ModManifest` — документ данных, не configuration provider. Manifest/API versions
обязательны и сейчас равны 1. `ModManifestCodec` принимает строгий JSON: неизвестные
и повторные members, implicit protocol versions, malformed/null fields отклоняются.

Поля: `id`, `version`, `reloadGroup`, `reloadPolicy`, `entryAssembly`, `entryType`,
`dependencies`, `files`, `sharedAssemblies`, `formatVersion`, `apiVersion`.

- IDs: 1..128 lowercase ASCII `a-z`, `0-9`, `.` и `-`, начало — буква.
- Module versions: canonical `major.minor.patch`, без prerelease/build suffix.
  Dependency interval: inclusive `minimumVersion`, exclusive
  `maximumVersionExclusive`; пустой/обратный диапазон запрещён.
- Только `WholeGroup`. Запрос содержит root и всю transitive dependency closure,
  без посторонних модулей; все имеют одинаковый `reloadGroup`. Cycles, duplicate
  IDs, missing dependencies и conflicting ranges отклоняются до выполнения кода.
- Directory name равен lowercase SHA256 **точных bytes `manifest.json`**.
  Каталог плоский, содержит только manifest и перечисленные `.dll`. Traversal,
  reparse points, extra files и alias filenames не разрешены.
- Каждая private DLL имеет точный размер и SHA256. По умолчанию bounds: 32 модуля,
  64 DLL на модуль, 128 KiB на manifest, 128 MiB private bytes на группу;
  до 256 managed references на DLL. Host копирует/валидирует `ModLoaderOptions`.
- Abstractions и Contracts обязательно shared по **точным Assembly.FullName и
  SHA256**. Дополнительные APIs задаются host allowlist
  `AdditionalSharedAssemblies` из default ALC; мод сам не расширяет allowlist.
  Изменение contract bytes требует coherent rebuild/republication SDK/host/mod.
- Private references ограничены собственным модулем и объявленной dependency
  closure. Конфликтующие simple names/identities/hashes запрещены; одинаковые
  identity+bytes могут использоваться совместно внутри группы.

Загрузчик проверяет managed PE metadata и references до entry activation,
отклоняет mixed-mode/netmodules/native imports. После проверки он загружает
проверенные bytes через `LoadFromStream`, удерживая read leases до cleanup/unload
request. Это сохраняет точный snapshot даже на платформах, где read lease не
запрещает внешнее изменение файла. Обновление публикует **новый каталог**, никогда
не переписывает DLL текущей generation.

`ModArtifactPublisher.Publish(root, manifest, privatePaths, additionalShared)`
копирует в owned staging, вычисляет file/shared hashes, записывает manifest и
атомарно переименовывает staging. Существующий равный артефакт используется
повторно; повреждённый отклоняется без перезаписи. Publisher не является admission
validator: совместимость с конкретным host и полной closure проверяет Runtime.

## Shared/private resolution

Вся группа использует **один collectible AssemblyLoadContext**. Entry assembly
загружается из проверенного private catalog. Общие API возвращаются из host;
private references разрешаются только по полному проверенному identity. Framework
fallback ограничен сборками установленного runtime directory и совместимыми
framework identities. App TPA/working directory/NuGet cache не используются как
источники private DLL. Произвольный `AssemblyDependencyResolver` probing отсутствует.

Это управляемое выполнение доверенного локального кода, а не security sandbox.
Native DLL mods и принудительный thread abort не поддерживаются. Мод обязан
кооперативно остановить tasks, таймеры, события и callbacks; API не обещает
безопасно выгрузить бесконечный поток или недисциплинированную подписку.

## App, ownership и ошибки

```csharp
var app = App.Builder(_ => { }).Setup(context =>
{
    // Stable host APIs/services register here before loading any module.
    context.UseAetherEcs();
    context.UseAetherMods(options =>
    {
        options.MaximumModules = 16;
        // options.AdditionalSharedAssemblies = [typeof(IMyGameApi).Assembly];
    });
}).Build();
// Composition root:
var mods = app.Services.Resolve<ModHost>();
var result = await mods.LoadAsync(new ModLoadRequest("my-mod", artifactDirectories));
```

`UseAetherMods` не запускает native/UI/Tooling, требует `UseAetherEcs`, повторное
подключение отклоняет. Регистрируется единственный disposable owner `ModHost`;
его start/stop aliases — nondisposable adapter. Cobalt и Microsoft DI используют
одни contracts. При необходимости entry factory задаётся до `UseAetherMods`.
Factory implementations не должны кэшировать entry Types или results.

После validation factories создают entries dependency-first, затем локальные
services; services запускаются прежде entries. `StartAsync` token относится к
отмене admission/startup. Успешно запущенные tasks должны иметь собственный
кооперативный Stop/drain, не полагаться на продолжение startup token после старта.
Host владеет опубликованной группой и failed unpublished owner; возвращаемый
`ModGroup` — borrowed handle. `CreateWorld` создаёт group-owned частную сцену,
используя внедрённые factories, существующий scalar loader и scheduler. Системы
видят настоящий ECS через Abstractions, без resolution на кадр.

Stop закрывает admission и отменяет pending startup **до ожидания gate**. Cleanup
ждёт активный tick/creation, останавливает entries и services reverse-order,
затем освобождает schedulers/systems **до Worlds**, services и entries. Только
после успешного cleanup очищаются registries/factories/Types, вызывается
`ALC.Unload` и освобождаются artifact leases. Обращения к группе/сессии сериализованы;
reentry в тот же group из module callbacks не поддерживается.

`ModLoadResult.Diagnostic` и `ModDiagnostic` содержат только bounded host-owned
строки/error enum. Модовые exceptions/InnerExceptions/Types не экспортируются.
`InvalidManifest`, `UnsupportedApi`, `LimitExceeded`, `HashMismatch`,
`MissingDependency`, `DependencyConflict`, `SharedContractMismatch`, `InvalidEntry`,
`ActivationFailed`, `Cancelled`, `CleanupFailed`, `AdmissionClosed` различаются.
Ошибка World creation/tick закрывает group и даёт нейтральный
`ModOperationException`; частичный owner остаётся для cleanup.

При `CleanupFailed` unload не запрашивается: owner остаётся у host для явного
повторного `group.UnloadAsync()` или `host.StopAsync()`. Успешные entry/service
Stop/Dispose не повторяются; неудачный entry/service шаг повторяется. Системы
сохраняют существующий cleanup contract EcsScheduler (включая retry failed Dispose).
При failed startup + failed cleanup
outcome указывает исходный startup code и возвращает retained group. App stop/
dispose делает cleanup failure видимым через `ModOperationException`.

## Подтверждение unload и проверки

`ModUnloadTicket` содержит только WeakReference. `UnloadRequested=true` означает
выполненный cleanup и запрос unload, а `IsAlive=false` — завершённую сборку ALC.
Проверять это нужно после выхода из loader/module stack frames. Forced GC и
`NoInlining` helper используются только в diagnostics/tests, не в игровом цикле.
Удержание закрытого `ModGroup`/`ModWorldSession` не удерживает модовые CLR types.
Если мод оставил внешние roots, ticket останется alive: host не выдаёт это за
завершённую выгрузку.

Техническое основание: [официальное руководство .NET по unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability),
[AssemblyLoadContext и shared dependencies](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext).
Пример package-only mod и воспроизводимая поставка:
[`ManualTests/ModConsumer`](../../ManualTests/ModConsumer/README.md).
Текущие фактические результаты и SHA фиксируются отдельно в ARCH36 baseline/HISTORY.
