# Рабочий уровень модулей перед движком

Обновление **2026-10-17 после ENG2**. Engine CPU viewport/native proof,
engine/UI Release и полный Debug solution suite выполнены заново. Общий Release
solution suite, UI package/golden/RichTextEditor native matrix и собственные
headless/performance proofs остальных модулей не повторялись. Документ сводит
разные runs; детальные задачи остаются в модулях.

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
| [Alloy / Argentis](alloy/STATUS.md) | RichTextEditor/PA7a реализован на Topten.RichTextKit с neutral contracts/options; Cobalt/DI, explicit fonts/native delivery и cleanup проверены в bounded Windows x64/net10.0 scope |2026-10-17:UI563/563 отдельно Debug/Release,6native rich editor cases, прежний golden360/360 с validation teardown,10fresh nupkgs/36App +4direct с rich text и expected negative control. [Evidence](alloy/HISTORY.md#richtexteditor--2026-10-17) | IME/full bidi/rich clipboard вне PA7a. K3 physical DPI, K7 lifecycle, K8 real loss, VRAM и final supported matrix остаются; alpha частична |
| [Engine / Editor](engine/STATUS.md) | ENG1 supervisor +ENG2 CPU ring/bounded receiver/Alloy OpenGL viewport, сохранены App/authoring state |2026-10-17:engine87/87 Debug/Release,34native cases (32ENG1 +2viewport), по20Start/Stop, actual pixels/resize/EOF/deadline/native close. [Evidence](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17) | ENG3 replacement/checkpoint/compiler ещё нет; packages/3D/shared GPU/long resource/performance proof не заявлены |

Последний общий nonincremental Debug build — **2026-10-17, ENG2**:
0errors/1211warnings. Full Debug1368/1368,8TRX/failed0/skipped0;
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI570/engine87.
Release engine87/87 и UI570/570 отдельно; полного Release solution run нет.
UI570 включает7новых owned pixel image cases; два native viewport runs отдельны
от прежних UI package/golden/RichTextEditor proofs в таблице. [UI adapter evidence](alloy/HISTORY.md#eng2-owned-pixel-image--2026-10-17).
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
