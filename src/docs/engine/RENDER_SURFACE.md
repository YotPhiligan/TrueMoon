# ARCH1f.4: required mesh surface и Playing fence

Нормативный профиль — [opaque-unlit/1](RENDERING_CONTRACTS.md), владельцы ресурсов —
[f.2](RENDER_RESOURCES.md), detached snapshots и submissions — [f.3](RENDER_SNAPSHOTS.md).
Подробная задача и очередь остаются в [PLAN](PLAN.md#arch1f-rendering).

## Подключение

В child/Player composition root вызвать `UseAetherSurfaceRendering(...)` после
`UseAether(...)` и до разрешения `AetherRuntimeHost`. Example `AetherRuntimeHost`
включает этот lazy модуль. App startup не создаёт World, Vulkan instance/device,
geometry или native shaders. Сервисы создаются DI/IFactory, ресурсы поверхности —
через типизированный factory argument; контейнер не используется на кадр/объект.

`UseConfiguredAetherSurfaceRendering` использует существующую точную schema
`AetherConfigurationSchemas.RenderResources`: источники и приоритеты собираются
общим configuration profile в composition root. Новых env/args/JSON правил нет.
Callbacks сохраняются и копируют проверенные limits до lazy resolution.

Выбранная game session явно реализует
`TrueMoon.Aether.Rendering.Snapshots.IRenderSnapshotGameSession` и возвращает
`RenderSceneSource`: borrowed World, immutable manifest, SceneId/source revision,
explicit camera и её revision. Game владеет World; surface view не освобождает его.
Это runtime API с World, **не** будущий neutral Mod SDK/Abstractions контракт.
Typed builtin/in-memory content фиксируется внутри выбранной game assembly;
родитель не загружает её CLR types и не передаёт mesh bytes через control/scene wire.

Каждый surface generation получает один resource host/device/binding/view.
World access, fixed ticks, commands, content reload и extraction сериализованы
async semaphore; обычный monitor не удерживается через await. Нельзя менять
World/SceneId/source revision внутри текущего view: сначала detach. Новый content
digest использует существующий atomic `RenderWorldBinding.ReloadAsync`, обновляющий
exact keys. Кадр не получает incomplete binding или fallback geometry.

## Рендеринг и ownership

Resource backend заимствует **тот же** VkDevice/graphics queue, который экспортирует
reusable surface. Собственные command pool/upload buffers освобождаются до borrowed
device. Native queue/resource/surface операции используют общий synchronization
gate. Отдельный standalone upload backend из f.2 остаётся доступным.

`mesh.vert` читает position float3 stride12; UInt32 index buffer начинается после
vertex bytes. Draw использует exact slot FirstIndex/IndexCount, baseVertex0 и
`world * viewProjection`. `mesh.frag` получает material-sRGB→linear × instance-tint,
затем кодирует sRGB один раз в RGBA8 UNORM. RH/+Y/-Z, Vulkan depth[0,1], один Y flip,
depth Less/write, cull none, sample1, без blending. Pipeline создаётся один раз на
использованный surface slot; tint/material не создают новые pipelines.

Paused frame повторно использует одно current envelope. Перед допуском нового
submission собираются подтверждённые завершённые uses, поэтому paused presentation
не исчерпывает лимит3. Bounded mailbox/bytes/counts f.3 и geometry/staging budgets f.2
сохраняются. Snapshot reservation передаётся owner до native work; ошибка/отмена
не разрешает ранний release. Cleanup подтверждает pending native fence, затем
освобождает submission/snapshot → binding → upload/backend → surface/device.

Stop/runtime failure сначала отменяет и drains producer, затем mesh view. Если
retirement не завершён, runtime **удерживает World/protocol и collectible game
loader** для retry. App остаётся fallback owner. Native diagnostics содержат
bounded последние16 `MeshResources` observations; validation/errors/messages берутся
у shared surface device. Borrowed backend сообщает0 created devices и не имеет
собственного validation messenger.

## Готовность

Hello сообщает `RenderViewVersion=1` только у mesh capability. Существующие control3,
frame2, primitive payload и headless behavior сохраняются; добавлена явная операция
`PresentRenderView` с fixed132byte little-endian mesh-view/1 stamp. Metadata содержит
View/World/Scene, source/tick/publication, binding/camera/surface и manifest hash.
CPU codec отвергает mesh metadata; GPU frame не смешивает mesh/preview/primitive/CPU
payloads. Matching host/game artifacts необходимо пересобрать.

CPU activation и upload/actual GPU completion не открывают Playing. Required view
готов только после **фактической презентации** соответствующего кадра. Стандартный
`FrameSession.Start/Resize` ждёт first actual ticket и подтверждает stamp. Для
последующих reload/camera frames UI вызывает `await Frames.PresentedAsync(ticket)`
после draw/present. EditorSession ведёт максимум один такой RPC и drains его при Stop.
Custom host использует `Supervisor.AcknowledgeRenderViewAsync(stamp)` после реального
показа своего кадра. Старые/чужие/будущие stamps не открывают fence. Если source успел
смениться, пока GPU-кадр летел к UI, acknowledgement возвращает false и следующий
current ticket может повторить попытку. `RenderViewPresentationAccepted` отделён
от общей RenderViewReady: устаревшая попытка не становится принятой из-за другого
уже показанного current кадра.

Detach/resize/suspend и изменённые content/camera закрывают readiness и приостанавливают
Playing. После новой actual presentation возобновляется прежний Playing; явный Pause
снимает automatic resume. Parent supervisor допускает только capability-specific
Playing→Paused с RenderViewReady=false; headless validation mode/tick остаётся строгой.

## Приёмка и границы

Воспроизводимый runner: [Verify.ps1](../../ManualTests/RenderSurface/Verify.ps1).
Он использует общий SDK/TFM/LangVersion/Features snapshot и CPM isolation через
`BuildProfile.ps1`, source Debug, FDD published Release, затем15 freshly built packages,
offline feed/private empty cache и три независимых package-only projects. Native
consumer запускается из unrelated working directory, проверяет exact installed/output
Aether DLL, core/synchronization validation и реальные RGBA pixels. Readback кадра
рисуется Skia на owner thread; acknowledgement следует только за DrawCore и
`HostedUiWindow.FramePresented`, а не за readback/GL fence.

| Требование | Проверяемое свидетельство |
| --- | --- |
| Production не равен presentation; invalid/stale/generation stamps | `RenderViewReadinessTests`, `RenderSnapshotStampTests` |
| Legacy headless независим; mesh Playing закрыт до fence | `RequiredMeshProtocolTests.Playing_RequiresActualCurrentPresentation_AndResizePausesUntilNewFence`, `HeadlessLegacyPlayIsIndependent_MeshProfileRejectsCpuSurfaceAndForeignPayloadKinds` |
| First ticket/поздняя camera/resize/отсутствующий stamp | `RequiredMeshPresentationTests` |
| Content change между capture/GPU completion/presentation | `RequiredMeshProtocolTests.ContentChangeDuringGpuFlight_OldActualPresentationCannotResumePlaying_AndCurrentFrameCanRetry` |
| Snapshot identity/exclusive payload, paused use сверх3frames | `GpuSnapshotFrameTests` |
| Lazy both-DI, shared geometry, borrowed World и reload | `RuntimeMeshRenderHostTests.AppRegistration_IsLazy_ReloadSharesGeometry_AndDetachReleasesBeforeBorrowedWorld` |
| Failed creation/cleanup retry/cancel late upload/World retention | Остальные `RuntimeMeshRenderHostTests`, включая `RuntimeCleanupFailure_KeepsBorrowedWorldAndProtocolUntilMeshRetirementRetry` |
| Shared mesh/material, red linear.5→sRGB188, сосед white255, reset, shared gray128, delete, geometry revision | Native proof captures `initial/tint/reset/material-reload/delete/mesh-reload` с pixel counts и PNG |
| Resize/suspend/resume/Stop/new game generation | Native `resize/resume`,0 live slots/handles/uploads и balanced buffers/memories/devices |
| Required-view failure после actual upload | `FailedMeshGame`, отдельный третий child,0validation errors/warnings, retained primary error и released owners |
| Replace без logical content checkpoint | `replace-rejected`: Unsupported capture до StopOld, прежние identity/view/image живы |

Полная logical material/content checkpoint/Replace migration остаётся **ARCH1f.6**;
этот этап проверяет корректный отказ и retirement, не обещает восстановление таких
ревизий. Textures/lighting/import/audio, device loss, physical multi-adapter matrix,
performance/long-run release acceptance и neutral Abstractions/Mod SDK сюда не входят.
Датированные результаты и manifest фиксируются в STATUS/HISTORY после запуска.
