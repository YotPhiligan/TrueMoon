# Рабочий уровень модулей перед движком

Обновление **2026-10-19 после UI INF7**. Воспроизводимый runner и read-only verifier:
UI613/Hosting79 в каждой Debug/Release,4TRX/1384passed/failed0/skipped0,
21runner guard checks,24native processes,10fresh packages прошли. All46commands
и standalone Assembly подтверждены; source/package Cobalt/DI×OpenGL/Vulkan×
Debug/published Release, package analyzer отключён. Input/artifact fingerprints,
ownership/startup errors/cleanup и required analyzer/native payloads проверены.
[Scope/evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).
Нового full-solution/nonincremental build/golden/engine viewport/native RTE/
performance запуска нет. Full Debug1490 ниже — INF6. Далее общий ENG3;
full INF5 частичен, hardware blockers прежние.

Предыдущее обновление **2026-10-18 после UI INF3**. Frame scheduling/wake/output guards:
UI613/Hosting39 Debug/Release, повторный full Debug1450/1450; первый1449/1450
имел Mithril readiness sharing failure, targeted20/20 и full94Mithril retry прошли.
Fresh7packages/2headless consumers,10native processes с standalone/HUD/golden360/
engine viewport прошли в Debug/published Release. CPU/GPU speedup не измерен.
Full INF5 частичен; следующий UI шаг INF4. Общий Release solution, прежняя full10package/
40consumer/RichTextEditor native matrix и собственные headless/
performance proofs остальных модулей в этом шаге не повторялись. Предыдущие
module results ниже сохраняют собственные даты и scopes.

Позднее2026-10-15 определён ENG0 и принят допуск ограниченного prototype с UI
рисками. Это документационное решение без нового module/engine regression run.

[Общий план](PLAN.md), [текущий статус](STATUS.md), [история сверки](HISTORY.md#матрица-качества-модулей--2026-10-15).

## Матрица

| Направление | Достигнутый рабочий уровень | Предыдущие проверки | Что остаётся за границей |
| --- | --- | --- | --- |
| [Core](core/STATUS.md) | Bounded headless quality проход завершён: configuration/bootstrap, diagnostics, cooperative lifecycle, owned cleanup |2026-10-15:107/107 Debug и Release;52source +80package +3README processes на Cobalt/DI. [Evidence](core/HISTORY.md#сквозной-headless-proof--2026-10-15) | Uncooperative Task не прерывается timeout; concurrent atomic GetOrCreate, crash-safe JSON, другие platform/deployment modes и engine workloads не доказаны |
| [Cobalt / DI](cobalt/STATUS.md) | COB6a–d, COB5/COB7 завершены в собственном TrueMoon API scope. Два projects/один consumer package; Cobalt остаётся default, Microsoft adapter сохранён |2026-10-15 COB7:runtime179/generator91 **Release**, source20functional scenarios и package120behavior scenarios; paired baseline. Core/MIT5 имеют отдельные более поздние integration runs. [Evidence](cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15) | Массовая migration explicit UseDI отложена; warm/generic/transient allocations требуют профиля. Scopes, reflection fallback, Microsoft ecosystem bridge и UI equivalence не заявлены |
| [Enerit](enerit/STATUS.md) | Typed same-build pipes, strict response/decode ownership, cancellation/error/reconnect и ENE5 baseline проверены; package composition с Mithril подтверждена отдельно |2026-10-15 ENE5:runtime126/generator114 Debug и Release;10cases/90samples. MIT5 позже:runtime126 Debug/Release и package108; generator/performance там не повторялись. [Baseline](enerit/BASELINE.md), [MIT5 evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15) | MMF/ENE3, schema negotiation, bounded queues/backpressure, remote handler cancellation и uncooperative shutdown не подтверждены. Frame transport ещё не реализован |
| [Mithril](mithril/STATUS.md) | MIT0–MIT5 завершены в bounded own-child/App scope: startup/stop/restart budget/parent exit, IPC composition, operational diagnostics |2026-10-15 MIT5:94/94 Debug и Release;40restart +18shutdown apphost/published cases;108fresh package cases,96с наблюдаемой диагностикой parent. [Evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15) | Borrowed attach отсутствует; independent external restart, arbitrary hooks/uncooperative callbacks и long resource trend не доказаны. Repeatable game session supervisor — отдельная будущая работа |
| [Alloy / Argentis](alloy/STATUS.md) | INF1–INF4/INF6/INF7/минимальный INF5; bounded work/frame/close, explicit root/compatibility и воспроизводимый runner |2026-10-19:UI613/Hosting79 Debug/Release,21runner guards,24native processes,10fresh packages. [INF7 evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19). Golden360 — INF3, RTE own matrix2026-10-17 | Full INF5 частичен. K3/K7/K8/VRAM/final matrix открыты; alpha частична; следующий общий шаг ENG3 |
| [Engine / Editor](engine/STATUS.md) | ENG1 supervisor +ENG2 CPU ring/bounded receiver/Alloy OpenGL viewport, сохранены App/authoring state |2026-10-17:engine87/87 Debug/Release,34native cases (32ENG1 +2viewport), по20Start/Stop, actual pixels/resize/EOF/deadline/native close. [Evidence](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17) | ENG3 replacement/checkpoint/compiler ещё нет; packages/3D/shared GPU/long resource/performance proof не заявлены |

Текущий INF7:4TRX/1384passed/failed0/skipped0 (UI613/Hosting79 Debug/Release),
21runner guards,24native processes,10fresh packages. Full solution не повторялся.

Предыдущий INF6 full Debug1490/1490: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/engine87,9TRX/failed0/skipped0; Release UI613/Hosting79.
Source12native processes и8independent native consumers прошли. Nonincremental
build/golden в INF6 не повторялись.

Предыдущий INF4 full Debug solution1466/1466: Core107/Cobalt179/generator91/Enerit126/
generator114/Mithril94/UI613/Hosting55/engine87,9TRX/failed0/skipped0; Release UI613/
Hosting55 отдельно.10manual/engine native processes и2independent native package
consumers прошли. Nonincremental build/golden в INF4 не повторялись.

Последний общий nonincremental Debug build — **2026-10-18, UI INF3**:
0errors/1211warnings. Повторный full Debug1450/1450,9TRX/failed0/skipped0;
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting39/engine87.
Первый full1449/1450/Mithril93/94 и targeted20/20 сохранены отдельно; passing retry
не доказывает устранение readiness race. Release UI613/613 и Hosting39/39 отдельно;
полного Release solution run нет. Hosting project
проверяет реальную Hosting assembly, fake surfaces не выдаются за GPU proof.
10native processes Debug/published Release: scheduling, legacy dispatcher, HUD,
read-only golden360 и2ENG2 viewport; это не32case headless ENG1 run. Independent
frame API проверен на7fresh packages/2headless cases.
[INF3 evidence](alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).
Предыдущие2026-10-17 engine87/UI570 Release, package/golden и rich editor matrix
сохраняют прежние scopes, не переносятся на final INF3 source автоматически.
Warnings не подавлены; UI hardware acceptance и общий release R1–R6 не закрыты.

## Решение о порядке работ

1. **UI package/Hosting proof выполнен2026-10-15** после bundled Cobalt и explicit
   font catalog. [Контракт и результат](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15).
   Воспроизведён missing HostedUiDispatcher resolver; explicit factory исправила
   startup без новой зависимости Hosting от Cobalt. Consumer/native/font/analyzer
   provenance и bounded startup/cleanup проверены свежими package consumers.
2. По решению пользователя2026-10-15 выбран ограниченный
   [ENG0 scope](engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15).
   Он определён документом; запрошенный перед ENG1 RichTextEditor/PA7a реализован
   и проверен2026-10-17. Затем выполнен ENG1 headless supervisor;
   ENG2 CPU ring/viewport выполнен; следующий code этап — ENG3 replacement.
   K3/K7/K8/VRAM/final UI alpha сохраняются; engine readiness и вся beta не заявляются.
3. Cobalt hot-path allocation и Enerit scalar CPU/allocation profiles остаются
   отдельным измерительным backlog. Оптимизацию выбирать по профилю; новые DI/IPC
   capabilities не становятся обязательными для завершения quality прохода.

Исходная проверка до реализации показала: `AlloyPackageConsumer` выбирал
`UseDI()` без варианта Cobalt, вызывал backend setup без `SkiaFontCatalog`, а
runner сохранял SourceFiles до запуска без финального сравнения. Поэтому его
успех2026-10-11 не закрывал новый совместный scope. Этот gap закрыт fresh proof
2026-10-15; failures и final evidence сохранены в UI HISTORY.

## UI ограничения и условия продолжения

| Критерий | Состояние по предыдущим запускам | Требуемое продолжение |
| --- | --- | --- |
|5.4/5.5 package / Core-Cobalt-Hosting |2026-10-17:10fresh packages,36App +4direct processes с rich text, transitive Topten/HarfBuzz, explicit fonts/native/bundled analyzer и финальные fingerprints прошли | Bounded delivery/cleanup scope завершён; прочие deployment modes и final alpha отдельно |
| K3 / Windows4.9 | Native144DPI/Scale1.5 и directed scales проверены; physical100/200%/разные DPI мониторов не были доступны | Подготовленное окружение и оставшиеся physical input/chrome checks. Смена Windows settings не назначена этой сверкой |
| K7 / lifecycle | Предыдущий full240-window Debug/Release **failed**:GDI23→137 при USER26→26. Raw/debugger trace2026-10-10 локализует screen HDC allocation до NVIDIA ICD32.0.16.1742 | По решению пользователя app-side workaround search остаётся остановлен. Возврат при supported fix либо отдельно согласованной, полностью проверенной alternate GPU configuration; raw AMD stability не является full UI pass |
| K8 / loss и VRAM | Controlled failure/recreate проверены отдельно; physical device/context loss и long VRAM evidence отсутствуют | Собственные воспроизводимые проверки на заявленной конфигурации; не подменять controlled injection реальной потерей |
| Final UI / G4 | Частичный alpha, прежние K1–K9/PA1–PA10 сохранены | Полный согласованный проход остаётся отдельным критерием; package smoke его не закрывает |

Полный UI alpha не является условием выбранного пользователем ENG1–ENG3 prototype.
Допуск явно записан в ENG0; для actual Editor/viewport readiness UI ограничения
сохраняются. Ни K7/G4 pass, ни общий release этим решением не объявлены.
Кроме отдельно запрошенного PA7a, PA1–PA10, MMF, scopes, Hot Reload и GPU frame sharing
не входят в этот quality этап.

## Что означает завершение этой сверки

Сводка и очередность согласованы между общим и модульными STATUS; старые результаты
сохраняют даты. UI Hosting получил собственный bounded code/package/native proof;
Core107/Cobalt179/generator91 отдельно Debug/Release повторены вместе с UI532.
Другие modules/performance не повторялись; full-solution test pass не заявляется.
Выбор DI остальных приложений и системные настройки не менялись.
