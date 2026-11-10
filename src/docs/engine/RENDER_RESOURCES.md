# Resource owners, leases и preparation — ARCH1f.2

**2026-11-07.** Реализация поверх [CPU v1](RENDERING_CONTRACTS.md#cpu-v1).
Этапы/acceptance — [ARCH1f](PLAN.md#arch1f-rendering), фактические проверки —
[HISTORY](HISTORY.md#render-resources-2026-11-07). Это resource readiness;
[Owned snapshots](RENDER_SNAPSHOTS.md) добавлены в f.3; draw/surface integration и Runtime Playing fence остаются f.4.

## App и владельцы

`UseAetherRenderResources(Action<RenderResourceOptions>?)` явно регистрирует один
App-owned RenderResourceHost и non-disposable typed Factory wrappers:

| Factory product | Аргументы | Ownership |
| --- | --- | --- |
| RenderManifestLease | RenderContentManifest | Caller освобождает CPU lease |
| RenderDeviceOwner | RenderDeviceRequest | Caller освобождает рано; Host сохраняет fallback owner |
| RenderPreparation | RenderPreparationRequest | Caller владеет operation/ready leases до DisposeAsync |
| RenderWorldBinding | RenderWorldBindingRequest | Caller владеет current/candidate/retired preparations; World borrowed |
| RenderSnapshotView | RenderSnapshotViewRequest | Caller владеет current/pending/recording/submission bundles; binding/device/World borrowed; [f.3](RENDER_SNAPSHOTS.md) |

Host exclusively owns Content catalog; backend factory borrowed. Backend result
owned device, upload result owned cache entry. Default backend — lazy Vulkan;
existing IFactory<IRenderResourceBackend,RenderDeviceRequest> сохраняется. Startup
не создаёт native context. Service resolution только в composition/factories,
не query/upload loop. Один disposable не регистрируется под несколькими DI aliases.

```csharp
context.UseAetherEcs();
context.UseAetherRenderResources();
```

Session получает typed factories через DI. Scene/view/binding products освобождаются
до World/App; Host отслеживает их для fallback cleanup до успешного release. Borrowed
CPU scope для registry также имеет явного владельца; host не объявляет CPU leases
освобождёнными за caller. Если App cleanup встретил live leases, admission закрыт,
owner retained, ошибка наблюдаема; после release нужен explicit DisposeAsync retry.

## Counted CPU scopes

RenderContentCatalog реализует IFactory<RenderManifestLease,RenderContentManifest>.
Acquire атомарно проверяет scope/revision/physical-buffer/lease peak budgets,
канонизирует entry definitions по уже удерживаемым exact keys, затем публикует lease.
Scope identity=(ProjectId,BuildId,Generation,Digest). Одинаковый key не удерживает
новую дублирующую definition из другого исходного manifest. Один physical definition
под разными logical keys считается по buffers один раз, но каждый key занимает entry.
Scope wrappers разделяют один validated canonical manifest и closure bookkeeping.

ResidentBytes считает actual managed array elements через Unsafe.SizeOf: vertices,
indices, MeshSlot/MaterialSlotBinding arrays. Material без arrays даёт0buffer bytes;
его object metadata ограничена revision count. Caller-held исходные definitions,
object overhead и весь RAM процесса не являются этим byte budget. Data, сохранённые
caller после lease release, не дают права готовить/использовать GPU resource.

RenderManifestLease — exclusive reference owner. Alias того же объекта не Acquire,
Dispose idempotent. Internal retains для scene/preparation проверяют общий lease cap;
public unrestricted Clone отсутствует. Last wrapper удаляет scope и уменьшает exact
key/physical refs. Catalog.Dispose закрывает admission и при live wrappers не теряет
свои entries/counters, позволяя release и retry. Getter Manifest даёт immutable data,
а не право создавать новые resources после закрытия.

`Ecs.Rendering.RenderResourceComponents.Register(builder,scope)` использует exact
f.1 schemas/converters и устанавливает single frozen AssetValidator. Validate callbacks
проверяют live/open scope до World allocation. Import context.Acquire получает одну
CPU lease wrapper на MeshRenderer/Camera; SceneWorld владеет ею. Root scope нужен,
пока registry принимает новые loads; существующая scene живёт на своих retains.
Другие asset domains требуют explicit composite validator. PreparedResources limit
scene loader не увеличен:4096renderers+camera требуют выбранного budget сверх4096.

## Device, budgets и generation fences

RenderDeviceRequest фиксирует nonempty session/world/project/build/catalog generation
и nonzero DeviceGeneration. Device owner GUID уникален. Scope другого catalog,
project/build/generation отклоняется до backend work; content revision берётся только
из captured manifest. Raw preparation не ищет глобальную latest revision.

Mesh cache key=exact mesh key внутри device. Handles содержат owner GUID/device
generation/slot/slot generation, не native pointer. Копия не приобретает ownership.
Default/foreign/stale/unready handle отвергается до native use. Last safe release
увеличивает slot generation; uint exhaustion retires slot, не wrap. Retired slot
records также ограничены MaximumGpuEntries, поэтому бесконечного metadata growth нет.

RenderAllocationBudget.Reserve(geometry,upload,cpu) атомарно выдаёт exclusive
idempotent reservation. Отрицательные values invalid; пределы проверяются до actual
allocation. Backend обязан удерживать reservation до освобождения соответствующих
buffers. Counts включают old/candidate/retiring и failed cleanup; Timeout не budget release.

| Options / default hard ceiling | Назначение |
| --- | --- |
| MaximumCpuRevisions2048 / MaximumCpuBytes64MiB | Exact keys и service-retained physical definition arrays |
| MaximumCpuScopes64 / MaximumCpuLeases16384 | Bounded scope metadata и scene/preparation lease wrappers |
| MaximumGeometryBytes64MiB | Actual device memory requirements.size |
| MaximumUploadStagingBytes16MiB / MaximumCpuStagingBytes16MiB | Actual staging allocations / CPU staging arrays |
| MaximumActivePreparations4 / MaximumQueuedPreparations16 | Executing/waiting jobs, Ready products отдельно counted CPU leases |
| MaximumGpuEntries2048 | Live/retiring resource entries и slot metadata |
| Host1device /1required World binding; binding4096renderers | Первый explicit view profile, без hidden world truncation |

Options копируются при composition/owner creation и могут только понижать ceilings.
Byte budgets допускают0, queued jobs0; остальные count limits положительны.
Новые secondary metadata caps64scopes/16384wrappers/2048slots выбраны явно для bounded
первого профиля. Existing scene/wire/primitive/checkpoint limits не менялись.
Snapshot payload/envelope/submission ceilings из f.0 уже применяются в [f.3](RENDER_SNAPSHOTS.md).
Pipeline16 и actual surface/frame integration остаются f.4; resource backend ещё не
создаёт pipelines/targets и не заявляет общий VRAM cap.

## Preparation, cancellation и partial creation

Device/Host typed factories создают RenderPreparation с retained CPU lease и bounded
queue reservation. Completion — Task без второго результата-owner. Pending→Ready
только после всей mesh closure и final token/identity/admission check. Material-only/
empty geometry scope может стать Ready без backend activation. GetMeshHandle разрешён
только через complete live lease; inspection raw handle не превращается в draw lease.

Несколько operations одного key разделяют один upload. Individual cancellation не
отменяет другого waiter. Last reference cancel-ит entry work, но ждёт actual completion
до destroy. Canceled queued jobs снимаются без запуска и не создают пятую active job.
Если backend игнорирует cancel и возвращает поздний успех, он не публикует ready
binding отменённому caller; actual owner всё равно очищается/удерживается для retry.

IRenderResourceBackend.MeshUploads возвращает IRenderMeshUpload owner до PrepareAsync.
Factory не должна скрывать native work до возврата; partial failure передаёт actual
owner через RenderResourceCreationException.CleanupOwner. Device прикрепляет owner
до await, проверяет IsReady/IsReleased, не выводит их из одного Task success.
Failed upload cleanup сохраняет slot/bytes/CPU lease до explicit retry. UnknownOutcome
fences device; submitted resources не освобождаются по timeout/отмене caller wait.
Инъецированный backend отвечает за корректность своих native proofs и reservation
contract; fake backend unit tests не считаются GPU evidence.

## Resource binding и reload

RenderWorldBinding borrows matching healthy World. InitializeAsync проверяет все
MeshRenderer values/renderer count до native work, готовит complete resources и
публикует Current с Revision1. Initial CPU wrapper отпускается после ready, не pin-ит
старую revision навечно. Camera selection/WorldTransform extraction здесь не делаются.

ReloadAsync принимает explicit same-project/build/catalog candidate scope. На первой
serialized boundary фиксирует membership/handles/values, строит validated новые keys
по persistent asset IDs; затем асинхронно готовит candidate. Перед commit повторяет
сравнение inputs: даже изменённый некорректный key даёт Conflict, не частичный rebind.
Candidate содержит совместимый binding-set для новой mesh revision, implicit remap нет.

После final cancellation check component keys и Current/Revision публикуются вместе
без user callbacks. Caller всё ещё сериализует доступ к World; это не lock произвольных
игровых потоков. Unexpected commit failure faults World и fences binding. Preflight/
cancel/failure сохраняют old Current и не откатывают изменения самого caller.
Old cleanup failure после publication возвращает Committed=true+CleanupError; current
новый, old owner retained. Failed candidate cleanup виден через binding.CleanupError
и удерживается до disposal retry. Binding never disposes borrowed World.

Host stop немедленно закрывает CPU/device/factory/use admission, затем cancels/drains
snapshot views/accepted uses, затем bindings/preparations, devices/backend и последним catalog.
Failed view cleanup удерживает dependencies до retry. Cancelled StopAsync
wait не отменяет actual cleanup task и не объявляет IsReleased. Successful stages не
повторяют native frees, failed owners остаются reachable. Existing SceneWorld owns
свои import leases; view cleanup должно закончиться до SceneWorld.Dispose.

## Default Vulkan backend

VulkanRenderResourceBackend создаёт standalone lazy instance/device/graphics queue;
VulkanMeshUpload заимствует их под NativeGate. Это явный platform/context owner
внутри backend factory result, а не заимствование прежнего external surface device.
Первый graphics-capable physical device выбирается при активации. Matching adapter/
sharing с existing surface renderer — будущий f.3/4 integration contract.

Каждый mesh имеет vertex/index buffer и временный host-visible/coherent staging.
Actual memory requirements учитываются до AllocateMemory. Geometry copy и visibility
barrier завершаются fence; staging освобождается до ready. VerifyUploads добавляет
GPU→staging readback и SHA256 comparison. CPU hash scratch на stack, owned raw byte
array резервируется заранее. Shader/draw/pipeline/window/exported NT handles не создаются.

После QueueSubmit неизвестный outcome сохраняет command/fence/buffers; cleanup ждёт
подтверждённый idle, иначе owner retained. Это соответствует требованиям
[vkDestroyBuffer](https://docs.vulkan.org/refpages/latest/refpages/source/vkDestroyBuffer.html)
и [vkWaitForFences](https://docs.vulkan.org/refpages/latest/refpages/source/vkWaitForFences.html);
[Khronos synchronization examples](https://docs.vulkan.org/guide/latest/synchronization_examples.html)
задают transfer/readback dependencies. Device-loss recovery не реализован.
Validation callback ABI использует existing Silk.NET declarations, GCHandle сохраняется
до messenger/instance teardown, native callback не выпускает managed exceptions.

[Consumer runner](../../ManualTests/RenderResources/README.md) проверяет CPU composition
и отдельно actual Vulkan source/published/fresh-package upload/readback/retirement.
Это не pixel/frame/Editor/native-input/general release acceptance.
