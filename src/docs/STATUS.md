# TrueMoon: общий статус

Последняя работа с кодом и проверками — **2026-10-11, UI K2/K4 fixed-font golden**. Реализованы каталог явных шрифтов, лицензированные font assets и 30 эталонных PNG. Debug и опубликованный Release: **360/360 полных сравнений изображений**, Vulkan validation — 0 ошибок и 0 предупреждений, включая завершение устройств; исходные эталоны не изменились. UI suite — **532 passed / 0 failed / 0 skipped**, включая 44 новых случая шрифтов, Hosting и сравнения пикселей. Контрольные проверки изменения пикселя, provenance и запрета перезаписи завершились ожидаемыми отказами. [Метод](alloy/GOLDEN_TESTS.md), [область вывода и команды](alloy/HISTORY.md#k2k4-fixed-font-golden--2026-10-11). Проверен UI на текущем Windows x64/NVIDIA; full solution, другие модули, NuGet, K7, physical DPI, real loss и VRAM в этом запуске не проверялись. Следующая общая независимая задача — CORE5/COB4 headless package/analyzer consumer.

Предыдущая документационная работа: **2026-10-11 — общий README и навигация документации**. Корневой README дополнен картой существующих модулей, source build/test командами, headless/UI quickstart, examples, local NuGet delivery и ограничениями. Старый Signals пример заменён текущими API; добавлен [индекс документации](README.md), актуальный порядок собран в PLAN без дублирования в release criteria. Проверены локальные ссылки/anchors и компиляция двух точных README C# snippets на ранее подготовленных локальных пакетах:2builds exit0/0warnings/0errors. Приложения, unit tests, полный solution и native проверки в этом документационном шаге не запускались. [Область сверки](HISTORY.md#общий-readme-и-навигация--2026-10-11).

Предыдущая code/package работа: **2026-10-11 — UI package/independent consumer5.4–5.5**. Исправлена transitive native Skia delivery, подготовлены локальные NuGet artifacts и packaged инструкции.12App/backend +4direct renderer smoke прошли Windows x64/net10.0/SDK10.0.401, Debug/Release ×build/publish, framework-dependent без RID;4consumer builds/4publishes exit0/0warnings/0errors. Это UI поставка на указанной конфигурации, не Core5/COB4 headless/analyzer proof и не готовность alpha. На тот момент следующий независимый UI шаг — fixed-font golden K2/K4; внешний NVIDIA K7 блокер сохраняется. Unit/full solution tests этим шагом не повторялись. [Результаты и ограничения](alloy/HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

Предыдущая диагностическая работа: **2026-10-10 — UI WSI stages/raw Win32 и driver screen DC**. [Общий план](PLAN.md), [правила ведения](PLANNING.md), [UI статус](alloy/STATUS.md), [команды, native stack и scope](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Non-incremental Debug UI consumer build exit0/errors0/warnings780, Release publish exit0; native stage/raw/repeat/Hosting controls повторены в Debug/published Release. Unit tests и full solution build этим diagnostic шагом не повторялись.

UI Hosting использует shared process owner loop; concurrent/cancellation/fault/shutdown checks повторно прошли Debug/published Release, validation0/0. GDI trend связан с первым vkCreateSwapchain на новом HWND/surface; raw repro и debugger фиксируют GetDC(NULL) внутри текущего NVIDIA ICD32.0.16.1742 и HDC без последующего ReleaseDC в bounded lifecycle. Raw24cycles NVIDIA GDI6→29, AMD5→5 в Debug/Release; full UI на AMD не проверен. Production ownership/GPU policy не изменены, safe app-side fix не установлен; G4/K7 остаются открыты. Предыдущий full240-window failed USER26→26/GDI23→137; текущий diagnostic его не повторяет.

Предыдущий dispatcher run: UI suite488/488, solution build0errors/1271warnings, Debug/Release functional/short evidence. [Область и ограничения](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10); его passing results не считаются текущими unit/solution checks.

Предыдущий общий Core/DI/Enerit run2026-10-10: **643passed/0failed/0skipped**, Mithril discovery0 отдельно. AppCreate failure устранён, contracts зафиксированы в модульных PLAN; NU1506/NU1507 устранены локальными package/source настройками. [Результаты и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Эти результаты остальных модулей не являются новым UI run.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Исправлены runner/rollback/error/cancellation, lifetime/console ownership, providers/diagnostics, scheduler/pools | TrueMoon.Tests81/81, Failed0/Skipped0, discovery81; AppCreate Passed. Следом CORE5 package/headless consumer |
| [Cobalt / DI](cobalt/STATUS.md) | Exact runtime graph, Composite/generics, containers, singleton/owned disposal и adapter wrapper | Runtime37/37, generator6/6; оба providers проверены в Core. Следом COB4 analyzer/package consumer |
| [Enerit](enerit/STATUS.md) | Inherited mapping сохранён; исправлены pools/worker/handler ownership, partial/EOF reads и pending responses | Runtime30/30, generator9/9, Failed0/Skipped0. Новые20 runtime cases in-process; MMF отключён. Следом ENE1/ENE2 own-process |
| [Mithril](mithril/STATUS.md) | UnitsController, policies и process handles; production не менялся | Текущий solution discovery0/TRX total0; runtime/manual consumer собраны. Successful process checks не получены |
| [Alloy / Argentis](alloy/STATUS.md) | Shared owner, native package delivery, independent consumer, explicit font catalog и30full golden fixtures. Alpha частична | **2026-10-11:** UI532/532, golden360/360 Debug/published Release, validation0/0. Package16processes — предыдущий отдельный run; K7 failed/NVIDIA external blocker, physical DPI/real loss/VRAM сохраняются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

UI532/golden360 относятся к текущему K2/K4 run; Core/Cobalt/Enerit/Mithril/package сведения — к предыдущим отдельным runs с их собственными датами и областью вывода. Исторические проверки не выдаются за новые результаты других модулей.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Актуальные solution build/discovery/test результаты получены по всем7 test projects; Mithril tests0 явно выделен |
| G2. Core и DI | Известные defects исправлены; component/integration lifecycle на обоих providers проходит. Package/independent headless proof ещё нужен |
| G3. IPC и units | Реализация существует, сквозной отказ/restart/shutdown proof не выполнен этой работой |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | ManualTests существуют; весь заявленный набор общего плана ещё не подтверждён |
| G6. Общая поставка | Общие R1–R6 не закрыты; версия0.2.2.7-alpha не является readiness свидетельством |

## Общие проверки — датированные сведения

**UI WSI stages2026-10-10:** current non-incremental AlloyVulkanTest Debug consumer build exit0/errors0/warnings780, Release publish exit0. Stage/raw/repeat matrix Debug/published Release: queries без GDI trend, первый swapchain на новой surface/window воспроизводит его; raw teardown orders и thread-exit control проверены. Debug native stacks — screen GetDC(NULL) внутри NVIDIA ICD32.0.16.1742; safe app-side fix не установлен. Hosted dispatcher Debug/Release regression passed/validation0/0. Unit/full solution/full240-window/physical/package checks не повторялись. [Команды и ограничения](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10).

**UI dispatcher2026-10-10:** Windows/Debug/net10.0/SDK10.0.401; non-incremental `TrueMoon.slnx` build exit0/errors0/warnings1271, UI tests488passed/failed0/skipped0. Release UI consumer publish exit0. Native concurrent/callback/chrome/controlled failure/short cleanup Debug и published Release прошли, validation0/0; два полных240-window прогона **failed** на прежнем GDI limit и предшествуют финальным lifecycle guards. Native24-cycle controls определяют WSI scope, не exact allocator и не VRAM. [Подробности и фактические команды](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Другие модульные tests, physical DPI и package consumers не запускались.

**Итог исправлений Core/DI2026-10-10:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet`: exit0/0errors/1271warnings. Стандартный solution test с no-build/no-restore и отдельная list-tests discovery: exit0. Core81, Cobalt37, Cobalt.Generator6, Enerit30, Enerit.Generator9, Alloy480 — все Passed, Failed0/Skipped0. Mithril TRX total0 не считается passing suite. [Команды, requirement matrix, TRX и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10); локальные детали — [Cobalt](cobalt/HISTORY.md), [Enerit](enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10). Release/pack/own-process/native UI не запускались.

**Предыдущий Core review2026-10-10:** TrueMoon.Tests **Passed3/Failed1/Skipped0**, discovered4; AppCreate падал на `ServiceResolvingException<IAppLifetime>` через чужой LifeTimeExecutor resolver. TrueMoon.Core incremental build exit0/errors0. [Команды, scope и выводы](core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Два passing RunAsync теста тогда содержали Assert.True(true); в текущем исправлении заменены behavioral assertions.

Предыдущая UI investigation: consumer Debug build/published Release, native WGL/thread240-cycle comparisons и shared-loop prototype с двумя live окнами/close/recreate. [Команды и ограничения](alloy/HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Production dispatcher и полный mixed resource result этим шагом не исправлены; unit suite/общий baseline/другие модули той UI работой не запускались заново.

Предыдущая lifecycle работа2026-10-10: UI suite **480passed/0failed/0skipped**, short/callback/retirement proofs и два полных240-window Debug/published Release прогона. Оба полных window resource results **failed** на USER growth при нулевых hooks/registry/subscriptions/retained UI roots. [Команды и ограничения](alloy/HISTORY.md#lifecycle-resize-soak-и-native-resource-growth--2026-10-10). Предыдущие TextBox/Name20repeats и backend comparison — [отдельный запуск](alloy/HISTORY.md#textbox-control-text-и-name-smoke--2026-10-10).

**Интеграция2026-10-10, main `d56f217`:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Enerit runtime **10passed**, generator **9passed**, Alloy UI **469passed**, во всех трёх проектах Failed0/Skipped0. AlloyVulkanTest consumer build exit0, **0errors/227warnings**. Debug native callback probe exit0:6direct+12hosted failures+12healthy recreations, HWND/tree/registry/hooks cleanup, Vulkan validation0errors/0warnings. Команды и локальный scope — [Enerit история](enerit/HISTORY.md#интеграция-в-main--2026-10-10), [UI история](alloy/HISTORY.md#интеграция-в-main--2026-10-10). Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Общий solution build/tests, Release publish и physical DPI matrix при интеграции не повторялись; их исторические результаты не являются новым запуском.

Из записи предыдущего UI code run **2026-10-09** сохранено: non-incremental TrueMoon.slnx build **0errors/1341warnings**, SDK10.0.401/net10.0; локальные Release publish AlloyTest/AlloyVulkanTest exit0. Подробные команды, configuration и native ограничения — [UI история](alloy/HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

Это предыдущий общий build и два UI consumers, не successful full solution test suite и не проверка package consumers остальных модулей. При реорганизации2026-10-09 и интеграции2026-10-10 эти команды не повторялись.

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. AppCreate отдельно воспроизведён review2026-10-10, затем исправлен и проверен текущим run; Mithril discovery0 подтверждён текущим запуском. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- Mithril test project имеет current discovery0; process startup/restart/shutdown/parent-exit не подтверждены. Passing других модулей не закрывает MIT0–MIT3.
- G2 package/headless consumer и generator delivery ещё нужны. Cooperative shutdown не прерывает user Task, игнорирующий cancellation; Microsoft DI alias disposal отличается от Cobalt. Контракты/limitations закреплены в локальных PLAN.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- UI K7: предыдущий full check **failed** USER26→26/GDI23→137. Текущий native trace локализует screen HDC allocation/lifetime до первого swapchain в NVIDIA ICD32.0.16.1742; raw AMD control не воспроизводит GDI trend, но full UI на AMD не проверен. Safe app-side fix не установлен; supported NVIDIA fix или полностью проверенная alternate configuration, затем full matrix с прежними limits. Private/handles и VRAM остаются; G4 не закрыт.
- Общий lifecycle/ownership/error integration proof и независимая поставка generators ещё не подтверждены.

## Следующий конкретный шаг

1. G2/CORE5/COB4: независимый headless package consumer на Cobalt/adapter, analyzer delivery, diagnostics, cancellation/error и cleanup. Текущий solution baseline не заменяет package proof.
2. G3: Enerit ENE1/ENE2 serialization и own-process pipes; Mithril MIT0 bounded own-child startup/exit/cleanup. Current discovery0 требует собственных executable process scenarios.
3. UI: fixed-font golden K2/K4 проверен2026-10-11 в Debug/published Release; следующий [локальный scope](alloy/STATUS.md#следующий-конкретный-шаг) — оставшаяся physical4.9/K3, real loss/K8/VRAM и итоговая matrix. Independent package consumer5.4–5.5 — предыдущий отдельный run, новые font overloads через NuGet ещё не проверены. По решению пользователя NVIDIA GDI defect — внешний K7 блокер; поиск app-side workaround пока прекращён. Возврат при supported NVIDIA fix или полностью проверенной alternate GPU configuration, затем full check с прежними limits; private/handles отдельно. Alpha acceptance сохраняется; пользовательские драйвер/Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.
