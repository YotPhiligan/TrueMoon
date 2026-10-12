# TrueMoon: общий статус

Последняя работа с кодом и проверками — **2026-10-12, Mithril MIT3**.
Bounded Never/Once/OnCrash/Always, fresh process/control endpoint, budget/delay,
terminal callback, cancel/drain monitor при Stop/Dispose. Windows x64/net10.0:
**Mithril89/89 Debug +89/89 Release**, restart manual40/40; fresh App Never28/28
Cobalt/DI и legacy16/16. Enerit runtime71/71 в обеих configurations.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit3-bounded-restart-контракт--2026-10-12),
[команды/границы](mithril/HISTORY.md#mit3-bounded-restart--2026-10-12).
Следующий общий шаг — MIT4/ENE4: сквозной App/AddUnit + generated Enerit IPC/restart.
External restart/package/другие OS/stress и остальные suites/UI не проверены.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT1/MIT2**.
App-local configurations, main hosting в текущем graph/PID, owned external,
controller rollback/error propagation и cancellable parent/control listener.
Windows x64/net10.0/SDK10.0.401, App lifetime/Never, Cobalt и Microsoft DI:
**Mithril65/65 Debug +65/65 Release**, **App manual28/28**, fresh legacy16/16,
Completed=true; Enerit runtime regression71/71 в каждой configuration.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit1mit2-app-composition-контракт--2026-10-11),
[команды и ограничения](mithril/HISTORY.md#mit1mit2-app-composition--2026-10-11).
На момент этого run следующим шагом был MIT3, затем MIT4/ENE4 App+IPC.
Другие module suites/UI, Enerit generator/manual и package consumers не повторены.

Предыдущая работа с кодом и проверками — **2026-10-11, Enerit ENE1/ENE2 pipes baseline**.
Runtime **71/71**, generator **34/34** отдельно Debug/Release, Failed0/Skipped0;
own-process apphost matrix **12/12**, Completed=true. Same-build typed payload,
32concurrent, local cancellation/late response, remote errors, disconnect/reconnect,
cooperative shutdown и owned child cleanup проверены на Windows x64/net10.0.
Non-incremental Debug solution build exit0/errors0/warnings1242;
другие модульные suites/UI этим запуском не проверялись.
[Контракт и ограничения](enerit/PLAN.md#ene1-ene2-контракт--2026-10-11),
[команды](enerit/HISTORY.md#ene1-ene2-pipes-baseline--2026-10-11).
App/AddUnit/controller/parent-exit в том pipes run не проверялись; restart/общий IPC и Enerit packages остаются открыты.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT0 own-child baseline**.
Убрана очистка одинаково названных процессов; child start/stop/disposal теперь
освобождают owned Process и ждут exit после принудительного завершения.
**Mithril14/14 Debug и14/14 Release**, Failed0/Skipped0; **manual16/16**
Debug/published Release apphost, Completed=true на Windows x64/net10.0/SDK10.0.401.
Проверен `Never` child-handle scope, App/AddUnit/parent-crash/restart/IPC — открыты.
[Команды и границы](mithril/HISTORY.md#mit0-own-child-baseline--2026-10-11),
[runner](../ManualTests/MithrilLifecycleProbe/README.md).
Другие unit suites/full solution/UI этим запуском не проверялись.

Предыдущая работа с кодом и проверками — **2026-10-11, CORE5/COB4 headless package consumer**.
Проверены **64/64 matrix processes + 3/3 packaged README examples** на local NuGet
в Debug/Release build/publish: Cobalt, Microsoft DI и Microsoft DI без consumer
generator. Actual analyzer path/SHA256/generated sources и отрицательная сборка
без generator подтверждены; metadata и package README дополнены.
Регрессии: **Core81/81, Cobalt37/37, generator6/6**, failed0/skipped0.
[Воспроизведение](../ManualTests/CorePackageConsumer/README.md),
[команды и scope](core/HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11).
Full solution, другие модули, UI/IPC/units этим запуском не проверялись.
На момент pipes baseline следующим шагом были Mithril MIT1/MIT2
contracts; Core/Cobalt — COB5 измерения.

Предыдущая UI работа **2026-10-11 — K2/K4 fixed-font golden**: font catalog,
30 reference PNG, UI532/532, golden360/360 Debug/published Release,
Vulkan validation0/0 и negative controls. [Метод](alloy/GOLDEN_TESTS.md),
[scope](alloy/HISTORY.md#k2k4-fixed-font-golden--2026-10-11).
K7/physical DPI/real loss/VRAM остаются открыты; UI проверки не повторены в CORE5/COB4.

Предыдущая документационная работа: **2026-10-11 — общий README и навигация документации**. Корневой README дополнен картой существующих модулей, source build/test командами, headless/UI quickstart, examples, local NuGet delivery и ограничениями. Старый Signals пример заменён текущими API; добавлен [индекс документации](README.md), актуальный порядок собран в PLAN без дублирования в release criteria. Проверены локальные ссылки/anchors и компиляция двух точных README C# snippets на ранее подготовленных локальных пакетах:2builds exit0/0warnings/0errors. Приложения, unit tests, полный solution и native проверки в этом документационном шаге не запускались. [Область сверки](HISTORY.md#общий-readme-и-навигация--2026-10-11).

Предыдущая code/package работа: **2026-10-11 — UI package/independent consumer5.4–5.5**. Исправлена transitive native Skia delivery, подготовлены локальные NuGet artifacts и packaged инструкции.12App/backend +4direct renderer smoke прошли Windows x64/net10.0/SDK10.0.401, Debug/Release ×build/publish, framework-dependent без RID;4consumer builds/4publishes exit0/0warnings/0errors. Это UI поставка на указанной конфигурации, не Core5/COB4 headless/analyzer proof и не готовность alpha. На тот момент следующий независимый UI шаг — fixed-font golden K2/K4; внешний NVIDIA K7 блокер сохраняется. Unit/full solution tests этим шагом не повторялись. [Результаты и ограничения](alloy/HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

Предыдущая диагностическая работа: **2026-10-10 — UI WSI stages/raw Win32 и driver screen DC**. [Общий план](PLAN.md), [правила ведения](PLANNING.md), [UI статус](alloy/STATUS.md), [команды, native stack и scope](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Non-incremental Debug UI consumer build exit0/errors0/warnings780, Release publish exit0; native stage/raw/repeat/Hosting controls повторены в Debug/published Release. Unit tests и full solution build этим diagnostic шагом не повторялись.

UI Hosting использует shared process owner loop; concurrent/cancellation/fault/shutdown checks повторно прошли Debug/published Release, validation0/0. GDI trend связан с первым vkCreateSwapchain на новом HWND/surface; raw repro и debugger фиксируют GetDC(NULL) внутри текущего NVIDIA ICD32.0.16.1742 и HDC без последующего ReleaseDC в bounded lifecycle. Raw24cycles NVIDIA GDI6→29, AMD5→5 в Debug/Release; full UI на AMD не проверен. Production ownership/GPU policy не изменены, safe app-side fix не установлен; G4/K7 остаются открыты. Предыдущий full240-window failed USER26→26/GDI23→137; текущий diagnostic его не повторяет.

Предыдущий dispatcher run: UI suite488/488, solution build0errors/1271warnings, Debug/Release functional/short evidence. [Область и ограничения](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10); его passing results не считаются текущими unit/solution checks.

Предыдущий общий Core/DI/Enerit run2026-10-10: **643passed/0failed/0skipped**, Mithril discovery0 отдельно. AppCreate failure устранён, contracts зафиксированы в модульных PLAN; NU1506/NU1507 устранены локальными package/source настройками. [Результаты и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Эти результаты остальных модулей не являются новым UI run.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Runner/rollback/cancellation, providers/diagnostics/utilities и независимый package/headless consumer | **2026-10-11:** Core81/81; общий consumer64matrix + 3README processes, metadata/package delivery проверены. Далее COB5 baseline; общий own-child этап отдельно |
| [Cobalt / DI](cobalt/STATUS.md) | Exact graph, Composite/generics, owned disposal, adapter wrapper и explicit analyzer delivery | **2026-10-11:** Runtime37/37, generator6/6; actual packaged analyzer и оба providers в общей64matrix, Microsoft DI также без generator. Далее COB5 измерения |
| [Enerit](enerit/STATUS.md) | Same-build generated payload contract, TME001/size limits, acknowledged errors/void, concurrent framing и local cancellation/reconnect | **2026-10-11:** Runtime71/71, generator34/34 отдельно Debug/Release; own-process manual12/12. ENE1/ENE2 bounded baseline; MMF/package/App composition открыты |
| [Mithril](mithril/STATUS.md) | App-local units, owned processes, controller/parent lifecycle и bounded process restart | **2026-10-12:**89/89 Debug +89/89 Release, restart40/40, App Never28/28 Cobalt/DI + legacy16/16. Сквозной App/IPC/restart остаётся MIT4/ENE4 |
| [Alloy / Argentis](alloy/STATUS.md) | Shared owner, native package delivery, independent consumer, explicit font catalog и30full golden fixtures. Alpha частична | **2026-10-11:** UI532/532, golden360/360 Debug/published Release, validation0/0. Package16processes — предыдущий отдельный run; K7 failed/NVIDIA external blocker, physical DPI/real loss/VRAM сохраняются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

Текущий MIT3 run: Mithril89+89/manual40+28+16 и Enerit runtime71+71.
Enerit generator34+34/manual12, MIT0 Mithril14+14/manual16, Core81/Cobalt37/generator6, headless package64+3,
UI532/golden360 и UI package — отдельные предыдущие runs с собственным scope.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Solution baseline2026-10-10 сохранён отдельно; тогда Mithril tests0. Текущий MIT3 suite89+89; full solution build успешен, остальные suites отдельно |
| G2. Core и DI | Известные defects исправлены; component и bounded independent package lifecycle на обоих providers проверены. Дополнительные configuration/utility/platform критерии сохраняют свой scope |
| G3. IPC и units | MIT0/MIT1/MIT2 App Never и MIT3 bounded handle restart, ENE1/ENE2 pipes проверены; общий App+IPC+restart proof открыт |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | Headless package consumer64+3 проверен; App/units в Never scope подтверждён отдельно; restart/IPC и весь заявленный набор открыты |
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

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. AppCreate отдельно воспроизведён review2026-10-10, затем исправлен и проверен тем run; Mithril discovery0 подтверждён2026-10-10, текущая suite14+14 добавлена MIT0. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- Mithril MIT0 Never child handle baseline проверен; App configuration/hosting, controller cleanup, parent-exit и restart требуют MIT1–MIT3. Own-process IPC composition ещё не подтверждена.
- G2 headless package/analyzer consumer подтверждён предыдущим CORE5/COB4 run. Cooperative shutdown не прерывает user Task, игнорирующий cancellation; Microsoft DI alias disposal отличается от Cobalt. Контракты/limitations закреплены в локальных PLAN.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- UI K7: предыдущий full check **failed** USER26→26/GDI23→137. Текущий native trace локализует screen HDC allocation/lifetime до первого swapchain в NVIDIA ICD32.0.16.1742; raw AMD control не воспроизводит GDI trend, но full UI на AMD не проверен. Safe app-side fix не установлен; supported NVIDIA fix или полностью проверенная alternate configuration, затем full matrix с прежними limits. Private/handles и VRAM остаются; G4 не закрыт.
- Общий process/IPC/restart integration proof и независимая поставка Enerit generator ещё не подтверждены; Cobalt analyzer имеет отдельный CORE5/COB4 proof.

## Следующий конкретный шаг

1. G3: Mithril MIT1/MIT2 — configuration isolation и App/AddUnit/controller/parent-exit contracts. MIT0 own-child и ENE1/ENE2 bounded pipes выполнены; затем MIT3 restart и ENE4 composition.
2. Core/Cobalt: COB5 измерения на фиксированном graph. CORE5/COB4 headless package/analyzer consumer уже проверен2026-10-11; другие consumer/platform/release критерии сохраняют собственный scope.
3. UI: fixed-font golden K2/K4 проверен2026-10-11 в Debug/published Release; следующий [локальный scope](alloy/STATUS.md#следующий-конкретный-шаг) — оставшаяся physical4.9/K3, real loss/K8/VRAM и итоговая matrix. Independent package consumer5.4–5.5 — предыдущий отдельный run, новые font overloads через NuGet ещё не проверены. По решению пользователя NVIDIA GDI defect — внешний K7 блокер; поиск app-side workaround пока прекращён. Возврат при supported NVIDIA fix или полностью проверенной alternate GPU configuration, затем full check с прежними limits; private/handles отдельно. Alpha acceptance сохраняется; пользовательские драйвер/Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.
