# ECS execution: queries, commands и systems

**2026-11-06, ARCH1e.2.** Выбранный контракт первого headless execution profile.
Этапы/acceptance — [engine PLAN](PLAN.md#arch1e-ecs), результат проверок — [STATUS](STATUS.md).
Authoring materializer и initial Runtime integration реализованы в
[e.3a](ECS_LOADING.md)/[e.3b](ECS_RUNTIME_LOADING.md); mesh/material renderer и
checkpoint integration остаются e.5/f; [builtin primitive profile](ECS_PRIMITIVES.md)
использует три ordered systems и prime-before-activation.

## Queries

`World.Query<T>()` и `Query<T1,T2>()` возвращают value descriptors EntityQuery.
`Without<T>()` добавляет до8distinct excluded types; unsupported managed values,
повтор required type и required/excluded contradiction отклоняются до обхода.
Unknown/empty pools не регистрируются lookup. Двухкомпонентный query обходит меньший
required pool и проверяет membership второго. Packed order не является stable order.

`ForEach(QueryReadAction)` передаёт entity и scoped in values; `ForEachWrite`
передаёт scoped ref первого и scoped in второго; `ForEachWriteBoth` — два scoped ref.
Однокомпонентный query поддерживает read/write. Callback синхронный, refs не должны
покидать callback; ref/in parameters исключают async callback. Default descriptor,
disposed World и недопустимый access отклоняются. Nested queries пока не поддержаны.

На время обхода запрещены Create/Destroy/Add/Remove/Set, Dispose и Playback.
Read methods возвращают copies; запись — только через объявленный write callback.
Recording command buffer допустим. Finally снимает guard даже при callback failure;
прямой write-query не является транзакцией: уже сделанные writes сохраняются.
Raw unsafe/reflection обход caller contracts не является supported API.

## Command buffer

`EntityCommandBuffer(World,int maximumCommands=4096)` — caller-owned коллекция
команд. Допустимый предел1..1048576. Create возвращает DeferredEntity; Destroy,
Add<T>, Set<T>, Remove<T> принимают CommandTarget с implicit conversion из live
EntityHandle или deferred token. Token содержит buffer/batch GUID и ordinal, не
сериализуется. Чужой/default/старый token отклоняется до recording.

До Playback World неизменен. Playback сначала симулирует всю FIFO последовательность:
live/generation/membership, create/destroy allocator с retirement и type/slot limits.
Add duplicate и Set missing — ошибки; Remove missing idempotent, как World.
Preflight читает исходный allocator, держит только touched state и виртуальный
free stack; не копирует все component values и не резервирует native resources.

Validation failure: World unchanged, buffer Faulted, AppliedCommandCount0. Явный
Reset освобождает команды, инвалидирует batch tokens и разрешает новую запись.
Cancellation в preflight: World unchanged, recording/token сохранены для explicit retry.
После последней cancellation check начинается неотменяемый bounded commit без user
callbacks. AppliedCommandCount считает завершённые команды. Неожиданная ошибка commit
оставляет prefix/возможный partial command: buffer и World Faulted, продолжение мутаций
запрещено; owner должен Dispose/recreate World. Это не общий rollback transaction.

Successful Playback очищает queue, меняет batch identity и возвращает immutable
CommandPlaybackResult. Resolve(token) выдаёт созданный handle; если entity в том же
batch уже уничтожена, handle остаётся observational mapping и IsAlive=false.
Empty playback переиспользует empty result. Reset/Dispose не делают Dispose value payload.
Callbacks/instrumentation для искусственного commit failure доступны только internal tests.

## Системы и порядок

`IEcsSystem : IDisposable` предоставляет Execute(EcsSystemContext) и optional Stop().
Каждый factory result exclusively owned scheduler; injected dependencies и factories
заимствуются. Description задаёт ID, EcsPhase (Input/Gameplay/Movement/Transform/
Extraction), Read<T>/Write<T>, After/Before. Descriptions копируются до activation.
ID1..128 ASCII alphanumeric/._-, distinct; dependency graph и phase inversions
проверяются до создания систем. Topological order стабилен по phase/registration order.

`EcsScheduler.Create(World, registrations, options)` — low-level factory;
App path: UseAetherEcs затем UseAetherEcsSystems(builder=>builder.Add<T>(description)).
В DI предварительно регистрируется IFactory<T,World>. Module регистрирует только
IFactory<EcsScheduler,World>, разрешает system factories один раз в composition root,
не хранит IServiceResolver в tick. Options: MaximumSystems128 (1..1024),
MaximumCommandsPerPhase4096 (1..1048576). Никакого отдельного timer или DI scope.

Scheduler exclusively leases один World, но не владеет его Dispose. Сначала Dispose
scheduler, затем World. Factory validation/alias rejection/failed activation освобождают
уже созданные systems в reverse order. Если cleanup не завершён, creation exception
содержит CleanupOwner для explicit Dispose retry; ownership не теряется.

`Tick(TimeSpan deltaTime,CancellationToken)` принимает положительный fixed step от
внешнего loop. Context содержит World, phase Commands, target TickNumber, DeltaTime,
CancellationToken и RequestStop(). Commands — EcsCommandWriter, который разрешает
только recording; captured inactive writers не могут Reset/Playback/Dispose общего
phase buffer или записывать вне своего Execute. Writer содержит invocation generation:
он не оживает при следующем Execute той же системы. Context работает только во время своего Execute.
Read/write declarations проверяются query и direct component API; structural mutation
внутри Execute запрещена, recording через Commands разрешён (typed writes должны
быть объявлены). Structural effects применяются после последней системы каждой фазы,
видны следующей фазе/следующему tick. TickNumber увеличивается только после полного
успеха. Reentrant Tick и World disposal с активным scheduler отклоняются.

Ready → Running → Ready на успехе. RequestStop thread-safe/cooperative; Stop/Dispose/
Tick требуют serialized owner. Stop во время Execute запрашивает остановку после
callback. Pending commands текущей фазы отбрасываются; выполненные writes/прошлые
phase commits сохраняются. Subsequent systems не запускаются, Tick=false, State=Stopped.
Pre-cancelled Tick ничего не меняет и оставляет Ready; cancellation после начала
останавливает graph terminal и бросает OperationCanceledException. Игнорирующий
token callback не прерывается принудительно.

System/commit exception: graph Faulted, World mutations blocked, pending commands
discarded, stop callbacks reverse order; исходная ошибка не скрывается cleanup errors.
Inspection reads допустимы, автоматического resume/rollback нет. Dispose best-effort
reverse order; успешно disposed entries не освобождаются дважды, failed entries
сохраняются для retry. World lease снимается после полного cleanup.

Queries и systems используют cached delegates/descriptors/contexts; command records
могут аллоцировать на structural change. Steady allocations измеряются на отдельном
неструктурном workload; zero-allocation/throughput обещание без измерения не даётся.
Parallel jobs, arbitrary runtime queries, change notifications и event channels —
следующие capability profiles, не скрытая часть этого scheduler.
