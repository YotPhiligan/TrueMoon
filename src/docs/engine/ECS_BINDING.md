# ECS binding к Abstractions — ARCH3.2

`TrueMoon.Aether.Runtime` использует новый `TrueMoon.Aether.Abstractions.Ecs` API поверх
существующих World, typed pools, scheduler и phase command buffer. Второго мира
или отдельного scheduler для extensions нет. Контрактная библиотека не получает
ссылку на Runtime, контейнер, concrete World или native/UI backend.

## Подключение

Extension ссылается только на `TrueMoon.Aether.Abstractions` и экспортирует
reference-free struct components, `ISystem`/`IEcsSystem` и metadata:

```csharp
public readonly record struct Counter(int Value) : IComponent;
public sealed class CounterSystem : IEcsSystem
{
    public static EcsSystemMetadata Describe() =>
        new EcsSystemDescription("game.counter").Write<Counter>().Freeze();
    public void Execute(EcsSystemContext context) =>
        context.Query<Counter>().ForEachWrite(
            (EntityHandle entity, ref Counter value) => value = new(value.Value + 1));
    public void Dispose() { }
}
```

Host composition root регистрирует DI-фабрику `IFactory<CounterSystem>` и вызывает
`UseAetherEcs()` и `UseAetherEcsSystems(s => s.Add<CounterSystem>(CounterSystem.Describe()))`.
Параметризованный `IFactory<T, World>` также поддерживается, когда host-система
явно нуждается в borrowed World при создании. Одновременно регистрировать две
разные фабрики этих профилей для одного типа нельзя. Metadata копируется при Add;
изменение builder после этого не меняет активируемый граф. Результат scheduler
factory принадлежит вызывающему коду; scheduler владеет созданными системами и
заимствует World. Factory results не становятся owned сервисами DI автоматически.

Низкоуровневый `EcsSystemRegistration` принимает neutral description/metadata и
`IFactory<ISystem>` либо `IFactory<ISystem, World>`. Прежние registrations
совместимы; оба API используют один access/order graph. Алиасы одного экземпляра
в legacy/neutral registrations отклоняются с единственным cleanup владельцем.
Разрешение зависимостей выполняется в composition/factory adapter, не на tick/entity.

## Доступ и остановка

Binding проверяет invocation stamp, owner thread, World liveness/fault,
cancellation и существующие read/write declarations. `WorldId` — только GUID,
он не открывает доступ к World. Сохранённые context/query/writer не оживают при
следующем Execute. Запрещены nested queries и прямые structural changes во время
query. Все пять форм callbacks используют те же generic component columns,
включая exclusions и optional unmarked reference-free structs. IComponent не
создаёт boxed storage; компонентов с managed references в этих пулах нет.

Commands используют существующие batch/reservation identities, bounds и playback
на границе фазы. Live handles сохраняют WorldId/index/generation; default, stale
и wrong-World значения не становятся валидными после преобразования. Неуспешный
callback оставляет уже сделанные direct writes, отбрасывает pending commands и
терминально fault-ит scheduler/World. Cooperative cancellation или RequestStop
останавливают scheduler без публикации неполной фазы.

Stop предшествует Dispose. Неудачный cleanup удерживает прежний owner и World
lease для повторного Dispose. После успешного Dispose binding удаляет ссылки на
system/World, поэтому сохранённый context не удерживает весь World. Прежние
ownership/preflight/rollback правила ECS остаются действующими.

## Scene registry

`ComponentRegistryBuilder.RegisterValues<T>(neutralSchema, IFactory<T, neutralComponentData>)`
адаптирует точный stable TypeId/schema version к прежнему loader. Фабрика получает
отдельный immutable снимок эффективных scalar values, включая defaults и overrides.
Boolean/integer/number/string/null передаются без coercion; schema drift,
multiplicity и неоднозначные TypeId/runtime-type bindings отклоняются прежними
проверками. Регистрация не даёт фабрике World или краткоживущий load context.

Этот путь предназначен для данных. Entity/asset reference resolution и acquisition
ресурсов остаются явным runtime importer API. Scene documents и builtin component values теперь канонические Abstractions types,
resolver находится в Runtime.Scenes. Movement,
Transform и PrimitiveExtraction поддерживают neutral system API; production
PrimitiveSimulationFactory активирует именно его. Их прежние публичные entrypoints
сохранены. Физическое выделение Runtime выполнено в [ARCH3.3](RUNTIME.md).

## Shared ABI и выгрузка

ARCH3.3 заменяет монолитный ABI на canonical Abstractions и отдельный Runtime.
Текущий artifact schema2 разделяет Abstractions + Contracts; concrete Runtime
добавляется только при RuntimeAccess. Full assembly identities и SHA256 проверяются,
private shared copies запрещены. Compiler передаёт AetherAbstractionsPath,
TrueMoonContractsPath и optional AetherRuntimePath; capture сохраняет точные bytes.
Child/Player pair identity включает Runtime/Abstractions/Contracts/Enerit/Mithril.
Старые schema1 artifacts требуется пересобрать. [Границы и миграция](RUNTIME.md).

До unload необходимо отпустить не только экземпляры систем:

- World pools и generic columns удерживают component Type; World.Dispose очищает их.
- Registry bindings, metadata, importers и DI factories удерживают Types/closures
  до освобождения соответствующего graph/generation.
- Scheduler registrations и factories могут удерживать extension даже после
  успешного Dispose систем; следует отпустить сам scheduler и factory graph.
- Сохранённые queries с generic types/exclusions, delegates, contexts и внешние
  static/event/cache ссылки также могут удерживать collectible assembly.

Unload request не доказывает collection ALC или безопасность hot replacement.
Production mod manager/state migration остаются следующими этапами ARCH3.
Новые allocation/performance преимущества без измерений не заявляются.

Проверки и точные результаты: [manifest](ARCH32_BASELINE.json),
[история](HISTORY.md#arch32-2026-11-12),
[независимый real World host](../../ManualTests/EcsContractHost/README.md).
