# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. UseAether options
выбирают lazy App-owned IGameViewportTransport; default CPU. FrameSession сохраняет
общий Start/Resize/Stop/first-present lifecycle, GameEditor/GameReplacement —
selected DLL/checkpoint/rollback. ViewportSurface не содержит native/mapping handles;
ViewportFrame ticket подтверждается только для актуальной presentation.
GpuViewportTransport копирует borrowed texture в независимый UI image, returns
slot после observed GL completion и удерживает первый image до actual first-present.
Aether остаётся одной assembly без UI dependency.
[Контракт](PLAN.md#eng4d-gpu-viewport-bridge--2026-10-20),
[API](../../TrueMoon.Aether/README.md#выбор-transport-в-gameeditor--eng4d2),
[evidence](HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).

## Проверки

Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,
9TRX/failed0/skipped0. Nonincremental Debug0errors/1499warnings. Final native
54cases/56commands/exit0,406source/76binary fingerprints; Debug apphost и
published Release. Fresh12packages/4Cobalt+DI consumers/18commands проверили
neutral ticket/first-present/strategy ownership, новые APIs и exact managed/native
payload hashes. Current evidence — complete3, package2 и eng4d2-complete-20261020.

Каждый ordinary GPU Editor proof:20A/B DLL Replace,20failure cases,53reaped children,
один App/window и MaxLive1. Проверяются loaded manifest/DLL hashes, complete state,
first presented tick после restore, selected next-Step behavior, прежний Paused/
Playing mode, compile/cancel/stale/Stop, single rollback/double failure, cleanup retry
и native close с pending Replace/compiler. Дополнительно GPU resize64×32/suspend/
resume160×90 и stale ticket rejection. Все53cooperative teardown reports:
47devices/141slots created/released, core/sync validation0errors/0warnings.
Imported ReadPixels0; GPU resources/NT handles/mappings/UI sessions0, authoring
сохранён, Game.Logic в Editor не загружен. Прежние ENG1–ENG4d regressions повторены.

Managed cases проверяют latest/foreign/closed tickets, общий CPU receiver ticket,
connection drain до process Stop, pending first-present cancellation, worker failure,
original+cleanup causes/retry, exactly-once strategy ownership, runtime loaded/
restored capture и RGBA frame metadata. Background failed cleanup доступен через
Frames.TransportError; успешный Stop retry не воспроизводит faulted watcher task.

## Границы

- Explicit --viewport-gpu/--viewport-gpu-project использует обычный Editor View и
  команды; --viewport/--viewport-project сохраняют CPU. Автоматического fallback
  при GPU fault нет; transport выбирается на App configuration.
- Runtime frameProvider получает atomic loaded/restored game's tick/color. Пока
  это CPU-generated reference pattern и Vulkan staging upload, не 3D renderer или
  zero-copy. Pixels не передаются через pipe и не читаются из imports для delivery.
- Reference Windows x64/RTX5070Ti/OpenGL Editor/Vulkan child. Ordinary GPU proof
  extents64×32/160×90; larger1280×720 и premul/stored-sRGB150%-DPI/clipping проверены
  отдельными GPU probes. Retained UI pixels после actual FramePresented — не OS
  compositor/physical-display color proof. Bounds1920×1080 не равны новому native proof.
- UI consumer owns copied images на исходном surface/context thread до UI teardown;
  dispatcher/context borrowed до GPU cleanup. Failed cleanup retains connection/
  imports и блокирует новый Start до explicit retry. Arbitrary dispatcher/driver
  hang не прерывается managed deadline; device loss не инжектировался.
- API experimental/packable. Package consumers проверяют managed transport API,
  App ownership и native delivery; actual GPU drawing выполнен source apphost/
  published Release, не package consumer. Full Release solution/golden/RTE/
  performance/VRAM/hardware/final UI matrix/publication не выполнены. K3/K7/K8/VRAM/
  final alpha прежние; driver/Windows settings/K7 workaround policy не менялись.

## Следующий конкретный шаг

**ENG4e: сопоставимый CPU/GPU viewport baseline до 3D.** Зафиксировать одинаковые
pattern/extents/cadence, warmup/repetitions/measurement intervals; сохранить p50/p95
production/copy/GPU retained-copy/actual-present latency, received/presented/dropped,
allocations и доступные resource counters с raw samples. Отсутствующие counters
не считать0. После измерений выбирать оптимизации, затем ENG5 renderer/input/3D.
[Acceptance](PLAN.md#eng4e-cpugpu-viewport-baseline-перед-3d--следующий-шаг).