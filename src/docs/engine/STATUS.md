# Игровой движок и редактор: состояние

Последняя работа — **2026-10-17, ENG2 CPU frame viewport**.
[Scope/точный layout](PLAN.md#eng2-cpu-frame-viewport--2026-10-17),
[commands/failures/evidence](HISTORY.md#eng2-cpu-frame-viewport--2026-10-17),
[запуск окна/probe](../../ManualTests/EngineSessionProbe/README.md), [общий статус](../STATUS.md).

**ENG0 определён; ENG1 и ENG2 реализованы в bounded Windows x64/net10.0 scope.**
Probe host/contracts/Game.Logic/tests остаются в ManualTests. CPU ring и одно
Alloy/OpenGL окно реализованы; production engine packages/3D/replacement ещё нет.
Production UI дополнен owned `SkiaImageSource.CopyBgraPixels`; Enerit/Mithril/Core/
Cobalt APIs для ENG2 не менялись. RichTextEditor improvements остаются в
[отложенном UI плане](../alloy/RICH_TEXT_EDITOR_PLAN.md).

Проверки2026-10-17/SDK10.0.402: engine87/87 и UI570/570 отдельно Debug/Release;
full Debug solution1368/1368,8TRX/failed0/skipped0. Native34/34 Debug apphost/
published framework-dependent Release без RID:32ENG1 regression cases и2viewport
runs с14сценариями каждый, включая по20Start/Stop при одном Editor App/window.
Nonincremental Debug build0errors/1211warnings. Это не полный Release
solution suite и не общий UI/package/performance acceptance.

## Принятый scope и собственная проверенность

| Область | Реализация / проверки ENG1–ENG2 | Граница вывода |
| --- | --- | --- |
| Host/lifecycle | Fresh owned Mithril child handle/generation, Never restart; Hello/scene/mode/Step/status/stop, stale/identity/timeout/cancel/crash/disconnect/heartbeat/parent exit/reap повторены | Probe-only, long resource trend не измерен |
| Frame codec | Explicit little-endian header1024 +3slot descriptors, checked layout/shape/identity, CRC32 и frame/tick pattern; corruption/EOF/abandonment/100ms timeout terminal | Windows named MMF/mutex/event; не Enerit ENE3/MMF или shared GPU |
| Ring/receiver | Free/Writing/Ready/Reading, latest-ready, producer drop/no overwrite Reading;2private buffers/1pending; slow UI, disposal/drain и stale display rejection | Image/native/GPU storage отдельно; VRAM trend не доказан |
| Viewport | Actual OpenGL readback corners и enlarged frame/tick counters, BGRA/origin; first-presented readiness, resize16×16..1920×1080, zero-size/resume, live-child EOF и native WM_CLOSE | Physical DPI/minimize/driver-loss acceptance общего UI не заменяет |
| Limits | Common startup15s включает launch/Hello/scene/surface/first presentation; deadline/cancel/fence,≤2mappings/50MiB, EOF/crash cleanup и failed-reap retry | p50/p95 diagnostics записаны без performance baseline; target30fps/60Hz не гарантия |
| Authoring | Revision/Dirty/selection/panels и App сохраняются; Game.Logic в Editor не загружается | Нет checkpoint/restore или редактирования игровой сцены |

## Оставшиеся границы

- IUnitsController после Stop не возобновляется; probe использует fresh owned handle
  и launch context через existing protected hooks. Reflection/borrowed attach нет.
- Same-build control требует совпадающих host/contracts binaries; schema negotiation
  Enerit не добавлена. Разные executables/standalone game относятся к поздним этапам.
- InputBatch/focus/reset/overflow contract сохраняется в PLAN, реализация отложена
  до engine input scope. ENG2 control buttons не означают игровых input events.
- UI K3 physical DPI, K7 NVIDIA lifecycle, K8 real device/context loss, VRAM и final
  alpha остаются открыты. Обычный child crash не доказывает GPU/driver isolation;
  driver/Windows settings не менялись, workaround search не возобновлялся.
- Physics/audio/import/generic ECS, произвольная migration и непрерывная игра при
  replacement не обещаются этим прототипом. Runtime self guard2minutes сохраняется.

## Следующий конкретный шаг

**ENG3: versioned Game.Logic replacement.** Добавить compiler job/build-ID каталоги,
минимальный versioned camera/interaction checkpoint, bounded stop/reap/start/restore,
сохранение old build при compile failure и не более1rollback attempt. Проверить
first-frame readiness после restore и20Replace cycles при одном Editor App.
CPU transport/viewport и authoring state должны пережить replacement; старый child
полностью reap до запуска candidate. ENG3 пока не реализован.

## Проверки документации

Сверка ENG2 links/anchors/fences/whitespace, final source/binary fingerprints и
staged-state guard отражены в HISTORY. ENG1 проверки и прежние собственные module
results сохранены отдельно; исторические run не выдаются за текущий acceptance.
