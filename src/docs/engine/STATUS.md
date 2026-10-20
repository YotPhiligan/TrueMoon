# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-19, реализация подключения через App**. Публичный
UseAether валидирует и фиксирует options; AetherHost владеет Supervisor/Frames,
App Stop/rollback/disposal выполняют drain и child cleanup. Startup не запускает
игру автоматически. Concurrent shutdown разделяет completion; failed cleanup
наблюдаем и допускает явный retry. Headless/viewport examples переведены на модуль.
Правило такого подключения функциональных модулей закреплено в
[AGENTS.md](../../AGENTS.md#подключение-модулей-к-app) и общем плане.
[API/пример](../../TrueMoon.Aether/README.md#подключение-к-app),
[проверки и границы](HISTORY.md#app-integration-реализация--2026-10-19).

Aether115/115 Debug и Release; full Debug1518/1518,9TRX/failed0/skipped0.
Native36/36 Debug apphost/published Release:34headless и2viewport с actual GL
pixels, по20Start/Stop и child/mapping cleanup. Свежие9nupkgs и4независимых
package consumers проверили App integration на Cobalt/DI. Nonincremental Debug
build:0errors/1507warnings, без предупреждений в новых integration tests.
Aether наследует IsPackable=true; локальная package delivery проверена, публичной
публикации не было. ENG3a остаётся следующим code шагом.

Предшествующий read-only [аудит App integration](HISTORY.md#проверка-app-integration--2026-10-19)
описывает состояние до реализации. Deferred-preview metadata audit ниже относится
к прежнему input set с IsPackable=false.

Предыдущий package metadata check — **2026-10-19**: Aether получил самостоятельный PACKAGE.md,
тематические tags и общие project/repository/license/icon metadata. Explicit local
preview pack/nuspec/payload checks прошли; default IsPackable=false сохранён,
NuGet delivery не включена. Own unit/native checks в этом metadata шаге не повторялись.
[Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Предыдущая работа — **2026-10-19, выделение TrueMoon.Aether**. Одна engine library
объединила contracts, supervisor/protocol, control adapters/native child bridge и
CPU ring/receiver/FrameSession. Namespaces TrueMoon.Aether/TrueMoon.Aether.Contracts;
отдельный Engine.Probe.Contracts project удалён. TrueMoon.Aether.Tests использует
прямой ProjectReference на модуль, без dependency на executable/linked source copies.
Пример Editor/Game.Runtime и отдельная Game.Logic DLL остались consumers.
[Архитектура](PLAN.md#truemoonaether-одна-engine-assembly--2026-10-19),
[README](../../TrueMoon.Aether/README.md),
[команды/evidence](HISTORY.md#truemoonaether--2026-10-19).

Aether87/87 отдельно Debug/Release; full Debug solution1490/1490,9TRX,
failed0/skipped0. Native34/34 Debug apphost/published Release:32headless и2viewport
с actual GL pixels, по20Start/Stop, generation/identity/errors/cleanup. Source339/
binary76 fingerprints сохранены. UI613/Hosting79 Debug — часть full solution;
own Release UI/golden/RTE/пакеты/performance/hardware matrix не повторялись.
На дату выделения модуль был IsPackable=false; теперь true, API экспериментальный.
ENG3 по-прежнему не реализован.
Следующий code подэтап — ENG3a в Aether; UI/3D/release ограничения прежние.

Предыдущая работа — **2026-10-19, сверка плана после UI INF7**. Очередность ENG3
сохранена; [декомпозиция ENG3a–d и вопросы до code](PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19)
сверены с loader/identity, protocol/supervisor и first-frame path. Уточнены
артефакты на generation, restore-before-present, recoverable checkpoint error,
cancellation/fences и необходимость распределить общий deadline. Это документация:
runtime/production code не менялся, build/tests/native/performance не запускались.
ENG3 не реализован; прежние ENG1/ENG2 passing результаты не обновлены.
[Findings/границы сверки](HISTORY.md#сверка-плана-после-ui-inf7--2026-10-19).

UI integration check — **2026-10-18, INF4**: ViewportProbe использует HostedUiWindow
Closing для stop/drain engine tasks вместо ручного NativeWindow.Closing interception.
Перед возвратом handler проверяет UI/canvas живыми и mappings0. Debug apphost и
published Release smoke прошли, по14scenarios/25child launches/20StartStop, actual
GL pixels, native-close-before-UI-disposal, sessions/mappings0/childrenreleased.
Engine87/87 Debug в full solution1466/1466; own Release suite/32headless не повторялись.
[Команды/evidence](../alloy/HISTORY.md#inf4-асинхронное-закрытие--2026-10-18).
Это UI integration rerun; ENG3 не начинался, scope ENG2 не расширен.

Предыдущий UI integration check — **2026-10-18, INF3**: ViewportProbe явно выбирает Continuous
для receiver polling и RequestFrame после dynamic upload; session diagnostics
проверяют IDs/phases/Disposed/Pending0 и actual presentation count. Два ENG2 smoke
Debug apphost/published Release прошли с actual GL pixels и cleanup. Engine87Debug
повторён в full solution; engine Release/32headless native cases не повторялись.
[Единственные подробные команды/evidence UI](../alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).
Это instrumentation consumer, ENG3 не начинался и scope ENG2 не расширен.

Последний собственный engine этап — **2026-10-17, ENG2 CPU frame viewport**.
[Scope/точный layout](PLAN.md#eng2-cpu-frame-viewport--2026-10-17),
[commands/failures/evidence](HISTORY.md#eng2-cpu-frame-viewport--2026-10-17),
[запуск окна/probe](../../ManualTests/EngineSessionProbe/README.md), [общий статус](../STATUS.md).

**ENG0 определён; ENG1 и ENG2 реализованы в bounded Windows x64/net10.0 scope.**
С2026-10-19 engine implementation/contracts и tests находятся в Aether/Aether.Tests;
host/Game.Logic остаются в ManualTests. CPU ring и одно
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

**ENG3a: compiler job и immutable build artifact.** Сначала зафиксировать descriptor/
dependency set/child-only loader и правила build cancel/timeout/stale completion,
затем реализовать сборку и выбор build на generation без перезаписи active host.
Compile failure сохраняет рабочую session и authoring state; Editor не загружает
Game.Logic. После этого ENG3b checkpoint, ENG3c transaction/rollback и ENG3d proof
по [детальному плану](PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19).

Полный **ENG3: versioned Game.Logic replacement** включает compiler job/build-ID каталоги,
минимальный versioned camera/interaction checkpoint, bounded stop/reap/start/restore,
сохранение old build при compile failure и не более1rollback attempt. Проверить
first-frame readiness после restore и20Replace cycles при одном Editor App.
CPU transport/viewport и authoring state должны пережить replacement; старый child
полностью reap до запуска candidate. ENG3 пока не реализован.

## Проверки документации

Сверка ENG2 links/anchors/fences/whitespace, final source/binary fingerprints и
staged-state guard отражены в HISTORY. ENG1 проверки и прежние собственные module
results сохранены отдельно; исторические run не выдаются за текущий acceptance.
