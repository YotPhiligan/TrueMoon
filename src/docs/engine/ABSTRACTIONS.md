# Aether Abstractions — ARCH3.1–4

`PackageId = AssemblyName = RootNamespace = TrueMoon.Aether.Abstractions`.
Общий профиль .NET11/SDK/runtime-async сохраняется. Единственная зависимость
фреймворка — `TrueMoon.Contracts`; остальное BCL. Пакет не содержит World/pools/
scheduler, compiler/project services, native handles, GPU/audio backends, IPC или UI.
Контрактный пакет не владеет сервисами/ресурсами, поэтому фиктивного UseAbstractions нет.

## Граница этого этапа

Создан новый ABI с отдельными namespace. Подготовлены105соответствий нейтральных
типов исходной карты: game/input/checkpoint contracts, ECS values/metadata,
идентификаторы и immutable scene/render/preview data. Дополнительно замкнуты чистые
validation/math/codec helpers. Текущий SDK экспортирует116типов; полный public
signature/assembly graph проверен без Runtime/Tooling/UI/backend dependencies.

**ARCH3.2 подключил новый ECS API к существующему World/scheduler.**
[Binding, App/DI, registry и совместимость](ECS_BINDING.md). Extension ссылается
только на Abstractions; host регистрирует его фабрики и metadata. Production
movement/transform/primitive extraction используют новый system API поверх тех же
пулов. ARCH3.3 объединяет builtin values и scene/game models в канонических Abstractions
типах; World/scheduler вынесены в Runtime. Прежние host-specific system overloads
находятся в Runtime.Ecs; type-forwarding не введён.
Новый SDK не является binary/source facade прежней DLL. Общая planned0.4.0-alpha
migration и explicit incompatibility остаются решением ARCH3.0; центральная версия
сейчас не повышена. [Фактическая migration map](ARCH33_MIGRATION.csv) дополняет исходные105соответствий.

Runtime зависит от Abstractions. [Schema2 game artifact и compiler ABI](RUNTIME.md)
по умолчанию разделяет только Abstractions + Contracts; Runtime — явно разрешённое
расширение. Exact full identities/hashes, child/Player pair fingerprints и запрет
private shared copies сохранены. Старые artifacts требуют rebuild. Stable IDs и
scene/control/frame/checkpoint byte formats сохранены.

## ECS

```csharp
using TrueMoon.Aether.Abstractions.Ecs;

public readonly record struct Counter(int Value) : IComponent;
public sealed class CounterSystem : IEcsSystem
{
    public static EcsSystemMetadata Describe() =>
        new EcsSystemDescription("game.counter").Write<Counter>().Freeze();

    public void Execute(EcsSystemContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Query<Counter>().ForEachWrite(
            (EntityHandle entity, ref Counter value) => value = new(value.Value + 1));
    }
    public void Dispose() { }
}
```

IEcsSystem сохраняет synchronous Execute, Stop до Dispose и exclusive scheduler
ownership после успешной factory activation. ISystem — базовое имя того же
контракта. Host выбирает DI/factory/lifetime в App composition root. Общие factory
contracts — из Contracts; на кадре/сущности service resolution не выполняется.
Failed activation очищает создающий owner; неудачный cleanup не считается успешным unload.

EcsSystemContext — readonly value с точным nonzero invocation stamp. Возвращает
только данные, typed queries и recording writer; concrete World/контейнер отсутствуют.
Copies контекста/queries/writer не оживают в следующем вызове того же backend.
Host binding `IEcsSystemAccess` обязан проверять stamp, owner thread, active/terminal
state, cancellation и read/write declarations **при каждой операции**. Wrappers
не заменяют эти проверки в Runtime implementation. Member getters тоже проходят
invocation validation. Caller не может вызвать playback/reset/dispose phase buffer.

Callbacks получают scoped in/ref значения, structural changes идут через commands
и применяются на безопасной границе фазы. Контракт сохраняет существующую политику:
предыдущие direct writes переживают callback failure, scheduler fault терминален,
pending commands отбрасываются. Cancellation cooperative, без forced abort. Полная
проверка этих правил с реальными World/pools/phase playback выполнена в ARCH3.2; host
ARCH3.1 использует явно обозначенный contract double.

Read/write/order metadata создаётся через EcsSystemDescription и копируется Freeze
в независимый EcsSystemMetadata. IDs1..128ASCII, at most1024distinct dependencies
на сторону; query имеет до8distinct exclusions и не исключает обязательные типы.
Write также разрешает read. CLR Type в metadata — live access identity, не формат хранения.

IComponent optional: существующие unmarked reference-free struct остаются допустимы.
Managed-reference structs отклоняются общей ComponentStorage validation; generic
API не вводит marker constraint. Данные не должны храниться как object/IComponent.
EntityHandle остаётся value(WorldId,index,generation), default invalid; создание
значения не доказывает liveness, stale/wrong-World handles проверяет host.
Отдельный reference IEntity на каждый entity не вводится. Persistent EntityId
отделён от transient handle; ComponentSchemaReference = ComponentTypeId + exact
positive version. Unknown schemas требуют explicit preserve/reject/reset policy,
а не молчаливого удаления данных или CLR-name serialization.

## Проверки и продолжение

ARCH3.6 добавляет отдельные [Mod SDK/manifest/entry/factory contracts](MODS.md)
в `Mods`. Они остаются нейтральными: SDK-only мод использует Abstractions и
Contracts, implementation loader — Runtime, publisher — Tooling. Исторический
extension helper ниже сохраняет прежний scope; новая unload/collection приёмка
фиксируется отдельно в ARCH36 baseline.

[Independent extension](../../ManualTests/Abstractions.Extension/Abstractions.Extension.csproj)
ссылается только на Abstractions; [host proof](../../ManualTests/Abstractions.Host/README.md)
создаёт систему через DI factory на Cobalt/MS DI, проверяет общий Type identity,
typed callback/recording и единственный Stop/Dispose owner. Это acceptance helper,
не новый production loader/mod manager. Он не подтверждает collection ALC:
unload requested/GC collection различаются, retained Type/metadata/factory/queries
остаются возможными roots до release владельцами.

[Type map](ARCH31_TYPE_MAP.csv), [project graph](ARCH31_PROJECT_MAP.csv),
[manifest/SHA](ARCH31_BASELINE.json), [история](HISTORY.md#arch31-2026-11-12).
Canonical mesh/material/preview bytes и legacy scene/input representations сверены;
пакет не запускает IO/native operations для validation этих значений.
[ARCH3.2 real World host](../../ManualTests/EcsContractHost/README.md) проверяет
независимую DLL через App/DI, import в typed pools и phase playback на обоих DI
providers в source/publish/fresh-package profiles. [Binding/evidence](ECS_BINDING.md).
Physical Runtime/Tooling/UI extraction и live mod replacement остаются далее;
ALC collection и allocation/performance выигрыш этим этапом не объявляются.
