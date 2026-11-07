# ECS registry и загрузка сцены

**2026-11-06, ARCH1e.3a.** Первый bounded headless load profile. Задачи —
[engine PLAN](PLAN.md#arch1e-ecs), проверки — [STATUS](STATUS.md). Реализация идёт
отдельно от existing primitive control/IGameSession; initial Runtime activation —
[e.3b](ECS_RUNTIME_LOADING.md).

## Registry

`TrueMoon.Aether.Ecs.Loading.ComponentRegistryBuilder.Register<T>(schema, importer,
references=null, validate=null)` связывает exact stable ComponentTypeId/schema version
с одним value type без managed references. Build возвращает immutable ComponentRegistry;
повторный Build/дальнейшие изменения builder не меняют прежний registry. Максимум1024bindings.
Один stable type использует один CLR type во всех зарегистрированных версиях, и
один CLR type не назначается двум stable type IDs. Exact duplicate отклоняется.

Schema equality включает properties/kinds/nullability/defaults/RequiredTypes и
AllowMultiple. Никакого fallback на последнюю версию, coercion или implicit migration.
Первый World singleton profile не поддерживает AllowMultiple=true; ошибка явная,
не выбор первого occurrence. Неиспользуемые catalog entries не требуют runtime binding.

ComponentReference описывает String property ID и Entity/Asset semantic. Null
разрешён только исходной nullable schema; nonnull reference — nonempty GUID в D format.
Даже optional nonnull dangling entity reference — ошибка. Asset references требуют
registry AssetValidator; он проверяет unique referenced IDs до создания World.
Asset type/revision/content/provider policy уточняются в ARCH1f; Guid check не
объявляет mesh/material pipeline готовым. CLR bindings и validators живут в Runtime.

## Полный payload и limits

SceneWorldData содержит formatVersion1 и immutable SceneDocument целиком: IDs,
definitions/inheritance/revisions, overrides, schemas/defaults/null и entity metadata.
SceneWorldCodec использует existing strict SceneFileCodec body, добавляя TMWL magic,
little-endian version/length и SHA256 body checksum (44bytes header). MaximumBytes —
4MiB body+44. Все scalar strings/defaults/overrides должны иметь корректный UTF16;
lone surrogates отклоняются до serialization/import вместо замены на U+FFFD.
Corrupt/truncated/trailing/unknown version отклоняется; checksum —
целостность, не authentication. Existing authoring v1/v2 body не меняется.

Декодирование/кодирование не требует Game.Logic/registry. Payload не является
полным runtime checkpoint. Старые64KiB control/checkpoint и64primitive limits не
расширяются; [runtime transfer](ECS_RUNTIME_LOADING.md) отправляет envelope частями
через explicit capability, сохраняя предел каждого control message.

SceneWorldLoadOptions: MaximumEntities4096 (1..4096), MaximumComponentInstances65536
(1..65536), MaximumPropertyValues262144 (0..262144), MaximumPreparedResources4096
(0..65536). Counts проверяются до import callbacks/World publication; codec отдельно
ограничивает bytes. Это admission counts, не byte quota intermediate authoring resolution.
Source catalog/patch collections ограничены existing file profile; resolved cardinality
проверяется до World factory. Резервов памяти/performance SLA или async provider API нет.

## Загрузка и conversion

SceneWorldLoader реализует IFactory<SceneWorld,SceneWorldLoadRequest>; constructor
принимает frozen registry, IFactory<World>, SceneResolutionService и copied options.
Request содержит SceneWorldData и CancellationToken. Load(data,token)/Load(document,token)
— low-level conveniences того же пути. Loader не удерживает продукты.

1. Проверить version/source limits, разрешить authoring snapshot, проверить все
   visible bindings/schema/multiplicity/dependencies/reference IDs и resolved budgets.
   Custom validate получает ComponentValidationContext без World/handles/resources.
2. Получить fresh empty healthy idle World через injected factory. Reject alias/
   existing owner/nonempty result без освобождения чужого World. SceneWorld claim
   блокирует внешнюю mutation/disposal/scheduler activation до публикации.
3. Создать все entities в deterministic hierarchy order, замкнуть persistent EntityId
   → generational handle mapping. Importers получают ComponentLoadContext с read-only
   authoring data, Entity/Parent и GetEntityReference/GetAssetReference, без mutable World.
4. Подготовить все typed values и exclusively owned resource leases; затем добавить
   их в private World без user callbacks. Проверить cancellation до публикации.
5. Publish complete SceneWorld. SceneWorld.Scene сохраняет immutable resolved metadata,
   Entities — initial identity mapping; runtime destruction может сделать handle stale.
   Source document/history не меняются. Scheduler подключается caller после publish.

Contexts active только внутри callback. Scalar helpers GetValue/GetBoolean/GetInteger/
GetInt32/GetNumber/GetSingle/GetString требуют exact kind, finite conversion и явные
inclusive ranges; null не заменяется автоматически default(T). GetSingle проверяет
double→float overflow; GetInt32 — Int64→Int32 overflow. Custom importer отвечает за
семантику сконструированного T, generic reflection по его fields не применяется.

Import context Acquire<T>(Func<CancellationToken,T>) принимает только IDisposable
leases от injected provider/factory. Budget проверяется до acquire; returned lease
сразу учитывается, даже если callback отменил token. Same reference дважды отклоняется
без double dispose. Borrowed services нельзя отдавать как owned leases. Provider
должен сам clean up failed acquisition до возврата; callbacks synchronous/cooperative.

## Ownership и errors

SceneWorld owns World и acquired leases; World property — borrowed. Dispose требует
сначала завершить caller-owned scheduler, затем инвалидирует World и освобождает
leases в reverse acquisition order. Failed disposals остаются owned для retry;
успешные не повторяются. Direct World.Dispose отвергается, пока SceneWorld владеет им.

Load failure/cancel не публикует частичный результат: private World уничтожается,
leases clean up reverse. Если cleanup failed, SceneWorldLoadException.CleanupOwner
сохраняет owner для explicit Dispose retry, inner aggregate — исходную ошибку и cleanup.
Successful cleanup сохраняет cancellation; semantic errors имеют stable error code и
scene/entity/component/property context. Alias/invalid World factory не передаёт
ownership чужого World loader. Повторная загрузка создаёт независимые World/handles.

## App

UseAetherSceneLoading(Func<IServiceResolver,ComponentRegistry> registryFactory,
Action<SceneWorldLoadOptions>? configure=null) вызывается после UseAetherEcs.
Регистрация одноразовая; создаёт App-local registry, SceneWorldLoader и typed factory
alias (loader не IDisposable), SceneWorldCodec и stateless resolution dependencies.
Registry dependencies разрешаются только в composition root. Codec сохраняет
existing SceneFileCodec/authoring factory overrides; при их отсутствии создаёт private
default validation graph внутри DI registration factory, без persistence store/UI/IO.
App startup не создаёт World и не исполняет importers. App не владеет factory results.

Registry/service dependencies borrowed, SceneWorld caller-owned и должен быть disposed
до App. Нет game CLR loading в Editor, global registry, нового DI scope, native renderer,
implicit mapping или автоматически расширенного transport.
Explicit [primitive ECS bindings](ECS_PRIMITIVES.md) сохраняют старые v1 schemas.
Initial Runtime handshake/load activation описаны в [e.3b](ECS_RUNTIME_LOADING.md).
E.4 builtin transforms/primitive extraction реализованы отдельно; следующие steps:
f — asset/content/render lifecycle; e.5 — logical checkpoint.
