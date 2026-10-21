# TrueMoon: общий статус

Последняя работа — **2026-10-21, ENG5b input/camera**: control wire v2,
optional fixed-tick gameplay input, camera/checkpoint/Replace и интерактивный
GPU sample `--scene`. UI keys/mapping дополнены. Full Debug/Release1968/1968
в каждой configuration,9TRX/failed0/skipped0; Aether562/UI615/Hosting79/Mithril95.
Полная native regression56/56 и отдельный усиленный input proof2/2 по15cases/5PNG,
validation0/0/zero ownership. Три local packs/payload совпали с published DLLs;
отдельный package drawing не запускался. [Scope/commands/provenance](engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
Далее ENG5c комната/interaction/HUD. Raw cursor/physical input/3D performance
и прежние UI hardware/release ограничения остаются открытыми.

Предыдущая работа — **2026-10-21, ENG5a Vulkan3D primitive renderer**:
immutable scene/camera/material, depth-tested cube в App-owned GPU viewport,
без CPU pixels/staging. Debug/published Release: по5PNG assertions, resize/
suspend/resume/checkpoint Replace, validation0/0 и zero owned resources после
teardown. Full solution Debug1926/1926 иRelease1926/1926, по9TRX/failed0/skipped0;
Aether520/UI615/Hosting79/Mithril95. [Контракт, commands и scope](engine/HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
Native matrix12/12cases/14commands,430source/76binary hashes; local Aether pack
и payload SHA256 check прошли, отдельный package drawing consumer не запускался.
Далее ENG5b input/camera; весь ENG5 и3D performance ещё открыты.

Предыдущая работа — **2026-10-21, ENG4e и Mithril graceful-stop fix**:
CPU/GPU baseline12cases/36intervals/24cooperative children,421source/94binary
fingerprints; raw captures и frozen budgets сохранены. Release1280×720 median
per-run p95: CPU65–66ms/GPU21–33ms; reference GPU allocation~107MiB/s.
Debug CPU имеет2FPS/6latency advisory breaches, budgets не ослаблены.
Исправлена гонка error delivery/remaining graceful wait; red/green и native20
Mithril shutdown cases прошли. Отдельный GPU validation run6cases прошёл.
Свежие units packages:108/108cases/8nupkgs; expected analyzer negative control.
Full solution Debug1907/1907 иRelease1907/1907, failed0/skipped0, по9TRX.
[Метрики, команды, failures и ограничения](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Следующий функциональный шаг — ENG5; UI PA8a/RTE и прежние hardware/release
ограничения сохраняются. Reference timings не доказывают будущий3D performance.

Предыдущая работа — **2026-10-21, исправление структуры проектов**:44проекта/
597C#files, namespace/category violations0. Типы сгруппированы по назначению,
публичные namespace и consumers обновлены; общий Contracts остаётся независимым.
Full Debug/Release1898/1898 в каждой конфигурации, failed0/skipped0; build0errors/
1499warnings. Fresh Core80+3README/units108/UI16source+8package и36+4smoke/Aether4
App consumers прошли. Итоговая targeted native engine matrix20cases прошла;
выявленный lookup исторических leases по повторно использованному PID исправлен
на прямую ссылку текущего owned lease. [Миграция, команды, повторы и ограничения](HISTORY.md#исправление-структуры-проектов--2026-10-21).
На момент этой работы следующим был ENG4e; прежние UI hardware/release ограничения
сохраняются. Native probe теперь сохраняет исходные причины до assertion.

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. Общий
transport/presentation contract подключён к Start/selected DLL/Build/Replace/
checkpoint/rollback; explicit GPU сохраняет обычный View/commands, CPU default.
Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,9TRX/
failed0/skipped0. Build0errors/1499warnings; fresh12packages/4App consumers.
Native54cases/56commands,406source/76binary fingerprints; per GPU Editor20Replace/20failures/53children, validation0/0, imported readback0.
[Команды/границы](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured CPU/GPU baseline перед 3D. K3/K7/K8/VRAM/final alpha
сохраняются; drivers/Windows settings/K7 stop policy прежние.

## Состояние модулей

| Модуль | Предыдущий собственный результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-15:107/107 Debug и Release,52source +80package +3README; bounded configuration/bootstrap/diagnostics/lifecycle proof | Новых Core API для UI quality не требуется; cooperative shutdown и platform limits сохраняются |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-15 COB7:runtime179/generator91 Release, paired App comparison и independent consumers; два projects/один package | Default Cobalt/adapter сохранены; массовая UseDI migration отложена. UI получает свой proof, allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-15 ENE5:runtime126/generator114 Debug/Release,10cases/90samples. Позднее MIT5 повторил runtime126 Debug/Release/package108 | Same-build pipes scope; MMF/schema/backpressure/engine frames не подтверждены |
| [Mithril](mithril/STATUS.md) |2026-10-21:95/95 Debug/Release, graceful delivery race red/green,20native shutdown и108fresh package cases; own standalone restart matrix отдельно не повторена | Прежний bounded own-child/cooperative scope, budgets не увеличены; external restart не объявлен готовым |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-21:UI615/Hosting79 Debug/Release; W/S/D/E mapping и bounded ENG5b input proof | Далее ENG5c комната/HUD; PA8a/RTE запланированы. Full INF5 частичен, K3/K7/K8/VRAM/final matrix открыты |
| [Aether / Engine](engine/STATUS.md) |2026-10-21:Aether562 Debug/Release; ENG5a renderer и ENG5b fixed-tick input/camera; native56+2 scoped cases | ENG5c комната/interaction/HUD;3D performance и CPU Debug/reference allocation debt открыты, API experimental |

Подробные counts/configuration/commands и ошибки остаются у владельцев. Эта таблица
сводит разные предыдущие runs и не является одним full-solution regression pass.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Headless, units и fresh UI consumer подтверждены отдельными runs; bounded CPU/GPU Editor/Replace, ENG5a3D renderer и ENG5b input/camera проверены; комната/HUD/standalone предстоят |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний full solution run — **2026-10-21, ENG5b**:
Debug1968/1968 иRelease1968/1968, по9TRX, failed0/skipped0: Core107/Cobalt179/
generator91/Enerit126/generator114/Mithril95/UI615/Hosting79/Aether562.
Incremental dotnet test builds; nonincremental build не повторён.
[Точные current commands/evidence](engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
Предыдущие reference performance12cases, GPU validation6 и Mithril native20 —
отдельные [ENG4e scopes/failures](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Прежние nonincremental0errors/1499warnings и fresh structural consumers находятся
в [предыдущей записи](HISTORY.md#исправление-структуры-проектов--2026-10-21).
Golden/RTE/full UI performance/hardware/release acceptance не повторялись;
исторические результаты сохраняют даты и ограничения у владельцев.

## Общие блокеры и неопределённости

- UI package/Hosting/fonts gap закрыт2026-10-15; fresh40process proof не закрывает
  physical/final UI acceptance. Cobalt concrete View/model registrations обязательны
  для consumer source generation; автоматическая массовая UseDI migration не назначена.
- K7: предыдущий full240-window failed (GDI23→137/USER26→26). Raw trace связан с
  NVIDIA ICD32.0.16.1742. По решению пользователя workaround search остановлен;
  возврат при supported fix либо согласованной полностью проверенной alternate
  configuration. Raw AMD comparison не является full UI pass.
- Physical DPI100/200%/разные мониторы не были доступны в последнем проверенном
  окружении; real device/context loss, VRAM и final UI matrix не подтверждены.
- Cooperative shutdown не прерывает игнорирующий token user Task. Provider alias
  disposal semantics различаются. Same-build proofs не обещают другие OS/RID/AOT,
  долгие resource trends, engine frames/reload/session lifecycle.
- Общий supported configuration/SDK/OS scope, warnings policy и R1–R6 release
  acceptance остаются отдельными решениями; historical suites нельзя суммировать
  в current full-solution pass.

## Следующий конкретный шаг

1. UI INF0–INF4/INF6/INF7 и минимальный INF5 завершены в указанном scope.
   Full INF5 частичен. ENG1/ENG2 сохраняют bounded proof.
   ENG3a–d compiler/checkpoint/Replace/Editor commands и20Replace proof выполнены2026-10-20.
   ENG4a capability decision/read-only native probe выполнен в bounded scope.
   ENG4b one-frame import/completion/pixels/failure/cleanup проверен с validation.
   ENG4c slots/surface lifecycle/retry/queued resize проверен с validation.
   ENG4d borrowed texture/retained GPU copy/actual viewport bridge проверен.
   ENG4d2 ordinary GPU Editor/selected DLL/checkpoint/rollback выполнен.
   ENG4e сопоставимый CPU/GPU latency/allocations/resource baseline получен.
   ENG5a первый depth-tested3D frame в существующем viewport реализован/проверен.
   ENG5b input/camera выполнен; следующий общий code шаг — [ENG5c](engine/STATUS.md#следующий-конкретный-шаг):
   комната/осмысленный interaction/переключатель/HUD.
   CPU Debug budget breaches/reference GPU allocation — measured backlog;
   performance настоящего3D измерять на новом workload.
   UI K3/K7/K8/VRAM/full alpha сохраняются; PA1–PA10 автоматически не начинаются.
2. K7 workaround search остаётся остановлен по решению пользователя. Supported fix
   или согласованная alternate configuration требуют собственного полного proof;
   driver/Windows settings без соответствующего запроса не менять.
3. Cobalt allocation и Enerit scalar CPU/allocation investigation — отдельный
   measured backlog перед оптимизацией; default/adapter/migration решение прежнее.

## Изменения формата — 2026-10-09

Исходная история разделения планов и проверок сохранена в
[архиве общего статуса](HISTORY.md#сводка-до-quality-matrix--2026-10-15).
Обновление2026-10-15 сократило STATUS до текущей сводки и синхронизировало очередность;
критерии модулей, G1–G6/R1–R6 и UI K1–K9/PA1–PA10 не отменены.
