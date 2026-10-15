# Рабочий уровень модулей перед движком

Сверка **2026-10-15** по локальным PLAN/STATUS и исходникам существующих consumers.
Это сводная оценка для выбора следующей работы. Build/tests/process/GPU/pack и
измерения в этой сверке не запускались; ниже приведены **предыдущие** результаты
с датой и областью вывода. Одинаковая дата разных запусков не означает один revision
или общую successful suite. Детальные задачи остаются в планах владельцев.

[Общий план](PLAN.md), [текущий статус](STATUS.md), [история сверки](HISTORY.md#матрица-качества-модулей--2026-10-15).

## Матрица

| Направление | Достигнутый рабочий уровень | Предыдущие проверки | Что остаётся за границей |
| --- | --- | --- | --- |
| [Core](core/STATUS.md) | Bounded headless quality проход завершён: configuration/bootstrap, diagnostics, cooperative lifecycle, owned cleanup |2026-10-15:107/107 Debug и Release;52source +80package +3README processes на Cobalt/DI. [Evidence](core/HISTORY.md#сквозной-headless-proof--2026-10-15) | Uncooperative Task не прерывается timeout; concurrent atomic GetOrCreate, crash-safe JSON, другие platform/deployment modes и engine workloads не доказаны |
| [Cobalt / DI](cobalt/STATUS.md) | COB6a–d, COB5/COB7 завершены в собственном TrueMoon API scope. Два projects/один consumer package; Cobalt остаётся default, Microsoft adapter сохранён |2026-10-15 COB7:runtime179/generator91 **Release**, source20functional scenarios и package120behavior scenarios; paired baseline. Core/MIT5 имеют отдельные более поздние integration runs. [Evidence](cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15) | Массовая migration explicit UseDI отложена; warm/generic/transient allocations требуют профиля. Scopes, reflection fallback, Microsoft ecosystem bridge и UI equivalence не заявлены |
| [Enerit](enerit/STATUS.md) | Typed same-build pipes, strict response/decode ownership, cancellation/error/reconnect и ENE5 baseline проверены; package composition с Mithril подтверждена отдельно |2026-10-15 ENE5:runtime126/generator114 Debug и Release;10cases/90samples. MIT5 позже:runtime126 Debug/Release и package108; generator/performance там не повторялись. [Baseline](enerit/BASELINE.md), [MIT5 evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15) | MMF/ENE3, schema negotiation, bounded queues/backpressure, remote handler cancellation и uncooperative shutdown не подтверждены. Frame transport ещё не реализован |
| [Mithril](mithril/STATUS.md) | MIT0–MIT5 завершены в bounded own-child/App scope: startup/stop/restart budget/parent exit, IPC composition, operational diagnostics |2026-10-15 MIT5:94/94 Debug и Release;40restart +18shutdown apphost/published cases;108fresh package cases,96с наблюдаемой диагностикой parent. [Evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15) | Borrowed attach отсутствует; independent external restart, arbitrary hooks/uncooperative callbacks и long resource trend не доказаны. Repeatable game session supervisor — отдельная будущая работа |
| [Alloy / Argentis](alloy/STATUS.md) | Retained UI, controls/input, Rendering.Skia/Platform.Silk/Hosting, explicit fonts и independent native delivery реализованы; UI alpha частична |2026-10-11:UI532/532;golden360/360 Debug/published Release, validation0/0. Package16processes — отдельный более ранний run того же дня. [Golden](alloy/HISTORY.md#k2k4-fixed-font-golden--2026-10-11), [package](alloy/HISTORY.md#package-и-независимый-ui-consumer--2026-10-11) | Новый fonts API/текущий Cobalt ещё не получили совместного fresh UI package proof. K3 physical DPI, K7 lifecycle, K8 real loss, VRAM и final supported matrix остаются |
| [Engine / Editor](engine/STATUS.md) | ENG0–ENG8 запланированы; собственных проектов и реализации нет | Собственных passing checks нет | Game scenario/platform/API не выбраны; game session/control/frame contracts, 3D renderer и code replacement требуют новой реализации |

Последний записанный общий nonincremental Debug build — **2026-10-15, MIT5**:
0errors/1193warnings. Это компиляция solution, не подтверждение UI/native или всех
тестовых проектов. Warnings не подавлены; общий release R1–R6 не закрыт.

## Решение о порядке работ

1. **Следующий code этап — UI quality: свежий package/Hosting proof** после
   bundled Cobalt и explicit font catalog. [Единственный детальный контракт](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15).
   Задача доступна без смены driver/desktop: consumer delivery, Cobalt/DI wiring,
   fonts/native provenance и bounded startup/cleanup. Конкретный runtime failure
   ещё не воспроизводился; исходная сверка выявила пробел acceptance, а не новый defect.
2. После него обновить эту оценку и выбрать доступную часть UI acceptance либо
   явно ограниченный ENG0 scope. Для начала движка по прежнему порядку пользователя
   требуется рассмотреть оставшийся UI риск; сводка не запускает engine автоматически.
3. Cobalt hot-path allocation и Enerit scalar CPU/allocation profiles остаются
   отдельным измерительным backlog. Оптимизацию выбирать по профилю; новые DI/IPC
   capabilities не становятся обязательными для завершения quality прохода.

Исходная проверка существующего `AlloyPackageConsumer` показывает: он выбирает
`UseDI()` без варианта Cobalt, вызывает backend setup без `SkiaFontCatalog`, а
runner сохраняет SourceFiles до запуска без финального сравнения. Поэтому его
успех2026-10-11 не закрывает новый совместный scope. Это основания обновить
consumer/acceptance, без объявления старого consumer сломанным.

## UI ограничения и условия продолжения

| Критерий | Состояние по предыдущим запускам | Требуемое продолжение |
| --- | --- | --- |
|5.4/5.5 package / Core-Cobalt-Hosting | Native delivery проверена2026-10-11, explicit fonts и current Cobalt вместе ещё нет | Свежий independent consumer на обоих providers и direct renderer без Hosting/App; детали у UI |
| K3 / Windows4.9 | Native144DPI/Scale1.5 и directed scales проверены; physical100/200%/разные DPI мониторов не были доступны | Подготовленное окружение и оставшиеся physical input/chrome checks. Смена Windows settings не назначена этой сверкой |
| K7 / lifecycle | Предыдущий full240-window Debug/Release **failed**:GDI23→137 при USER26→26. Raw/debugger trace2026-10-10 локализует screen HDC allocation до NVIDIA ICD32.0.16.1742 | По решению пользователя app-side workaround search остаётся остановлен. Возврат при supported fix либо отдельно согласованной, полностью проверенной alternate GPU configuration; raw AMD stability не является full UI pass |
| K8 / loss и VRAM | Controlled failure/recreate проверены отдельно; physical device/context loss и long VRAM evidence отсутствуют | Собственные воспроизводимые проверки на заявленной конфигурации; не подменять controlled injection реальной потерей |
| Final UI / G4 | Частичный alpha, прежние K1–K9/PA1–PA10 сохранены | Полный согласованный проход остаётся отдельным критерием; package smoke его не закрывает |

Полный UI alpha не назначается новым автоматическим требованием для документального
обсуждения ENG0. Для actual Editor/viewport readiness UI ограничения сохраняются;
допуск ограниченного прототипа нужно сформулировать явно, не выдавая его за K7/G4 pass.
PA1–PA10, MMF, scopes, Hot Reload и GPU frame sharing не входят в этот quality этап.

## Что означает завершение этой сверки

Сводка и очередность согласованы между общим и модульными STATUS; старые результаты
сохраняют даты. Следующий исполнимый scope и acceptance записаны у владельца UI.
Исходники runtime, выбор DI в существующих приложениях и системные настройки в
этой работе не меняются. Проверка документов/ссылок описана в общей HISTORY;
passing результатов build/tests/native эта матрица не создаёт.
