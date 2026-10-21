# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-21, ENG5b gameplay input/camera**:
control v2, optional IInputGameSession, bounded staging/lease и fixed-tick camera,
отдельная CameraGame, interactive `--scene` с WASD/mouse/E/Escape. Input transient;
camera/tick/interaction сохраняются при checkpoint Replace. App-owned supervisor
проверяет exact identity внутри gate; stale input не retargeted. Один30Hz sender
drains до host/UI; Pause/focus/capture/resize/suspend/Stop/restore сбрасывают input.
Argentis W/S/D/E и Silk mapping дополнены. Raw mouse/cursor lock отсутствуют.
42новых xUnit cases; full Debug1968/1968 иRelease1968/1968,9TRX/configuration,
failed0/skipped0: Aether562/UI615/Hosting79/Mithril95.
Native control/CPU/GPU/3D/input regression56/56cases/58commands; усиленный input
proof2/2 (Debug/published Release), по15cases/5PNG с реальным Silk key callback.
Validation0/0, imported ReadPixels0, resources/handles/mappings/UI sessions0,
sender завершён. После full matrix изменён только proof harness; library DLLs
сверены между полным и усиленным run. Local packs Aether/Argentis/Platform.Silk
прошли, DLL payload сверены с published proof; отдельный package drawing не запускался.
[Контракт, exact tests/commands, provenance и ограничения](HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
Далее ENG5c комната/interaction/HUD; весь ENG5 ещё частичен.

Предыдущая работа — **2026-10-21, ENG5a: первый Vulkan3D renderer**.
Optional immutable scene snapshot под simulation/checkpoint lock, perspective
camera, cube, opaque material и slot-owned depth/pipeline реализованы в одной
TrueMoon.Aether. Vulkan рисует в exportable image без CPU pixels/staging; обычный
App/Editor/GPU transport и default CPU/reference путь сохранены.
Debug и published Release 3D proof: по5PNG assertions (depth/material/camera,
resize/suspend/resume/checkpoint Replace), core/sync errors/warnings0,
resources/handles/mappings/UI sessions0, imported ReadPixels0.
Итоговая native matrix12/12cases/14commands,430source/76binary hashes: новые
scene и прежние import/surface/viewport/ordinary GPU Editor/repeat прошли.
Local pack прошёл; DLL package совпадает SHA256 с native published proof.
Новые focused tests19/19; full solution Debug1926/1926 иRelease1926/1926,
по9TRX/failed0/skipped0: Aether520, UI615, Hosting79, Mithril95.
Это source apphost/published3D proof; package GPU drawing и3D performance не
подтверждены. [Контракт, команды, failures и evidence](HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
ENG5a завершает первый renderer, весь ENG5 остаётся частичным; далее ENG5b input/camera.

Предыдущая работа — **2026-10-21, ENG4e CPU/GPU baseline**. Реализованы opt-in
stage timings, bounded raw/process captures и воспроизводимый runner.
Финальная матрица12/12cases/36intervals/24cooperative children/36pixel checks,
15commands/421source/94binary fingerprints, Completed=true. Release1280×720:
медиана per-run p95 CPU65–66ms/GPU21–33ms; GPU reference allocation~107MiB/s.
Debug CPU:2FPS/6latency advisory breaches, включая12.51fps outlier; budgets прежние.
Raw QPC stages37766, cohorts5244produced/received и5182presented. Local GPU
resources/handles/mappings/UI sessions0; imported ReadPixels0. Это reference
CPU-generated/Vulkan-staging workload, не3D/VRAM/physical-display performance.

Найден/исправлен Mithril exit-delivery/graceful-wait race с red/green regression;
native20shutdown cases прошли. Full solution Debug1907/1907 иRelease1907/1907,
по9TRX/failed0/skipped0: Aether501/UI615/Hosting79/Mithril95, остальные counts
сохранены. Отдельный GPU correctness run:6cases/8commands, Debug/published Release,
core/sync validation0/0; per ordinary GPU Editor20Replace/20failures/53children,
retained copies829/813, imported readback0. Nonincremental build/golden/RTE/full
UI/hardware/VRAM matrix этим не повторены. [Подробные результаты, исходные failures,
команды и ограничения](HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Fresh units packages108/108cases/8nupkgs проверили доставку Mithril fix; это не
Aether GPU package drawing. Независимый replay36intervals/raw37766records и
1048source/170binary hashes прошёл (`TestResults/ViewportBaseline/development/completion.json`).

Исправление структуры — **2026-10-21**: Aether.Contracts устранён;21тип размещён
по назначению в Sessions/Scenes/Transport/Frames/Checkpoints, engine assembly одна.
Все Aether/test/example namespace соответствуют путям. Aether493/493 Debug/Release;
общий solution1898/1898 в каждой конфигурации. Fresh12packages/4App consumers и
итоговая targeted native matrix20cases (Debug/published Release) прошли.
Native checks выявили PID reuse: GPU adapter искал lease по PID в истории.
Оба viewport examples теперь получают текущий owned NativeRuntimeLease напрямую,
а fault decorator раскрывает конкретный inner lease. Probe сохраняет исходную
ошибку до assertion; условия проверок сохранены. Итоговый run включает CPU/GPU
20Replace/20failures proofs. [Команды/границы/миграция](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. UseAether options
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

## Предыдущие проверки ENG4d2 — 2026-10-20

Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,
9TRX/failed0/skipped0. Nonincremental Debug0errors/1499warnings. Final native
54cases/56commands/exit0,406source/76binary fingerprints; Debug apphost и
published Release. Fresh12packages/4Cobalt+DI consumers/18commands проверили
neutral ticket/first-present/strategy ownership, новые APIs и exact managed/native
payload hashes. Evidence запуска2026-10-20 — complete3, package2 и eng4d2-complete-20261020.

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
- Runtime frameProvider получает atomic loaded/restored game's tick/color или
  optional primitive scene. Reference pattern остаётся CPU-generated/staging;
  ENG5a scene рисуется Vulkan без CPU pixel buffer. UI retained GPU copy остаётся.
  Pixels не передаются через pipe и не читаются из imports для delivery.
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
  published Release, не package consumer. Current full solution/ENG4e measurements
  указаны выше; own Aether package/native drawing, golden/RTE/full performance/
  VRAM/hardware/final UI matrix/publication этим не подтверждены. K3/K7/K8/VRAM/
  final alpha прежние; driver/Windows settings/K7 workaround policy не менялись.

## Следующий конкретный шаг

**ENG5c: небольшая комната и HUD.** Развить renderer snapshot в комнату/пол/
несколько примитивов, осмысленный interaction (reach/raycast), переключатель
цвета/света, нужные collisions и Alloy HUD/pause. Сохранить ENG5b fixed-tick input,
camera/checkpoint/Replace, App ownership и surface lifecycle. Raw/locked mouse
profile и physical input acceptance остаются отдельным уточнением.
Standalone launch — отдельный ENG7 scope.
[Переход после baseline](PLAN.md#продолжение-после-eng4e).
CPU Debug generation/CRC/cadence и reference GPU frame buffer allocation —
measured backlog. Их оптимизации требуют до/после этого workload; timings
будущего3D измерять отдельно. GPU query/utilization/VRAM остаются unavailable.
UI PA8a/RTE backlog и K3/K7/K8/VRAM/final alpha не закрыты; K7 search остановлен.
