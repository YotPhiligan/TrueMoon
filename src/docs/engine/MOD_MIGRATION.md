# ARCH3.8: модовые компоненты и новый World

`ModHost.CreateWorldMigrationSession()` создаёт App-owned сессию, которая владеет
candidate/current/retired Worlds и collectible groups. Это отдельный managed
профиль поверх [Mod SDK](MODS.md). Для stable shared компонентов остаётся
[`CreateSystemSession(World)`](MOD_RELOAD.md): его World по-прежнему caller-owned.
Tooling/UI, native loading и изменение game IPC форматов этому API не нужны.

## Использование и владение

```csharp
// Composition root: UseAetherEcs(); UseAetherMods(); затем App.StartAsync().
var host = app.Services.Resolve<ModHost>();
var session = host.CreateWorldMigrationSession();
var initial = await session.LoadAsync(initialRequest, seed);
if (!initial.Committed) throw new InvalidOperationException(initial.Diagnostic.ToString());
session.Tick(TimeSpan.FromMilliseconds(20));
var result = await session.ReplaceAsync(candidateRequest);
if (result.Committed)
{
    var currentHandle = session.Entity(persistentEntityId);
}
// CleanupFailed не отменяет Committed; RetryCleanupAsync освобождает retained owners.
// App stop/dispose освобождает сессию и Worlds; заимствующий код их не Dispose.
```

Resolution host относится к composition root; прикладному коду передавать host/
session/factory явно. На tick resolution нет. Прямой `IFactory<ModWorldMigrationSession>`
возвращает caller-owned результат: его нужно успешно очистить до освобождения App.
Через host результат заимствованный. Повторный UseAetherMods отклоняется. Поддержаны
Cobalt и Microsoft DI adapter. `CurrentWorld` действителен до следующего commit.
Прямой доступ к World сериализует вызывающий код с session operations; повторный
вход в сессию из module callback запрещён. Между generations сохранять EntityId,
ComponentId и ComponentTypeId, а не handles/queries/commands/CLR Type/ref в pool.

## Binding и migrators

В `IModEntry.Register` вызываются новые методы `IModRegistration`:

```csharp
registration.AddMigratingComponent<Counter>(schema,
    new Factory<Counter, ModComponentReadArguments>(a =>
        new Counter(a.Data.Properties["value"].GetInteger())),
    new Factory<ComponentData, ModComponentWriteArguments<Counter>>(a =>
        new ComponentData(a.Id, schema.TypeId, schema.Version,
            new Dictionary<string, PropertyValue> { ["value"] = PropertyValue.From(a.Value.Value) })));
registration.AddComponentMigration(typeId, 1, 2, migratorFactory);
```

Counter — reference-free struct; IComponent необязателен. Import/export factories
локальны группе, их Types/delegates не входят в root DI. Entry владеет заимствованными
фабриками и их зависимостями. Export получает копию значения и возвращает прежние
occurrence/type IDs, точную версию и **все** schema properties: пропуск нельзя скрыть
под default. Import получает проверенные effective values. Один компонент CLR типа
на entity; managed-reference structs/AllowMultiple этот ECS не поддерживает.

При прежней версии сравниваются свойства/defaults/nullability/required types и
multiplicity. Private CLR struct может измениться при той же нейтральной схеме.
При новой версии обязателен точный migrator `(typeId, fromVersion, toVersion)`;
автоматической цепочки/конверсий нет. Migrator получает обе схемы/данные, сохраняет
IDs и возвращает target version. Результат проходит scene codec/resolution/registry.
`IModComponentMigrator` создаётся IFactory один раз на используемое правило операции,
является exclusively owned IDisposable. Aliasing с module/service/другим converter
отклоняется. Dispose выполняется до commit; failed Dispose отклоняет кандидата,
owner сохраняется для retry. Успешные converter slots очищаются. Мигратор не меняет
старый World и не выполняет внешние side effects.

Удаление полей требует точного `removedProperties` в правиле **и** host options
`new ModWorldMigrationOptions(AllowFieldRemoval: true)`. Unknown type, missing
binding/migrator и изменённая схема под прежней версией отклоняются. Автоматического
удаления component types, reset или потери system state нет.

## Снимок, идентичность и связи

ModWorldSnapshot содержит neutral SceneDocument, copied ModSystemState по точным
system IDs и номер завершённого tick. Это effective runtime данные: inheritance/
authoring history развёрнуты, документ проекта не перезаписывается. Сохраняются
scene ID/revision, entity/occurrence IDs, schema ID/version, name/sibling order и
parent graph. Runtime-created entities требуют TrackEntity, добавленные компоненты
— TrackComponent с постоянным occurrence ID. Неучтённые живые данные и unexportable
storage отклоняются. Удалённые данные не воскресают; dangling refs запрещены.

Binding явно объявляет `ComponentReference(..., Entity)`. Import использует
`IModEntityMap.Resolve(EntityId)` для handle нового World; export —
`Persistent(EntityHandle)` для ID только живой entity исходного World. Все entities
резервируются до import: поддержаны forward/self refs; parent graph проверяется.
Foreign/stale/dead handles не переносятся. EntityMap не раскрывает World/контейнер
и не удерживает CLR Types. Asset reference bindings v1 отклоняет явно; подготовкой
resources/assets владеет модуль.

ModWorldSnapshotCodec не загружает моды: strict JSON envelope v1 поверх scene codec.
Unknown/duplicate/missing members, format versions, malformed values/state IDs и
invalid scene data отклоняются. Бюджеты: envelope16MiB, scene4MiB/4096entities/
4096definitions/1024schemas,128system states,64KiB/state,4MiB total state. Дополнительно
действуют World/registry/scheduler budgets. Снимок не содержит live components,
Assembly/Type, exceptions или callbacks.

## Boundary, rollback и остановка

1. Проверить seed до module activation; подготовить полный dependency group в
   отдельном ALC. Старые ticks продолжаются во время этой подготовки.
2. На boundary drain guarded callbacks, reversible quiesce entries/services,
   capture/export values и exact system state; проверить neutral snapshot.
3. Импортировать новые CLR компоненты в private World, создать detached systems,
   восстановить state/tick. Старый World защищён от записи/structural mutation при
   export/migration/import/factory/restore. Освободить временные converters.
4. Проверить cancellation; attach private scheduler и одной операцией опубликовать
   `(group, World, generation)`. Commit не вызывает module callbacks/await.
5. Retire old callbacks; stop/dispose scheduler, World/resources; очистить pools/
   bindings/factories, запросить collectible unload.

Reject/cancel до commit очищает кандидата и resumes старую generation с её данными,
handles/системами/счётчиками. Failed resume выставляет ResumeRequired; ResumeAsync
явно повторяет recovery. После commit CleanupFailed виден отдельно, не откатывает
новый World; RetryCleanupAsync освобождает retired owners. Старый World Dispose
выполняется только при successful retirement cleanup. Stop закрывает admission до
ожиданий, отменяет replacement **и capture**, drain операции, освобождает Worlds и
groups. Failed cleanup наблюдаем/retryable; collectible exceptions не остаются в
diagnostics или faulted disposed World.

Reset — отдельный ResetAsync(request, seed, ModWorldResetAdmission.Allow). Без Allow
он отклоняется; по ошибке migration не вызывается. Это сознательная замена прежнего
runtime state подготовленным seed. Seed может включать state; без него используются
constructor defaults и tick из seed.

Rollback ограничен supported managed policy: reversible IModReloadParticipant,
IModReloadableSystem, generation guards. Unfenced subscriptions, process globals,
files/network, untracked tasks/native/GPU effects и невозвращающийся module code
не становятся обратимыми из-за ALC. Host не sandbox/forced thread abort. Для таких
модов требуется отдельное restart/isolation решение; API не обещает их rollback.

[Проверки/SHA](ARCH38_BASELINE.json), [история](HISTORY.md#arch38-2026-11-15),
[`Verify.ps1 -Migration`](../../ManualTests/ModConsumer/README.md). Сквозные
architecture profiles/20reloadcycles и wider release acceptance остаются ARCH3.9.
