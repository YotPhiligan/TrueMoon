# Owned render snapshots — ARCH1f.3

Реализовано и проверено2026-11-09 в bounded CPU/backend-contract scope; [evidence/limits](HISTORY.md#render-snapshots-2026-11-09). [Единственный этап](PLAN.md#arch1f-rendering).
Основа — [f.1 content](RENDERING_CONTRACTS.md) и [f.2 resources](RENDER_RESOURCES.md).

Один view borrows serialized World/resource binding/device. App/IFactory создаёт
caller-owned view; Host удерживает fallback до успешного cleanup и освобождает view
перед binding/device/catalog. Capture вызывается после завершённого Transform tick,
вне concurrent scheduler/reload/World changes; async ожидание resource gate предшествует
синхронному World обходу. Новый async ECS system contract не вводится.

Entity camera выбирается явно same-World live handle либо задаётся external rigid
pose+settings. First camera fallback отсутствует. Выбранная entity и все renderers
требуют WorldTransform с точным tick. Camera basis proper rigid; renderer matrix
полная affine, bounds по8углам сохраняют nonuniform/rotation/shear. Все renderer refs,
tint, transforms/bounds валидируются до enabled/mask. Slots идут по entity index/firstIndex;
linear material base умножается на instance tint. Primitive consumer/protocol отдельны.

Snapshot содержит view/session/world/project/build/catalog/scene/source-revision/tick,
publication sequence, binding revision, manifest digest, device generation, camera
revision и surface generation. SourceRevision — captured authoring revision, Tick —
completed simulation boundary, Sequence — новая успешная publication; они не являются
автоматическим счётчиком произвольных World mutations. Владелец сериализует World.
Immutable data не удерживают World и не дают native use без recording/submission owner.

View configuration проверяется на monotonic nonzero revisions: изменение camera source/
external values требует новой CameraRevision, изменение extent — SurfaceGeneration.
Live entity camera pose естественно меняется по World tick.0×0 suspend освобождает
current/pending, старый recording не может submit; accepted submissions сохраняются.
Partial zero/unsupported extent отвергаются. Failed/canceled candidate сохраняет old current.

Bounded profile:4096renderers/4096draw ranges,10000logical entities,2MiB accounted payload,
16MiB retained snapshot buffers,7distinct retained envelopes,3accepted submissions.
Payload =256accounted header + exact array length×Unsafe.SizeOf<RenderDrawRange>;
object/allocator overhead не объявляется RAM quota. До header/array/retain проверяются
соответствующие count/byte budgets. Current/pending могут разделять immutable allocation;
recording/submission передают bounded внутренние refs, public unrestricted Clone нет.
Failed cleanup остаётся counted; budget pressure отвергает candidate без truncation.

Current — последний успешно опубликованный snapshot, pending — единственный latest.
Coalescing освобождает старый pending; recording — один CPU/data reservation; native recording начинается только в accepted backend owner. Caller
заканчивает через Dispose либо Submit. Submit принимает injected backend factory:
она возвращает owner до native work, partial creation возвращает CleanupOwner через
RenderResourceCreationException. Ledger/resource retain существуют до invocation.
ExecuteAsync завершается с IsComplete только после реального backend completion;
caller boolean/таймаут proof не заменяют. Ошибка/неизвестный outcome удерживает owner
и resources до safe DisposeAsync; неизвестный outcome fences новые captures/submits и device allocation/preparation.
F.4 обязан доказать этот backend contract actual Vulkan completion/pixels отдельно.

Stop закрывает admission, отменяет pending backend work, освобождает current/pending/
recording и ждёт accepted uses/actual cleanup. Отмена Stop wait не освобождает leases.
Cleanup failure оставляет owner/исходную ошибку для retry. Backend/device не уничтожаются
при live snapshot refs. Source/published/fresh-package CPU proof и tests отделены от
нового surface renderer, Runtime Playing fence, device loss и physical GPU acceptance.
